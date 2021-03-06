﻿using ChessEngine;
using Chessman.Controls;
using Chessman.Util;
using Chessman.ViewModel;
using Framework.MVVM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236
namespace Chessman.View
{
    public sealed partial class ChessBoardView : UserControl
    {
        private const double MinDragPieceSize = 80;
        private const double MinimumDragDistance = 4;

        public static readonly DependencyProperty SuggestedMoveProperty =DependencyProperty.Register("SuggestedMove", typeof(Move), typeof(ChessBoardView), new PropertyMetadata(null, OnSuggestedMoveChangedThunk));
        public static readonly DependencyProperty HasDragAndDropProperty = DependencyProperty.Register("HasDragAndDrop", typeof(bool), typeof(ChessBoardView), new PropertyMetadata(false));

        private bool isDragStarted = false;
        private Point dragStartPoint = new Point();
        private SquareView pointerPressSquare = null;
        private ChessPieceView draggingPieceView = null;
        private ChessPieceViewModel dragSourcePieceViewModel = null;
        private PromotionMoveTask promotionTask = null;
        private Ellipse dropHintEllipse = null;

        public Move SuggestedMove
        {
            get { return (Move)GetValue(SuggestedMoveProperty); }
            set { SetValue(SuggestedMoveProperty, value); }
        }

        public bool HasDragAndDrop
        {
            get { return (bool)GetValue(HasDragAndDropProperty); }
            set { SetValue(HasDragAndDropProperty, value); }
        }

        public ChessBoardViewModel ViewModel
        {
            get { return DataContext as ChessBoardViewModel; }
        }

        public ChessBoardView()
        {
            this.InitializeComponent();

            Binding suggestedMoveBinding = new Binding() { Path =  new PropertyPath("SuggestedMove") };
            SetBinding(SuggestedMoveProperty, suggestedMoveBinding);
        }

        public void RegisterAnimationHandlers()
        {
            Messenger.Default.Register<GenericMessage<MoveTask>>(this, NotificationMessages.AnimateMoveTaskCreated, OnAnimateMoveTaskReceived);
            Messenger.Default.Register<GenericMessage<MoveTask>>(this, NotificationMessages.AnimateUndoMoveTaskCreated, OnAnimateUndoMoveTaskReceived);
            Messenger.Default.Register<GenericMessage<PromotionMoveTask>>(this, NotificationMessages.AnimatePromotionMoveTask, OnAnimatePromotionMoveTask);
        }

        public void UnRegisterAnimationHandlers()
        {
            Messenger.Default.Unregister<GenericMessage<MoveTask>>(this);
            Messenger.Default.Unregister<GenericMessage<PromotionMoveTask>>(this);
        }

        private void OnAnimateMoveTaskReceived(GenericMessage<MoveTask> message)
        {
            AnimateMoveTask(message.Content, false);
        }

        private void OnAnimateUndoMoveTaskReceived(GenericMessage<MoveTask> message)
        {
            AnimateMoveTask(message.Content, true);
        }

        private void AnimateMoveTask(MoveTask moveTask, bool isUndo)
        {
            MoveAnimationsFactory moveAnimationsFactory = new MoveAnimationsFactory();

            foreach (var pieceMove in moveTask.MovedPiecesCoordinates)
            {
                Coordinate fromCoordinate = isUndo ? pieceMove.Item2 : pieceMove.Item1;
                Coordinate toCoordinate = isUndo ? pieceMove.Item1 : pieceMove.Item2;

                Point animationStartPoint = GetCoordinatePositionOnBoard(fromCoordinate);
                Point animationEndPoint = GetCoordinatePositionOnBoard(toCoordinate);

                ContentPresenter pieceItemView = (ContentPresenter)piecesItemsControl.ContainerFromItem(ViewModel.GetPiece(toCoordinate));
                moveAnimationsFactory.AddMoveAnimation(pieceItemView, animationStartPoint, animationEndPoint);
            }

            if (moveTask.CapturedPieceCoordinate != null)
            {
                // if is not undo the removed piece is marked to be removed and only removed after the animation
                ChessPieceViewModel removedPiece = isUndo ? ViewModel.GetPiece(moveTask.CapturedPieceCoordinate) : ViewModel.GetRemovedPiece(moveTask.CapturedPieceCoordinate);
                ContentPresenter pieceItemView = (ContentPresenter)piecesItemsControl.ContainerFromItem(removedPiece);
                moveAnimationsFactory.AddRemoveAnimation(pieceItemView);
            }

            moveAnimationsFactory.StoryBoard.Completed += (o, e) =>
            {
                moveTask.CompleteTask();
                IsHitTestVisible = true;
            };

            moveAnimationsFactory.StoryBoard.Begin();
            // dont allow user interactions with the board for the duration of the animation
            IsHitTestVisible = false;
        }

        private void OnAnimatePromotionMoveTask(GenericMessage<PromotionMoveTask> message)
        {
            promotionTask = message.Content;
            Move promotionMove = promotionTask.Move;

            if (promotionTask.InstantMove)
            {
                ChangePieceLocation(promotionMove.GetFrom(), promotionMove.GetTo());
                ShowPromotionPopup();
            }
            else
            {
                Point animationStartPoint = GetCoordinatePositionOnBoard(promotionMove.GetFrom());
                Point animationEndPoint = GetCoordinatePositionOnBoard(promotionMove.GetTo());

                ContentPresenter pieceItemView = (ContentPresenter)piecesItemsControl.ContainerFromItem(ViewModel.GetPiece(promotionMove.GetFrom()));
                MoveAnimationsFactory moveAnimationsFactory = new MoveAnimationsFactory();
                moveAnimationsFactory.AddMoveAnimation(pieceItemView, animationStartPoint, animationEndPoint);
                // dont allow user interactions with the board for the duration of the animation
                IsHitTestVisible = false;

                moveAnimationsFactory.StoryBoard.Completed += (o, e) =>
                {
                    IsHitTestVisible = true;
                    ShowPromotionPopup();
                };

                moveAnimationsFactory.StoryBoard.Begin();
            }
        }

        private void ShowPromotionPopup()
        {
            if (promotionTask == null)
                return;

            Move promotionMove = promotionTask.Move;

            PromotionView promotionView = new PromotionView();
            Point toSquarePoint = GetCoordinatePositionOnBoard(promotionMove.GetTo());
            bool isBlackPromotion = promotionMove.GetTo().Y == 0;

            // if the popup apears on bottom, display the queen on the bottom
            bool displayedOnBottom = toSquarePoint.Y != 0;

            PromotionViewModel promotionVM = new PromotionViewModel(isBlackPromotion ? PieceColor.Black : PieceColor.White, displayedOnBottom)
            {
                PieceSelectedCommand = new RelayCommand((p) => {

                    promotionTask.OnPieceSelected((PieceType)p);
                    if ((PieceType)p == PieceType.None)
                        CancelPromotionMove();

                    promotionTask = null;
                    promotionSelectionPopup.IsOpen = false;
                })
            };

            promotionView.DataContext = promotionVM;

            double squareWidth = board.ActualWidth / 8;
            promotionView.PieceSize = squareWidth;
            promotionSelectionPopup.Child = promotionView;
            promotionSelectionPopup.IsOpen = true;
            promotionSelectionPopup.HorizontalOffset = toSquarePoint.X;

            promotionSelectionPopup.VerticalOffset = 0;
            if (displayedOnBottom)
                promotionSelectionPopup.VerticalOffset = (8 - promotionVM.Pieces.Count) * squareWidth;
        }

        private void CancelPromotionMove()
        {
            if (promotionTask == null)
                throw new NotSupportedException("No promotion task pending");

            Move promotionMove = promotionTask.Move;

            //move the pawn back to original position
            ChangePieceLocation(promotionMove.GetFrom(), promotionMove.GetFrom());
            promotionTask = null;
        }

        private void ChangePieceLocation(Coordinate from, Coordinate to)
        {
            ContentPresenter pieceItemView = (ContentPresenter)piecesItemsControl.ContainerFromItem(ViewModel.GetPiece(from));
            Point toPoint = GetCoordinatePositionOnBoard(to);

            Canvas.SetLeft(pieceItemView, toPoint.X);
            Canvas.SetTop(pieceItemView, toPoint.Y);
        }

        private void OnPromotionPopupClosed(object sender, object e)
        {
            if (promotionTask != null)
                CancelPromotionMove();
        }

        private Point GetCoordinatePositionOnBoard(Coordinate coordinate)
        {
            FrameworkElement squareItem = (FrameworkElement)board.ContainerFromIndex(ViewModel.GetSquareIndex(coordinate));
            return squareItem.TransformToVisual(board).TransformPoint(new Point(0, 0));
        }

        private void OnBoardPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (isDragStarted && e.Pointer.IsInContact)
            {
                MoveDraggedPiece(e.GetCurrentPoint(board));
                e.Handled = true;
            }
        }

        private void OnSquarePointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (isDragStarted)
            {
                ((SquareView)sender).IsDropTarget = true;
                if (dropHintEllipse != null)
                    MoveDropHintEllipseToSquare((SquareView)sender);
            }
        }

        private void MoveDropHintEllipseToSquare(SquareView square)
        {
            if (dropHintEllipse == null)
                throw new InvalidOperationException("The drop hint ellipse should not be null.");

            SquareViewModel squareVM = (SquareViewModel)square.DataContext;
            Point squareMiddlePoint = GetSquareMiddlePoint(squareVM.Coordinate);

            Canvas.SetTop(dropHintEllipse, squareMiddlePoint.Y - dropHintEllipse.Height / 2);
            Canvas.SetLeft(dropHintEllipse, squareMiddlePoint.X - dropHintEllipse.Width / 2);
        }

        private void OnSquarePointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (isDragStarted)
                ((SquareView)sender).IsDropTarget = false;
        }

        private void MoveDraggedPiece(PointerPoint point)
        {
            Canvas.SetLeft(draggingPieceView, point.Position.X - draggingPieceView.Width / 2);

            double positionY = point.PointerDevice.PointerDeviceType == PointerDeviceType.Touch ?
                point.Position.Y - draggingPieceView.Height : point.Position.Y - draggingPieceView.Height / 2;
            Canvas.SetTop(draggingPieceView, positionY);
        }

        private void OnBoardPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            EndDragMove();
        }

        private void EndDragMove()
        {
            if (!isDragStarted)
                return;

            dragCanvas.Children.Clear();

            isDragStarted = false;

            dragSourcePieceViewModel.IsDragSource = false;
            dragSourcePieceViewModel = null;
            pointerPressSquare = null;
            dropHintEllipse = null;
        }

        private void OnSquarePointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!isDragStarted && pointerPressSquare != null && e.Pointer.IsInContact)
            {
                SquareViewModel squareVM = (SquareViewModel)pointerPressSquare.DataContext;
                dragSourcePieceViewModel = ViewModel.GetPiece(squareVM.Coordinate);

                if (dragSourcePieceViewModel != null)
                {
                    PointerPoint pointerPoint = e.GetCurrentPoint(board);
                    if (!HasPointerMovedEnaugh(pointerPoint.Position))
                        return;

                    StartDragMove(e.Pointer, pointerPoint);
                }
            }
        }

        private void StartDragMove(Pointer pointer, PointerPoint pointerPoint)
        {
            double optimalPieceSize = pointerPressSquare.ActualWidth;
            if (pointer.PointerDeviceType == PointerDeviceType.Touch && optimalPieceSize < MinDragPieceSize - 20)
            {
                optimalPieceSize = Math.Max(pointerPressSquare.ActualWidth * 1.5, MinDragPieceSize);
                CreateDropHintEllipse();
            }

            draggingPieceView = new ChessPieceView()
            {
                Width = optimalPieceSize,
                Height = optimalPieceSize,
                DataContext = new ChessPieceViewModel(dragSourcePieceViewModel.Piece, null),
                IsHitTestVisible = false
            };

            MoveDraggedPiece(pointerPoint);
            dragCanvas.Children.Add(draggingPieceView);

            board.SelectedItem = pointerPressSquare.DataContext;

            isDragStarted = true;
            dragSourcePieceViewModel.IsDragSource = true;
        }

        private void CreateDropHintEllipse()
        {
            dropHintEllipse = new Ellipse()
            {
                Opacity = 0.2,
                Fill = new SolidColorBrush(Colors.Black),
                Width = pointerPressSquare.ActualWidth * 2,
                Height = pointerPressSquare.ActualHeight * 2,
                IsHitTestVisible = false
            };

            MoveDropHintEllipseToSquare(pointerPressSquare);
            dragCanvas.Children.Add(dropHintEllipse);
        }

        private bool HasPointerMovedEnaugh(Point postion)
        {
            return Math.Abs(postion.X - dragStartPoint.X) > MinimumDragDistance ||
                Math.Abs(postion.Y - dragStartPoint.Y) > MinimumDragDistance;
        }

        private void OnSquarePointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (!HasDragAndDrop)
                return;

            dragStartPoint = e.GetCurrentPoint(board).Position;
            pointerPressSquare = (SquareView)sender;
        }

        private void OnSquarePointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (isDragStarted)
            {
                SquareView dropSquare = (SquareView)sender;
                dropSquare.IsDropTarget = false;
                ViewModel.OnPieceDropped((SquareViewModel)dropSquare.DataContext);

                // make sure to re-layout possible changed pieces
                // when a piece changes its coordinate it does not change view arrange
                if (promotionTask == null)
                    piecesItemsControl.ItemsPanelRoot.InvalidateArrange();
            }

            EndDragMove();
        }

        private void OnBoardPointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (isDragStarted)
                board.CapturePointer(e.Pointer);
        }

        private void OnBoardPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (isDragStarted)
                board.ReleasePointerCaptures();
        }

        private static void OnSuggestedMoveChangedThunk(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ChessBoardView)d).RefreshArrows();
        }

        private void RefreshArrows()
        {
            if (SuggestedMove != null)
                DrawSuggestedMoveArrow();
            else
                ClearAllArrows();
        }

        private void DrawSuggestedMoveArrow()
        {
            if (board.ActualWidth == 0)
                return;

            Point start = GetSquareMiddlePoint(SuggestedMove.GetFrom());
            Point end = GetSquareMiddlePoint(SuggestedMove.GetTo());

            double squareWidth = board.ActualWidth / 8;
            DrawArrow(start, end, squareWidth / 4);
        }

        private Point GetSquareMiddlePoint(Coordinate coordinate)
        {
            double squareWidth = board.ActualWidth / 8;
            double halfSquareWidth = squareWidth / 2;

            // from black perspective the board is rotated
            double actualX = ViewModel.Perspective == SideColor.White ? coordinate.X : 7 - coordinate.X;
            double actualY = ViewModel.Perspective == SideColor.White ? 7 - coordinate.Y : coordinate.Y;

            Point middlePoint = new Point()
            {
                X = actualX * squareWidth + halfSquareWidth,
                Y = actualY * squareWidth + halfSquareWidth
            };

            return middlePoint;
        }

        private void DrawArrow(Point start, Point end, double baseWidth)
        {
            ClearAllArrows();

            double distance = ArrowDrawUtil.GetDistance(start, end);
            var points = ArrowDrawUtil.GetArrowGeometryPoints(distance, baseWidth);
            double angle = ArrowDrawUtil.GetLineAngle(start, end);

            var pathFigure = new PathFigure() { StartPoint = points[0] };
            points.Skip(1).ToList().ForEach(p => pathFigure.Segments.Add(new LineSegment() { Point = p }));

            Path bestMoveArrowPath = new Path() { Fill = (Brush)App.Current.Resources["BoardArrowBrush"] };

            Canvas.SetLeft(bestMoveArrowPath, start.X);
            Canvas.SetTop(bestMoveArrowPath, start.Y);
            bestMoveArrowPath.Data = new PathGeometry() { Figures = new PathFigureCollection() { pathFigure } };
            bestMoveArrowPath.RenderTransform = new RotateTransform() { Angle = angle };

            arrowsCanvas.Children.Add(bestMoveArrowPath);
        }

        private void ClearAllArrows()
        {
            arrowsCanvas.Children.Clear();
        }

        private void OnBoardSizeChanged(object sender, SizeChangedEventArgs e)
        {
            RefreshArrows();
        }        
    }
}

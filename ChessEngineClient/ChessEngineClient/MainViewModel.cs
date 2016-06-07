﻿using Framework.MVVM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChessEngineClient.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        public ChessBoardViewModel BoardViewModel { get; set; }

        public AnalysisViewModel AnalysisViewModel { get; set; }

        public NotationViewModel NotationViewModel { get; set; }

        public MainViewModel()
        {
            BoardViewModel = new ChessBoardViewModel();
            AnalysisViewModel = new AnalysisViewModel();
            NotationViewModel = new NotationViewModel();
        }
    }
}

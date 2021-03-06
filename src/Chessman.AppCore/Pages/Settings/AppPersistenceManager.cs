﻿using Chessman.ViewModel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Windows.Foundation.Collections;
using Windows.Storage;

namespace Chessman
{
    public static class AppPersistenceManager
    {
        //TODO: all these should be set in an object
        public const string EnableMoveSoundsKey = "EnableMoveSoundsKey";
        public const string ShowLegalMovesKey = "ShowLegalMovesKey";
        public const string NotationTypeKey = "NotationType";
        public const string MultipleLinesKey = "MultipleLines";
        public const string ShowBestMoveArrowKey = "ShowAnalysisArrow";
        public const string ComputerStrengthKey = "ComputerStrength";
        public const string SavePositionsBetweenSessionsKey = "SavePositionsBetweenSessions";

        public const string SavedAnalysisPositionPgnKey = "SavedAnalysisPositionPgnKey";
        public const string SavedPracticePositionPgnKey = "SavedPracticePositionPgnKey";

        public static void InitializeDefaultSettings(ApplicationDataContainer settingsContainer)
        {
            if (!settingsContainer.Values.ContainsKey(NotationTypeKey))
                settingsContainer.Values[NotationTypeKey] = (int)NotationType.Figurines;

            if (!settingsContainer.Values.ContainsKey(ComputerStrengthKey))
                settingsContainer.Values[ComputerStrengthKey] = 5;

            if (!settingsContainer.Values.ContainsKey(ShowBestMoveArrowKey))
                settingsContainer.Values[ShowBestMoveArrowKey] = true;

            if (!settingsContainer.Values.ContainsKey(MultipleLinesKey))
                settingsContainer.Values[MultipleLinesKey] = 2;

            if (!settingsContainer.Values.ContainsKey(SavePositionsBetweenSessionsKey))
                settingsContainer.Values[SavePositionsBetweenSessionsKey] = true;

            if (!settingsContainer.Values.ContainsKey(EnableMoveSoundsKey))
                settingsContainer.Values[EnableMoveSoundsKey] = true;

            if (!settingsContainer.Values.ContainsKey(ShowLegalMovesKey))
                settingsContainer.Values[ShowLegalMovesKey] = true;
        }

        public static void SaveApplicationState(ApplicationDataContainer settingsContainer)
        {
            bool savesSessions = true;
            if (settingsContainer.Values.ContainsKey(SavePositionsBetweenSessionsKey))
                savesSessions = (bool)settingsContainer.Values[SavePositionsBetweenSessionsKey];

            if (savesSessions)
            {
                if (ViewModelLocator.MainViewModelCreated)
                {
                    PositionLoadOptions analysisPosition = ViewModelLocator.MainViewModel.GetPositionLoadOptions(BoardSerializationType.PGN, false);
                    settingsContainer.Values[SavedAnalysisPositionPgnKey] = JsonConvert.SerializeObject(analysisPosition);
                }

                if (ViewModelLocator.PracticeViewModelCreated)
                {
                    PositionLoadOptions practicePosition = ViewModelLocator.PracticeViewModel.GetPositionLoadOptions(BoardSerializationType.PGN, false);
                    settingsContainer.Values[SavedPracticePositionPgnKey] = JsonConvert.SerializeObject(practicePosition);
                }
            }
            else
            {
                settingsContainer.Values.Remove(SavedAnalysisPositionPgnKey);
                settingsContainer.Values.Remove(SavedPracticePositionPgnKey);
            }
        }

        public static PositionLoadOptions GetSavedPosition(IAppSettings apppSettings, string settingsKey)
        {
            if (apppSettings.Values.ContainsKey(settingsKey))
            {
                string serializedBoard = (string)apppSettings.Values[settingsKey];
                try
                {
                    return JsonConvert.DeserializeObject<PositionLoadOptions>(serializedBoard);
                }
                catch(Exception ex)
                {
                    //TODO: log the error 
                }
            }
            return null;
        }
    }
}

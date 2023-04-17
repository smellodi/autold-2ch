﻿using Olfactory2Ch.Utils;
using Smop.IonVision;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Text.Json;
using System.Threading.Tasks;
using static Olfactory2Ch.Tests.Comparison.Procedure;

namespace Olfactory2Ch.Tests.Comparison
{
    internal class DMS
    {
        public static DMS Instance => _instance ??= new();

        private DMS()
        {
            var logFolder = Properties.Settings.Default.Logger_Folder;
            _folder = string.IsNullOrEmpty(logFolder) 
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                : logFolder;
            _folder = Path.Combine(_folder, "dms");

            if (!Directory.Exists(_folder))
            {
                Directory.CreateDirectory(_folder);
            }
        }

        public async Task<string> Init(Settings settings)
        {
            _settings = settings;
            _isActive = settings.Sniffer == GasSniffer.DMS;

            if (!_isActive)
            {
                return null;
            }

            var subfolder = $"scan_{DateTime.Now:u}".ToPath();
            _folder = Path.Combine(_folder, subfolder);
            Directory.CreateDirectory(_folder);

            _eventLogger.Add(LogSource.Comparison, "DMS", "folder", subfolder);

            try
            {
                await _comunicator.SetSettingsClock();
                _isActive = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DMS] set clock error: {ex}");
            }

            if (!_isActive)
            {
                SystemSounds.Exclamation.Play();
                return L10n.T("DMSErrorCannotConnect");
            }

            return null;
        }

        public async Task<string> SetProject()
        {
            if (!_isActive)
            {
                return null;
            }

            try
            {
                await Task.Delay(INTER_REQUEST_PAUSE);
                PrintResponse("set project", await _comunicator.SetProject());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DMS] project set error: {ex}");
                return L10n.T("DMSErrorProject");
            }

            return null;
        }

        public async Task<string> SetParams()
        {
            if (!_isActive)
            {
                return null;
            }

            try
            {
                await Task.Delay(INTER_REQUEST_PAUSE);
                PrintResponse("set param", await _comunicator.SetParameter());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DMS] parameter set error: {ex}");
                return L10n.T("DMSErrorParameter");
            }

            return null;
        }

        public async void StartScan(MixturePair pair, MixtureID mixtureID)
        {
            if (!_isActive)
            {
                return;
            }

            // Set a scan marker
            try
            {
                var mix = mixtureID == MixtureID.First ? pair.Mix1 : pair.Mix2;
                var mixID = mixtureID == MixtureID.First ? 0 : 1;
                var pulse = GasMixer.ToPulse(pair, mixID, _settings.TestOdorFlow, _settings.OdorFlowDurationMs, _settings.Gas1, _settings.Gas2);
                var marker = new List<string>() { mix.ToString() };
                if (pulse.Channel1 != null)
                    marker.Add($"{_settings.Gas1}={pulse.Channel1.Flow:F1}");
                if (pulse.Channel2 != null)
                    marker.Add($"{_settings.Gas2}={pulse.Channel2.Flow:F1}");
                var comment = string.Join(',', marker);
                PrintResponse(
                    $"set scan marker '{comment}'",
                    await _comunicator.SetScanResultComment(new Comment(comment))
                );
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DMS] marker error: {ex}");
            }

            try
            {
                await Task.Delay(INTER_REQUEST_PAUSE);
                PrintResponse("start scan", await _comunicator.StartScan());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DMS] scan start error: {ex}");
                _scanStartError = L10n.T("DMSErrorScanStart");
            }
        }

        public string SaveScan()
        {
            if (!_isActive)
            {
                return null;
            }

            string error = null;

            if (_scanStartError != null)
            {
                error = _scanStartError;
                _scanStartError = null;
                return error;
            }

            Task.Run(async () =>
            {
                try
                {
                    // Wait, if neeeded, until the scanning is finished
                    API.Response<ScanProgress> res;
                    while ((res = await _comunicator.GetScanProgress()).Success)
                    {
                        PrintResponse("scan progress", res);
                        await Task.Delay(1000);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DMS] waiting error: {ex}");
                    error = L10n.T("DMSErrorScanWait");
                    return;
                }

                // Get the scan result
                API.Response<ScanResult> dataRetrievalResult = null;
                try
                {
                    await Task.Delay(INTER_REQUEST_PAUSE);
                    dataRetrievalResult = await _comunicator.GetScanResult();
                    PrintResponse("get scan", dataRetrievalResult);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DMS] data retrieval error: {ex}");
                    error = L10n.T("DMSErrorScanResult");
                    return;
                }

                // Save the scan result to a file
                if (dataRetrievalResult?.Success ?? false)
                {
                    try
                    {
                        var filename = $"scan_{DateTime.Now:u}.json".ToPath();
                        var filepath = Path.Combine(_folder, filename);
                        using StreamWriter file = new(filepath);
                        file.WriteLine(JsonSerializer.Serialize(dataRetrievalResult.Value));
                        _eventLogger.Add(LogSource.Comparison, "DMS", "file", filename);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[DMS] saving data error: {ex}");
                        error = L10n.T("DMSErrorScanSave");
                    }
                }
            }).Wait();

            return error;
        }

        // Internal

        static readonly string IonVisionSettingsFilename = "Properties/IonVision.json";

        const int INTER_REQUEST_PAUSE = 150;

        static DMS _instance;

        static DMS()
        {
            Smop.IonVision.Settings.DefaultFilename = IonVisionSettingsFilename;
        }

        readonly Communicator _comunicator = new(IonVisionSettingsFilename, Storage.Instance.IsDebugging);
        readonly FlowLogger _eventLogger = FlowLogger.Instance;

        string _folder;

        Settings _settings;
        bool _isActive;
        string _scanStartError = null;

        private static void PrintResponse<T>(string request, API.Response<T> response)
        {
            if (response.Value is Confirm confirm)
            {
                var result = response.Success ? confirm.Message : response.Error;
                Debug.WriteLine($"[DMS] {request}: {result} ");
            }
            else if (response.Value is ScanProgress progress)
            {
                var result = response.Success ? progress.Progress.ToString() : response.Error;
                Debug.WriteLine($"[DMS] {request}: {result} ");
            }
            else if (response.Value is ScanResult scan)
            {
                var result = response.Success ? scan.ToString().Max(100) : response.Error;
                Debug.WriteLine($"[DMS] {request}: {result} ");
            }
        }
    }

    public static class StringExtension
    {
        public static string Max(this string self, int maxLength, bool printSkippedCharsCount = true)
        {
            var suffix = printSkippedCharsCount ? $"... and {self.Length - maxLength} chars more." : null;
            return self.Length > maxLength ? (self[..maxLength] + suffix) : self;
        }
    }
}

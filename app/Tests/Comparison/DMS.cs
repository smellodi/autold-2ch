using AutOlD2Ch.Utils;
using Smop.IonVision;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Text.Json;
using System.Threading.Tasks;
using static AutOlD2Ch.Tests.Comparison.Procedure;

namespace AutOlD2Ch.Tests.Comparison
{
    internal class DMS : IDisposable
    {
        public static DMS Instance => _instance ??= new();

        public static DMS Recreate() => _instance = new DMS();

        public Smop.IonVision.Settings Settings => _comunicator.Settings;
        public string SupportedVersion => _comunicator.SupportedVersion;
        public string DetectedVersion { get; private set; } = null;
        public bool? IsConnected { get; private set; } = null;
        public bool? IsCorrectVersion { get; private set; } = null;

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

        public async Task Precheck()
        {
            await Task.Delay(INTER_REQUEST_PAUSE);

            try
            {
                await Task.Delay(1000);
                var result = await _comunicator.GetSystemInfo();

                IsConnected = result.Success;

                if (!result.Success)
                {
                    throw new Exception(result.Error);
                }

                DetectedVersion = result.Value.CurrentVersion;
                IsCorrectVersion = result.Value.CurrentVersion == _comunicator.SupportedVersion;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                Debug.WriteLine($"[DMS] get system info error: {ex}");
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
                await _comunicator.SetClock();
                _isActive = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DMS] set clock error: {ex}");
            }

            if (!_isActive)
            {
                SystemSounds.Exclamation.Play();
                return L10n.T("CannotConnectToDMS");
            }

            return null;
        }
        public async Task<string[]> GetProjects()
        {
            await Task.Delay(INTER_REQUEST_PAUSE);

            try
            {
                var result = await _comunicator.GetProjects();

                if (!result.Success)
                {
                    throw new Exception(result.Error);
                }

                return result.Value;
            }
            catch (Exception ex)
            { 
                Debug.WriteLine($"[DMS] get projects error: {ex}");
                return Array.Empty<string>();
            }
        }

        public async Task<Parameter[]> GetProjectParameters(string projectName)
        {
            await Task.Delay(INTER_REQUEST_PAUSE);

            try
            {
                var result = await _comunicator.GetProjectDefinition(projectName);

                if (!result.Success)
                {
                    throw new Exception(result.Error);
                }

                return result.Value.Parameters;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DMS] get project parameters error: {ex}");
                return Array.Empty<Parameter>();
            }
        }

        public async Task<string> GetProject()
        {
            if (!_isActive)
            {
                return null;
            }

            try
            {
                await Task.Delay(INTER_REQUEST_PAUSE);

                var result = await _comunicator.GetProject();
                PrintResponse("get project", result);
                if (!result.Success)
                {
                    throw new Exception(result.Error);
                }
                return result.Value.Project;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DMS] project get error: {ex}");
                return L10n.T("ScanProjectFailed");
            }
        }

        public async Task<string> SetProject(int waitingDuration)
        {
            if (!_isActive)
            {
                return null;
            }

            try
            {
                await Task.Delay(INTER_REQUEST_PAUSE);
                PrintResponse("set project", await _comunicator.SetProjectAndWait(waitingDuration));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DMS] project set error: {ex}");
                return L10n.T("ScanProjectFailed");
            }

            return null;
        }

        public async Task<string> GetParam()
        {
            if (!_isActive)
            {
                return null;
            }

            try
            {
                await Task.Delay(INTER_REQUEST_PAUSE);

                var result = await _comunicator.GetParameter();
                PrintResponse("get param", result);
                if (!result.Success)
                {
                    throw new Exception(result.Error);
                }
                return result.Value.Parameter.Name;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DMS] parameter get error: {ex}");
                return L10n.T("ScanParameterFailed");
            }
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
                PrintResponse("set param", await _comunicator.SetParameterAndPreload());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DMS] parameter set error: {ex}");
                return L10n.T("ScanParameterFailed");
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
                    await _comunicator.SetScanResultComment(new { Text = comment })
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
                _scanStartError = L10n.T("ScanStartFailed");
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
                    error = L10n.T("ScanWaitFailed");
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
                    error = L10n.T("ScanResultFailed");
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
                        error = L10n.T("ScanSaveFailed");
                    }
                }
            }).Wait();

            return error;
        }

        public void Dispose()
        {
            _comunicator.Dispose();
            GC.SuppressFinalize(this);
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

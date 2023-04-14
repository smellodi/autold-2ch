using Olfactory2Ch.Utils;
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
        public DMS()
        {
            if (!Directory.Exists(_folder))
            {
                Directory.CreateDirectory(_folder);
            }
        }

        public void Init(Settings settings)
        {
            _settings = settings;
            _isActive = settings.Sniffer == GasSniffer.DMS;

            if (!_isActive)
            {
                return;
            }

            var subfolder = $"scan_{DateTime.Now:u}".ToPath();
            _folder = Path.Combine(_folder, subfolder);
            Directory.CreateDirectory(_folder);

            _eventLogger.Add(LogSource.Comparison, "DMS", "folder", subfolder);

            Task.Run(async () =>
            {
                _isActive = await _comunicator.CheckConnection();
                if (!_isActive)
                {
                    SystemSounds.Exclamation.Play();
                    return;
                }

                if (_isActive)
                {
                    await Task.Delay(300);
                    PrintResponse("set project", await _comunicator.SetProject());
                    await Task.Delay(300);
                    PrintResponse("set param", await _comunicator.SetParameter());
                }
            }).Wait();
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
                await Task.Delay(300);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DMS] marker error: {ex}");
            }

            PrintResponse("start scan", await _comunicator.StartScan());
        }

        public void SaveScan()
        {
            if (!_isActive)
            {
                return;
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
                    await Task.Delay(300);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DMS] waiting error: {ex}");
                    return;
                }

                // Get the scan result
                API.Response<ScanResult> dataRetrievalResult = null;
                try
                {
                    dataRetrievalResult = await _comunicator.GetScanResult();
                    PrintResponse("get scan", dataRetrievalResult);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DMS] data retrieval error: {ex}");
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
                    }
                }
                await Task.Delay(300);
            }).Wait();
        }

        // Internal

        static string IonVisionSettingsFilename = "Properties/IonVision.json";

        static DMS()
        {
            Smop.IonVision.Settings.DefaultFilename = IonVisionSettingsFilename;
        }

        readonly Communicator _comunicator = new(IonVisionSettingsFilename, Storage.Instance.IsDebugging);
        readonly FlowLogger _eventLogger = FlowLogger.Instance;

        string _folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\scan" ;

        Settings _settings;
        bool _isActive;

        private void PrintResponse<T>(string request, API.Response<T> response)
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

using Olfactory2Ch.Tests.Comparison;
using System;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Olfactory2Ch.Pages.ThresholdTest
{
    public partial class Wait : Page, IPage<EventArgs>
    {
        public event EventHandler<EventArgs> Next;

        public Wait()
        {
            InitializeComponent();

            Storage.Instance
                .BindScaleToZoomLevel(sctScale)
                .BindVisibilityToDebug(lblDebug);
        }

        public void Init(Settings settings)
        {
            _dms = DMS.Instance;

            Task.Run(async () =>
            {
                await Task.Delay(INTER_REQUEST_INTERVAL);
                if (!HandleError(await _dms.Init(settings)))
                    return;

                Dispatcher.Invoke(() => lblInfo.Content = Utils.L10n.T("LoadingProject"));

                await Task.Delay(INTER_REQUEST_INTERVAL);
                var project = await _dms.GetProject();
                if (project != _dms.Settings.Project)
                {
                    await Task.Delay(INTER_REQUEST_INTERVAL);
                    if (!HandleError(await _dms.SetProject()))
                        return;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"No need to change the project");
                }

                Dispatcher.Invoke(() => lblInfo.Content = Utils.L10n.T("LoadingParameter"));

                await Task.Delay(INTER_REQUEST_INTERVAL);
                var param = await _dms.GetParam();

                if (param != _dms.Settings.ParameterName)
                {
                    await Task.Delay(INTER_REQUEST_INTERVAL);
                    if (!HandleError(await _dms.SetParams()))
                        return;

                    Dispatcher.Invoke(() => lblInfo.Content = Utils.L10n.T("WaitingSystemReady"));
                    await Task.Delay(PARAMETER_LOADING_DURATION);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"No need to change the param");
                }

                Dispatcher.Invoke(() => lblInfo.Content = Utils.L10n.T("DMSReady"));
                await Task.Delay(1000);

                Dispatcher.Invoke(() => Next?.Invoke(this, new EventArgs()));
            });
        }

        // Internal

        DMS _dms = null;

        const int INTER_REQUEST_INTERVAL = 1000;
        const int PARAMETER_LOADING_DURATION = 10000; // ms

        private bool HandleError(string error)
        {
            if (error != null)
            {
                string errorInstruction = Utils.L10n.T("CloseAppAndRestart");
                Dispatcher.Invoke(() => lblInfo.Content = $"{error}\n{errorInstruction}");
            }
            return error == null;
        }
    }
}

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
            Task.Run(async () =>
            {
                await Task.Delay(1000);
                if (!HandleError(await _dms.Init(settings)))
                    return;

                Dispatcher.Invoke(() => lblInfo.Content = Utils.L10n.T("DMSSettingProject"));
                await Task.Delay(1000);

                if (!HandleError(await _dms.SetProject()))
                    return;

                Dispatcher.Invoke(() => lblInfo.Content = Utils.L10n.T("DMSSettingParameter"));
                await Task.Delay(1000);

                if (!HandleError(await _dms.SetParams()))
                    return;

                Dispatcher.Invoke(() => lblInfo.Content = Utils.L10n.T("DMSLoadingParameter"));
                await Task.Delay(PARAMETER_LOADING_DURATION);

                Dispatcher.Invoke(() => lblInfo.Content = Utils.L10n.T("DMSReady"));
                await Task.Delay(1000);

                Dispatcher.Invoke(() => Next?.Invoke(this, new EventArgs()));
            });
        }

        // Internal

        readonly DMS _dms = DMS.Instance;

        const int PARAMETER_LOADING_DURATION = 10000; // ms

        private bool HandleError(string error)
        {
            if (error != null)
            {
                string errorInstruction = Utils.L10n.T("DMSErrorInstruction");
                Dispatcher.Invoke(() => lblInfo.Content = $"{error}\n{errorInstruction}");
            }
            return error == null;
        }
    }
}

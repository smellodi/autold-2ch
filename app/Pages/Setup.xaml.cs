using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AutOlD2Ch.Comm;
using IndicatorDataSource = AutOlD2Ch.Controls.ChannelIndicator.DataSource;

namespace AutOlD2Ch.Pages;

public partial class Setup : Page, IPage<Tests.Test>, INotifyPropertyChanged, IDisposable
{
    public event EventHandler<Tests.Test> Next;
    public event PropertyChangedEventHandler PropertyChanged;

    public double Scale { get; private set; } = 1;

    public string MFCAction => _mfc?.IsOpen ?? false ? "Close" : "Open";        // keys in L10n dictionaries
    public string PIDAction => _pid?.IsOpen ?? false ? "Close" : "Open";        // keys in L10n dictionaries

    public string ScentedAir1 => ScentedAir(1);
    public string ScentedAir2 => ScentedAir(2);

    public Setup()
    {
        InitializeComponent();

        DataContext = this;

        Storage.Instance
            .BindScaleToZoomLevel(sctScale)
            .BindContentToZoomLevel(lblZoom)
            .BindVisibilityToDebug(lblDebug);

        UpdatePortList(cmbPIDPort);
        UpdatePortList(cmbMFCPort);

        cmbGas1.ItemsSource = Enum.GetValues(typeof(Gas));
        cmbGas2.ItemsSource = Enum.GetValues(typeof(Gas));

        Application.Current.Exit += async (s, e) => await Close();

        LoadSettings();

        _usb.Inserted += (s, e) => Dispatcher.Invoke(() =>
            {
                UpdatePortList(cmbPIDPort);
                UpdatePortList(cmbMFCPort);
            });

        _usb.Removed += (s, e) => Dispatcher.Invoke(() =>
            {
                UpdatePortList(cmbPIDPort);
                UpdatePortList(cmbMFCPort);
            });

        _mfc.Closed += (s, e) =>
        {
            DisableIndicators("MFC");
            UpdateUI();
        };
        _pid.Closed += (s, e) =>
        {
            DisableIndicators("PID");
            UpdateUI();
        };


        long startTs = Utils.Timestamp.Ms;
        _mfcTimer.Interval = 1000 * MFC_UPDATE_INTERVAL;
        _mfcTimer.AutoReset = true;
        _mfcTimer.Elapsed += (s, e) => Dispatcher.Invoke(() =>
        {
            if (_mfc.IsOpen)
            {
                var result = _mfc.GetSample(out MFCSample sample);
                _monitor.LogData(LogSource.MFC, sample);
                UpdateIndicators(sample);
            }
            else
            {
                var sample = new MFCSample
                {
                    Time = Utils.Timestamp.Ms
                };
                _monitor.LogData(LogSource.MFC, sample);
            }
        });

        _pidTimer.Interval = 1000 * PID_UPDATE_INTERVAL;
        _pidTimer.AutoReset = true;
        _pidTimer.Elapsed += (s, e) => Dispatcher.Invoke(() =>
        {
            if (_pid.IsOpen)
            {
                var result = _pid.GetSample(out PIDSample sample);
                _monitor.LogData(LogSource.PID, sample);
                UpdateIndicators(sample);
            }
            else
            {
                var sample = new PIDSample
                {
                    Time = Utils.Timestamp.Ms
                };
                _monitor.LogData(LogSource.PID, sample);
            }
        });

        UpdateUI();
    }

    public void Dispose()
    {
        _mfcTimer.Stop();
        _pidTimer.Stop();

        _mfcTimer.Dispose();
        _pidTimer.Dispose();

        GC.SuppressFinalize(this);

        Task.Delay(300).Wait();
    }


    // Internal

    const double MFC_UPDATE_INTERVAL = 1;   // seconds
    const double PID_UPDATE_INTERVAL = 0.2; // seconds

    readonly USB _usb = new();
    readonly MFC _mfc = MFC.Instance;
    readonly PID _pid = PID.Instance;

    CommMonitor _monitor;

    readonly System.Timers.Timer _mfcTimer = new();
    readonly System.Timers.Timer _pidTimer = new();

    Controls.ChannelIndicator _currentIndicator = null;

    private static string ScentedAir(int id) => Utils.L10n.T("ScentedAir") + $" #{id}";

    private static void UpdatePortList(ComboBox cmb)
    {
        var current = cmb.SelectedValue;

        cmb.Items.Clear();

        var availablePorts = System.IO.Ports.SerialPort.GetPortNames();
        var ports = new HashSet<string>(availablePorts);
        foreach (var port in ports)
        {
            cmb.Items.Add(port);
        }

        if (current != null)
        {
            foreach (var item in ports)
            {
                if (item == current.ToString())
                {
                    cmb.SelectedItem = item;
                    break;
                }
            }
        }
    }

    private void UpdateUI()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MFCAction)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PIDAction)));

        cmbPIDPort.IsEnabled = !_pid.IsOpen;
        cmbMFCPort.IsEnabled = !_mfc.IsOpen;

        btnOdorProduction.IsEnabled = _pid.IsOpen && _mfc.IsOpen;
        btnComparison.IsEnabled = _pid.IsOpen && _mfc.IsOpen;
        btnLptController.IsEnabled = _pid.IsOpen && _mfc.IsOpen;

        grdPlayground.IsEnabled = _mfc.IsOpen;

        foreach (Controls.ChannelIndicator chi in stpIndicators.Children)
        {
            if ("MFC".Equals(chi.Tag))
            {
                chi.IsEnabled = _mfc.IsOpen;
            }
            else if ("PID".Equals(chi.Tag))
            {
                chi.IsEnabled = _pid.IsOpen;
            }
        }
    }

    private bool Toggle(CommPort port, string address)
    {
        bool success = true;

        if (!port.IsOpen)
        {
            var result = port.Start(address);
            success = result.Error == Error.Success;

            LogSource source = port.Name == "PID" ? LogSource.PID : LogSource.MFC;
            _monitor.LogResult(source, result);
        }
        else
        {
            port.Stop();
        }

        return success;
    }

    private async Task Close()
    {
        if (_pid.IsOpen)
        {
            _pid.Stop();
        }
        if (_mfc.IsOpen)
        {
            _mfc.Stop();
        }

        _mfcTimer.Stop();
        _pidTimer.Stop();

        SaveSettings();

        await Dispatcher.Yield();
    }

    private void LoadSettings()
    {
        var settings = Properties.Settings.Default;

        foreach (string item in cmbMFCPort.Items)
        {
            if (item == settings.Setup_MFCPort)
            {
                cmbMFCPort.SelectedItem = item;
                break;
            }
        }

        foreach (string item in cmbPIDPort.Items)
        {
            if (item == settings.Setup_PIDPort)
            {
                cmbPIDPort.SelectedItem = item;
                break;
            }
        }

        txbFreshAir.Text = settings.Setup_MFC_FreshAir.ToString();
        txbOdor1.Text = settings.Setup_MFC_Odor1.ToString();
        txbOdor2.Text = settings.Setup_MFC_Odor2.ToString();
        cmbGas1.SelectedItem = (Gas)settings.Setup_Gas1;
        cmbGas2.SelectedItem = (Gas)settings.Setup_Gas2;
    }

    private void SaveSettings()
    {
        var settings = Properties.Settings.Default;
        try
        {
            settings.Setup_MFC_FreshAir = double.Parse(txbFreshAir.Text);
            settings.Setup_MFC_Odor1 = double.Parse(txbOdor1.Text);
            settings.Setup_MFC_Odor2 = double.Parse(txbOdor2.Text);
            settings.Setup_MFCPort = cmbMFCPort.SelectedItem?.ToString() ?? "";
            settings.Setup_PIDPort = cmbPIDPort.SelectedItem?.ToString() ?? "";
            settings.Setup_Gas1 = (int)(Gas)cmbGas1.SelectedItem;
            settings.Setup_Gas2 = (int)(Gas)cmbGas2.SelectedItem;
        }
        catch { }
        settings.Save();
    }

    private void GetValveStates()
    {
        rdbValve1ToUser.IsChecked = _mfc.OdorDirection.HasFlag(MFC.ValvesOpened.Valve1);
        rdbValve1ToWaste.IsChecked = !rdbValve1ToUser.IsChecked;
        rdbValve2ToUser.IsChecked = _mfc.OdorDirection.HasFlag(MFC.ValvesOpened.Valve2);
        rdbValve2ToWaste.IsChecked = !rdbValve2ToUser.IsChecked;
    }

    private void DisableIndicators(string deviceType)
    {
        foreach (Controls.ChannelIndicator chi in stpIndicators.Children)
        {
            if (deviceType.Equals(chi.Tag))
            {
                chi.Value = 0;
            }
        }

        if (_currentIndicator != null && deviceType.Equals(_currentIndicator.Tag))
        {
            _currentIndicator.IsActive = false;
            _currentIndicator = null;
            lmsGraph.Empty();
        }
    }

    private void UpdateGraph(IndicatorDataSource dataSource, double timestamp, double value)
    {
        if (_currentIndicator?.Source == dataSource)
        {
            lmsGraph.Add(timestamp, value);
        }
    }

    private void ResetGraph(IndicatorDataSource dataSource, double baseValue)
    {
        if (_currentIndicator?.Source == dataSource)
        {
            var updateInterval = dataSource switch
            {
                IndicatorDataSource.CleanAir or IndicatorDataSource.ScentedAir1 or IndicatorDataSource.ScentedAir2 => MFC_UPDATE_INTERVAL,
                IndicatorDataSource.Temp or IndicatorDataSource.PID => PID_UPDATE_INTERVAL,
                _ => throw new NotImplementedException($"Data source {dataSource} is now supported")
            };
            lmsGraph.Reset(updateInterval, baseValue);
        }
    }

    private void UpdateIndicators(MFCSample sample)
    {
        chiFreshAir.Value = sample.A.MassFlow;
        chiOdor1.Value = sample.B.MassFlow;
        chiOdor2.Value = sample.C.MassFlow;

        double timestamp = 0.001 * sample.Time;
        UpdateGraph(IndicatorDataSource.CleanAir, timestamp, sample.A.MassFlow);
        UpdateGraph(IndicatorDataSource.ScentedAir1, timestamp, sample.B.MassFlow);
        UpdateGraph(IndicatorDataSource.ScentedAir2, timestamp, sample.C.MassFlow);
    }

    private void UpdateIndicators(PIDSample sample)
    {
        chiPIDTemp.Value = sample.Temperature;
        chiPIDVoltage.Value = sample.PID;

        double timestamp = 0.001 * sample.Time;
        UpdateGraph(IndicatorDataSource.Temp, timestamp, sample.Temperature);
        UpdateGraph(IndicatorDataSource.PID, timestamp, sample.PID);
    }

    private void ResetIndicatorGraphValue(MFCSample? sample)
    {
        ResetGraph(IndicatorDataSource.CleanAir, sample?.A.MassFlow ?? 0);
        ResetGraph(IndicatorDataSource.ScentedAir1, sample?.B.MassFlow ?? 0);
        ResetGraph(IndicatorDataSource.ScentedAir2, sample?.C.MassFlow ?? 0);
    }

    private void ResetIndicatorGraphValue(PIDSample? sample)
    {
        ResetGraph(IndicatorDataSource.Temp, sample?.Temperature ?? 0);
        ResetGraph(IndicatorDataSource.PID, sample?.PID ?? 0);
    }


    // UI events

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        _monitor = CommMonitor.Instance;
        _monitor.MFCUpdateInterval = MFC_UPDATE_INTERVAL;
        _monitor.PIDUpdateInterval = PID_UPDATE_INTERVAL;

        if (Focusable)
        {
            Focus();
        }

        if (_mfc.IsOpen)
        {
            ResetIndicatorGraphValue((MFCSample?)null);

            _mfcTimer.Start();
            GetValveStates();
        }

        if (_pid.IsOpen)
        {
            if (_pid.GetSample(out PIDSample sample).Error == Error.Success)
            {
                ResetIndicatorGraphValue(sample);
            }

            _pidTimer.Start();
        }
    }

    private async void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        _mfcTimer.Stop();
        _pidTimer.Stop();

        await Task.Delay(100);
    }

    private void Page_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F2)
        {
            cmbMFCPort.Items.Clear();
            cmbMFCPort.Items.Add("COM3");
            cmbMFCPort.SelectedIndex = 0;

            cmbPIDPort.Items.Clear();
            cmbPIDPort.Items.Add("COM4");
            cmbPIDPort.SelectedIndex = 0;

            _mfc.IsDebugging = true;
            _pid.IsDebugging = true;

            Storage.Instance.IsDebugging = true;
            lblDebug.Visibility = Visibility.Visible;

            MFCToggle_Click(this, null);
            PIDToggle_Click(this, null);
        }
    }

    private void Port_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateUI();
    }

    private void MFCToggle_Click(object sender, RoutedEventArgs e)
    {
        if (!Toggle(_mfc, (string)cmbMFCPort.SelectedItem))
        {
            Utils.MsgBox.Error(App.Name, Utils.L10n.T("CannotOpenPort"));
        }
        else if (_mfc.IsOpen)
        {
            _mfcTimer.Start();

            ResetIndicatorGraphValue((MFCSample?)null);
            GetValveStates();
        }
        else
        {
            _mfcTimer.Stop();
        }

        UpdateUI();
    }

    private void PIDToggle_Click(object sender, RoutedEventArgs e)
    {
        if (!Toggle(_pid, (string)cmbPIDPort.SelectedItem))
        {
            Utils.MsgBox.Error(App.Name, Utils.L10n.T("CannotOpenPort"));
        }
        else if (_pid.IsOpen)
        {
            _pidTimer.Start();

            if (_pid.GetSample(out PIDSample sample).Error == Error.Success)
            {
                ResetIndicatorGraphValue(sample);
            }
        }
        else
        {
            _pidTimer.Stop();
        }

        UpdateUI();
    }

    private void FreshAir_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SetFreshAir_Click(sender, e);
        }
    }

    private void SetFreshAir_Click(object sender, RoutedEventArgs e)
    {
        Utils.Validation.Do(txbFreshAir, 0, 10, (object s, double value) => _mfc.FreshAirSpeed = value );
    }

    private void Odor1_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SetOdor1_Click(sender, e);
        }
    }

    private void Odor2_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SetOdor2_Click(sender, e);
        }
    }

    private void SetOdor1_Click(object sender, RoutedEventArgs e)
    {
        Utils.Validation.Do(txbOdor1, 0, 500, (object s, double value) => _mfc.Odor1Speed = value );
    }

    private void SetOdor2_Click(object sender, RoutedEventArgs e)
    {
        Utils.Validation.Do(txbOdor2, 0, 500, (object s, double value) => _mfc.Odor2Speed = value);
    }

    private void SetDirection_Click(object sender, RoutedEventArgs e)
    {
        _mfc.OdorDirection = (rdbValve1ToUser.IsChecked, rdbValve2ToUser.IsChecked) switch
        {
            (false, false) => MFC.ValvesOpened.None,
            (true, false) => MFC.ValvesOpened.Valve1,
            (false, true) => MFC.ValvesOpened.Valve2,
            (true, true) => MFC.ValvesOpened.All,
            _ => throw new NotImplementedException()
        };
    }

    private void LptController_Click(object sender, RoutedEventArgs e)
    {
        _mfcTimer.Stop();
        _pidTimer.Stop();

        Next?.Invoke(this, Tests.Test.LptController);
    }

    private void Comparison_Click(object sender, RoutedEventArgs e)
    {
        _mfcTimer.Stop();
        _pidTimer.Stop();

        Next?.Invoke(this, Tests.Test.Comparison);
    }

    private void OdorProduction_Click(object sender, RoutedEventArgs e)
    {
        _mfcTimer.Stop();
        _pidTimer.Stop();

        Next?.Invoke(this, Tests.Test.OdorProduction);
    }

    private void ChannelIndicator_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var chi = (Controls.ChannelIndicator)sender;
        if (!chi.IsActive)
        {
            if (_currentIndicator != null)
            {
                _currentIndicator.IsActive = false;
            }

            _currentIndicator = chi;
            _currentIndicator.IsActive = true;

            ResetIndicatorGraphValue((MFCSample?)null);
            ResetIndicatorGraphValue((PIDSample?)null);
        }
    }

    private void Language_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        System.Threading.Thread.CurrentThread.CurrentCulture = culture;
        System.Threading.Thread.CurrentThread.CurrentUICulture = culture;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ScentedAir1)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ScentedAir2)));
    }

    private void Gas_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cmbGas1.SelectedItem != null && cmbGas2.SelectedItem != null)
        {
            SaveSettings();
        }
    }
}

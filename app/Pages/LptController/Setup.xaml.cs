using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Settings = AutOlD2Ch.Tests.LptController.Settings;

namespace AutOlD2Ch.Pages.LptController;

public partial class Setup : Page, IPage<Settings?>, Tests.ITestEmulator
{
    public event EventHandler<Settings?>? Next;

    public Setup()
    {
        InitializeComponent();

        Storage.Instance
            .BindScaleToZoomLevel(sctScale)
            .BindContentToZoomLevel(lblZoom)
            .BindVisibilityToDebug(lblDebug);

        UpdateComPortList(cmbComPort);
        _usb.Inserted += (s, e) => Dispatcher.Invoke(() =>
        {
            UpdateComPortList(cmbComPort);
        });

        _usb.Removed += (s, e) => Dispatcher.Invoke(() =>
        {
            UpdateComPortList(cmbComPort);
        });

        cmbLptPort.ItemsSource = LptPort.GetPorts();
        if (cmbLptPort.Items.Count > _settings.LptPort)
        {
            cmbLptPort.SelectedIndex = _settings.LptPort;
        }
        else if (cmbLptPort.Items.Count > 0)
        {
            cmbLptPort.SelectedIndex = 0;
        }

        if (cmbComPort.Items.Count > 0)
        {
            foreach (string port in cmbComPort.Items)
            {
                if (port == _settings.ComPort)
                {
                    cmbComPort.SelectedItem = port;
                }
            }
        }

        txbFreshAir.Text = _settings.FreshAir.ToString("F1");
        txbPulses.Text = _settings.SerializePulses();
        txbOdorFlowDuration.Text = _settings.OdorFlowDuration.ToString();
        txbPIDSamplingInterval.Text = _settings.PIDReadingInterval.ToString();
    }

    public void EmulationInit() { }

    public void EmulationFinalize() { }


    // Internal

    readonly Settings _settings = new();
    readonly USB _usb = new();

    private static void UpdateComPortList(ComboBox cmb)
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

    private Utils.Validation? CheckInput()
    {
        if (!Storage.Instance.IsDebugging && cmbLptPort.SelectedIndex < 0)
        {
            return new Utils.Validation(cmbLptPort, Utils.L10n.T("LptNotSelected"));
        }
        /*
        if (cmbComPort.SelectedIndex < 0)
        {
            return new Utils.Validation(cmbComPort, Utils.L10n.T("ComNotSelected"));
        }*/

        var pulses = Settings.ParsePulses(txbPulses.Text.Replace("\r\n", "\n"), out string? error);
        if (pulses == null)
        {
            return new Utils.Validation(txbPulses, error);
        }

        var validations = new List<Utils.Validation>
        {
            new Utils.Validation(txbFreshAir, 1, 10,
                    Utils.Validation.ValueFormat.Float),
            new Utils.Validation(txbOdorFlowDuration, 0.1,
                    Comm.MFC.MAX_SHORT_PULSE_DURATION,
                    Utils.Validation.ValueFormat.Float),
            new Utils.Validation(txbPIDSamplingInterval, 100, 5000,
                    Utils.Validation.ValueFormat.Integer),
        };

        foreach (var (marker, pulse) in pulses)
        {
            if (pulse.Channel1 != null)
            {
                validations.Add(new Utils.Validation(txbPulses,
                    pulse.Channel1.Delay.ToString(), 0, 65000,
                    Utils.Validation.ValueFormat.Integer));
                validations.Add(new Utils.Validation(txbPulses,
                    pulse.Channel1.GetDuration(0).ToString(), 0,
                    Comm.MFC.MAX_SHORT_PULSE_DURATION,
                    Utils.Validation.ValueFormat.Integer));
                validations.Add(new Utils.Validation(txbPulses,
                    pulse.Channel1.Flow.ToString(), 0, 250,
                    Utils.Validation.ValueFormat.Float));
            }
            if (pulse.Channel2 != null)
            {
                validations.Add(new Utils.Validation(txbPulses,
                    pulse.Channel2.Delay.ToString(), 0, 65000,
                    Utils.Validation.ValueFormat.Integer));
                validations.Add(new Utils.Validation(txbPulses,
                    pulse.Channel2.GetDuration(0).ToString(), 0,
                    Comm.MFC.MAX_SHORT_PULSE_DURATION,
                    Utils.Validation.ValueFormat.Integer));
                validations.Add(new Utils.Validation(txbPulses,
                    pulse.Channel2.Flow.ToString(), 0, 250,
                    Utils.Validation.ValueFormat.Float));
            }
        }

        foreach (var v in validations)
        {
            if (!v.IsValid)
            {
                return v;
            }
        }

        double.TryParse(txbOdorFlowDuration.Text, out double odorFlowDurationSec);

        var longestDurationMs = pulses.Max(kv => kv.Value.GetDuration((int)(odorFlowDurationSec * 1000)));
        var isOdorFlowDurationLongEnough = new Utils.Validation(
            txbOdorFlowDuration, 0.001 * longestDurationMs,
            Comm.MFC.MAX_SHORT_PULSE_DURATION,
            Utils.Validation.ValueFormat.Float);
        if (!isOdorFlowDurationLongEnough.IsValid)
        {
            return isOdorFlowDurationLongEnough;
        }

        return null;
    }


    // UI events

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        var validation = CheckInput();
        if (validation != null)
        {
            var msg = Utils.L10n.T("CorrectAndTryAgain");
            Utils.MsgBox.Error(App.Name, $"{validation}.\n{msg}");
            validation.Source.Focus();
            (validation.Source as TextBox)?.SelectAll();
        }
        else
        {
            _settings.LptPort = cmbLptPort.SelectedIndex;
            _settings.ComPort = (string)cmbComPort.SelectedItem;
            _settings.FreshAir = double.Parse(txbFreshAir.Text);
            _settings.Pulses = Settings.ParsePulses(txbPulses.Text.Replace("\r\n", "\n"), out string? _);
            _settings.OdorFlowDuration = double.Parse(txbOdorFlowDuration.Text);
            _settings.PIDReadingInterval = int.Parse(txbPIDSamplingInterval.Text);

            _settings.Save();

            Next?.Invoke(this, _settings);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Next?.Invoke(this, null);
    }
}

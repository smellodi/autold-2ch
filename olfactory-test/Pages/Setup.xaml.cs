﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Olfactory.Comm;
using IndicatorDataSource = Olfactory.Controls.ChannelIndicator.DataSource;
using BreathingStage = Olfactory.Tests.ThresholdTest.BreathingStage;

namespace Olfactory.Pages
{
    public partial class Setup : Page, IPage<Tests.Test>, INotifyPropertyChanged
    {
        public event EventHandler<Tests.Test> Next;
        public event PropertyChangedEventHandler PropertyChanged;

        public double Scale { get; private set; } = 1;

        public string MFCAction => _mfc?.IsOpen ?? false ? "Close" : "Open";        // keys in L10n dictionaries
        public string PIDAction => _pid?.IsOpen ?? false ? "Close" : "Open";        // keys in L10n dictionaries

        public Setup()
        {
            InitializeComponent();

            DataContext = this;

            Storage.Instance
                .BindScaleToZoomLevel(sctScale1)
                .BindScaleToZoomLevel(sctScale2)
                .BindVisibilityToDebug(lblDebug);

            UpdatePortList(cmbPIDPort);
            UpdatePortList(cmbMFCPort);

            Application.Current.Exit += (s, e) => Close();

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

            _breathingDetector.StageChanged += (s, e) => Dispatcher.Invoke(() =>
            {
                rctBreathingStage.Fill = e switch
                {
                    BreathingStage.Inhale => Tests.ThresholdTest.BreathingDetector.InhaleBrush,
                    BreathingStage.Exhale => Tests.ThresholdTest.BreathingDetector.ExhaleBrush,
                    _ => null
                };
                lblBreathingStage.Text = Utils.L10n.T(e.ToString());
            });

            UpdateUI();
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

        readonly Tests.ThresholdTest.BreathingDetector _breathingDetector = new();

        Controls.ChannelIndicator _currentIndicator = null;

        private void UpdatePortList(ComboBox cmb)
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
            btnThresholdTest.IsEnabled = _pid.IsOpen && _mfc.IsOpen;

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

        private void Close()
        {
            if (_pid.IsOpen)
            {
                _pid.Stop();
            }
            if (_mfc.IsOpen)
            {
                _mfc.Stop();
            }

            SaveSettings();
        }

        private void LoadSettings()
        {
            var settings = Properties.Settings.Default;
            txbFreshAir.Text = settings.Setup_MFC_FreshAir.ToString();
            txbOdor.Text = settings.Setup_MFC_Odor.ToString();
            rdbValve1ToWaste.IsChecked = settings.Setup_MFC_Valve1 == 0;
            rdbValve1ToSystem.IsChecked = settings.Setup_MFC_Valve1 == 1;
            rdbValve2ToWaste.IsChecked = settings.Setup_MFC_Valve2 == 0;
            rdbValve2ToUser.IsChecked = settings.Setup_MFC_Valve2 == 1;

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
        }

        private void SaveSettings()
        {
            var settings = Properties.Settings.Default;
            try
            {
                settings.Setup_MFC_FreshAir = double.Parse(txbFreshAir.Text);
                settings.Setup_MFC_Odor = double.Parse(txbOdor.Text);
                settings.Setup_MFC_Valve1 = rdbValve1ToWaste.IsChecked ?? false ? 0 : 1;
                settings.Setup_MFC_Valve2 = rdbValve2ToWaste.IsChecked ?? false ? 0 : 1;
                settings.Setup_MFCPort = cmbMFCPort.SelectedItem?.ToString() ?? "";
                settings.Setup_PIDPort = cmbPIDPort.SelectedItem?.ToString() ?? "";
            }
            catch { }
            settings.Save();
        }

        private void GetValveStates()
        {
            rdbValve1ToSystem.IsChecked = _mfc.OdorDirection.HasFlag(MFC.OdorFlowsTo.System);
            rdbValve1ToWaste.IsChecked = !rdbValve1ToSystem.IsChecked;
            rdbValve2ToUser.IsChecked = _mfc.OdorDirection.HasFlag(MFC.OdorFlowsTo.User);
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
                    IndicatorDataSource.CleanAir or IndicatorDataSource.ScentedAir => MFC_UPDATE_INTERVAL,
                    IndicatorDataSource.Loop or IndicatorDataSource.PID => PID_UPDATE_INTERVAL,
                    _ => throw new NotImplementedException($"Data source {dataSource} is now supported")
                };
                lmsGraph.Reset(updateInterval, baseValue);
            }
        }

        private void UpdateIndicators(MFCSample sample)
        {
            chiFreshAir.Value = sample.A.MassFlow;
            chiOdor.Value = sample.B.MassFlow;

            double timestamp = 0.001 * sample.Time;
            UpdateGraph(IndicatorDataSource.CleanAir, timestamp, sample.A.MassFlow);
            UpdateGraph(IndicatorDataSource.ScentedAir, timestamp, sample.B.MassFlow);
        }

        private void UpdateIndicators(PIDSample sample)
        {
            chiPIDTemp.Value = sample.Loop;
            chiPIDVoltage.Value = sample.PID;

            double timestamp = 0.001 * sample.Time;
            UpdateGraph(IndicatorDataSource.Loop, timestamp, sample.Loop);
            UpdateGraph(IndicatorDataSource.PID, timestamp, sample.PID);

            _breathingDetector.Feed(sample.Time, sample.Loop);
        }

        private void ResetIndicatorGraphValue(MFCSample? sample)
        {
            ResetGraph(IndicatorDataSource.CleanAir, sample?.A.MassFlow ?? 0);
            ResetGraph(IndicatorDataSource.ScentedAir, sample?.B.MassFlow ?? 0);
        }

        private void ResetIndicatorGraphValue(PIDSample? sample)
        {
            ResetGraph(IndicatorDataSource.Loop, sample?.Loop ?? 0);
            ResetGraph(IndicatorDataSource.PID, sample?.PID ?? 0);

            rctBreathingStage.Fill = null;
            lblBreathingStage.Text = "";
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
                //lmsOdor.Reset();
                ResetIndicatorGraphValue((MFCSample?)null);

                _mfcTimer.Start();
                GetValveStates();
            }

            if (_pid.IsOpen)
            {
                if (_pid.GetSample(out PIDSample sample).Error == Error.Success)
                {
                    //lmsPIDValue.Reset(sample.PID);
                    ResetIndicatorGraphValue(sample);
                }

                _pidTimer.Start();
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _mfcTimer.Stop();
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
            }
            else if (e.Key >= Key.D0 && e.Key <= Key.D9 && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                PIDEmulator.Instance.Model._PulseInput(e.Key - Key.D0);
            }
            else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9 && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                PIDEmulator.Instance.Model._PulseOutput(e.Key - Key.NumPad0);
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
                Utils.MsgBox.Error(Title, Utils.L10n.T("CannotOpenPort"));
            }
            else if (_mfc.IsOpen)
            {
                _mfcTimer.Start();
                //lmsOdor.Reset(/*Controls.LiveMeasurement.OdorColor(_mfc.OdorDirection)*/);
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
                Utils.MsgBox.Error(Title, Utils.L10n.T("CannotOpenPort"));
            }
            else if (_pid.IsOpen)
            {
                _pidTimer.Start();

                if (_pid.GetSample(out PIDSample sample).Error == Error.Success)
                {
                    //lmsPIDValue.Reset(/*Controls.LiveMeasurement.BRUSH_NEUTRAL, */sample.PID);
                    ResetIndicatorGraphValue(sample);
                }
            }
            else
            {
                _pidTimer.Stop();
            }

            UpdateUI();
        }

        private void SetFreshAir_Click(object sender, RoutedEventArgs e)
        {
            Utils.Validation.Do(txbFreshAir, 0, 10, (object s, double value) => _mfc.FreshAirSpeed = value );
        }

        private void SetOdor_Click(object sender, RoutedEventArgs e)
        {
            Utils.Validation.Do(txbOdor, 0, 500, (object s, double value) => _mfc.OdorSpeed = value );
        }

        private void SetDirection_Click(object sender, RoutedEventArgs e)
        {
            _mfc.OdorDirection = (rdbValve1ToWaste.IsChecked, rdbValve2ToWaste.IsChecked) switch
            {
                (true, true) => MFC.OdorFlowsTo.Waste,
                (true, false) => MFC.OdorFlowsTo.WasteAndUser,
                (false, true) => MFC.OdorFlowsTo.SystemAndWaste,
                (false, false) => MFC.OdorFlowsTo.SystemAndUser,
                _ => throw new NotImplementedException()
            };
        }

        private void OdorProduction_Click(object sender, RoutedEventArgs e)
        {
            _mfcTimer.Stop();
            _pidTimer.Stop();

            Next?.Invoke(this, Tests.Test.OdorProduction);
        }

        private void ThresholdTest_Click(object sender, RoutedEventArgs e)
        {
            _mfcTimer.Stop();
            _pidTimer.Stop();

            Next?.Invoke(this, Tests.Test.Threshold);
        }

        private void FreshAir_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SetFreshAir_Click(sender, e);
            }
        }

        private void Odor_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SetOdor_Click(sender, e);
            }
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
        }
    }
}

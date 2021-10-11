using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Olfactory.Utils;
using FlowStart = Olfactory.Tests.ThresholdTest.Settings.FlowStartTrigger;
using ProcedureType = Olfactory.Tests.ThresholdTest.Settings.ProcedureType;

namespace Olfactory.Pages.ThresholdTest
{
    public partial class Setup : Page, IPage<Tests.ThresholdTest.Settings>
    {
        public event EventHandler<Tests.ThresholdTest.Settings> Next;

        public Setup()
        {
            InitializeComponent();

            Storage.Instance.BindScaleToZoomLevel(sctScale);
            Storage.Instance.BindVisibilityToDebug(lblDebug);

            txbFreshAir.Text = _settings.FreshAir.ToString("F1");
            txbPPMs.Text = string.Join(LIST_DELIM, _settings.PPMs);
            txbOdorPreparationDuration.Text = _settings.OdorPreparationDuration.ToString();
            txbPenSniffingDuration.Text = _settings.PenSniffingDuration.ToString();
            txbTurningPoints.Text = _settings.TurningPoints.ToString();
            txbTurningPointsToCount.Text = _settings.TurningPointsToCount.ToString();
            txbRecognitionsInRow.Text = _settings.RecognitionsInRow.ToString();
            txbFamiliarizationDuration.Text = _settings.FamiliarizationDuration.ToString();
            chkFeedbackLoopToReachLevel.IsChecked = _settings.UseFeedbackLoopToReachLevel;
            chkFeedbackLoopToKeepLevel.IsChecked = _settings.UseFeedbackLoopToKeepLevel;
            chkUseValveTimer.IsChecked = _settings.UseValveTimer;

            cmbFlowStart.ItemsSource = FLOW_START_TOOLTIPS
                .Select(item => new ComboBoxItem()
                {
                    Content = L10n.T(item.Key.ToString()),
                    ToolTip = item.Value,
                });
            cmbFlowStart.SelectedIndex = (int)_settings.FlowStart;

            cmbProcedureType.ItemsSource = PROCEDURE_TYPE_TOOLTIPS
                .Select(item => new ComboBoxItem()
                {
                    Content = L10n.T(item.Key.ToString()),
                    ToolTip = item.Value,
                });
            cmbProcedureType.SelectedIndex = (int)_settings.Type;

            FlowStart_SelectionChanged(null, null);

            _pidSampling = _settings.PIDReadingInterval;
        }


        // Internal

        const char LIST_DELIM = ' ';
        const int PID_SAMPLING_INTERVAL_FOR_INHALE_DETECTOR = 200;

        readonly Tests.ThresholdTest.Settings _settings = new();
        readonly Dictionary<FlowStart, string> FLOW_START_TOOLTIPS = new()
        {
            { FlowStart.Immediate, L10n.T("OdorStartsImmediately") },
            { FlowStart.Manual, L10n.T("OdorStartsAfterKeyPress") },
            { FlowStart.Automatic, L10n.T("OdorStartsAfterInhale") },
        };
        readonly Dictionary<ProcedureType, string> PROCEDURE_TYPE_TOOLTIPS = new()
        {
            { ProcedureType.ThreePens, L10n.T("ProcTypeThreePens") },
            { ProcedureType.TwoPens, L10n.T("ProcTypeTwoPens") },
            { ProcedureType.OnePen, L10n.T("ProcTypeOnePen") },
        };

        int _pidSampling;

        bool IsAutomaticFlowStart => (FlowStart)cmbFlowStart.SelectedIndex == FlowStart.Automatic;

        private Utils.Validation CheckInput()
        {
            int.TryParse(txbTurningPoints.Text, out int maxTurningPoints);

            var validations = new Utils.Validation[]
            {
                new Utils.Validation(txbFreshAir, 1, 10, Utils.Validation.ValueFormat.Float),
                new Utils.Validation(txbPPMs, 0.5, 250, Utils.Validation.ValueFormat.Float, LIST_DELIM),
                new Utils.Validation(txbFamiliarizationDuration, 1, 5, Utils.Validation.ValueFormat.Integer),
                new Utils.Validation(txbOdorPreparationDuration, 10, 300, Utils.Validation.ValueFormat.Integer),
                new Utils.Validation(txbPenSniffingDuration, 0.2, 10, Utils.Validation.ValueFormat.Float),
                new Utils.Validation(txbTurningPoints, 2, 15, Utils.Validation.ValueFormat.Integer),
                new Utils.Validation(txbTurningPointsToCount, 2, maxTurningPoints, Utils.Validation.ValueFormat.Integer),
                new Utils.Validation(txbRecognitionsInRow, 1, 5, Utils.Validation.ValueFormat.Integer),
                new Utils.Validation(txbPIDSamplingInterval, 100, 5000, Utils.Validation.ValueFormat.Integer),
            };

            foreach (var v in validations)
            {
                if (!v.IsValid)
                {
                    return v;
                }
            }

            return null;
        }


        // UI events

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            var validation = CheckInput();
            if (validation != null)
            {
                var msg = L10n.T("CorrectAndTryAgain");
                MsgBox.Error(Title, $"{validation}.\n{msg}");
                validation.Source.Focus();
                validation.Source.SelectAll();
            }
            else
            {
                _settings.FreshAir = double.Parse(txbFreshAir.Text);
                _settings.PPMs = txbPPMs.Text
                    .Split(LIST_DELIM)
                    .Select(val => double.Parse(val))
                    .ToArray();
                _settings.OdorPreparationDuration = int.Parse(txbOdorPreparationDuration.Text);
                _settings.PenSniffingDuration = double.Parse(txbPenSniffingDuration.Text);
                _settings.TurningPoints = int.Parse(txbTurningPoints.Text);
                _settings.TurningPointsToCount = int.Parse(txbTurningPointsToCount.Text);
                _settings.RecognitionsInRow = int.Parse(txbRecognitionsInRow.Text);
                _settings.FamiliarizationDuration = double.Parse(txbFamiliarizationDuration.Text);
                if (!IsAutomaticFlowStart)
                {
                    _settings.PIDReadingInterval = int.Parse(txbPIDSamplingInterval.Text);
                }
                _settings.UseFeedbackLoopToReachLevel = chkFeedbackLoopToReachLevel.IsChecked ?? false;
                _settings.UseFeedbackLoopToKeepLevel = chkFeedbackLoopToKeepLevel.IsChecked ?? false;
                _settings.FlowStart = (FlowStart)cmbFlowStart.SelectedIndex;
                _settings.Type = (ProcedureType)cmbProcedureType.SelectedIndex;
                _settings.UseValveTimer = chkUseValveTimer.IsChecked ?? false;

                _settings.Save();

                if (IsAutomaticFlowStart)
                {
                    // it will not be saved into settings file
                    _settings.PIDReadingInterval = PID_SAMPLING_INTERVAL_FOR_INHALE_DETECTOR;
                }

                Next?.Invoke(this, _settings);
            }
        }
 
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Next?.Invoke(this, null);
        }

        private void PIDSamplingInterval_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsAutomaticFlowStart && int.TryParse(txbPIDSamplingInterval.Text, out int pidSampling))
            {
                _pidSampling = pidSampling;
            }
        }

        private void FlowStart_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            txbPIDSamplingInterval.IsEnabled = !IsAutomaticFlowStart;
            txbPIDSamplingInterval.Text = (IsAutomaticFlowStart ? PID_SAMPLING_INTERVAL_FOR_INHALE_DETECTOR : _pidSampling).ToString();
        }
    }
}
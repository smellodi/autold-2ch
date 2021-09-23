using System;

namespace Olfactory.Tests.ThresholdTest
{
    /// <summary>
    /// Yes-No procedure.
    /// Requires min and max PPM values only
    /// </summary>
    public class TurningYesNo : TurningBase
    {
        public override double PPM => _ppmValue;

        public override bool IsValid => _ppmValue >= 0;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="minPPM">minimal PPM value</param>
        /// <param name="maxPPM">maximal PPM value</param>
        /// <param name="requiredRecognitions">required recognitions to treat a PPM value as recognized</param>
        public TurningYesNo(double minPPM, double maxPPM, int requiredRecognitions) : base(requiredRecognitions)
        {
            _minPPM = minPPM;
            _maxPPM = maxPPM;

            _requiredRecognitions = requiredRecognitions;

            _ppmValue = _minPPM + (_maxPPM - _minPPM) * INITIAL_REL_PPM_VALUE;
            _ppmStep = (_maxPPM - _minPPM) * INITIAL_REL_PPM_STEP;
        }

        public override void Next(Pen[] pens)
        {
            base.Next(pens);

            var color = _odoredPenInRow switch
            {
                0 => PenColor.Odor,
                >= ODORED_PEN_COUNT_PER_ODORLESS_PEN => PenColor.NonOdor,
                _ => _rnd.NextDouble() < 1d / ODORED_PEN_COUNT_PER_ODORLESS_PEN ? PenColor.NonOdor : PenColor.Odor
            };

            pens[0].Color = color;

            _odoredPenInRow = color == PenColor.Odor ? _odoredPenInRow + 1 : 0;
        }

        public override bool AcceptAnswer(bool wasRecognized)
        {
            if (PenSmells)
            {
                if (!wasRecognized)
                {
                    UpdateDirection(IProcState.PPMChangeDirection.Increasing);
                    UpdatePPMValue();
                }
                else if (++_recognitionsInRow == _requiredRecognitions)    // decrease ppm only if recognized correctly few times in a row
                {
                    UpdateDirection(IProcState.PPMChangeDirection.Decreasing);
                    UpdatePPMValue();
                }
            }
            else if (!wasRecognized)
            {
                Penalize();
                UpdatePPMValue();
            }

            return IsValid;
        }


        // Internal

        const double INITIAL_REL_PPM_VALUE = 0.25;
        const double INITIAL_REL_PPM_STEP = 1d / 8;
        const int ODORED_PEN_COUNT_PER_ODORLESS_PEN = 4;

        readonly Random _rnd = new((int)DateTime.Now.Ticks);
        readonly double _minPPM;
        readonly double _maxPPM;

        int _odoredPenInRow = 0;

        double _ppmValue;
        double _ppmStep;

        bool PenSmells => _odoredPenInRow > 0;

        private void UpdateDirection(IProcState.PPMChangeDirection direction)
        {
            if (_direction != direction)
            {
                _turningPointPPMs.Add(_ppmValue);
                _direction = direction;

                _ppmStep /= 2;
            }

            _recognitionsInRow = 0;
        }

        private void UpdatePPMValue()
        {
            _ppmValue += _direction == IProcState.PPMChangeDirection.Increasing ? _ppmStep : -_ppmStep;

            if (_ppmValue > _maxPPM)
            {
                _ppmValue = -1;
            }
        }

        private void Penalize()
        {
            // penalty for answering "I smell odor" when there was no odor
            _direction = IProcState.PPMChangeDirection.Increasing;
            _recognitionsInRow = 0;
            _ppmStep *= 2;
        }
    }
}

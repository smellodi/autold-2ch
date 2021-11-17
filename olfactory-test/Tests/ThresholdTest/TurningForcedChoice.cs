using System;
using Olfactory.Utils;

namespace Olfactory.Tests.ThresholdTest
{
    /// <summary>
    /// Sniffin' Sticks procedure.
    /// Requires a full list of PPM values.
    /// </summary>
    public class TurningForcedChoice : TurningBase
    {
        public override double PPM => _ppmLevel >= 0 && _ppmLevel < _ppms.Length ? _ppms[_ppmLevel] : 0;

        public override bool IsValid => _ppmLevel >= 0;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="ppms">A list of PPM values at each level (typically, 10-16 values)</param>
        /// <param name="requiredRecognitions">required recognitions to treat a PPM value as recognized</param>
        public TurningForcedChoice(double[] ppms, int requiredRecognitions) : base(requiredRecognitions)
        {
            _ppms = ppms;
        }

        public override void Next(Pen[] pens)
        {
            base.Next(pens);

            _rnd.Shuffle(pens);
        }

        public override bool AcceptAnswer(bool wasRecognized)
        {
            if (!wasRecognized)
            {
                var step = _turningPointPPMs.Count == 0 ? 2 * PPM_LEVEL_STEP : PPM_LEVEL_STEP; // increase with x2 step before the first turning point
                Update(step, IProcState.PPMChangeDirection.Increasing);
            }
            else if (++_recognitionsInRow == _requiredRecognitions)    // decrease ppm only if recognized correctly few times in a row
            {
                // we cannot let going to the ppm level below 0
                var step = _ppmLevel > 0 ? -PPM_LEVEL_STEP : 0;
                var direction = _ppmLevel > 0 ? IProcState.PPMChangeDirection.Decreasing : _direction;
                Update(step, direction);
            }

            return IsValid;
        }


        // Internal

        const int PPM_LEVEL_STEP = 1;

        readonly Random _rnd = new((int)DateTime.Now.Ticks);
        readonly double[] _ppms;

        int _ppmLevel = 0;

        private void Update(int levelChange, IProcState.PPMChangeDirection direction)
        {
            if (_direction != direction)
            {
                _turningPointPPMs.Add(_ppms[_ppmLevel]);
                _direction = direction;
            }

            _recognitionsInRow = 0;
            _ppmLevel += levelChange;

            if (_ppmLevel >= _ppms.Length)
            {
                _ppmLevel = -1;
            }
        }
    }
}

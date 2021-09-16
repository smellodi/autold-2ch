using System;
using Olfactory.Utils;

namespace Olfactory.Tests.ThresholdTest
{
    /// <summary>
    /// Sniffin' Sticks procedure.
    /// Requires a full list of PPM values
    /// </summary>
    public class TurningForcedChoice : TurningBase
    {
        public override double PPM => _ppmLevel >= 0 && _ppmLevel < _ppms.Length ? _ppms[_ppmLevel] : 0;

        public override bool IsValid => _ppmLevel >= 0;

        public TurningForcedChoice(double[] ppms, int requiredRecognitions) : base(ppms, requiredRecognitions) { }

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
                Update(-PPM_LEVEL_STEP, IProcState.PPMChangeDirection.Decreasing);
            }

            return IsValid;
        }


        // Internal

        const int PPM_LEVEL_STEP = 1;

        int _ppmLevel = 0;

        readonly Random _rnd = new((int)DateTime.Now.Ticks);

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

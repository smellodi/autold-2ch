using System;
using System.Linq;

namespace Olfactory.Tests.ThresholdTest
{
    /// <summary>
    /// Yes-No procedure.
    /// Requires min and max PPM values
    /// </summary>
    public class TurningYesNo : TurningBase
    {
        public override double PPM => _ppmValue;

        public override bool IsValid => _ppmValue >= 0;

        public TurningYesNo(double[] ppms, int requiredRecognitions) : base(ppms, requiredRecognitions)
        {
            _minPPM = ppms.First();
            _maxPPM = ppms.Last();

            _ppmValue = _minPPM + 0.25 * (_maxPPM - _minPPM);
            _ppmStep = (_maxPPM - _minPPM) / 8;
        }

        public override void Next(Pen[] pens)
        {
            base.Next(pens);

            var color = _odoredPenInRow switch
            {
                0 => PenColor.Odor,
                >= 4 => PenColor.NonOdor,
                _ => _rnd.NextDouble() < 0.25 ? PenColor.NonOdor : PenColor.Odor
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

        bool PenSmells => _odoredPenInRow > 0;

        readonly Random _rnd = new((int)DateTime.Now.Ticks);

        int _odoredPenInRow = 0;
        
        double _minPPM;
        double _maxPPM;
        double _ppmValue;
        double _ppmStep;

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

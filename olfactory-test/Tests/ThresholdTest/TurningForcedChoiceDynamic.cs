using System;
using System.Collections.Generic;
using System.Linq;
using Olfactory.Utils;

namespace Olfactory.Tests.ThresholdTest
{
    /// <summary>
    /// A force choice from 2+ alternatives with dynamic step.
    /// Requires min and max PPM values only.
    /// </summary>
    public class TurningForcedChoiceDynamic : TurningBase
    {
        public override double PPM => _ppmValue;

        public override bool IsValid => _ppmValue >= 0;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="minPPM">minimal PPM value</param>
        /// <param name="maxPPM">maximal PPM value</param>
        /// <param name="requiredRecognitions">required recognitions to treat a PPM value as recognized</param>
        /// <param name="allowedWrongAnswers">max number of allowed wrong answers that do not reset the PPM value recognition chain</param>
        public TurningForcedChoiceDynamic(double minPPM, double maxPPM, int requiredRecognitions, int allowedWrongAnswers) : 
            base(requiredRecognitions, true)
        {
            _minPPM = minPPM;
            _maxPPM = maxPPM;

            _answersBufferSize = requiredRecognitions + allowedWrongAnswers;

            _ppmValue = _minPPM + (_maxPPM - _minPPM) * INITIAL_REL_PPM_VALUE;
            _ppmStep = (_maxPPM - _minPPM) * INITIAL_REL_PPM_STEP;
        }
        
        public override void Next(Pen[] pens)
        {
            base.Next(pens);

            if (_randomize)
            {
                _rnd.Shuffle(pens);
            }
        }

        public override bool AcceptAnswer(bool wasRecognized)
        {
            _lastAnswers.Enqueue(wasRecognized);
            while (_lastAnswers.Count > _answersBufferSize)
            {
                _lastAnswers.Dequeue();
            }

            _recognitionsInRow = CorrectAnswerCount;

            if ((!wasRecognized && _lastAnswers.Count == 1) ||                      // the only answer so far, and it is not correct
                WrongAnswerCount > (_answersBufferSize - _requiredRecognitions))    // exceeds the number of allowed wrong answers
            {
                UpdateDirection(IProcState.PPMChangeDirection.Increasing);
                UpdatePPMValue();
            }
            else if (_recognitionsInRow >= _requiredRecognitions)
            {
                UpdateDirection(IProcState.PPMChangeDirection.Decreasing);
                UpdatePPMValue();
            }

            return IsValid;
        }


        // Internal

        const double INITIAL_REL_PPM_VALUE = 0.25;
        const double INITIAL_REL_PPM_STEP = 1d / 8;

        readonly Random _rnd = new((int)DateTime.Now.Ticks);
        readonly Queue<bool> _lastAnswers = new();
        readonly double _minPPM;
        readonly double _maxPPM;
        readonly int _answersBufferSize;

        double _ppmValue;
        double _ppmStep;

        int CorrectAnswerCount => _lastAnswers.Where(answer => answer == true).Count();
        int WrongAnswerCount => _lastAnswers.Where(answer => answer == false).Count();

        private void UpdateDirection(IProcState.PPMChangeDirection direction)
        {
            if (_direction != direction)
            {
                _turningPointPPMs.Add(_ppmValue);
                _direction = direction;

                _ppmStep /= 2;
            }
        }

        private void UpdatePPMValue()
        {
            _ppmValue += _direction == IProcState.PPMChangeDirection.Increasing ? _ppmStep : -_ppmStep;

            _lastAnswers.Clear();

            if (_ppmValue > _maxPPM)
            {
                _ppmValue = -1;
            }
        }
    }
}

using System;
using System.Linq;
using System.Collections.Generic;
using Olfactory.Utils;

namespace Olfactory.Tests.ThresholdTest
{
    public abstract class TurningBase : IProcState
    {
        public int Step => _stepID;

        public IProcState.PPMChangeDirection Direction => _direction;

        public int TurningPointCount => _turningPointPPMs.Count;

        public int RecognitionsInRow => _recognitionsInRow;

        public abstract double PPM { get; }

        public abstract bool IsValid { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="requiredRecognitions">required recognitions to treat a PPM value as recognized</param>
        /// <param name="randomize">Set it to true if pens must be randomized at each trial</param>
        public TurningBase(int requiredRecognitions, bool randomize)
        {
            _requiredRecognitions = requiredRecognitions;
            _randomize = randomize;
        }

        /// <summary>
        /// Prepares the next trial
        /// </summary>
        /// <param name="pens">list of pens to update</param>
        public virtual void Next(Pen[] pens)
        {
            _stepID++;

            if (_randomize)
            {
                _rnd.Shuffle(pens);
            }
        }

        /// <summary>
        /// Computed the resuting PPM level
        /// </summary>
        /// <param name="turningPointCount">turning points to count, or 0 to count all the points</param>
        /// <returns>Average PPM level of the counted turning points, or -1 if the average cannot be computed</returns>
        public virtual double Result(int turningPointCount)
        {
            return IsValid && _turningPointPPMs.Count > 0 ?
                _turningPointPPMs.TakeLast(turningPointCount).Average() :
                -1;
        }

        /// <summary>
        /// Updates statistics after the trial is done
        /// </summary>
        /// <param name="wasRecognized">Trial result</param>
        /// <returns>'True' if the procedure can continue, 'False' otherwise</returns>
        public abstract bool AcceptAnswer(bool wasRecognized);

        /// <summary>
        /// [DEBUG only] Adds a turning point and stores its PPM value
        /// </summary>
        /// <param name="value">PPM value</param>
        public void _SimulateTurningPoint(double value)
        {
            _turningPointPPMs.Add(value);
        }


        // Internal

        protected readonly List<double> _turningPointPPMs = new();

        protected int _stepID = -1;
        protected IProcState.PPMChangeDirection _direction = IProcState.PPMChangeDirection.Increasing;
        protected int _recognitionsInRow = 0;

        protected readonly int _requiredRecognitions;
        protected readonly bool _randomize;

        readonly Random _rnd = new((int)DateTime.Now.Ticks);
    }
}

using System;

namespace Olfactory.Comm
{
    public class OdorController
    {
        public static OdorController Instance => _instance ??= new(); // is this line too complex? it simply return the instance is it exists, otherwise it creates a new instance, memorizes and returns it

        /// <summary>
        /// Prepares the odor dilution (in ppm) to be ready in a given amount of time
        /// </summary>
        /// <param name="interval">Time interval available for preparation</param>
        /// <param name="ppm">Desired odor dilution</param>
        /// <returns>Estimated interval required to prepare the odor</returns>
        public double GetReady(double interval, double ppm)
        {
            var odorSpeed = _mfc.PredictFlowSpeed(interval);

            _mfc.OdorSpeed = Math.Min(Math.Round(odorSpeed, 1), MFC.ODOR_MAX_SPEED);

            var estimatedInterval = _mfc.EstimateFlowDuration(MFC.FlowEndPoint.User, _mfc.OdorSpeed);

            Utils.DispatchOnce.Do(estimatedInterval, () => _mfc.OdorSpeed = _mfc.PPM2Speed(ppm));

            return estimatedInterval;
        }

        /// <summary>
        /// Directs the odored flow to the user
        /// </summary>
        public void OpenFlow()
        {
            _mfc.OdorDirection = MFC.OdorFlowsTo.SystemAndUser;
        }

        /// <summary>
        /// Directs the fresh air to the user and stops odoring the flow to the waste
        /// </summary>
        public void CloseFlow()
        {
            _mfc.OdorDirection = MFC.OdorFlowsTo.SystemAndWaste;

            // no need to wait till the trial is over, just stop odor flow at this point already
            Utils.DispatchOnce.Do(0.5, () => _mfc.OdorSpeed = MFC.ODOR_MIN_SPEED);  // delay 0.5 sec. just in case
        }


        // Internal

        static OdorController _instance;


        MFC _mfc = MFC.Instance;


        private OdorController() { }
    }
}

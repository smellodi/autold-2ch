using System;

namespace Olfactory.Comm
{
    public class OdorController
    {
        public static OdorController Instance => _instance = _instance ?? new(); // is this line too complex? it simply return the instance is it exists, otherwise it creates a new instance, memorizes and returns it

        /// <summary>
        /// Prepares the odor dilution (in ppm) to be read in a given amount of time
        /// </summary>
        /// <param name="interval">Time interval available for preparation</param>
        /// <param name="ppm">Desired odor dilution</param>
        public void GetReady(double interval, double ppm)
        {
            var odorSpeed = _mfc.PredictFlowSpeed(interval);

            _mfc.OdorSpeed = Math.Round(odorSpeed, 1);

            Utils.DispatchOnce.Do(interval, () => _mfc.OdorSpeed = _mfc.PPM2Speed(ppm));
        }

        /// <summary>
        /// Directs the odored flow to the user
        /// </summary>
        public void OpenFlow()
        {
            _mfc.OdorDirection = MFC.OdorFlow.ToUser;
        }

        /// <summary>
        /// Directs the fresh air to the user and stops odoring the flow to the waste
        /// </summary>
        public void CloseFlow()
        {
            _mfc.OdorDirection = MFC.OdorFlow.ToWaste;

            // no need to wait till the trial is over, just stop odor flow at this point already
            Utils.DispatchOnce.Do(0.5, () => _mfc.OdorSpeed = MFC.ODOR_MIN_SPEED);  // delay 0.5 sec. just in case
        }


        // Internal

        static OdorController _instance;


        MFC _mfc = MFC.Instance;


        private OdorController() { }
    }
}

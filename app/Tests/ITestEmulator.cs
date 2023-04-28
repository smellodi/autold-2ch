namespace Olfactory2Ch.Tests
{
    public interface ITestEmulator
    {
        /// <summary>
        /// Modifies the test parameters to emulate the flow (for example, to make it faster)
        /// </summary>
        void EmulationInit();

        /// <summary>
        /// Tells the test if should stop as soon as possible, and lets emulate data that are still missing (not collected),
        /// then exit the test procedure
        /// </summary>
        void EmulationFinilize();
    }
}

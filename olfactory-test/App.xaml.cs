using System.Globalization;
using System.Windows;

namespace Olfactory
{
    public partial class App : Application
    {
        // Set the US-culture across the application to avoid decimal point parsing/logging issues
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            var culture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            System.Threading.Thread.CurrentThread.CurrentCulture = culture;
            System.Threading.Thread.CurrentThread.CurrentUICulture = culture;
        }
    }
}

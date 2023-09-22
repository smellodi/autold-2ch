using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace AutOlD2Ch
{
    public partial class App : Application
    {
        public static readonly string Name = "AutOlD 2-channel";
        public static readonly string Version = "1.3";

        // Set the US-culture across the application to avoid decimal point parsing/logging issues
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            var culture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            System.Threading.Thread.CurrentThread.CurrentCulture = culture;
            System.Threading.Thread.CurrentThread.CurrentUICulture = culture;

            EventManager.RegisterClassHandler(typeof(TextBox),
                UIElement.GotFocusEvent,
                new RoutedEventHandler(TextBox_GotFocus));
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            (sender as TextBox).SelectAll();
        }
    }
}

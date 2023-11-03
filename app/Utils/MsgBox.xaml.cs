using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace AutOlD2Ch.Utils
{
    public partial class MsgBox : Window
    {
        public enum Button
        {
            /// <summary>
            /// Used internally
            /// Note that each name should have a corresponding translation in resourse files
            /// </summary>
            None = 0,
            Yes = 1,
            No = 2,
            OK = 4,
            Cancel = 8,
            Save = 16,
            SaveAs = 32,
            Discard = 64,
            Abort = 128,
            Retry = 256,
            Ignore = 512,
            Custom = 32768,
        }

        public class Result
        {
            public Button Button { get; private set; }
            public int ID { get; private set; }
            public Result(Button button, int customButtonID)
            {
                Button = button;
                ID = customButtonID;
            }
        }

        public Button ClickedButton { get; private set; } = Button.None;
        public int CustomButtonID { get; private set; } = -1;


        public static void Notify(string title, string message) =>
            Show(title, message, MsgIcon.Info, null, new Button[] { Button.OK });
        public static Button Notify(string title, string message, params Button[] stdButtons) =>
            Show(title, message, MsgIcon.Info, null, stdButtons).Button;
        public static Result Notify(string title, string message, string[] customButtons, params Button[] stdButtons) =>
            Show(title, message, MsgIcon.Info, customButtons, stdButtons);
        public static Result Notify(string title, string message, string customButton, params Button[] stdButtons) =>
            Show(title, message, MsgIcon.Info, new string[] { customButton }, stdButtons);

        public static void Error(string title, string message) =>
            Show(title, message, MsgIcon.Error, null, new Button[] { Button.OK });
        public static Button Error(string title, string message, params Button[] stdButtons) =>
            Show(title, message, MsgIcon.Error, null, stdButtons).Button;
        public static Result Error(string title, string message, string[] customButtons, params Button[] stdButtons) =>
            Show(title, message, MsgIcon.Error, customButtons, stdButtons);
        public static Result Error(string title, string message, string customButton, params Button[] stdButtons) =>
            Show(title, message, MsgIcon.Error, new string[] { customButton }, stdButtons);

        public static Button Ask(string title, string message) =>
            Show(title, message, MsgIcon.Question, null, new Button[] { Button.Yes, Button.No }).Button;
        public static Button Ask(string title, string message, params Button[] stdButtons) =>
            Show(title, message, MsgIcon.Question, null, stdButtons).Button;
        public static Result Ask(string title, string message, string[] customButtons, params Button[] stdButtons) =>
            Show(title, message, MsgIcon.Question, customButtons, stdButtons);
        public static Result Ask(string title, string message, string customButton, params Button[] stdButtons) =>
            Show(title, message, MsgIcon.Question, new string[] { customButton }, stdButtons);

        public static Button Warn(string title, string message) =>
            Show(title, message, MsgIcon.Warning, null, new Button[] { Button.OK, Button.Cancel }).Button;
        public static Button Warn(string title, string message, params Button[] stdButtons) =>
            Show(title, message, MsgIcon.Warning, null, stdButtons).Button;
        public static Result Warn(string title, string message, string[] customButtons, params Button[] stdButtons) =>
            Show(title, message, MsgIcon.Warning, customButtons, stdButtons);
        public static Result Warn(string title, string message, string customButton, params Button[] stdButtons) =>
            Show(title, message, MsgIcon.Warning, new string[] { customButton }, stdButtons);


        public new bool? ShowDialog()
        {
            var sysSound = _icon switch
            {
                MsgIcon.Info => System.Media.SystemSounds.Asterisk,
                MsgIcon.Error => System.Media.SystemSounds.Hand,
                MsgIcon.Question => System.Media.SystemSounds.Question,
                MsgIcon.Warning => System.Media.SystemSounds.Exclamation,
                _ => throw new NotImplementedException("Unknown icon")
            };

            sysSound.Play();

            return base.ShowDialog();
        }

        // Internal

        enum MsgIcon
        {
            Info,
            Error,
            Question,
            Warning,
        }

        readonly MsgIcon _icon;

        const int GWL_STYLE = -16;
        const int WS_SYSMENU = 0x80000;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        
        private MsgBox(string title, string message, MsgIcon icon, string[] customButtons, Button[] stdButtons)
        {
            InitializeComponent();

            if (stdButtons?.Length + customButtons?.Length == 0)
            {
                throw new ArgumentException("Cannot show message box wit no buttons.");
            }

            _icon = icon;

            Title = title;
            txbMessage.Text = message;
            txbMessage.MaxWidth = 220 + message.Length * 2;

            var iconFilename = icon switch
            {
                MsgIcon.Info => "information",
                MsgIcon.Error => "error",
                MsgIcon.Question => "question",
                MsgIcon.Warning => "exclamation",
                _ => throw new NotImplementedException("Unknown icon")
            };

            var uriSource = new Uri($@"/autold_2ch;component/Assets/images/{iconFilename}.png", UriKind.Relative);
            imgIcon.Source = new BitmapImage(uriSource);

            if (customButtons != null)
            {
                int i = 0;
                foreach (var text in customButtons)
                {
                    var btn = new System.Windows.Controls.Button()
                    {
                        Content = L10n.T(text),
                        Tag = i,
                    };
                    btn.Click += (s, e) =>
                    {
                        ClickedButton = Button.Custom;
                        CustomButtonID = (int)btn.Tag;
                        DialogResult = true;
                    };

                    stpButtons.Children.Add(btn);
                    i++;
                }
            }

            if (stdButtons != null)
            {
                foreach (var type in stdButtons)
                {
                    var btn = new System.Windows.Controls.Button()
                    {
                        Content = L10n.T(type.ToString())
                    };
                    btn.Click += (s, e) =>
                    {
                        ClickedButton = type;
                        DialogResult = true;
                    };
                    stpButtons.Children.Add(btn);
                }
            }
        }

        private static Result Show(string title, string message, MsgIcon icon, string[] customButtons, Button[] stdButtons)
        {
            if (Application.Current.Dispatcher.Thread != System.Threading.Thread.CurrentThread)
            {
                return Application.Current.Dispatcher.Invoke(() =>
                {
                    var box = new MsgBox(title, message, icon, customButtons, stdButtons)
                    {
                        WindowStartupLocation = WindowStartupLocation.CenterScreen,
                        ShowInTaskbar = true
                    };
                    box.ShowDialog();

                    return new Result(box.ClickedButton, box.CustomButtonID);
                });
            }
            else
            {
                var box = new MsgBox(title, message, icon, customButtons, stdButtons);
                if (!Application.Current.MainWindow.IsLoaded)
                {
                    box.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    box.ShowInTaskbar = true;
                }
                else
                {
                    box.Owner = Application.Current.MainWindow;
                }

                box.ShowDialog();

                return new Result(box.ClickedButton, box.CustomButtonID);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~WS_SYSMENU);
        }
    }
}

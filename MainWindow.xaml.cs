using Microsoft.Win32;
using PicaPico;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using ToolStripMenuItem = System.Windows.Forms.ToolStripMenuItem;

namespace ClipboardServer
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly string APP_NAME = "ClipboardServer";
        private static NotifyIcon notifyIcon;
        private ToolStripMenuItem startupMenu;
        public MainWindow()
        {
            InitializeComponent();
            ThemeListener.ThemeChanged += OnThemeSettingsChanged;
            Init();
        }

        private void Init()
        {
            CopyRightYear.Content = DateTime.Now.Year;
            var chinese = IsChinese();
            startupMenu = new ToolStripMenuItem(chinese ? "开机启动" : "Startup", null, OnSetStartup)
            {
                Checked = IsStartupEnabled(),
                CheckOnClick = true,
            };
            var exitMenu = new ToolStripMenuItem(chinese ? "退出" : "Exit", null, Exit)
            {
                Margin = new System.Windows.Forms.Padding(0, 0, 0, 2),
            };
            var aboutMenu = new ToolStripMenuItem(chinese ? "关于" : "About", null, ShowWindow)
            {
                Margin = new System.Windows.Forms.Padding(0, 2, 0, 0),
            };
            notifyIcon = new NotifyIcon()
            {
                Text = "Clipboard Server\n\n" + (chinese ? "监听端口：" : "Listen On ") + Server.HTTP_PORT,
            };
            notifyIcon.AddMenu(new ToolStripMenuItem[]
            {
                aboutMenu, startupMenu, exitMenu
            });
            SetIcon(ThemeListener.IsDarkMode ? WindowsTheme.Dark : WindowsTheme.Light);
        }

        private void ShowWindow(object sender, EventArgs e)
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private bool IsChinese()
        {
            var languages = Windows.System.UserProfile.GlobalizationPreferences.Languages;
            if (languages != null && languages.Count > 0)
            {
                var language = languages[0].ToLower();
                if (language.Contains("zh") || language.Contains("cn")) return true;
            }
            return false;
        }

        private void Exit(object sender, EventArgs e)
        {
            notifyIcon.Dispose();
            Application.Current.Shutdown();
        }

        private void OnSetStartup(object sender, EventArgs e)
        {
            string keyName = @"Software\Microsoft\Windows\CurrentVersion\Run";
            using (RegistryKey rKey = Registry.CurrentUser.OpenSubKey(keyName, true))
            {
                if (startupMenu.Checked)
                {
                    rKey.SetValue(APP_NAME, Process.GetCurrentProcess().MainModule.FileName);
                }
                else
                {
                    rKey.DeleteValue(APP_NAME, false);
                }
                rKey.Close();
            }
        }

        private bool IsStartupEnabled()
        {
            string keyName = @"Software\Microsoft\Windows\CurrentVersion\Run";
            using (RegistryKey rKey = Registry.CurrentUser.OpenSubKey(keyName))
            {
                return rKey.GetValue(APP_NAME) != null;
            }
        }

        private void SetIcon(WindowsTheme systemTheme)
        {
            if (systemTheme == WindowsTheme.Dark)
            {
                notifyIcon.Icon = Properties.Resources.clipboard_white;
            }
            else
            {
                notifyIcon.Icon = Properties.Resources.clipboard_black;
            }
            AboutLogo.Source = Imaging.CreateBitmapSourceFromHIcon(
                notifyIcon.Icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }


        private void OnThemeSettingsChanged(bool isDark)
        {
            Dispatcher.Invoke(() =>
            {
                SetIcon(isDark ? WindowsTheme.Dark : WindowsTheme.Light);
            });
        }

        private void ClipboardServerClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        public enum WindowsTheme
        {
            Light,
            Dark,
            Default
        }

        private void OpenGithub(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            string githubUrl = "https://github.com/hehang0/" + APP_NAME;
            Process.Start(new ProcessStartInfo(githubUrl) { UseShellExecute = true });
        }
    }
}

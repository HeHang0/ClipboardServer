using Hardcodet.Wpf.TaskbarNotification;
using HttpServerLite;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Windows.Globalization;
using WK.Libraries.WTL;
using Clipboard = System.Windows.Forms.Clipboard;
using Image = System.Drawing.Image;

namespace ClipboardServer
{
    public static class HttpRequestHelper
    {
        public static Dictionary<string, string> DataAsForm(this HttpRequest request)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            string[] kvArr = request.DataAsString.Split(new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var kv in kvArr)
            {
                string[] kvItem = kv.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                if (kvItem != null && kvItem.Length == 2)
                {
                    result.Add(kvItem[0], kvItem[1]);
                }
            }
            return result;
        }
    }
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private static string AppName = "ClipboardServer";
        private static int HttpPort = 37259;
        private static readonly int MaxDataLength = 10485760;
        Webserver server = new Webserver("*", HttpPort, false, null, null, ClipboardType);
        public MainWindow()
        {
            InitializeComponent();
        }

        [ParameterRoute(HttpMethod.GET, "/clipboard/type")]
        public static async Task ClipboardType(HttpContext ctx)
        {
            Dictionary<string, object> reponse = new Dictionary<string, object>();
            RunAsSTA(() =>
            {
                reponse.Add("text", Clipboard.ContainsText());
                reponse.Add("image", Clipboard.ContainsImage());
                reponse.Add("audio", Clipboard.ContainsAudio());
                reponse.Add("file", Clipboard.ContainsFileDropList());
            });
            string resp = JsonConvert.SerializeObject(reponse);
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";
            ctx.Response.ContentLength = resp.Length;
            await ctx.Response.SendAsync(resp);
            await sendString(ctx, resp, "application/json");
            return;
        }

        [ParameterRoute(HttpMethod.GET, "/clipboard/text")]
        public static async Task ClipboardText(HttpContext ctx)
        {
            string resp = string.Empty;
            RunAsSTA(() =>
            {
                resp = Clipboard.GetText();
            });
            await sendString(ctx, resp);
            return;
        }

        [ParameterRoute(HttpMethod.PUT, "/clipboard")]
        public static async Task PutClipboard(HttpContext ctx)
        {
            if(ctx.Request.ContentLength > MaxDataLength)
            {
                await sendString(ctx, "Too Large Size");
                return;
            }
            Image image = IsValidImage(ctx.Request.DataAsBytes);
            if (image != null)
            {
                RunAsSTA(() =>
                {
                    Clipboard.SetImage(image);
                });
            }
            else
            {
                string text = Encoding.UTF8.GetString(ctx.Request.DataAsBytes);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    RunAsSTA(() =>
                    {
                        Clipboard.SetText(text);
                    });
                }
            }

            await sendString(ctx, "OK");
            return;
        }

        private static Image IsValidImage(byte[] bytes)
        {
            try
            {
                using (MemoryStream ms = new MemoryStream(bytes))
                return Image.FromStream(ms);
            }
            catch (ArgumentException)
            {
            }
            return null;
        }

        private static async Task sendString(HttpContext ctx, string msg, string contentType= "text/plain;charset=utf-8")
        {
            ctx.Response.StatusCode = (int)HttpStatusCode.OK;
            ctx.Response.ContentType = contentType;
            var data = Encoding.UTF8.GetBytes(msg);
            ctx.Response.ContentLength = data.Length;
            await ctx.Response.SendAsync(data);
            return;
        }

        [ParameterRoute(HttpMethod.GET, "/clipboard/image")]
        public static async Task ClipboardImage(HttpContext ctx)
        {
            Image imageSrc = null;
            RunAsSTA(() =>
            {
                imageSrc = Clipboard.GetImage();
            });
            
            if (imageSrc == null)
            {
                ctx.Response.StatusCode = (int)HttpStatusCode.NotFound;
                await ctx.Response.SendAsync(0);
            }
            else
            {
                using(MemoryStream ms = new MemoryStream())
                {
                    ImageFormat imageFormat = imageSrc.RawFormat;
                    if (imageFormat.Equals(ImageFormat.MemoryBmp))
                    {
                        imageFormat = ImageFormat.Png;
                    }
                    
                    ctx.Response.ContentType = "image/" + imageFormat.ToString().ToLower();
                    imageSrc.Save(ms, imageFormat);
                    ctx.Response.StatusCode = (int)HttpStatusCode.OK;
                    ctx.Response.ContentLength = ms.Length;
                    await ctx.Response.SendAsync(ms.ToArray());
                }
            }
            return;
        }

        [ParameterRoute(HttpMethod.GET, "/clipboard/file")]
        public static async Task ClipboardFile(HttpContext ctx)
        {
            string clipboardFile = null;
            RunAsSTA(() =>
            {
                var files = Clipboard.GetFileDropList();
                if(files != null && files.Count > 0) clipboardFile = files[0];
            });

            if (clipboardFile == null)
            {
                ctx.Response.StatusCode = (int)HttpStatusCode.NotFound;
                await ctx.Response.SendAsync(0);
            }
            else
            {
                string encodedFileName = WebUtility.UrlEncode(Path.GetFileName(clipboardFile));
                ctx.Response.ContentType = "application/octet-stream";
                ctx.Response.Headers.Add("Content-Disposition", $"attachment; filename=\"{encodedFileName}\"");
                ctx.Response.StatusCode = (int)HttpStatusCode.OK;
                using (var fileStream = new FileStream(clipboardFile, FileMode.Open, FileAccess.Read))
                {
                    ctx.Response.ContentLength = fileStream.Length;
                    ctx.Response.TrySend(fileStream.Length, fileStream);
                }
            }
            return;
        }

        private static void RunAsSTA(Action threadStart)
        {
            try
            {
                Thread t = new Thread(new ThreadStart(threadStart));
                t.SetApartmentState(ApartmentState.STA);
                t.Start();
                t.Join();
            }
            catch (Exception )
            {

            }
        }
        MenuItem startupMenu;
        private void Init()
        {
            var chinese = IsChinese();
            startupMenu = new MenuItem()
            {
                Header = chinese ? "开机启动" : "Startup",
                IsCheckable = true,
                IsChecked = IsStartupEnabled()
            };
            startupMenu.Click += OnSetStartup;
            var exitMenu = new MenuItem()
            {
                Header = chinese ? "退出" : "Exit"
            };
            exitMenu.Click += Exit;
            notifyIcon = new TaskbarIcon()
            {
                ContextMenu = new ContextMenu()
                {
                    Items = { startupMenu, exitMenu }
                },
                ToolTipText = "Clipboard Server\n\n" + (chinese ? "监听端口：" : "Listen On ") + HttpPort
            };
            SetMenuItemStyle(startupMenu);
            SetMenuItemStyle(exitMenu);
            SetIcon();
            server.Start();
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
            Close();
        }

        private void SetMenuItemStyle(MenuItem menuItem)
        {
            menuItem.Height = 20;
            menuItem.MinWidth = 105;
            menuItem.FontSize = 12;
            menuItem.VerticalContentAlignment = System.Windows.VerticalAlignment.Center;
            menuItem.Margin = new Thickness(5, 0, 5, 0);
            menuItem.Padding = new Thickness(5, 0, 5, 0);
        }

        private void OnSetStartup(object sender, EventArgs e)
        {
            string keyName = @"Software\Microsoft\Windows\CurrentVersion\Run";
            using (RegistryKey rKey = Registry.CurrentUser.OpenSubKey(keyName, true))
            {
                if (startupMenu.IsChecked)
                {
                    rKey.SetValue(AppName, System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
                }
                else
                {
                    rKey.DeleteValue(AppName, false);
                }
                rKey.Close();
            }
        }

        private bool IsStartupEnabled()
        {
            string keyName = @"Software\Microsoft\Windows\CurrentVersion\Run";
            using (RegistryKey rKey = Registry.CurrentUser.OpenSubKey(keyName))
            {
                return rKey.GetValue(AppName) != null;
            }
        }
        
        private void SetIcon()
        {
            notifyIcon.ContextMenu.UpdateDefaultStyle();
            WindowsTheme systemTheme = ThemeHelper.GetWindowsTheme();
            if (systemTheme == WindowsTheme.Dark)
            {
                notifyIcon.Icon = Properties.Resources.clipboard_white;
            }
            else
            {
                notifyIcon.Icon = Properties.Resources.clipboard_black;
            }
        }

        private void ClipboardServerSourceInitialized(object sender, EventArgs e)
        {
            ThemeListener.Enabled = true;
            ThemeListener.ThemeSettingsChanged += ThemeSettingsChanged;
            Visibility = Visibility.Hidden;
            Hide();
            Init();
        }

        private void ThemeSettingsChanged(object sender, ThemeListener.ThemeSettingsChangedEventArgs e)
        {
            SetIcon();
        }

        private TaskbarIcon notifyIcon;
        private void ClipboardServerClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            notifyIcon.Dispose();
        }

        const int WM_DWMCOLORIZATIONCOLORCHANGED = 0x320;
        IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_DWMCOLORIZATIONCOLORCHANGED:
                    SetIcon();
                    return IntPtr.Zero;
                default:
                    return IntPtr.Zero;
            }
        }

        public enum WindowsTheme
        {
            Light,
            Dark,
            Default
        }

        public class ThemeHelper
        {
            private const string _registryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

            private const string _registryValueName = "AppsUseLightTheme";


            public static WindowsTheme GetWindowsTheme()
            {
                object registryValueObject;
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(_registryKeyPath))
                {
                    registryValueObject = key?.GetValue(_registryValueName);
                    if (registryValueObject != null)
                    {
                        int registryValue = (int)registryValueObject;
                        return registryValue > 0 ? WindowsTheme.Light : WindowsTheme.Dark;
                    }
                }
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(_registryKeyPath))
                {
                    registryValueObject = key?.GetValue(_registryValueName);
                    if (registryValueObject != null)
                    {
                        int registryValue = (int)registryValueObject;
                        return registryValue > 0 ? WindowsTheme.Light : WindowsTheme.Dark;
                    }
                }
                return WindowsTheme.Light;
            }
        }
    }
}

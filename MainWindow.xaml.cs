using HttpServerLite;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using Clipboard = System.Windows.Forms.Clipboard;

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
            //HttpServer();
        }

        [ParameterRoute(HttpMethod.GET, "/clipboard/type")]
        public static async Task ClipboardType(HttpContext ctx)
        {
            Dictionary<string, object> reponse = new Dictionary<string, object>();
            RunAsSTA(() =>
            {
                reponse.Add("text", Clipboard.ContainsText());
                reponse.Add("image", Clipboard.ContainsImage());
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

        [ParameterRoute(HttpMethod.PUT, "/clipboard/text")]
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
            startupMenu = new MenuItem("Startup", OnSetStartup)
            {
                Checked = IsStartupEnabled()
            };
            notifyIcon = new NotifyIcon()
            {
                ContextMenu = new ContextMenu(new MenuItem[] {startupMenu, new MenuItem("Exit", Exit) }),
                Text = "Listen On " + HttpPort,
                Visible = true,
            };
            SetIcon();
            server.Start();
        }

        private void Exit(object sender, EventArgs e)
        {
            Close();
        }

        private void OnSetStartup(object sender, EventArgs e)
        {
            startupMenu.Checked = !startupMenu.Checked;
            string keyName = @"Software\Microsoft\Windows\CurrentVersion\Run";
            using (RegistryKey rKey = Registry.CurrentUser.OpenSubKey(keyName, true))
            {
                if (startupMenu.Checked)
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
            WindowsTheme systemTheme = ThemeHelper.GetWindowsTheme();
            if(systemTheme == WindowsTheme.Dark)
            {
                notifyIcon.Icon = Properties.Resources.clipboard_white;
            }else
            {
                notifyIcon.Icon = Properties.Resources.clipboard_black;
            }
        }

        private void ClipboardServerSourceInitialized(object sender, EventArgs e)
        {
            IntPtr wptr = new WindowInteropHelper(this).Handle;
            HwndSource hs = HwndSource.FromHwnd(wptr);
            hs.AddHook(new HwndSourceHook(WndProc));
            Visibility = Visibility.Hidden;
            Hide();
            Init();
        }

        private NotifyIcon notifyIcon;
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

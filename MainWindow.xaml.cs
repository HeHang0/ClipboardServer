﻿using HttpServerLite;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Text.Json;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using MimeMapping;
using PicaPico;
using Clipboard = System.Windows.Forms.Clipboard;
using Image = System.Drawing.Image;
using ToolStripMenuItem = System.Windows.Forms.ToolStripMenuItem;

namespace ClipboardServer
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly string APP_NAME = "ClipboardServer";
        private static readonly int MAX_DATA_LENGTH = 10485760;
        private static int HTTP_PORT = 37259;
        private static byte[] faviconLight;
        private static byte[] faviconDark;
        private static NotifyIcon notifyIcon;
        private ToolStripMenuItem startupMenu;
        public MainWindow()
        {
            InitializeComponent();
            InitFavicon();
        }

        private void InitFavicon()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                Properties.Resources.clipboard_black.Save(ms);
                faviconDark = ms.ToArray();
            }
            using (MemoryStream ms = new MemoryStream())
            {
                Properties.Resources.clipboard_white.Save(ms);
                faviconLight = ms.ToArray();
            }
        }

        private void ClipboardServerSourceInitialized(object sender, EventArgs e)
        {
            ThemeListener.ThemeChanged += OnThemeSettingsChanged;
            Visibility = Visibility.Hidden;
            Hide();
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
                Text = "Clipboard Server\n\n" + (chinese ? "监听端口：" : "Listen On ") + HTTP_PORT,
            };
            notifyIcon.AddMenu(new ToolStripMenuItem[]
            {
                aboutMenu, startupMenu, exitMenu
            });
            SetIcon(ThemeListener.IsDarkMode ? WindowsTheme.Dark : WindowsTheme.Light);
            InitWebServer();
        }

        private void ShowWindow(object sender, EventArgs e)
        {
            Show();
            Activate();
        }

        private void InitWebServer()
        {
            Webserver webServer = new Webserver("+", HTTP_PORT, false, null, null, ClipboardIndex);
            webServer.Events.Logger = WebServerLogger;
            webServer.Start();
            Webserver webServerV6 = new Webserver("[::]", HTTP_PORT, false, null, null, ClipboardIndex);
            webServerV6.Events.Logger = WebServerLogger;
            webServerV6.Start();
        }

        private void WebServerLogger(string msg)
        {
            Console.WriteLine(msg);
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

        #region Router
        public static async Task ClipboardIndex(HttpContext ctx)
        {
#if DEBUG
            var indexPath = Path.GetFullPath(Path.Combine("..", "..", "Resources", "index.html"));
            await sendString(ctx, File.ReadAllText(indexPath), "text/html");
#else
                await sendString(ctx, Properties.Resources.index, "text/html");
#endif
            return;
        }

        [ParameterRoute(HttpMethod.GET, "/favicon.ico")]
        public static async Task ClipboardIcon(HttpContext ctx)
        {
            ctx.Response.StatusCode = (int)HttpStatusCode.OK;
            ctx.Response.ContentType = "image/x-icon";
            await ctx.Response.SendAsync(ctx.Request.QuerystringExists("dark") ? faviconDark : faviconLight);
            return;
        }

        [ParameterRoute(HttpMethod.GET, "/clipboard/type")]
        public static async Task ClipboardType(HttpContext ctx)
        {
            Dictionary<string, object> reponse = new Dictionary<string, object>();
            RunAsSTA(() =>
            {
                reponse.Add("text", Clipboard.ContainsText());
                reponse.Add("image", Clipboard.ContainsImage());
                reponse.Add("file", Clipboard.ContainsFileDropList());
            });
            string resp = JsonSerializer.Serialize(reponse);
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

        [ParameterRoute(HttpMethod.GET, "/clipboard/image")]
        public static async Task ClipboardImage(HttpContext ctx)
        {
            MemoryStream imageSrc = null;
            string format = "";
            RunAsSTA(() =>
            {
                var dataObject = Clipboard.GetDataObject();
                if (dataObject != null && dataObject.GetFormats().Length > 0)
                {
                    var formats = dataObject.GetFormats();
                    format = formats[0];
                    if (format.ToLower().Contains("bitmap"))
                    {
                        Image image = dataObject.GetData(System.Windows.Forms.DataFormats.Bitmap, true) as Image;
                        imageSrc = new MemoryStream();
                        ImageFormat imageFormat = image.RawFormat.Equals(ImageFormat.MemoryBmp) ? ImageFormat.Png : image.RawFormat;
                        image.Save(imageSrc, ImageFormat.Png);
                        format = imageFormat.ToString();
                    }
                    else
                    {
                        imageSrc = dataObject.GetData(format) as MemoryStream;
                    }
                }
            });

            if (imageSrc == null)
            {
                ctx.Response.StatusCode = (int)HttpStatusCode.NotFound;
                await ctx.Response.SendAsync(0);
            }
            else
            {
                ctx.Response.ContentType = "image/" + format.ToLower();
                ctx.Response.StatusCode = (int)HttpStatusCode.OK;
                ctx.Response.ContentLength = imageSrc.Length;
                await ctx.Response.SendAsync(imageSrc.ToArray());
                imageSrc.Close();
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
                if (files != null && files.Count > 0) clipboardFile = files[0];
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

        [ParameterRoute(HttpMethod.PUT, "/clipboard")]
        public static async Task PutClipboard(HttpContext ctx)
        {
            if (ctx.Request.ContentLength > MAX_DATA_LENGTH)
            {
                await sendString(ctx, "Too Large Size", statusCode: (int)HttpStatusCode.NotAcceptable);
                return;
            }
            var contentType = ctx.Request.Headers.Get("Content-Type")?.ToLower() ?? string.Empty;
            if (contentType.StartsWith("text"))
            {
                string text = Encoding.UTF8.GetString(ctx.Request.DataAsBytes);
                System.Windows.Forms.TextDataFormat format = System.Windows.Forms.TextDataFormat.Text;
                string plainText = string.Empty;
                if (contentType.Contains("rtf"))
                {
                    format = System.Windows.Forms.TextDataFormat.Rtf;
                    using (var rtb = new System.Windows.Forms.RichTextBox())
                    {
                        rtb.Rtf = text;
                        plainText = rtb.Text;
                    }
                }
                else if (contentType.Contains("html"))
                {
                    format = System.Windows.Forms.TextDataFormat.Html;
                    plainText = text;
                }
                else if (contentType.Contains("unicode"))
                {
                    format = System.Windows.Forms.TextDataFormat.UnicodeText;
                    plainText = text;
                }
                else if (contentType.Contains("comma"))
                {
                    format = System.Windows.Forms.TextDataFormat.UnicodeText;
                    plainText = text;
                }
                if (!string.IsNullOrWhiteSpace(text))
                {
                    RunAsSTA(() =>
                    {
                        Clipboard.SetText(text, format);
                        if(!string.IsNullOrEmpty(plainText))
                        {
                            Clipboard.SetText(plainText, System.Windows.Forms.TextDataFormat.Text);
                        }
                    });
                }
            }
            else if (contentType.StartsWith("image"))
            {
                Image image = IsValidImage(ctx.Request.DataAsBytes);
                if (image != null)
                {
                    RunAsSTA(() =>
                    {
                        Clipboard.SetImage(image);
                    });
                }
            }
            else
            {
                var contentName = ctx.Request.Headers.Get("Content-Name");
                if (contentName != null && contentName.Length > 0)
                {
                    contentName = Uri.UnescapeDataString(contentName);
                }
                else
                {
                    try
                    {
                        var extensions = MimeUtility.GetExtensions(contentType);
                        contentName = "Unnamed." + extensions[0];
                    }
                    catch (Exception)
                    {
                        contentName = "UnnamedFile";
                    }
                }
                var filePath = Path.Combine(Path.GetTempPath(), contentName);
                try
                {
                    File.WriteAllBytes(filePath, ctx.Request.DataAsBytes);
                    RunAsSTA(() =>
                    {
                        var fileList = new System.Collections.Specialized.StringCollection() { filePath };
                        Clipboard.SetFileDropList(fileList);
                    });
                }
                catch (Exception)
                {
                }
                await sendString(ctx, "OK");
                return;
            }

            await sendString(ctx, "OK");
            return;
        }
        #endregion

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

        private static async Task sendString(HttpContext ctx, string msg, string contentType= "text/plain;charset=utf-8", int statusCode= (int)HttpStatusCode.OK)
        {
            try
            {
                ctx.Response.StatusCode = statusCode;
                ctx.Response.ContentType = contentType;
                var data = Encoding.UTF8.GetBytes(msg);
                ctx.Response.ContentLength = data.Length;
                await ctx.Response.SendAsync(data);
            }
            catch (Exception)
            {
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


        private void OnThemeSettingsChanged(bool isDark)
        {
            Dispatcher.Invoke((Action)(() =>
            {
                SetIcon(isDark ? WindowsTheme.Dark : WindowsTheme.Light);
            }));
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

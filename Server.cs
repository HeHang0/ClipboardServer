using HttpServerLite;
using MimeMapping;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ClipboardServer
{
    public class Server
    {
        private static readonly int MAX_DATA_LENGTH = 10485760;
        private static readonly int RTFD_DATA_LENGTH = 102400;
        public static int HTTP_PORT = 37259;
        private static byte[] faviconLight;
        private static byte[] faviconDark;
        private static Dispatcher dispatcher = Dispatcher.CurrentDispatcher;

        public Server()
        {
            InitFavicon();
            InitWebServer();
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
            Trace.WriteLine(msg);
        }

        #region Router
        public static async Task ClipboardIndex(HttpContext ctx)
        {
#if DEBUG
            var indexPath = Path.GetFullPath(Path.Combine("..", "..", "Resources", "index.html"));
            await SendString(ctx, File.ReadAllText(indexPath), "text/html");
#else
                await SendString(ctx, Properties.Resources.index, "text/html");
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
            dispatcher.Invoke(() =>
            {
                reponse.Add("text", Clipboard.ContainsText());
                reponse.Add("image", Clipboard.ContainsImage());
                reponse.Add("file", Clipboard.ContainsFileDropList());
            });
            string resp = JsonSerializer.Serialize(reponse);
            await SendString(ctx, resp, "application/json");
            return;
        }

        [ParameterRoute(HttpMethod.GET, "/clipboard/text")]
        public static async Task ClipboardText(HttpContext ctx)
        {
            string resp = string.Empty;
            dispatcher.Invoke(() =>
            {
                resp = Clipboard.GetText();
            });
            await SendString(ctx, resp);
            return;
        }

        [ParameterRoute(HttpMethod.GET, "/clipboard/image")]
        public static async Task ClipboardImage(HttpContext ctx)
        {
            byte[] imageData = null;
            string filePath = string.Empty;
            dispatcher.Invoke(() =>
            {
                
                var dataObject = Clipboard.GetDataObject();
                var formats = dataObject?.GetFormats();
                if (formats != null && formats.Length > 0)
                {
                    string format = formats.FirstOrDefault(m => m.ToLower().Contains("bitmap"));
                    if (!string.IsNullOrEmpty(format))
                    {
                        BitmapSource bitmapSource = dataObject.GetData(DataFormats.Bitmap, true) as BitmapSource;

                        PngBitmapEncoder encoder = new PngBitmapEncoder();
                        BitmapFrame outputFrame = BitmapFrame.Create(bitmapSource);
                        encoder.Frames.Add(outputFrame);
                        using (MemoryStream ms = new MemoryStream())
                        {
                            encoder.Save(ms);
                            imageData = ms.ToArray();
                        }
                    }
                    else
                    {
                        format = formats.FirstOrDefault(m => m.ToLower().Contains("filename") || m.ToLower().Contains("text"));
                        if(!string.IsNullOrEmpty(format))
                        {
                            object data = dataObject.GetData(format);
                            string filename = string.Empty;
                            if (data is string[] fileNames) filename = fileNames[0];
                            else if (data is string fileName) filename = fileName;
                            if (!string.IsNullOrEmpty(filename) && IsRealImage(filename))
                            {
                                filePath = filename;
                            }
                        }
                    }
                }
            });
            if (!string.IsNullOrEmpty(filePath))
            {
                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    ctx.Response.ContentType = "image/" + Path.GetExtension(filePath).TrimStart('.');
                    ctx.Response.StatusCode = (int)HttpStatusCode.OK;
                    ctx.Response.ContentLength = fileStream.Length;
                    await ctx.Response.SendAsync(fileStream.Length, fileStream);
                }
            }
            else if(imageData != null)
            {
                ctx.Response.ContentType = "image/png";
                ctx.Response.StatusCode = (int)HttpStatusCode.OK;
                ctx.Response.ContentLength = imageData.Length;
                await ctx.Response.SendAsync(imageData);
            }
            else
            {
                ctx.Response.StatusCode = (int)HttpStatusCode.NotFound;
                await ctx.Response.SendAsync(0);
            }
            return;
        }

        [ParameterRoute(HttpMethod.GET, "/clipboard/file")]
        public static async Task ClipboardFile(HttpContext ctx)
        {
            string clipboardFile = null;
            dispatcher.Invoke(() =>
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
                await SendString(ctx, "Too Large Size", statusCode: (int)HttpStatusCode.NotAcceptable);
                return;
            }
            if (ctx.Request.ContentLength == 0)
            {
                await SendString(ctx, "No Size", statusCode: (int)HttpStatusCode.NotAcceptable);
                return;
            }
            var contentType = ctx.Request.Headers.Get("Content-Type")?.ToLower() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(contentType) && ctx.Request.DataAsBytes.Length < RTFD_DATA_LENGTH &&
                ctx.Request.DataAsString.StartsWith("rtfd") &&
                ctx.Request.DataAsString.Contains("TXT.rtf"))
            {
                contentType = "text/x-rtfd";
            }
            if (contentType.StartsWith("text"))
            {
                string text = Encoding.UTF8.GetString(ctx.Request.DataAsBytes);
                var match = Regex.Match(text, @"({\\rtf.*})", RegexOptions.Singleline);
                if (match.Success)
                {
                    text = match.Groups[1].Value;
                }
                string format = DataFormats.Text;
                string plainText = string.Empty;
                if (contentType.Contains("rtf"))
                {
                    format = DataFormats.Rtf;
                    dispatcher.Invoke(() =>
                    {
                        using (var rtb = new System.Windows.Forms.RichTextBox())
                        {
                            rtb.Rtf = text;
                            plainText = rtb.Text;
                        }
                    });
                }
                else if (contentType.Contains("html"))
                {
                    format = DataFormats.Html;
                    plainText = StripHtmlTags(text);
                }
                else if (contentType.Contains("unicode"))
                {
                    format = DataFormats.UnicodeText;
                    plainText = text;
                }
                else if (contentType.Contains("comma"))
                {
                    format = DataFormats.CommaSeparatedValue;
                    plainText = text;
                }
                if (!string.IsNullOrWhiteSpace(text))
                {
                    dispatcher.Invoke(() =>
                    {
                        if (format == DataFormats.Text && !string.IsNullOrEmpty(plainText))
                        {
                            Clipboard.SetText(plainText, TextDataFormat.Text);
                        }
                        else
                        {
                            var data = new DataObject();
                            data.SetData(format, text);
                            data.SetData(DataFormats.Text, plainText);
                            if(format != DataFormats.UnicodeText)
                            {
                                data.SetData(DataFormats.UnicodeText, plainText);
                            }
                            Clipboard.SetDataObject(data);
                        }
                    });
                }
            }
            else if (contentType.StartsWith("image"))
            {
                BitmapSource image = IsValidImage(ctx.Request.DataAsBytes);
                if (image != null)
                {
                    dispatcher.Invoke(() =>
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
                    dispatcher.Invoke(() =>
                    {
                        var fileList = new System.Collections.Specialized.StringCollection() { filePath };
                        Clipboard.SetFileDropList(fileList);
                    });
                }
                catch (Exception)
                {
                }
                await SendString(ctx, "OK");
                return;
            }

            await SendString(ctx, "OK");
            return;
        }
        #endregion

        private static BitmapSource IsValidImage(byte[] data)
        {
            try
            {
                var image = new BitmapImage();
                using (var memoryStream = new MemoryStream(data))
                {
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = memoryStream;
                    image.EndInit();
                }
                return image;
            }
            catch (ArgumentException)
            {
            }
            return null;
        }

        public static bool IsRealImage(string path)
        {
            try
            {
                System.Drawing.Image img = System.Drawing.Image.FromFile(path);
                Console.WriteLine("\nIt is a real image");
                return true;
            }
            catch (Exception)
            {
                Console.WriteLine("\nIt is a fate image");
                return false;
            }
        }

        private static async Task SendString(HttpContext ctx, string msg, string contentType = "text/plain;charset=utf-8", int statusCode = (int)HttpStatusCode.OK)
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

        private static string StripHtmlTags(string input)
        {
            input = input.Replace("\n", "");
            input = input.Replace("\r", "");
            input = Regex.Replace(input, "<br.*?>", "\n");
            return Regex.Replace(input, "<.*?>", "");
        }
    }
}

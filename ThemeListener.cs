using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static ClipboardServer.MainWindow;
using System.Windows.Interop;
using System.Windows;

namespace ClipboardServer
{
    public class ThemeListener
    {
        public delegate void ThemeChangedEventHandler(WindowsTheme theme);
        public event ThemeChangedEventHandler ThemeChanged;
        private Timer timer = null;
        public ThemeListener(Window win)
        {
            IntPtr wptr = new WindowInteropHelper(win).Handle;
            HwndSource hs = HwndSource.FromHwnd(wptr);
            hs.AddHook(new HwndSourceHook(WndProc));
        }

        private const int WM_DWMCOLORIZATIONCOLORCHANGED = 0x320;
        private const int WM_THEMECHANGED = 0x031A;
        private const int WM_SETTINGCHANGE = 0x001A;
        private const string ImmersiveColorSet = "ImmersiveColorSet";
        IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_DWMCOLORIZATIONCOLORCHANGED:
                case WM_THEMECHANGED:
                    OnThemeChange();
                    break;
                case WM_SETTINGCHANGE:
                    string systemParam = Marshal.PtrToStringUni(lParam);
                    if (systemParam == ImmersiveColorSet)
                    {
                        OnThemeChange();
                    }
                    break;
            }
            return IntPtr.Zero;
        }

        private void OnThemeChange()
        {
            timer?.Dispose();
            timer = new Timer(ChangeTheme, null, 1000, Timeout.Infinite);
        }

        private void ChangeTheme(object state)
        {
            ThemeChanged?.Invoke(ThemeHelper.GetWindowsTheme());
        }
    }
}

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace EasyShut
{
    internal static class AppIcon
    {
        // The geometry and alpha channel are identical in both resources.
        private static readonly Icon White = new Icon(typeof(AppIcon), "App.ico");
        private static readonly Icon Black = new Icon(typeof(AppIcon), "App.black.ico");

        public static Icon Caption(bool active)
        {
            if (SystemInformation.HighContrast)
                return OnBackground(active ? SystemColors.ActiveCaption : SystemColors.InactiveCaption);

            // Classic WinForms captions stay light even when Windows apps use dark mode.
            // Only an enabled title-bar accent changes their background.
            const string dwm = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM";
            if (ReadNumber(dwm, "ColorPrevalence", 0) != 0)
            {
                long color = ReadNumber(dwm, active ? "AccentColor" : "AccentColorInactive", -1);
                if (color != -1)
                    return OnBackground(Color.FromArgb((int)(color & 255), (int)((color >> 8) & 255), (int)((color >> 16) & 255)));
            }
            return Black;
        }

        public static Icon Taskbar()
        {
            if (SystemInformation.HighContrast) return OnBackground(SystemColors.Window);
            const string personalize = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
            return ReadNumber(personalize, "SystemUsesLightTheme", 0) != 0 ? Black : White;
        }

        private static Icon OnBackground(Color color)
        {
            double luminance = .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
            return luminance > .179 ? Black : White;
        }

        private static double Linear(byte component)
        {
            double value = component / 255.0;
            return value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4);
        }

        private static long ReadNumber(string key, string name, long fallback)
        {
            try
            {
                object value = Registry.GetValue(key, name, null);
                return value is int ? unchecked((uint)(int)value) : fallback;
            }
            catch (System.Security.SecurityException) { return fallback; }
            catch (UnauthorizedAccessException) { return fallback; }
            catch (System.IO.IOException) { return fallback; }
        }
    }

    // Windows has separate small (caption) and large (taskbar / Alt+Tab) icons.
    // Form.Icon alone assigns one color to both, which disappears on the other surface.
    internal class IconForm : Form
    {
        private Icon captionSource, taskbarSource, captionIcon, taskbarIcon;
        private int iconDpi;
        private bool captionActive;

        protected IconForm() { Icon = AppIcon.Taskbar(); }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateIcons();
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_SETICON = 0x80, WM_NCACTIVATE = 0x86;
            if (m.Msg == WM_SETICON)
            {
                Icon selected = m.WParam == new IntPtr(1) ? taskbarIcon : captionIcon;
                if (selected != null) m.LParam = selected.Handle;
            }
            if (m.Msg == WM_NCACTIVATE) captionActive = m.WParam != IntPtr.Zero;
            base.WndProc(ref m);
            // Theme, accent, high-contrast, activation and monitor DPI can change while open.
            if (m.Msg == WM_NCACTIVATE || m.Msg == 0x1A || m.Msg == 0x15 ||
                m.Msg == 0x31A || m.Msg == 0x320 || m.Msg == 0x2E0)
                UpdateIcons();
        }

        private void UpdateIcons()
        {
            if (!IsHandleCreated || IsDisposed || Disposing) return;
            int dpi = 96;
            try { dpi = (int)GetDpiForWindow(Handle); }
            catch (EntryPointNotFoundException) { }
            if (dpi <= 0) dpi = 96;
            Icon caption = AppIcon.Caption(captionActive), taskbar = AppIcon.Taskbar();
            if (captionSource == caption && taskbarSource == taskbar && iconDpi == dpi) return;

            Icon oldCaption = captionIcon, oldTaskbar = taskbarIcon;
            captionIcon = new Icon(caption, new Size(16 * dpi / 96, 16 * dpi / 96));
            taskbarIcon = new Icon(taskbar, new Size(32 * dpi / 96, 32 * dpi / 96));
            captionSource = caption; taskbarSource = taskbar; iconDpi = dpi;
            SendMessage(Handle, 0x80, IntPtr.Zero, captionIcon.Handle);
            SendMessage(Handle, 0x80, new IntPtr(1), taskbarIcon.Handle);
            if (oldCaption != null) oldCaption.Dispose();
            if (oldTaskbar != null) oldTaskbar.Dispose();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            if (captionIcon != null) { captionIcon.Dispose(); captionIcon = null; }
            if (taskbarIcon != null) { taskbarIcon.Dispose(); taskbarIcon = null; }
            captionSource = null; taskbarSource = null;
        }

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr window);
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    }
}

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EasyShut
{
    public sealed class NativePower : IPower
    {
        private const uint Continuous = 0x80000000, SystemRequired = 1, DisplayRequired = 2;
        private readonly MonitorWindow monitor = new MonitorWindow();
        public void Hold(bool screenOn)
        {
            if (SetThreadExecutionState(Continuous | SystemRequired | (screenOn ? DisplayRequired : 0)) == 0)
                throw new Win32Exception("Windows nie przyjął blokady usypiania.");
        }
        public void Release() { SetThreadExecutionState(Continuous); }
        public void Dispose() { Release(); monitor.Dispose(); }
        public void TurnScreenOff()
        {
            // Our invisible window uses DefWindowProc to handle SC_MONITORPOWER.
            // No broadcast to other apps, and no queued worker surviving Stop/Start.
            UIntPtr result;
            if (SendMessageTimeout(monitor.Handle, 0x0112, new IntPtr(0xf170), new IntPtr(2), 2, 1000, out result) == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        private sealed class MonitorWindow : NativeWindow, IDisposable
        {
            public MonitorWindow() { CreateHandle(new CreateParams { Caption = "EasyShut power" }); }
            public void Dispose() { DestroyHandle(); }
        }
        public void Execute(PowerAction action)
        {
            using (new ShutdownPrivilege())
            {
                if (action == PowerAction.Sleep)
                {
                    if (!SetSuspendState(false, false, false)) throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                else if (!InitiateSystemShutdownEx(null, "EasyShut: upłynął ustawiony czas.", 0, true, false, 0x80040000))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        [DllImport("kernel32.dll")] private static extern uint SetThreadExecutionState(uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out UIntPtr result);
        [DllImport("powrprof.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool SetSuspendState([MarshalAs(UnmanagedType.U1)] bool hibernate, [MarshalAs(UnmanagedType.U1)] bool force, [MarshalAs(UnmanagedType.U1)] bool disableWake);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool InitiateSystemShutdownEx(string machine, string message, uint timeout, bool forceAppsClosed, bool reboot, uint reason);

        private sealed class ShutdownPrivilege : IDisposable
        {
            private IntPtr token;
            private TokenPrivileges previous;
            private bool adjusted;
            public ShutdownPrivilege()
            {
                try
                {
                    if (!OpenProcessToken(Process.GetCurrentProcess().Handle, 0x20 | 0x8, out token)) throw new Win32Exception(Marshal.GetLastWin32Error());
                    Luid luid;
                    if (!LookupPrivilegeValue(null, "SeShutdownPrivilege", out luid)) throw new Win32Exception(Marshal.GetLastWin32Error());
                    var requested = new TokenPrivileges { Count = 1, Luid = luid, Attributes = 2 };
                    int returned;
                    if (!AdjustTokenPrivileges(token, false, ref requested, Marshal.SizeOf(typeof(TokenPrivileges)), out previous, out returned))
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    int error = Marshal.GetLastWin32Error();
                    if (error != 0) throw new Win32Exception(error);
                    adjusted = true;
                }
                catch { Dispose(); throw; }
            }
            public void Dispose()
            {
                if (token == IntPtr.Zero) return;
                if (adjusted) { TokenPrivileges ignored; int length; AdjustTokenPrivileges(token, false, ref previous, Marshal.SizeOf(typeof(TokenPrivileges)), out ignored, out length); }
                CloseHandle(token); token = IntPtr.Zero;
            }
        }
        [StructLayout(LayoutKind.Sequential)] private struct Luid { public uint Low; public int High; }
        [StructLayout(LayoutKind.Sequential)] private struct TokenPrivileges { public uint Count; public Luid Luid; public uint Attributes; }
        [DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool LookupPrivilegeValue(string system, string name, out Luid luid);
        [DllImport("advapi32.dll", SetLastError = true)] private static extern bool AdjustTokenPrivileges(IntPtr token, bool disableAll, ref TokenPrivileges value, int size, out TokenPrivileges previous, out int length);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    }
}

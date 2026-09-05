using System;
using System.Reflection;
using System.Runtime.InteropServices;
using EasyShut;

internal static class NativeSmoke
{
    [DllImport("kernel32.dll")] private static extern uint SetThreadExecutionState(uint flags);
    [STAThread]
    private static int Main()
    {
        // Never call TurnScreenOff or Execute here. Only reversible power holds.
        using (var power = new NativePower())
        {
            power.Hold(true);
            uint flags = SetThreadExecutionState(0);
            if ((flags & 3) != 3) throw new Exception("Missing system/display execution-state flags: " + flags);
            power.Hold(false);
            flags = SetThreadExecutionState(0);
            if ((flags & 3) != 1) throw new Exception("Display hold was not cleared: " + flags);
            power.Release();
            flags = SetThreadExecutionState(0);
            if ((flags & 3) != 0) throw new Exception("Execution-state hold leaked: " + flags);
        }
        // Confirm the current user's shutdown privilege can be enabled and restored.
        // This does not request a shutdown or sleep.
        Type privilege = typeof(NativePower).GetNestedType("ShutdownPrivilege", BindingFlags.NonPublic);
        using ((IDisposable)Activator.CreateInstance(privilege, true)) { }
        Console.WriteLine("PASS native Windows: hold system/display, change display policy, release, enable/restore shutdown privilege. No shutdown/sleep/display-off requested.");
        return 0;
    }
}

using System.Runtime.InteropServices;

namespace EasyShut
{
    internal static class AppIdentity
    {
#if TESTING
        public const string Id = "szymongazinski.EasyShut.Test";
#else
        public const string Id = "szymongazinski.EasyShut";
#endif
        public static void Initialize() { Marshal.ThrowExceptionForHR(SetCurrentProcessExplicitAppUserModelID(Id)); }
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
    }
}

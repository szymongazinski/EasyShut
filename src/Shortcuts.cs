using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace EasyShut
{
    // Separate from registry integration so shortcut rollback can be tested in a temporary folder.
    public sealed class ShortcutRegistration : IDisposable
    {
        private sealed class Backup
        {
            public string Path;
            public byte[] Bytes;
            public bool Changed;
        }
        private readonly string target;
        private readonly List<Backup> backups = new List<Backup>();
        private bool started, committed;
        public ShortcutRegistration(string target, string programs, string desktop, bool addDesktop)
        {
            this.target = Path.GetFullPath(target);
            Add(Path.Combine(programs, "EasyShut.lnk"));
            if (addDesktop) Add(Path.Combine(desktop, "EasyShut.lnk"));
        }
        private void Add(string path)
        {
            if (File.Exists(path) && !IsOwned(path, target))
                throw new IOException("Istniejący skrót nie prowadzi do EasyShut: " + path);
            backups.Add(new Backup { Path = path, Bytes = File.Exists(path) ? File.ReadAllBytes(path) : null });
        }
        public void Register()
        {
            started = true;
            foreach (Backup backup in backups) Write(backup, target);
        }
        public void Commit()
        {
            committed = true;
            foreach (Backup backup in backups) Notify(backup.Path, backup.Bytes == null ? 2U : 0x2000U);
        }
        public void Dispose()
        {
            if (!started || committed) return;
            foreach (Backup backup in backups)
            {
                if (!backup.Changed) continue;
                if (backup.Bytes == null) File.Delete(backup.Path);
                else File.WriteAllBytes(backup.Path, backup.Bytes);
                Notify(backup.Path, backup.Bytes == null ? 4U : 0x2000U);
            }
        }
        public static bool IsOwned(string path, string directory)
        {
            if (!File.Exists(path)) return false;
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            try
            {
                dynamic link = shell.CreateShortcut(path);
                try
                {
                    string destination = link.TargetPath;
                    return !string.IsNullOrEmpty(destination) && string.Equals(Path.GetFullPath(destination),
                        Path.Combine(Path.GetFullPath(directory), "EasyShut-window.exe"), StringComparison.OrdinalIgnoreCase);
                }
                finally { Marshal.FinalReleaseComObject(link); }
            }
            catch (COMException) { return false; }
            catch (ArgumentException) { return false; }
            finally { Marshal.FinalReleaseComObject(shell); }
        }
        public static void RemoveOwned(string path, string directory)
        {
            if (!IsOwned(path, directory)) return;
            File.Delete(path);
            Notify(path, 4);
        }
        private static void Write(Backup backup, string target)
        {
            string path = backup.Path;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (File.Exists(path)) File.Delete(path);
            backup.Changed = true;
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            try
            {
                dynamic link = shell.CreateShortcut(path);
                try
                {
                    link.TargetPath = Path.Combine(target, "EasyShut-window.exe");
                    link.WorkingDirectory = target;
                    link.IconLocation = Path.Combine(target, "EasyShut-window.exe") + ",0";
                    link.Description = "EasyShut — blokada usypiania i odliczanie";
                    link.Save();
                }
                finally { Marshal.FinalReleaseComObject(link); }
            }
            finally { Marshal.FinalReleaseComObject(shell); }
            SetIdentity(path);
        }
        private static void SetIdentity(string path)
        {
            Guid iid = typeof(IPropertyStore).GUID;
            IPropertyStore store;
            Marshal.ThrowExceptionForHR(SHGetPropertyStoreFromParsingName(path, IntPtr.Zero, 2, ref iid, out store));
            var key = new PropertyKey { Format = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), Id = 5 };
            var value = new PropertyValue { Type = 31, Pointer = Marshal.StringToCoTaskMemUni(AppIdentity.Id) };
            try { store.SetValue(ref key, ref value); store.Commit(); }
            finally { Marshal.FreeCoTaskMem(value.Pointer); Marshal.FinalReleaseComObject(store); }
        }
        private static void Notify(string path, uint action)
        {
            // Notify both the shortcut and its containing folder, including redirected desktops.
            SHChangeNotify(action, 0x2005, path, IntPtr.Zero);
            SHChangeNotify(0x1000, 0x2005, Path.GetDirectoryName(path), IntPtr.Zero);
        }
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct PropertyKey { public Guid Format; public uint Id; }
        [StructLayout(LayoutKind.Explicit, Size = 24)]
        private struct PropertyValue
        {
            [FieldOffset(0)] public ushort Type;
            [FieldOffset(8)] public IntPtr Pointer;
        }
        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore
        {
            void GetCount(out uint count);
            void GetAt(uint index, out PropertyKey key);
            void GetValue(ref PropertyKey key, out PropertyValue value);
            void SetValue(ref PropertyKey key, ref PropertyValue value);
            void Commit();
        }
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHGetPropertyStoreFromParsingName(string path, IntPtr context, uint flags, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore store);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern void SHChangeNotify(uint action, uint flags, string path, IntPtr unused);
    }
}

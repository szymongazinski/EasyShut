// Compiled ONLY into build/test. The release does not contain this backend or
// test clock, and test builds cannot call native shutdown/sleep/display APIs.
using System;
using System.Globalization;
using System.IO;

namespace EasyShut
{
    public sealed class TestClock : IClock
    {
        private readonly string path = Environment.GetEnvironmentVariable("EASYSHUT_TEST_CLOCK");
        private double last;
        public TimeSpan Now
        {
            get
            {
                if (!string.IsNullOrEmpty(path))
                {
                    try { double value; if (double.TryParse(File.ReadAllText(path), NumberStyles.Float, CultureInfo.InvariantCulture, out value)) last = value; }
                    catch (IOException) { }
                }
                return TimeSpan.FromSeconds(last);
            }
        }
        public DateTimeOffset UtcNow { get { return new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) + Now; } }
    }
    public sealed class TestPower : IPower
    {
        public static void Log(string message)
        {
            string path = Environment.GetEnvironmentVariable("EASYSHUT_TEST_LOG");
            if (!string.IsNullOrEmpty(path)) File.AppendAllText(path, message + Environment.NewLine);
        }
        public void Hold(bool screenOn) { Log("hold:" + screenOn); }
        public void Release() { Log("release"); }
        public void TurnScreenOff() { Log("screen-off"); }
        public void Execute(PowerAction action) { Log("execute:" + action); }
        public void Dispose() { Release(); }
    }
}

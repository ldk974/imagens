using System;
using System.Diagnostics;

namespace WallpaperSync
{
    public static class DebugLogger
    {
        public static void Log(string message)
        {
#if DEBUG
            try
            {
                DebugLogForm.Instance?.AppendLine(message);
            }
            catch { /* não deixa logging quebrar o app */ }

            Debug.WriteLine(message);
#else
            // Trace.WriteLine(message);
#endif
        }
    }
}

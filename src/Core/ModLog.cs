using System;
using StardewModdingAPI;

namespace LifeWithWifeAndChild.Core
{
    /// <summary>统一日志封装：前缀 [LWWC]，并按 TECH-DESIGN §12.3 分级。</summary>
    internal static class ModLog
    {
        private static IMonitor? monitor;

        internal static bool Verbose { get; set; }

        internal static void Init(IMonitor monitorInstance, bool verbose)
        {
            monitor = monitorInstance;
            Verbose = verbose;
        }

        internal static void Trace(string message)
        {
            if (Verbose)
                monitor?.Log(Constants.LogPrefix + " " + message, LogLevel.Trace);
        }

        internal static void Info(string message)
        {
            monitor?.Log(Constants.LogPrefix + " " + message, LogLevel.Info);
        }

        internal static void Warn(string message)
        {
            monitor?.Log(Constants.LogPrefix + " " + message, LogLevel.Warn);
        }

        internal static void Error(string message, Exception? ex = null)
        {
            monitor?.Log(Constants.LogPrefix + " " + message + (ex != null ? Environment.NewLine + ex : string.Empty), LogLevel.Error);
        }
    }
}

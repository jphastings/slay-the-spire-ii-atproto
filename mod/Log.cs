using System;
using GameLog = MegaCrit.Sts2.Core.Logging.Log;

namespace Sts2At;

internal static class Log
{
    public static void Info(string msg)  => GameLog.Info($"[sts2.at] {msg}");
    public static void Warn(string msg)  => GameLog.Warn($"[sts2.at] {msg}");
    public static void Error(string msg, Exception? ex = null)
        => GameLog.Error($"[sts2.at] {msg}{(ex is null ? "" : $"\n{ex}")}");
}

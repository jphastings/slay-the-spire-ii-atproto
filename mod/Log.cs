using System;
using GameLog = MegaCrit.Sts2.Core.Logging.Log;

namespace AtprotoTracker;

internal static class Log
{
    public static void Info(string msg)  => GameLog.Info($"[atproto-tracker] {msg}");
    public static void Warn(string msg)  => GameLog.Warn($"[atproto-tracker] {msg}");
    public static void Error(string msg, Exception? ex = null)
        => GameLog.Error($"[atproto-tracker] {msg}{(ex is null ? "" : $"\n{ex}")}");
}

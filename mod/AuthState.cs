using System;

namespace AtprotoTracker;

internal enum AuthStatus
{
    Unconfigured,
    Checking,
    Ok,
    Failed,
    Offline,
}

internal static class AuthState
{
    private static readonly object _lock = new();
    private static AuthStatus _status = AuthStatus.Unconfigured;
    private static string? _handle;
    private static string? _did;
    private static string? _error;

    public static AuthStatus Status  { get { lock (_lock) return _status; } }
    public static string?    Handle  { get { lock (_lock) return _handle; } }
    public static string?    Did     { get { lock (_lock) return _did;    } }
    public static string?    Error   { get { lock (_lock) return _error;  } }

    public static event Action? Changed;

    public static void Set(AuthStatus status, string? handle = null, string? did = null, string? error = null)
    {
        lock (_lock)
        {
            _status = status;
            _handle = handle ?? _handle;
            _did    = did    ?? _did;
            _error  = error;
        }
        Changed?.Invoke();
    }
}

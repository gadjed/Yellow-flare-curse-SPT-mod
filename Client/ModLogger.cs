using System;
using System.IO;
using BepInEx.Logging;
using UnityEngine;

namespace YellowFlareCurse.Client;

/// <summary>
/// Writes to BepInEx console/LogOutput, Unity console, a dedicated log file,
/// and the SPT server console (via SPT.Common.Utils.ServerLog).
/// </summary>
internal static class ModLogger
{
    private const string Tag = "[YellowFlareCurse]";
    private const string ServerSource = "YellowFlareCurse";

    private static ManualLogSource? _bepInEx;
    private static readonly object FileLock = new();
    private static string? _logFilePath;
    private static bool _serverLogAvailable = true;

    public static void Init(ManualLogSource logger)
    {
        _bepInEx = logger;

        try
        {
            var pluginDir = Path.Combine(Paths.PluginPathOrFallback(), "YellowFlareCurse");
            var logDir = Path.Combine(pluginDir, "logs");
            Directory.CreateDirectory(logDir);
            _logFilePath = Path.Combine(logDir, $"yellowflarecurse-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            File.WriteAllText(
                _logFilePath,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} === Yellow Flare Curse client log started ==={Environment.NewLine}"
            );
            Info($"File logging → {_logFilePath}");
        }
        catch (Exception ex)
        {
            _bepInEx?.LogWarning($"{Tag} Could not create log file: {ex.Message}");
        }
    }

    public static void Info(string message) => Write(LogLevel.Info, message, toServer: true);

    public static void Warning(string message) => Write(LogLevel.Warning, message, toServer: true);

    public static void Error(string message) => Write(LogLevel.Error, message, toServer: true);

    public static void Debug(string message)
    {
        if (YellowFlareCursePlugin.Debug?.Value != true)
        {
            return;
        }

        Write(LogLevel.Debug, message, toServer: true);
    }

    private static void Write(LogLevel level, string message, bool toServer)
    {
        var line = message.StartsWith(Tag, StringComparison.Ordinal) ? message : $"{Tag} {message}";

        switch (level)
        {
            case LogLevel.Warning:
                _bepInEx?.LogWarning(line);
                break;
            case LogLevel.Error:
                _bepInEx?.LogError(line);
                break;
            case LogLevel.Debug:
                _bepInEx?.LogDebug(line);
                break;
            default:
                _bepInEx?.LogInfo(line);
                break;
        }

        try
        {
            switch (level)
            {
                case LogLevel.Warning:
                    UnityEngine.Debug.LogWarning(line);
                    break;
                case LogLevel.Error:
                    UnityEngine.Debug.LogError(line);
                    break;
                default:
                    UnityEngine.Debug.Log(line);
                    break;
            }
        }
        catch
        {
            // Unity console unavailable outside play mode / early init.
        }

        AppendFile(level, line);

        if (toServer)
        {
            TryServerLog(level, line);
        }
    }

    private static void AppendFile(LogLevel level, string line)
    {
        if (string.IsNullOrEmpty(_logFilePath))
        {
            return;
        }

        try
        {
            lock (FileLock)
            {
                File.AppendAllText(
                    _logFilePath,
                    $"{DateTime.Now:HH:mm:ss.fff} [{level}] {line}{Environment.NewLine}"
                );
            }
        }
        catch
        {
            // Avoid recursive logging failures.
        }
    }

    private static void TryServerLog(LogLevel level, string line)
    {
        if (!_serverLogAvailable)
        {
            return;
        }

        try
        {
            switch (level)
            {
                case LogLevel.Warning:
                    SPT.Common.Utils.ServerLog.Warn(ServerSource, line);
                    break;
                case LogLevel.Error:
                    SPT.Common.Utils.ServerLog.Error(ServerSource, line);
                    break;
                case LogLevel.Debug:
                    SPT.Common.Utils.ServerLog.Debug(ServerSource, line);
                    break;
                default:
                    SPT.Common.Utils.ServerLog.Info(ServerSource, line);
                    break;
            }
        }
        catch (Exception)
        {
            _serverLogAvailable = false;
            _bepInEx?.LogWarning($"{Tag} SPT ServerLog unavailable; continuing with local logs only.");
        }
    }

    private static class Paths
    {
        public static string PluginPathOrFallback()
        {
            try
            {
                return BepInEx.Paths.PluginPath;
            }
            catch
            {
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BepInEx", "plugins");
            }
        }
    }
}

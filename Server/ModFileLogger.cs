using Path = System.IO.Path;

namespace YellowFlareCurse;

/// <summary>
/// Mirrors ISptLogger output into a dedicated mod log file under the mod folder.
/// </summary>
public sealed class ModFileLogger
{
    private readonly object _lock = new();
    private readonly string _logFilePath;
    private readonly Action<string> _info;
    private readonly Action<string> _warning;
    private readonly Action<string> _error;
    private readonly Action<string> _success;

    public ModFileLogger(
        string modFolder,
        Action<string> info,
        Action<string> warning,
        Action<string> error,
        Action<string> success
    )
    {
        _info = info;
        _warning = warning;
        _error = error;
        _success = success;

        var logDir = Path.Combine(modFolder, "logs");
        Directory.CreateDirectory(logDir);
        _logFilePath = Path.Combine(logDir, $"yellowflarecurse-server-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        File.WriteAllText(
            _logFilePath,
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} === Yellow Flare Curse server log started ==={Environment.NewLine}"
        );
    }

    public string LogFilePath => _logFilePath;

    public void Info(string message)
    {
        _info(message);
        Append("INFO", message);
    }

    public void Warning(string message)
    {
        _warning(message);
        Append("WARN", message);
    }

    public void Error(string message)
    {
        _error(message);
        Append("ERROR", message);
    }

    public void Success(string message)
    {
        _success(message);
        Append("OK", message);
    }

    private void Append(string level, string message)
    {
        try
        {
            lock (_lock)
            {
                File.AppendAllText(
                    _logFilePath,
                    $"{DateTime.Now:HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}"
                );
            }
        }
        catch
        {
            // Ignore file IO failures to avoid breaking raid loot generation.
        }
    }

    public static ModFileLogger? Instance { get; set; }
}

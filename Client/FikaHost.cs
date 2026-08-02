using System;
using System.Reflection;

namespace YellowFlareCurse.Client;

/// <summary>
/// Host/authority check for Fika co-op. Solo SPT (no Fika) is always authority.
/// </summary>
internal static class FikaHost
{
    private static bool _resolved;
    private static bool _fikaExists;
    private static PropertyInfo? _isServer;

    public static bool IsAuthority()
    {
        Resolve();
        if (!_fikaExists || _isServer == null)
        {
            return true;
        }

        try
        {
            return (bool)_isServer.GetValue(null)!;
        }
        catch
        {
            return true;
        }
    }

    private static void Resolve()
    {
        if (_resolved)
        {
            return;
        }

        _resolved = true;

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.GetName().Name != "Fika.Core")
            {
                continue;
            }

            _fikaExists = true;
            var utils = assembly.GetType("Fika.Core.Main.Utils.FikaBackendUtils");
            _isServer = utils?.GetProperty("IsServer", BindingFlags.Public | BindingFlags.Static);
            return;
        }
    }
}

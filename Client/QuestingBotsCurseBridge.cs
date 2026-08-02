using System;
using System.Linq;
using System.Reflection;
using EFT;
using UnityEngine;

namespace YellowFlareCurse.Client;

/// <summary>
/// Optional QuestingBots bridge — stops quest objectives so cursed PMC/PScavs can hunt.
/// Scavs usually don't quest; this mainly helps PMC bots under QuestingBots Continuous.
/// </summary>
internal static class QuestingBotsCurseBridge
{
    private static bool _resolved;
    private static bool _present;
    private static Type? _objectiveManagerType;
    private static MethodInfo? _stopQuesting;

    public static bool IsReady => _present;

    /// <returns>True if StopQuesting was invoked.</returns>
    public static bool StopQuesting(BotOwner botOwner)
    {
        if (botOwner == null)
        {
            return false;
        }

        try
        {
            Resolve();
            if (!_present || _objectiveManagerType == null || _stopQuesting == null)
            {
                return false;
            }

            var manager = FindObjectiveManager(botOwner);
            if (manager == null)
            {
                return false;
            }

            _stopQuesting.Invoke(manager, null);
            return true;
        }
        catch (Exception ex)
        {
            ModLogger.Debug($"QuestingBots bridge failed: {ex.Message}");
            return false;
        }
    }

    private static object? FindObjectiveManager(BotOwner botOwner)
    {
        // Factory attaches to botOwner.GetPlayer.gameObject
        try
        {
            var player = botOwner.GetPlayer;
            if (player != null)
            {
                var onPlayer = player.gameObject.GetComponent(_objectiveManagerType);
                if (onPlayer != null)
                {
                    return onPlayer;
                }
            }
        }
        catch
        {
            // GetPlayer may throw if bot is mid-despawn.
        }

        if (botOwner.gameObject != null)
        {
            var onOwner = botOwner.gameObject.GetComponent(_objectiveManagerType);
            if (onOwner != null)
            {
                return onOwner;
            }
        }

        return null;
    }

    private static void Resolve()
    {
        if (_resolved)
        {
            return;
        }

        _resolved = true;

        try
        {
            _objectiveManagerType = FindType("QuestingBots.Components.BotObjectiveManager");

            // Fallback: scan loaded plugin assemblies by short name only.
            if (_objectiveManagerType == null)
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var name = asm.GetName().Name ?? string.Empty;
                    if (name.IndexOf("Questing", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    try
                    {
                        _objectiveManagerType = asm.GetType("QuestingBots.Components.BotObjectiveManager", throwOnError: false);
                        if (_objectiveManagerType != null)
                        {
                            break;
                        }
                    }
                    catch
                    {
                        // ignore unloadable assemblies
                    }
                }
            }

            _stopQuesting = _objectiveManagerType?.GetMethod(
                "StopQuesting",
                BindingFlags.Public | BindingFlags.Instance
            );

            _present = _objectiveManagerType != null && _stopQuesting != null;
            ModLogger.Info(
                _present
                    ? "QuestingBots bridge ready (will StopQuesting on cursed bots)."
                    : "QuestingBots not found — no quest pause."
            );
        }
        catch (Exception ex)
        {
            ModLogger.Debug($"QuestingBots resolve failed: {ex.Message}");
            _present = false;
        }
    }

    private static Type? FindType(string fullName)
    {
        foreach (var asmName in new[] { "QuestingBots", "QuestingBotsContinuous", "SPTQuestingBots" })
        {
            var t = Type.GetType($"{fullName}, {asmName}", throwOnError: false);
            if (t != null)
            {
                return t;
            }
        }

        return AppDomain.CurrentDomain
            .GetAssemblies()
            .Select(a =>
            {
                try
                {
                    return a.GetType(fullName, throwOnError: false);
                }
                catch
                {
                    return null;
                }
            })
            .FirstOrDefault(t => t != null);
    }
}

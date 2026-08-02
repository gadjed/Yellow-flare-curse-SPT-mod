using System;
using System.Linq;
using System.Reflection;
using EFT;
using UnityEngine;

namespace YellowFlareCurse.Client;

/// <summary>
/// Optional SAIN bridge via reflection — no hard dependency on SAIN.dll.
/// Sets a last-known place so SAIN treats the player as EnemyKnown and will hunt.
/// </summary>
internal static class SainCurseBridge
{
    private static bool _resolved;
    private static bool _sainPresent;
    private static Type? _botComponentType;
    private static MethodInfo? _getSainByProfileId;
    private static MethodInfo? _getSainByBotOwner;
    private static object? _botManagerInstance;

    public static bool IsReady => _sainPresent;

    /// <returns>True if SAIN enemy was marked known.</returns>
    public static bool NotifySeen(BotOwner botOwner, IPlayer target)
    {
        if (botOwner == null || target == null)
        {
            return false;
        }

        try
        {
            Resolve();
            if (!_sainPresent)
            {
                return false;
            }

            var sain = ResolveBotComponent(botOwner);
            if (sain == null)
            {
                return false;
            }

            var enemyController = sain.GetType().GetProperty("EnemyController")?.GetValue(sain);
            if (enemyController == null)
            {
                return false;
            }

            var checkAdd = enemyController.GetType().GetMethod("CheckAddEnemy", new[] { typeof(IPlayer) });
            var enemy = checkAdd?.Invoke(enemyController, new object[] { target });
            if (enemy == null)
            {
                return false;
            }

            var knownPlaces = enemy.GetType().GetProperty("KnownPlaces")?.GetValue(enemy);
            if (knownPlaces == null)
            {
                return false;
            }

            var update = knownPlaces
                .GetType()
                .GetMethod("UpdateSeenPlace", new[] { typeof(Vector3), typeof(float) });
            if (update == null)
            {
                return false;
            }

            update.Invoke(knownPlaces, new object[] { target.Position, Time.time });
            return true;
        }
        catch (Exception ex)
        {
            ModLogger.Debug($"SAIN bridge failed: {ex.Message}");
            return false;
        }
    }

    private static object? ResolveBotComponent(BotOwner botOwner)
    {
        // 1) Direct component on BotOwner / its GameObject (most reliable).
        if (_botComponentType != null)
        {
            var onOwner = botOwner.GetComponent(_botComponentType);
            if (onOwner != null)
            {
                return onOwner;
            }

            if (botOwner.gameObject != null)
            {
                var onGo = botOwner.gameObject.GetComponent(_botComponentType);
                if (onGo != null)
                {
                    return onGo;
                }
            }
        }

        // 2) BotManagerComponent.GetSAIN(BotOwner, out BotComponent)
        if (_getSainByBotOwner != null)
        {
            RefreshBotManagerInstance();
            if (_botManagerInstance != null)
            {
                var args = new object?[] { botOwner, null };
                if (_getSainByBotOwner.Invoke(_botManagerInstance, args) is true && args[1] != null)
                {
                    return args[1];
                }
            }
        }

        // 3) SAINEnableClass.GetSAIN(profileId, out)
        if (_getSainByProfileId != null)
        {
            var profileId = botOwner.ProfileId;
            if (string.IsNullOrEmpty(profileId))
            {
                profileId = botOwner.Profile?.Id;
            }

            if (!string.IsNullOrEmpty(profileId))
            {
                var args = new object?[] { profileId, null };
                if (_getSainByProfileId.Invoke(null, args) is true && args[1] != null)
                {
                    return args[1];
                }
            }
        }

        return null;
    }

    private static void RefreshBotManagerInstance()
    {
        if (_botManagerInstance != null || _getSainByBotOwner == null)
        {
            return;
        }

        try
        {
            var declaring = _getSainByBotOwner.DeclaringType;
            var instanceProp = declaring?.GetProperty(
                "Instance",
                BindingFlags.Public | BindingFlags.Static
            );
            _botManagerInstance = instanceProp?.GetValue(null);
        }
        catch
        {
            _botManagerInstance = null;
        }
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
            // Real type lives in namespace SAIN (NOT SAIN.Plugin).
            var enableType =
                FindType("SAIN.SAINEnableClass")
                ?? FindType("SAIN.Plugin.SAINEnableClass");

            _botComponentType =
                FindType("SAIN.Components.BotComponent")
                ?? FindType("SAIN.SAINComponent.BotComponent");

            var managerType = FindType("SAIN.Components.BotManagerComponent");

            if (enableType != null)
            {
                _getSainByProfileId = enableType
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m =>
                        m.Name == "GetSAIN"
                        && m.GetParameters().Length == 2
                        && m.GetParameters()[0].ParameterType == typeof(string)
                        && m.GetParameters()[1].ParameterType.IsByRef
                    );
            }

            if (managerType != null)
            {
                _getSainByBotOwner = managerType
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m =>
                        m.Name == "GetSAIN"
                        && m.GetParameters().Length == 2
                        && typeof(BotOwner).IsAssignableFrom(m.GetParameters()[0].ParameterType)
                        && m.GetParameters()[1].ParameterType.IsByRef
                    );
            }

            _sainPresent = _botComponentType != null || _getSainByProfileId != null || _getSainByBotOwner != null;
            ModLogger.Info(
                _sainPresent
                    ? $"SAIN bridge ready (component={_botComponentType != null}, byOwner={_getSainByBotOwner != null}, byProfile={_getSainByProfileId != null})."
                    : "SAIN not found — using vanilla aggro only."
            );
        }
        catch (Exception ex)
        {
            ModLogger.Warning($"SAIN resolve failed: {ex.Message}");
            _sainPresent = false;
        }
    }

    private static Type? FindType(string fullName)
    {
        var asmQualified = Type.GetType(fullName + ", SAIN", throwOnError: false);
        if (asmQualified != null)
        {
            return asmQualified;
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

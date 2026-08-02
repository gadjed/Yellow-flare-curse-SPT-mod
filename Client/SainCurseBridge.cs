using System;
using System.Linq;
using System.Reflection;
using EFT;
using UnityEngine;

namespace YellowFlareCurse.Client;

/// <summary>
/// Optional SAIN bridge via reflection — no hard dependency on SAIN.dll.
/// Marks the player as a known + heard (gunshot) enemy so bots commit to hunt
/// instead of standing still after teleport.
/// </summary>
internal static class SainCurseBridge
{
    private static bool _resolved;
    private static bool _sainPresent;
    private static Type? _botComponentType;
    private static Type? _hearingReportType;
    private static Type? _soundTypeEnum;
    private static Type? _placeTypeEnum;
    private static MethodInfo? _getSainByProfileId;
    private static MethodInfo? _getSainByBotOwner;
    private static object? _botManagerInstance;

    public static bool IsReady => _sainPresent;

    /// <returns>True if SAIN enemy was marked known/heard.</returns>
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

            var now = Time.time;
            var pos = target.Position;
            var marked = false;

            // Strongest path: treat as personally seen (updates last-known + squad report).
            var updateLastSeen = enemy.GetType().GetMethod(
                "UpdateLastSeenPosition",
                new[] { typeof(Vector3), typeof(float) }
            );
            if (updateLastSeen != null)
            {
                updateLastSeen.Invoke(enemy, new object[] { pos, now });
                marked = true;
            }
            else
            {
                var knownPlaces = enemy.GetType().GetProperty("KnownPlaces")?.GetValue(enemy);
                var update = knownPlaces
                    ?.GetType()
                    .GetMethod("UpdateSeenPlace", new[] { typeof(Vector3), typeof(float) });
                if (update != null)
                {
                    update.Invoke(knownPlaces, new object[] { pos, now });
                    marked = true;
                }
            }

            // Fake a dangerous gunshot from the player so SAIN leaves "peace" / slow Search
            // and commits to engage (EnemyHeardFromPeace + EnemyGunshotHeardFromPeace).
            if (TrySetHeardGunshot(enemy, pos, now))
            {
                marked = true;
            }

            // Explicitly keep EnemyKnown latched.
            var knownChecker = enemy.GetType().GetProperty("KnownChecker", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(enemy)
                ?? enemy.GetType().GetProperty("KnownChecker")?.GetValue(enemy);
            var setKnown = knownChecker
                ?.GetType()
                .GetMethod("SetEnemyKnown", new[] { typeof(bool), typeof(float) });
            setKnown?.Invoke(knownChecker, new object[] { true, now });

            return marked;
        }
        catch (Exception ex)
        {
            ModLogger.Debug($"SAIN bridge failed: {ex.Message}");
            return false;
        }
    }

    private static bool TrySetHeardGunshot(object enemy, Vector3 position, float currentTime)
    {
        try
        {
            if (_hearingReportType == null || _soundTypeEnum == null || _placeTypeEnum == null)
            {
                return false;
            }

            var hearing = enemy.GetType().GetProperty("Hearing")?.GetValue(enemy);
            var setHeard = hearing?.GetType().GetMethod("SetHeard", new[] { _hearingReportType, typeof(float) });
            if (hearing == null || setHeard == null)
            {
                return false;
            }

            var report = Activator.CreateInstance(_hearingReportType)!;
            SetFieldOrProp(report, "position", position);
            SetFieldOrProp(report, "soundType", Enum.Parse(_soundTypeEnum, "Shot"));
            SetFieldOrProp(report, "placeType", Enum.Parse(_placeTypeEnum, "Hearing"));
            SetFieldOrProp(report, "isDanger", true);
            SetFieldOrProp(report, "shallReportToSquad", true);

            setHeard.Invoke(hearing, new object[] { report, currentTime });
            return true;
        }
        catch (Exception ex)
        {
            ModLogger.Debug($"SAIN SetHeard failed: {ex.Message}");
            return false;
        }
    }

    private static void SetFieldOrProp(object obj, string name, object value)
    {
        var t = obj.GetType();
        var field = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(obj, value);
            return;
        }

        var prop = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(obj, value);
    }

    private static object? ResolveBotComponent(BotOwner botOwner)
    {
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
            var enableType =
                FindType("SAIN.SAINEnableClass")
                ?? FindType("SAIN.Plugin.SAINEnableClass");

            _botComponentType =
                FindType("SAIN.Components.BotComponent")
                ?? FindType("SAIN.SAINComponent.BotComponent");

            var managerType = FindType("SAIN.Components.BotManagerComponent");
            _hearingReportType = FindType("SAIN.Models.Structs.SAINHearingReport");
            _soundTypeEnum = FindType("SAIN.SAINSoundType");
            _placeTypeEnum =
                FindType("SAIN.SAINComponent.Classes.EnemyClasses.EEnemyPlaceType")
                ?? FindEnumType("EEnemyPlaceType");

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
                    ? $"SAIN bridge ready (component={_botComponentType != null}, byOwner={_getSainByBotOwner != null}, byProfile={_getSainByProfileId != null}, hear={_hearingReportType != null})."
                    : "SAIN not found — using vanilla aggro only."
            );
        }
        catch (Exception ex)
        {
            ModLogger.Warning($"SAIN resolve failed: {ex.Message}");
            _sainPresent = false;
        }
    }

    private static Type? FindEnumType(string shortName)
    {
        return AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(a =>
            {
                try
                {
                    return a.GetTypes();
                }
                catch
                {
                    return Array.Empty<Type>();
                }
            })
            .FirstOrDefault(t => t.IsEnum && t.Name == shortName);
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

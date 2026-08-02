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
    private static MethodInfo? _getSain;

    public static void NotifySeen(BotOwner botOwner, IPlayer target)
    {
        if (botOwner == null || target == null)
        {
            return;
        }

        try
        {
            Resolve();
            if (_getSain == null)
            {
                return;
            }

            var profileId = botOwner.ProfileId;
            if (string.IsNullOrEmpty(profileId))
            {
                profileId = botOwner.Profile?.Id;
            }

            if (string.IsNullOrEmpty(profileId))
            {
                return;
            }

            var args = new object?[] { profileId, null };
            if (_getSain.Invoke(null, args) is not true || args[1] == null)
            {
                return;
            }

            var sain = args[1]!;
            var enemyController = sain.GetType().GetProperty("EnemyController")?.GetValue(sain);
            if (enemyController == null)
            {
                return;
            }

            var checkAdd = enemyController.GetType().GetMethod("CheckAddEnemy", new[] { typeof(IPlayer) });
            var enemy = checkAdd?.Invoke(enemyController, new object[] { target });
            if (enemy == null)
            {
                return;
            }

            var knownPlaces = enemy.GetType().GetProperty("KnownPlaces")?.GetValue(enemy);
            if (knownPlaces == null)
            {
                return;
            }

            var update = knownPlaces
                .GetType()
                .GetMethod("UpdateSeenPlace", new[] { typeof(Vector3), typeof(float) });
            update?.Invoke(knownPlaces, new object[] { target.Position, Time.time });
        }
        catch (Exception ex)
        {
            ModLogger.Debug($"SAIN bridge failed: {ex.Message}");
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
            var type =
                Type.GetType("SAIN.Plugin.SAINEnableClass, SAIN", throwOnError: false)
                ?? AppDomain.CurrentDomain
                    .GetAssemblies()
                    .Select(a => a.GetType("SAIN.Plugin.SAINEnableClass", throwOnError: false))
                    .FirstOrDefault(t => t != null);

            _getSain = type
                ?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m =>
                    m.Name == "GetSAIN"
                    && m.GetParameters().Length == 2
                    && m.GetParameters()[0].ParameterType == typeof(string)
                    && m.GetParameters()[1].ParameterType.IsByRef
                );

            ModLogger.Info(_getSain != null ? "SAIN bridge ready." : "SAIN not found — using vanilla aggro only.");
        }
        catch (Exception ex)
        {
            ModLogger.Debug($"SAIN resolve failed: {ex.Message}");
            _getSain = null;
        }
    }
}

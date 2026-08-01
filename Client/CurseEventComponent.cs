using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.Airdrop;
using UnityEngine;

namespace YellowFlareCurse.Client;

public class CurseEventComponent : MonoBehaviour
{
    public static CurseEventComponent? Instance { get; private set; }

    private GameWorld? _gameWorld;
    private bool _eventUsed;
    private bool _eventActive;
    private bool _airdropSpawned;
    private float _airdropAtTime;
    private Vector3 _flarePosition;
    private string _countdownText = string.Empty;

    public void Init(GameWorld gameWorld)
    {
        Instance = this;
        _gameWorld = gameWorld;
        _eventUsed = false;
        _eventActive = false;
        _airdropSpawned = false;
        _countdownText = string.Empty;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void TryStartCurse(Vector3 flarePosition)
    {
        if (!YellowFlareCursePlugin.Enabled.Value)
        {
            return;
        }

        if (_eventUsed)
        {
            if (YellowFlareCursePlugin.Debug.Value)
            {
                YellowFlareCursePlugin.Log.LogInfo("[YellowFlareCurse] Event already used this raid.");
            }

            return;
        }

        if (_gameWorld == null)
        {
            return;
        }

        if (!HasAirdropPoints())
        {
            YellowFlareCursePlugin.Log.LogWarning(
                "[YellowFlareCurse] No AirdropPoints on this map — event not started."
            );
            return;
        }

        _eventUsed = true;
        _eventActive = true;
        _airdropSpawned = false;
        _flarePosition = flarePosition;
        _airdropAtTime = Time.time + YellowFlareCursePlugin.AirdropDelaySeconds.Value;

        var cursed = ApplyCurseSnapshot();
        YellowFlareCursePlugin.Log.LogInfo(
            $"[YellowFlareCurse] Curse started at {flarePosition}. Aggroed {cursed} bot group(s). "
                + $"Airdrop in {YellowFlareCursePlugin.AirdropDelaySeconds.Value:0}s."
        );
    }

    private void Update()
    {
        if (!_eventActive || _airdropSpawned || _gameWorld == null)
        {
            return;
        }

        var remaining = _airdropAtTime - Time.time;
        if (remaining > 0f)
        {
            var minutes = Mathf.FloorToInt(remaining / 60f);
            var seconds = Mathf.FloorToInt(remaining % 60f);
            _countdownText = $"CURSE AIRDROP  {minutes:00}:{seconds:00}";
            return;
        }

        _countdownText = "CURSE AIRDROP  INBOUND";
        SpawnAirdrop();
    }

    private void OnGUI()
    {
        if (!_eventActive || !YellowFlareCursePlugin.ShowCountdown.Value || string.IsNullOrEmpty(_countdownText))
        {
            return;
        }

        var style = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 22,
            fontStyle = FontStyle.Bold,
        };
        style.normal.textColor = new Color(1f, 0.85f, 0.2f, 1f);

        var width = 320f;
        var height = 40f;
        var rect = new Rect((Screen.width - width) * 0.5f, 28f, width, height);
        GUI.Box(rect, _countdownText, style);
    }

    private void SpawnAirdrop()
    {
        if (_airdropSpawned || _gameWorld == null)
        {
            return;
        }

        _airdropSpawned = true;

        try
        {
            _gameWorld.InitAirdrop(
                YellowFlareCursePlugin.CurseContainerId,
                takeNearbyPoint: true,
                position: _flarePosition
            );
            YellowFlareCursePlugin.Log.LogInfo(
                $"[YellowFlareCurse] Airdrop requested near {_flarePosition} (container={YellowFlareCursePlugin.CurseContainerId})."
            );
        }
        catch (System.Exception ex)
        {
            YellowFlareCursePlugin.Log.LogError($"[YellowFlareCurse] Failed to spawn airdrop: {ex}");
        }
    }

    private int ApplyCurseSnapshot()
    {
        if (_gameWorld == null)
        {
            return 0;
        }

        var targets = CollectCurseTargets(_gameWorld);
        if (targets.Count == 0)
        {
            YellowFlareCursePlugin.Log.LogWarning("[YellowFlareCurse] No player/group targets for curse.");
            return 0;
        }

        var cursedGroups = 0;
        var seenGroups = new HashSet<BotsGroup>();

        foreach (var botPlayer in _gameWorld.AllAlivePlayersList)
        {
            if (botPlayer == null || !botPlayer.IsAI || !botPlayer.HealthController.IsAlive)
            {
                continue;
            }

            var botOwner = botPlayer.AIData?.BotOwner;
            if (botOwner == null || botOwner.BotState != EBotState.Active)
            {
                continue;
            }

            if (!IsEligibleRole(botOwner))
            {
                continue;
            }

            var group = botOwner.BotsGroup;
            if (group == null || !seenGroups.Add(group))
            {
                continue;
            }

            foreach (var target in targets)
            {
                try
                {
                    group.AddEnemy(target, EBotEnemyCause.addPlayer);
                }
                catch (System.Exception ex)
                {
                    if (YellowFlareCursePlugin.Debug.Value)
                    {
                        YellowFlareCursePlugin.Log.LogWarning(
                            $"[YellowFlareCurse] AddEnemy failed for {botOwner.Profile.Nickname}: {ex.Message}"
                        );
                    }
                }
            }

            cursedGroups++;
            if (YellowFlareCursePlugin.Debug.Value)
            {
                YellowFlareCursePlugin.Log.LogInfo(
                    $"[YellowFlareCurse] Cursed group of {botOwner.Profile.Nickname} ({botOwner.Profile.Info.Settings.Role})."
                );
            }
        }

        return cursedGroups;
    }

    private static List<IPlayer> CollectCurseTargets(GameWorld gameWorld)
    {
        var targets = new List<IPlayer>();
        var main = gameWorld.MainPlayer;
        if (main == null || !main.HealthController.IsAlive)
        {
            return targets;
        }

        targets.Add(main);

        if (!YellowFlareCursePlugin.CursePlayerGroup.Value)
        {
            return targets;
        }

        var groupId = main.GroupId;
        if (string.IsNullOrEmpty(groupId))
        {
            return targets;
        }

        foreach (var player in gameWorld.AllAlivePlayersList)
        {
            if (player == null || player == main || player.IsAI || !player.HealthController.IsAlive)
            {
                continue;
            }

            if (player.GroupId == groupId)
            {
                targets.Add(player);
            }
        }

        return targets;
    }

    private static bool IsEligibleRole(BotOwner bot)
    {
        var role = bot.Profile.Info.Settings.Role;
        return role is WildSpawnType.assault
            or WildSpawnType.marksman
            or WildSpawnType.cursedAssault
            or WildSpawnType.assaultGroup
            or WildSpawnType.crazyAssaultEvent
            or WildSpawnType.pmcBot
            or WildSpawnType.pmcUSEC
            or WildSpawnType.pmcBEAR;
    }

    private static bool HasAirdropPoints()
    {
        try
        {
            var points = LocationScene.GetAll<AirdropPoint>();
            if (points == null)
            {
                return false;
            }

            var count = 0;
            foreach (var point in points)
            {
                if (point != null && point.gameObject != null)
                {
                    count++;
                }
            }

            return count > 0;
        }
        catch (System.Exception ex)
        {
            YellowFlareCursePlugin.Log.LogWarning($"[YellowFlareCurse] AirdropPoint check failed: {ex.Message}");
            return false;
        }
    }
}

using System.Collections.Generic;
using EFT;
using EFT.Airdrop;
using UnityEngine;

namespace YellowFlareCurse.Client;

public class CurseEventComponent : MonoBehaviour
{
    private const float CurseRefreshInterval = 5f;

    public static CurseEventComponent? Instance { get; private set; }

    private GameWorld? _gameWorld;
    private bool _eventUsed;
    private bool _eventActive;
    private bool _airdropSpawned;
    private bool _overlayVisible;
    private float _airdropAtTime;
    private float _announceUntil;
    private float _overlayHideAt;
    private float _nextCurseRefresh;
    private Vector3 _flarePosition;
    private string _countdownText = string.Empty;
    private string _announceTitle = string.Empty;
    private string _announceSubtitle = string.Empty;

    private GUIStyle? _bannerStyle;
    private GUIStyle? _bannerSubStyle;
    private GUIStyle? _countdownStyle;
    private Texture2D? _bannerBg;
    private Texture2D? _countdownBg;

    public void Init(GameWorld gameWorld)
    {
        Instance = this;
        _gameWorld = gameWorld;
        _eventUsed = false;
        _eventActive = false;
        _airdropSpawned = false;
        _overlayVisible = false;
        _announceUntil = 0f;
        _overlayHideAt = 0f;
        _nextCurseRefresh = 0f;
        _countdownText = string.Empty;
        _announceTitle = string.Empty;
        _announceSubtitle = string.Empty;

        var location = gameWorld.LocationId ?? "?";
        var airdropPoints = CountAirdropPoints();
        ModLogger.Info(
            $"Raid component ready. Location={location}, AirdropPoints={airdropPoints}, "
                + $"Enabled={YellowFlareCursePlugin.Enabled.Value}, "
                + $"Delay={YellowFlareCursePlugin.AirdropDelaySeconds.Value:0}s."
        );
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (_bannerBg != null)
        {
            Destroy(_bannerBg);
            _bannerBg = null;
        }

        if (_countdownBg != null)
        {
            Destroy(_countdownBg);
            _countdownBg = null;
        }
    }

    public void TryStartCurse(Vector3 flarePosition)
    {
        if (!YellowFlareCursePlugin.Enabled.Value)
        {
            ModLogger.Warning("TryStartCurse ignored — mod Enabled=false.");
            return;
        }

        if (_eventUsed)
        {
            ModLogger.Info("Event already used this raid — ignoring second yellow flare.");
            return;
        }

        if (_gameWorld == null)
        {
            ModLogger.Error("TryStartCurse failed — GameWorld is null.");
            return;
        }

        var pointCount = CountAirdropPoints();
        if (pointCount <= 0)
        {
            ModLogger.Warning("No AirdropPoints on this map — event not started.");
            return;
        }

        _eventUsed = true;
        _eventActive = true;
        _airdropSpawned = false;
        _overlayVisible = true;
        _flarePosition = flarePosition;
        var delay = YellowFlareCursePlugin.AirdropDelaySeconds.Value;
        _airdropAtTime = Time.time + delay;
        _announceUntil = Time.time + 8f;
        _overlayHideAt = 0f;
        _nextCurseRefresh = Time.time + CurseRefreshInterval;

        var minutes = Mathf.FloorToInt(delay / 60f);
        var seconds = Mathf.FloorToInt(delay % 60f);
        _announceTitle = "YELLOW FLARE CURSE";
        _announceSubtitle = $"Scavs & PMCs are hunting you  ·  Airdrop in {minutes:00}:{seconds:00}";
        _countdownText = $"AIRDROP  {minutes:00}:{seconds:00}";

        var cursed = ApplyCurseSnapshot(initial: true);
        ModLogger.Info(
            $"CURSE STARTED at {flarePosition}. Aggroed {cursed} bot(s). "
                + $"AirdropPoints={pointCount}. Airdrop in {delay:0}s "
                + $"(container={YellowFlareCursePlugin.CurseContainerId})."
        );
    }

    private void Update()
    {
        if (!_eventActive || _gameWorld == null)
        {
            return;
        }

        if (_overlayVisible && _overlayHideAt > 0f && Time.time >= _overlayHideAt)
        {
            HideOverlay();
        }

        if (Time.time >= _nextCurseRefresh)
        {
            _nextCurseRefresh = Time.time + CurseRefreshInterval;
            ApplyCurseSnapshot(initial: false);
        }

        if (_airdropSpawned)
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
        if (!_overlayVisible || !YellowFlareCursePlugin.ShowCountdown.Value)
        {
            return;
        }

        EnsureStyles();

        const float margin = 18f;
        const float gap = 8f;
        var countdownWidth = 360f;
        var countdownHeight = 42f;
        var bannerWidth = Mathf.Min(420f, Screen.width - margin * 2f);
        var bannerHeight = 72f;

        var showingAnnounce = Time.time < _announceUntil && !string.IsNullOrEmpty(_announceTitle);
        var stackHeight = countdownHeight + (showingAnnounce ? bannerHeight + gap : 0f);
        var stackBottom = Screen.height - margin;
        var stackTop = stackBottom - stackHeight;
        var stackRight = Screen.width - margin;

        if (showingAnnounce)
        {
            var bannerRect = new Rect(stackRight - bannerWidth, stackTop, bannerWidth, bannerHeight);
            GUI.Box(bannerRect, GUIContent.none, _bannerStyle);
            GUI.Label(new Rect(bannerRect.x, bannerRect.y + 6f, bannerRect.width, 32f), _announceTitle, _bannerStyle);
            GUI.Label(
                new Rect(bannerRect.x, bannerRect.y + 38f, bannerRect.width, 28f),
                _announceSubtitle,
                _bannerSubStyle
            );
            stackTop += bannerHeight + gap;
        }

        if (!string.IsNullOrEmpty(_countdownText))
        {
            var rect = new Rect(stackRight - countdownWidth, stackTop, countdownWidth, countdownHeight);
            GUI.Box(rect, _countdownText, _countdownStyle);
        }
    }

    private void EnsureStyles()
    {
        if (_bannerStyle != null && _countdownStyle != null)
        {
            return;
        }

        _bannerBg ??= MakeTex(2, 2, new Color(0.05f, 0.04f, 0.02f, 0.82f));
        _countdownBg ??= MakeTex(2, 2, new Color(0.08f, 0.06f, 0.01f, 0.75f));

        _bannerStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.82f, 0.15f, 1f), background = _bannerBg },
            wordWrap = false,
        };

        _bannerSubStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 16,
            fontStyle = FontStyle.Normal,
            normal = { textColor = new Color(1f, 0.92f, 0.65f, 1f) },
            wordWrap = true,
        };

        _countdownStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.85f, 0.2f, 1f), background = _countdownBg },
        };
    }

    private static Texture2D MakeTex(int width, int height, Color color)
    {
        var pixels = new Color[width * height];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color;
        }

        var tex = new Texture2D(width, height);
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private void SpawnAirdrop()
    {
        if (_airdropSpawned || _gameWorld == null)
        {
            return;
        }

        _airdropSpawned = true;
        _announceTitle = "CURSE AIRDROP";
        _announceSubtitle = "High-value crate inbound near the flare";
        _announceUntil = Time.time + 5f;
        _overlayHideAt = Time.time + 5f;
        _countdownText = "CURSE AIRDROP  INBOUND";

        try
        {
            ModLogger.Info(
                $"Requesting InitAirdrop near {_flarePosition} "
                    + $"(container={YellowFlareCursePlugin.CurseContainerId}, takeNearbyPoint=true)."
            );
            _gameWorld.InitAirdrop(
                YellowFlareCursePlugin.CurseContainerId,
                takeNearbyPoint: true,
                position: _flarePosition
            );
            ModLogger.Info("InitAirdrop call completed — overlay will hide in 5s.");
        }
        catch (System.Exception ex)
        {
            ModLogger.Error($"Failed to spawn airdrop: {ex}");
            HideOverlay();
        }
    }

    private void HideOverlay()
    {
        _overlayVisible = false;
        _overlayHideAt = 0f;
        _announceUntil = 0f;
        _countdownText = string.Empty;
        _announceTitle = string.Empty;
        _announceSubtitle = string.Empty;
        ModLogger.Debug("Overlay hidden — event UI finished.");
    }

    private int ApplyCurseSnapshot(bool initial)
    {
        if (_gameWorld == null)
        {
            return 0;
        }

        var targets = CollectCurseTargets(_gameWorld);
        if (targets.Count == 0)
        {
            if (initial)
            {
                ModLogger.Warning("No player/group targets for curse.");
            }

            return 0;
        }

        var cursedBots = 0;
        var scannedBots = 0;
        var skippedRole = 0;

        foreach (var botPlayer in _gameWorld.AllAlivePlayersList)
        {
            if (botPlayer == null || !botPlayer.IsAI || !botPlayer.HealthController.IsAlive)
            {
                continue;
            }

            scannedBots++;

            var botOwner = botPlayer.AIData?.BotOwner;
            if (botOwner == null || botOwner.BotState != EBotState.Active)
            {
                continue;
            }

            if (!IsEligibleRole(botOwner))
            {
                skippedRole++;
                continue;
            }

            var group = botOwner.BotsGroup;
            if (group == null)
            {
                continue;
            }

            foreach (var target in targets)
            {
                try
                {
                    // Hostility mark (not enough alone — especially with SAIN).
                    group.AddEnemy(target, EBotEnemyCause.addPlayer);

                    // Give bots a last-known position so they path / hunt.
                    group.ReportAboutEnemy(target, EEnemyPartVisibleType.Visible, botOwner);
                    group.CalcGoalForBot(botOwner);

                    // SAIN overrides ShallKnowEnemy — force a known place.
                    SainCurseBridge.NotifySeen(botOwner, target);
                }
                catch (System.Exception ex)
                {
                    ModLogger.Debug($"Curse apply failed for {botOwner.Profile.Nickname}: {ex.Message}");
                }
            }

            cursedBots++;
            if (initial)
            {
                ModLogger.Debug(
                    $"Cursed bot {botOwner.Profile.Nickname} ({botOwner.Profile.Info.Settings.Role})."
                );
            }
        }

        if (initial || YellowFlareCursePlugin.Debug.Value)
        {
            ModLogger.Info(
                $"Curse snapshot{(initial ? "" : " refresh")}: AliveAI={scannedBots}, "
                    + $"skippedRole={skippedRole}, cursedBots={cursedBots}, targets={targets.Count}."
            );
        }

        return cursedBots;
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

    private static int CountAirdropPoints()
    {
        try
        {
            var points = LocationScene.GetAll<AirdropPoint>();
            if (points == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var point in points)
            {
                if (point != null && point.gameObject != null)
                {
                    count++;
                }
            }

            return count;
        }
        catch (System.Exception ex)
        {
            ModLogger.Warning($"AirdropPoint check failed: {ex.Message}");
            return 0;
        }
    }
}

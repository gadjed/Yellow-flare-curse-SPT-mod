using System.Collections;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.Airdrop;
using UnityEngine;
using UnityEngine.AI;

namespace YellowFlareCurse.Client;

public class CurseEventComponent : MonoBehaviour
{
    private const float CurseRefreshInterval = 5f;
    private const float NavMeshSampleRadius = 8f;
    private const float GoldenAngleDegrees = 137.5f;
    private const float TagillaWaitTimeoutSeconds = 60f;
    private const int RingSearchAttempts = 12;
    /// <summary>Reject NavMesh hits closer than this fraction of the configured min radius.</summary>
    private const float MinAcceptedDistanceFactor = 0.85f;
    private static readonly float[] RingSampleRadii = { 12f, 25f, 40f };

    public static CurseEventComponent? Instance { get; private set; }

    /// <summary>True while curse is active and AI alliance mode is enabled.</summary>
    public static bool AllianceActive { get; private set; }

    private GameWorld? _gameWorld;
    private bool _eventUsed;
    private bool _eventActive;
    private bool _airdropSpawned;
    private bool _hasAirdropSupport;
    private bool _overlayVisible;
    private bool _teleportStarted;
    private bool _tagillaSpawnRequested;
    private bool _tagillaPlaced;
    private bool _tagillaAggroed;
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
        AllianceActive = false;
        _airdropSpawned = false;
        _hasAirdropSupport = false;
        _overlayVisible = false;
        _teleportStarted = false;
        _tagillaSpawnRequested = false;
        _tagillaPlaced = false;
        _tagillaAggroed = false;
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
                + $"Delay={YellowFlareCursePlugin.AirdropDelaySeconds.Value:0}s, "
                + $"SpawnTagilla={YellowFlareCursePlugin.SpawnTagilla.Value}, "
                + $"Authority={FikaHost.IsAuthority()}."
        );
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        AllianceActive = false;

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
        _hasAirdropSupport = pointCount > 0;
        if (!_hasAirdropSupport)
        {
            ModLogger.Warning(
                "No AirdropPoints on this map — curse will run without airdrop (Tagilla/hunt still active)."
            );
        }

        _eventUsed = true;
        _eventActive = true;
        AllianceActive = YellowFlareCursePlugin.AiAlliance.Value;
        _airdropSpawned = !_hasAirdropSupport;
        _overlayVisible = true;
        _teleportStarted = false;
        _tagillaSpawnRequested = false;
        _tagillaPlaced = false;
        _tagillaAggroed = false;
        _flarePosition = flarePosition;
        var delay = YellowFlareCursePlugin.AirdropDelaySeconds.Value;
        _airdropAtTime = Time.time + delay;
        _announceUntil = Time.time + 8f;
        _overlayHideAt = _hasAirdropSupport ? 0f : Time.time + 8f;
        _nextCurseRefresh = Time.time + CurseRefreshInterval;

        var minutes = Mathf.FloorToInt(delay / 60f);
        var seconds = Mathf.FloorToInt(delay % 60f);
        var tagillaHint = YellowFlareCursePlugin.SpawnTagilla.Value
            ? (YellowFlareCursePlugin.TagillaType.Value == TagillaVariant.Labyrinth
                ? " · Labyrinth Tagilla inbound"
                : " · Tagilla inbound")
            : string.Empty;
        _announceTitle = "YELLOW FLARE CURSE";
        if (_hasAirdropSupport)
        {
            _announceSubtitle =
                $"Scavs & PMCs are hunting you{tagillaHint}  ·  Airdrop in {minutes:00}:{seconds:00}";
            _countdownText = $"AIRDROP  {minutes:00}:{seconds:00}";
        }
        else
        {
            _announceSubtitle = $"Scavs & PMCs are hunting you{tagillaHint}  ·  No airdrop on this map";
            _countdownText = YellowFlareCursePlugin.SpawnTagilla.Value ? "TAGILLA  INBOUND" : "CURSE  ACTIVE";
        }

        if (YellowFlareCursePlugin.SpawnTagilla.Value && FikaHost.IsAuthority())
        {
            // Prefer player feet over flare sky position for boss PerfectPos.
            var spawnNear = _gameWorld.MainPlayer is { HealthController.IsAlive: true } main
                ? main.Position
                : flarePosition;
            TrySpawnTagilla(spawnNear);
        }
        else if (YellowFlareCursePlugin.SpawnTagilla.Value)
        {
            ModLogger.Info("Tagilla spawn skipped — not Fika host/authority.");
        }

        if (
            YellowFlareCursePlugin.TeleportBotsNearPlayer.Value
            && FikaHost.IsAuthority()
            && !_teleportStarted
        )
        {
            _teleportStarted = true;
            StartCoroutine(TeleportThenCurseRoutine());
        }
        else
        {
            if (YellowFlareCursePlugin.TeleportBotsNearPlayer.Value && !FikaHost.IsAuthority())
            {
                ModLogger.Info("Teleport skipped — not Fika host/authority.");
            }

            FinishCurseApply(initial: true);
        }

        ModLogger.Info(
            $"CURSE STARTED at {flarePosition}. AirdropPoints={pointCount}, "
                + $"Airdrop={(_hasAirdropSupport ? $"in {delay:0}s" : "skipped")}. "
                + $"Tagilla={YellowFlareCursePlugin.SpawnTagilla.Value}, "
                + $"Teleport={YellowFlareCursePlugin.TeleportBotsNearPlayer.Value}, "
                + $"Alliance={AllianceActive}, Authority={FikaHost.IsAuthority()}."
        );
    }

    private IEnumerator TeleportThenCurseRoutine()
    {
        var teleported = 0;
        var failed = 0;

        if (_gameWorld == null)
        {
            FinishCurseApply(initial: true);
            yield break;
        }

        var main = _gameWorld.MainPlayer;
        if (main == null || !main.HealthController.IsAlive)
        {
            ModLogger.Warning("Teleport aborted — no alive MainPlayer.");
            FinishCurseApply(initial: true);
            yield break;
        }

        var center = main.Position;
        var minR = Mathf.Min(
            YellowFlareCursePlugin.TeleportMinRadius.Value,
            YellowFlareCursePlugin.TeleportMaxRadius.Value
        );
        var maxR = Mathf.Max(
            YellowFlareCursePlugin.TeleportMinRadius.Value,
            YellowFlareCursePlugin.TeleportMaxRadius.Value
        );

        var bots = CollectEligibleBotPlayers(_gameWorld);
        ModLogger.Info(
            $"Teleporting {bots.Count} eligible AI near player "
                + $"(ring {minR:0}-{maxR:0}m, center={center})."
        );

        for (var i = 0; i < bots.Count; i++)
        {
            var botPlayer = bots[i];
            if (botPlayer == null || !botPlayer.HealthController.IsAlive)
            {
                continue;
            }

            var preferredAngle = (i * GoldenAngleDegrees) * Mathf.Deg2Rad;
            var preferredT = bots.Count <= 1 ? 0.5f : (i % 5) / 4f;
            var preferredRadius = Mathf.Lerp(minR, maxR, preferredT);

            if (!TryFindRingNavMesh(center, minR, maxR, preferredAngle, preferredRadius, out var navPos, out var actualR))
            {
                failed++;
                ModLogger.Debug(
                    $"No valid ring NavMesh for {botPlayer.Profile?.Nickname} "
                        + $"(wanted ≈{preferredRadius:0.0}m @ {preferredAngle * Mathf.Rad2Deg:0}°)."
                );
                continue;
            }

            try
            {
                botPlayer.Teleport(navPos, onServerToo: true);
                var mover = botPlayer.AIData?.BotOwner?.Mover;
                mover?.Teleport(navPos);
                teleported++;
                ModLogger.Debug(
                    $"Teleported {botPlayer.Profile?.Nickname} → {navPos} (actual r={actualR:0.0}m)."
                );
            }
            catch (System.Exception ex)
            {
                failed++;
                ModLogger.Debug($"Teleport failed for {botPlayer.Profile?.Nickname}: {ex.Message}");
            }

            if ((i + 1) % 3 == 0)
            {
                yield return null;
            }
        }

        ModLogger.Info($"Teleport finished: ok={teleported}, failed={failed}.");
        // Let physics/NavMesh + SAIN settle after the mass teleport, then apply aggro hard.
        yield return new WaitForSeconds(0.75f);
        FinishCurseApply(initial: true);
    }

    private void FinishCurseApply(bool initial)
    {
        if (AllianceActive)
        {
            ApplyAiAlliance();
        }

        var cursed = ApplyCurseSnapshot(initial);
        if (initial)
        {
            ModLogger.Info($"Initial curse apply finished — aggroed {cursed} bot(s).");
        }
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
            if (AllianceActive)
            {
                ApplyAiAlliance();
            }

            ApplyCurseSnapshot(initial: false);
            TryAggroPendingTagilla();
        }

        if (_airdropSpawned)
        {
            return;
        }

        if (!_hasAirdropSupport)
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

    private int ApplyAiAlliance()
    {
        if (_gameWorld == null)
        {
            return 0;
        }

        var bots = CollectEligibleBotPlayers(_gameWorld);
        if (bots.Count < 2)
        {
            return 0;
        }

        var allyLinks = 0;
        var clearedEnemies = 0;

        for (var i = 0; i < bots.Count; i++)
        {
            var botA = bots[i];
            var group = botA.AIData?.BotOwner?.BotsGroup;
            if (group == null)
            {
                continue;
            }

            for (var j = 0; j < bots.Count; j++)
            {
                if (i == j)
                {
                    continue;
                }

                var botB = bots[j];
                try
                {
                    if (group.IsEnemy(botB))
                    {
                        group.RemoveEnemy(botB);
                        clearedEnemies++;
                    }

                    if (!group.Allies.Contains(botB))
                    {
                        group.AddAlly(botB);
                        allyLinks++;
                    }
                }
                catch (System.Exception ex)
                {
                    ModLogger.Debug(
                        $"Alliance {botA.Profile?.Nickname}↔{botB.Profile?.Nickname} failed: {ex.Message}"
                    );
                }
            }
        }

        if (YellowFlareCursePlugin.Debug.Value)
        {
            ModLogger.Info(
                $"AI alliance refresh: bots={bots.Count}, newAllies={allyLinks}, clearedEnemies={clearedEnemies}."
            );
        }

        return allyLinks;
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
        var sainHits = 0;
        var qbStops = 0;

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

            if (!IsEligibleRole(botOwner) && !IsTagilla(botOwner))
            {
                skippedRole++;
                continue;
            }

            var group = botOwner.BotsGroup;
            if (group == null)
            {
                continue;
            }

            if (QuestingBotsCurseBridge.StopQuesting(botOwner))
            {
                qbStops++;
            }

            foreach (var target in targets)
            {
                try
                {
                    group.AddEnemy(target, EBotEnemyCause.addPlayer);
                    group.ReportAboutEnemy(target, EEnemyPartVisibleType.Visible, botOwner);
                    group.CalcGoalForBot(botOwner);

                    try
                    {
                        botOwner.Steering?.LookToPoint(target.Position);
                    }
                    catch
                    {
                        // Steering can throw mid-teleport.
                    }

                    if (SainCurseBridge.NotifySeen(botOwner, target))
                    {
                        sainHits++;
                    }
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
                    + $"skippedRole={skippedRole}, cursedBots={cursedBots}, targets={targets.Count}, "
                    + $"sainKnown={sainHits}, qbStop={qbStops}."
            );
        }

        return cursedBots;
    }

    private static List<Player> CollectEligibleBotPlayers(GameWorld gameWorld)
    {
        var list = new List<Player>();
        foreach (var botPlayer in gameWorld.AllAlivePlayersList)
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

            list.Add(botPlayer);
        }

        return list;
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

    /// <summary>
    /// Finds a NavMesh point in the configured ring around <paramref name="center"/>.
    /// Never falls back to the center itself — that previously dumped bots on top of the player
    /// whenever the preferred ring sample missed NavMesh (common outdoors / multi-level maps).
    /// </summary>
    private static bool TryFindRingNavMesh(
        Vector3 center,
        float minR,
        float maxR,
        float preferredAngle,
        float preferredRadius,
        out Vector3 position,
        out float actualRadius
    )
    {
        var minAccepted = minR * MinAcceptedDistanceFactor;

        for (var attempt = 0; attempt < RingSearchAttempts; attempt++)
        {
            var angle = preferredAngle + attempt * (Mathf.PI * 2f / RingSearchAttempts);
            var radius = attempt == 0
                ? preferredRadius
                : Mathf.Lerp(minR, maxR, (attempt % 5) / 4f);
            var candidate = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

            if (!TrySampleNavMeshAwayFrom(center, candidate, minAccepted, out position, out actualRadius))
            {
                continue;
            }

            return true;
        }

        position = default;
        actualRadius = 0f;
        return false;
    }

    private static bool TrySampleNavMeshAwayFrom(
        Vector3 center,
        Vector3 around,
        float minHorizontalDistance,
        out Vector3 position,
        out float actualRadius
    )
    {
        foreach (var sampleR in RingSampleRadii)
        {
            if (!NavMesh.SamplePosition(around, out var hit, sampleR, NavMesh.AllAreas))
            {
                continue;
            }

            actualRadius = HorizontalDistance(center, hit.position);
            if (actualRadius < minHorizontalDistance)
            {
                continue;
            }

            position = hit.position;
            return true;
        }

        // Last chance: tiny sample (legacy radius) still must stay outside the ring.
        if (NavMesh.SamplePosition(around, out var tightHit, NavMeshSampleRadius, NavMesh.AllAreas))
        {
            actualRadius = HorizontalDistance(center, tightHit.position);
            if (actualRadius >= minHorizontalDistance)
            {
                position = tightHit.position;
                return true;
            }
        }

        position = default;
        actualRadius = 0f;
        return false;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
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

    private static bool IsTagilla(BotOwner bot)
    {
        var role = bot.Profile.Info.Settings.Role;
        return role is WildSpawnType.bossTagilla
            or WildSpawnType.bossTagillaAgro
            or WildSpawnType.followerTagilla
            or WildSpawnType.tagillaHelperAgro;
    }

    private static (WildSpawnType Role, string BossName, string EscortType) GetSelectedTagillaSpawn()
    {
        return YellowFlareCursePlugin.TagillaType.Value == TagillaVariant.Labyrinth
            ? (WildSpawnType.bossTagillaAgro, "bossTagillaAgro", "tagillaHelperAgro")
            : (WildSpawnType.bossTagilla, "bossTagilla", "followerTagilla");
    }

    private void TrySpawnTagilla(Vector3 near)
    {
        if (_tagillaSpawnRequested)
        {
            return;
        }

        _tagillaSpawnRequested = true;
        var (role, bossName, escortType) = GetSelectedTagillaSpawn();

        try
        {
            var botGame = Singleton<IBotGame>.Instance;
            var botsController = botGame?.BotsController;
            if (botsController == null)
            {
                ModLogger.Error("Tagilla spawn failed — BotsController is null.");
                return;
            }

            var wave = new BossLocationSpawn
            {
                BossName = bossName,
                BossChance = 100f,
                BossZone = string.Empty,
                BossPlayer = false,
                BossDifficult = "hard",
                BossEscortDifficult = "normal",
                BossEscortType = escortType,
                BossEscortAmount = "0",
                Time = -1f,
                Delay = 0f,
                TriggerId = string.Empty,
                TriggerName = string.Empty,
                IgnoreMaxBots = true,
                ForceSpawn = true,
                PerfectPos = near,
                Supports = null,
            };
            wave.Init();
            wave.PerfectPos = near;
            wave.ShallSpawn = true;
            wave.ForceSpawn = true;
            wave.IgnoreMaxBots = true;

            botsController.ActivateBotsByWave(wave);
            ModLogger.Info(
                $"Requested Tagilla boss wave ({YellowFlareCursePlugin.TagillaType.Value}/{bossName}/{role}) near {near}."
            );
            StartCoroutine(PlaceTagillaWhenReady(near));
        }
        catch (System.Exception ex)
        {
            ModLogger.Error($"Tagilla BossLocationSpawn failed, trying BotWave fallback: {ex}");
            TrySpawnTagillaWaveFallback();
        }
    }

    private void TrySpawnTagillaWaveFallback()
    {
        try
        {
            var botGame = Singleton<IBotGame>.Instance;
            var spawner = botGame?.BotsController?.BotSpawner;
            if (spawner == null)
            {
                ModLogger.Error("Tagilla fallback failed — BotSpawner is null.");
                return;
            }

            var (role, _, _) = GetSelectedTagillaSpawn();
            var wave = new BotWaveDataClass
            {
                BotsCount = 1,
                Side = EPlayerSide.Savage,
                SpawnAreaName = string.Empty,
                Time = 0f,
                WildSpawnType = role,
                IsPlayers = false,
                Difficulty = BotDifficulty.hard,
                ChanceGroup = 100f,
                WithCheckMinMax = false,
            };

            _ = spawner.ActivateBotsByWave(wave);
            ModLogger.Info(
                $"Requested Tagilla via BotWaveDataClass fallback ({YellowFlareCursePlugin.TagillaType.Value}/{role})."
            );
            StartCoroutine(PlaceTagillaWhenReady(_flarePosition));
        }
        catch (System.Exception ex)
        {
            ModLogger.Error($"Tagilla BotWave fallback failed: {ex}");
        }
    }

    private IEnumerator PlaceTagillaWhenReady(Vector3 near)
    {
        var deadline = Time.time + TagillaWaitTimeoutSeconds;
        Player? tagilla = null;

        while (Time.time < deadline)
        {
            tagilla = FindAliveTagillaPlayer();
            if (tagilla == null || !tagilla.HealthController.IsAlive)
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            if (!_tagillaPlaced)
            {
                TeleportTagillaToRing(tagilla, near);
                _tagillaPlaced = true;
                // Give BotOwner a moment to settle after teleport before checking Active.
                yield return new WaitForSeconds(0.5f);
            }

            if (TryAggroTagilla(tagilla))
            {
                yield break;
            }

            yield return new WaitForSeconds(0.5f);
        }

        if (tagilla != null && tagilla.HealthController.IsAlive)
        {
            // Last resort: force aggro even if BotState never reached Active.
            var botOwner = tagilla.AIData?.BotOwner;
            if (botOwner != null)
            {
                AggroBotOnCurseTargets(botOwner);
                _tagillaAggroed = true;
                ModLogger.Warning(
                    $"Tagilla aggro forced without Active state "
                        + $"(state={botOwner.BotState}, nick={botOwner.Profile?.Nickname})."
                );
                yield break;
            }
        }

        ModLogger.Warning(
            $"Tagilla did not appear/activate within {TagillaWaitTimeoutSeconds:0}s after spawn request."
        );
    }

    private void TryAggroPendingTagilla()
    {
        if (_tagillaAggroed || !_tagillaSpawnRequested || _gameWorld == null)
        {
            return;
        }

        var tagilla = FindAliveTagillaPlayer();
        if (tagilla == null || !tagilla.HealthController.IsAlive)
        {
            return;
        }

        if (!_tagillaPlaced)
        {
            var center = _gameWorld.MainPlayer is { HealthController.IsAlive: true } main
                ? main.Position
                : _flarePosition;
            TeleportTagillaToRing(tagilla, center);
            _tagillaPlaced = true;
        }

        TryAggroTagilla(tagilla);
    }

    private bool TryAggroTagilla(Player tagilla)
    {
        var botOwner = tagilla.AIData?.BotOwner;
        if (botOwner == null)
        {
            return false;
        }

        if (botOwner.BotState != EBotState.Active)
        {
            ModLogger.Debug(
                $"Tagilla found but BotOwner not Active yet (state={botOwner.BotState}) — waiting."
            );
            return false;
        }

        AggroBotOnCurseTargets(botOwner);
        _tagillaAggroed = true;
        ModLogger.Info(
            $"Tagilla cursed/aggroed ({botOwner.Profile?.Nickname}, role={botOwner.Profile?.Info?.Settings?.Role})."
        );
        return true;
    }

    private void TeleportTagillaToRing(Player tagilla, Vector3 near)
    {
        var minR = Mathf.Min(
            YellowFlareCursePlugin.TagillaSpawnMinRadius.Value,
            YellowFlareCursePlugin.TagillaSpawnMaxRadius.Value
        );
        var maxR = Mathf.Max(
            YellowFlareCursePlugin.TagillaSpawnMinRadius.Value,
            YellowFlareCursePlugin.TagillaSpawnMaxRadius.Value
        );

        // Prefer the live player position — flare PerfectPos is often sky-high and useless for NavMesh.
        var center = _gameWorld?.MainPlayer is { HealthController.IsAlive: true } main
            ? main.Position
            : near;
        var preferredRadius = UnityEngine.Random.Range(minR, maxR);
        var preferredAngle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

        if (!TryFindRingNavMesh(center, minR, maxR, preferredAngle, preferredRadius, out var navPos, out var actualR))
        {
            ModLogger.Warning(
                $"Could not find ring NavMesh for Tagilla (wanted {minR:0}-{maxR:0}m around {center}); leaving spawn position."
            );
            return;
        }

        try
        {
            tagilla.Teleport(navPos, onServerToo: true);
            tagilla.AIData?.BotOwner?.Mover?.Teleport(navPos);
            ModLogger.Info($"Tagilla teleported to {navPos} (actual r={actualR:0}m from player).");
        }
        catch (System.Exception ex)
        {
            ModLogger.Warning($"Tagilla teleport failed: {ex.Message}");
        }
    }

    private void AggroBotOnCurseTargets(BotOwner botOwner)
    {
        if (_gameWorld == null)
        {
            return;
        }

        var targets = CollectCurseTargets(_gameWorld);
        var group = botOwner.BotsGroup;
        if (group == null || targets.Count == 0)
        {
            ModLogger.Warning(
                $"Tagilla aggro skipped — group={(group == null ? "null" : "ok")}, targets={targets.Count}."
            );
            return;
        }

        QuestingBotsCurseBridge.StopQuesting(botOwner);

        foreach (var target in targets)
        {
            try
            {
                group.AddEnemy(target, EBotEnemyCause.addPlayer);
                group.ReportAboutEnemy(target, EEnemyPartVisibleType.Visible, botOwner);
                group.CalcGoalForBot(botOwner);
                try
                {
                    botOwner.Steering?.LookToPoint(target.Position);
                }
                catch
                {
                    // ignore
                }

                SainCurseBridge.NotifySeen(botOwner, target);
                ModLogger.Debug(
                    $"Tagilla AddEnemy+Report → {target.Profile?.Nickname ?? target.ProfileId}."
                );
            }
            catch (System.Exception ex)
            {
                ModLogger.Warning($"Tagilla aggro failed: {ex.Message}");
            }
        }
    }

    private Player? FindAliveTagillaPlayer()
    {
        if (_gameWorld == null)
        {
            return null;
        }

        foreach (var botPlayer in _gameWorld.AllAlivePlayersList)
        {
            if (botPlayer == null || !botPlayer.IsAI || !botPlayer.HealthController.IsAlive)
            {
                continue;
            }

            var botOwner = botPlayer.AIData?.BotOwner;
            if (botOwner == null)
            {
                continue;
            }

            if (IsTagilla(botOwner))
            {
                return botPlayer;
            }
        }

        return null;
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

using BepInEx;
using BepInEx.Configuration;
using YellowFlareCurse.Client.Patches;

namespace YellowFlareCurse.Client;

public enum TagillaVariant
{
    /// <summary>Factory Tagilla (<c>bossTagilla</c>).</summary>
    Factory = 0,

    /// <summary>Labyrinth / Shadow of Tagilla (<c>bossTagillaAgro</c>).</summary>
    Labyrinth = 1,
}

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public class YellowFlareCursePlugin : BaseUnityPlugin
{
    public const string PluginGuid = "gadjed.yellowflarecurse";
    public const string PluginName = "Yellow Flare Curse";
    public const string PluginVersion = "1.4.5";

    /// <summary>
    /// Ammo template fired by RSP-30 Yellow (HandleFlareSuccessEvent receives ammo, not the handheld weapon id).
    /// Weapon/item id is 624c0b3340357b5f566e8766; ammo is patron_rsp_yellow.
    /// </summary>
    public const string YellowFlareTemplateId = "624c09e49b98e019a3315b66";

    /// <summary>Handheld RSP-30 Yellow item id (for docs / logs only).</summary>
    public const string YellowFlareWeaponId = "624c0b3340357b5f566e8766";

    /// <summary>Must match server config CurseContainerId.</summary>
    public const string CurseContainerId = "674a0fc0000000000000c001";

    public static ConfigEntry<bool> Enabled { get; private set; } = null!;
    public static ConfigEntry<bool> Debug { get; private set; } = null!;
    public static ConfigEntry<float> AirdropDelaySeconds { get; private set; } = null!;
    public static ConfigEntry<bool> ShowCountdown { get; private set; } = null!;
    public static ConfigEntry<bool> CursePlayerGroup { get; private set; } = null!;
    public static ConfigEntry<bool> TeleportBotsNearPlayer { get; private set; } = null!;
    public static ConfigEntry<float> TeleportMinRadius { get; private set; } = null!;
    public static ConfigEntry<float> TeleportMaxRadius { get; private set; } = null!;
    public static ConfigEntry<bool> AiAlliance { get; private set; } = null!;
    public static ConfigEntry<bool> SpawnTagilla { get; private set; } = null!;
    public static ConfigEntry<TagillaVariant> TagillaType { get; private set; } = null!;
    public static ConfigEntry<float> TagillaSpawnMinRadius { get; private set; } = null!;
    public static ConfigEntry<float> TagillaSpawnMaxRadius { get; private set; } = null!;
    public static ConfigEntry<bool> SpawnCultists { get; private set; } = null!;
    public static ConfigEntry<int> CultistEscortCount { get; private set; } = null!;

    private void Awake()
    {
        ModLogger.Init(Logger);

        Enabled = Config.Bind("1. General", "Enabled", true, "Enable the yellow flare curse event.");
        Debug = Config.Bind(
            "1. General",
            "Debug",
            false,
            "Verbose logging. Logs go to BepInEx console, Unity console, server console, and BepInEx/plugins/YellowFlareCurse/logs/."
        );
        AirdropDelaySeconds = Config.Bind(
            "1. General",
            "AirdropDelaySeconds",
            600f,
            new ConfigDescription(
                "Seconds after a successful yellow flare before the airdrop spawns.",
                new AcceptableValueRange<float>(30f, 1800f)
            )
        );
        ShowCountdown = Config.Bind(
            "2. UI",
            "ShowCountdown",
            true,
            "Show event-start banner and on-screen countdown until the airdrop."
        );
        CursePlayerGroup = Config.Bind(
            "3. Curse",
            "IncludePlayerGroup",
            true,
            "Cursed scavs / Tagilla / cultists also aggro your teammates (same GroupId)."
        );
        TeleportBotsNearPlayer = Config.Bind(
            "3. Curse",
            "TeleportBotsNearPlayer",
            true,
            "On curse start (host/authority only), teleport eligible scav AI (not PMCs) into a ring near the player."
        );
        TeleportMinRadius = Config.Bind(
            "3. Curse",
            "TeleportMinRadius",
            100f,
            new ConfigDescription("Minimum teleport ring radius (meters).", new AcceptableValueRange<float>(5f, 200f))
        );
        TeleportMaxRadius = Config.Bind(
            "3. Curse",
            "TeleportMaxRadius",
            150f,
            new ConfigDescription("Maximum teleport ring radius (meters).", new AcceptableValueRange<float>(10f, 200f))
        );
        AiAlliance = Config.Bind(
            "3. Curse",
            "AiAlliance",
            true,
            "During the curse, make eligible scav AI allied with each other so they only hunt players."
        );
        SpawnTagilla = Config.Bind(
            "3. Curse",
            "SpawnTagilla",
            true,
            "On curse start (host/authority), spawn Tagilla and pull him near the player."
        );
        TagillaType = Config.Bind(
            "3. Curse",
            "TagillaType",
            TagillaVariant.Factory,
            "Which Tagilla to spawn: Factory (bossTagilla) or Labyrinth / Shadow of Tagilla (bossTagillaAgro)."
        );
        TagillaSpawnMinRadius = Config.Bind(
            "3. Curse",
            "TagillaSpawnMinRadius",
            60f,
            new ConfigDescription(
                "Minimum NavMesh ring radius (meters) when placing Tagilla near the player.",
                new AcceptableValueRange<float>(10f, 150f)
            )
        );
        TagillaSpawnMaxRadius = Config.Bind(
            "3. Curse",
            "TagillaSpawnMaxRadius",
            75f,
            new ConfigDescription(
                "Maximum NavMesh ring radius (meters) when placing Tagilla near the player.",
                new AcceptableValueRange<float>(10f, 150f)
            )
        );
        SpawnCultists = Config.Bind(
            "3. Curse",
            "SpawnCultists",
            false,
            "On curse start (host/authority), also spawn a cultist squad (priest + warriors) near the player."
        );
        CultistEscortCount = Config.Bind(
            "3. Curse",
            "CultistEscortCount",
            4,
            new ConfigDescription(
                "Number of sectantWarrior escorts spawned with the cultist priest.",
                new AcceptableValueRange<int>(1, 8)
            )
        );

        new GameWorldPatch().Enable();
        new FlareSuccessPatch().Enable();
        new CurseAddEnemyPatch().Enable();

        ModLogger.Info(
            $"{PluginName} v{PluginVersion} loaded. YellowTpl={YellowFlareTemplateId}, "
                + $"Container={CurseContainerId}, Enabled={Enabled.Value}."
        );
    }
}

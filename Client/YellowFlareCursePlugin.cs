using BepInEx;
using BepInEx.Configuration;
using YellowFlareCurse.Client.Patches;

namespace YellowFlareCurse.Client;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public class YellowFlareCursePlugin : BaseUnityPlugin
{
    public const string PluginGuid = "gadjed.yellowflarecurse";
    public const string PluginName = "Yellow Flare Curse";
    public const string PluginVersion = "1.2.1";

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
            "Existing scav/PMC bots also aggro your teammates (same GroupId)."
        );

        new GameWorldPatch().Enable();
        new FlareSuccessPatch().Enable();

        ModLogger.Info(
            $"{PluginName} v{PluginVersion} loaded. YellowTpl={YellowFlareTemplateId}, "
                + $"Container={CurseContainerId}, Enabled={Enabled.Value}."
        );
    }
}

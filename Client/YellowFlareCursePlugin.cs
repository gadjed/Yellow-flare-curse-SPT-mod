using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using YellowFlareCurse.Client.Patches;

namespace YellowFlareCurse.Client;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public class YellowFlareCursePlugin : BaseUnityPlugin
{
    public const string PluginGuid = "gadjed.yellowflarecurse";
    public const string PluginName = "Yellow Flare Curse";
    public const string PluginVersion = "1.1.0";

    /// <summary>RSP-30 reactive signal cartridge (Yellow).</summary>
    public const string YellowFlareTemplateId = "624c0b3340357b5f566e8766";

    /// <summary>Must match server config CurseContainerId.</summary>
    public const string CurseContainerId = "674a0fc0000000000000c001";

    internal static ManualLogSource Log { get; private set; } = null!;

    public static ConfigEntry<bool> Enabled { get; private set; } = null!;
    public static ConfigEntry<bool> Debug { get; private set; } = null!;
    public static ConfigEntry<float> AirdropDelaySeconds { get; private set; } = null!;
    public static ConfigEntry<bool> ShowCountdown { get; private set; } = null!;
    public static ConfigEntry<bool> CursePlayerGroup { get; private set; } = null!;

    private void Awake()
    {
        Log = Logger;

        Enabled = Config.Bind("1. General", "Enabled", true, "Enable the yellow flare curse event.");
        Debug = Config.Bind("1. General", "Debug", false, "Verbose logging.");
        AirdropDelaySeconds = Config.Bind(
            "1. General",
            "AirdropDelaySeconds",
            600f,
            new ConfigDescription(
                "Seconds after a successful yellow flare before the airdrop spawns.",
                new AcceptableValueRange<float>(30f, 1800f)
            )
        );
        ShowCountdown = Config.Bind("2. UI", "ShowCountdown", true, "Show an on-screen countdown until the airdrop.");
        CursePlayerGroup = Config.Bind(
            "3. Curse",
            "IncludePlayerGroup",
            true,
            "Existing scav/PMC bots also aggro your teammates (same GroupId)."
        );

        new GameWorldPatch().Enable();
        new FlareSuccessPatch().Enable();

        Log.LogInfo($"{PluginName} v{PluginVersion} loaded.");
    }
}

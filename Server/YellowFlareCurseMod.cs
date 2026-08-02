using System.Reflection;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
using YellowFlareCurse.Patches;
using Path = System.IO.Path;

namespace YellowFlareCurse;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "gadjed.yellowflarecurse";
    public string Name { get; init; } = "Yellow Flare Curse";
    public string Author { get; init; } = "gadjed";
    public List<string>? Contributors { get; init; } = null;
    public SemanticVersioning.Version Version { get; init; } = new("1.2.0");
    public SemanticVersioning.Range SptVersion { get; init; } = new(">=4.0.0 <4.2.0");
    public bool HasPrepatcher { get; init; } = false;
    public List<string>? Incompatibilities { get; init; } = null;
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; } = null;
    public string? Url { get; init; } = "https://github.com/gadjed/Yellow-flare-curse-SPT-mod";
    public string License { get; init; } = "MIT";
}

[Injectable(TypePriority = OnLoadOrder.PostLoad + 1)]
public class YellowFlareCurseMod(
    ISptLogger<YellowFlareCurseMod> logger,
    ModHelper modHelper,
    AirdropConfig airdropConfig,
    PatchManager patchManager
) : IOnLoad
{
    public const string Tag = "[YellowFlareCurse]";

    /// <summary>
    /// toiletPaper is a forced-loot-only SPT profile with Supply crate icon (not Common/«общей поддержки»).
    /// Weight is nearly 0 for random drops; we overwrite its ForcedLoot for the curse container.
    /// </summary>
    public static readonly SptAirdropTypeEnum CurseAirdropType = SptAirdropTypeEnum.toiletPaper;

    public static ModConfig Config { get; private set; } = new();
    public static MongoId CurseContainerId { get; private set; } = new(CurseIds.DefaultContainerId);
    public static string CurseContainerIdString { get; private set; } = CurseIds.DefaultContainerId;
    public static Dictionary<MongoId, MinMax<int>> ForcedLoot { get; private set; } = new();
    public static AirdropLoot? CurseLootProfile { get; private set; }

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var pathToMod = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var fileLog = new ModFileLogger(
            pathToMod,
            msg => logger.Info(msg),
            msg => logger.Warning(msg),
            msg => logger.Error(msg),
            msg => logger.Success(msg)
        );
        ModFileLogger.Instance = fileLog;

        var configPath = Path.Combine(pathToMod, "config.json");
        Config = File.Exists(configPath)
            ? modHelper.GetJsonDataFromFile<ModConfig>(pathToMod, "config.json")
            : new ModConfig();

        fileLog.Info($"{Tag} Config loaded from {(File.Exists(configPath) ? configPath : "defaults")}.");

        if (!Config.Enabled)
        {
            fileLog.Warning($"{Tag} Disabled via config.");
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(Config.CurseContainerId) || Config.CurseContainerId.Length != 24)
        {
            fileLog.Error($"{Tag} Invalid CurseContainerId; expected 24-char MongoId.");
            return Task.CompletedTask;
        }

        CurseContainerIdString = Config.CurseContainerId;
        CurseContainerId = new MongoId(Config.CurseContainerId);
        ForcedLoot = BuildForcedLoot(Config);
        fileLog.Info($"{Tag} ForcedLoot built: {ForcedLoot.Count} template entries.");

        // Map curse container → Supply-style forced profile (NOT mixed/Common).
        airdropConfig.CustomAirdropMapping[CurseContainerId] = CurseAirdropType;
        fileLog.Info($"{Tag} CustomAirdropMapping[{CurseContainerId}] = {CurseAirdropType}.");

        if (!TryConfigureCurseLootProfile(airdropConfig, fileLog))
        {
            fileLog.Error($"{Tag} Failed to configure curse loot profile — airdrop will be wrong.");
            return Task.CompletedTask;
        }

        patchManager.PatcherName = "YellowFlareCurse";
        patchManager.AddPatch(new CurseAirdropLootPatch());
        patchManager.EnablePatches();

        fileLog.Success(
            $"{Tag} Loaded v1.2.0. Container={CurseContainerId}, Type={CurseAirdropType}, "
                + $"ForcedLoot={ForcedLoot.Count}, DelayHint={Config.AirdropDelaySeconds}s. "
                + $"FileLog={fileLog.LogFilePath}"
        );

        return Task.CompletedTask;
    }

    private static bool TryConfigureCurseLootProfile(AirdropConfig airdropConfig, ModFileLogger fileLog)
    {
        var key = CurseAirdropType.ToString();
        if (
            !airdropConfig.Loot.TryGetValue(key, out var profile)
            && !airdropConfig.Loot.TryGetValue(key.ToLowerInvariant(), out profile)
            && !airdropConfig.Loot.TryGetValue("toiletPaper", out profile)
        )
        {
            fileLog.Warning($"{Tag} Could not resolve loot profile key '{key}'.");
            return false;
        }

        // Forced-only table — no random fillers that turn it into a normal Supply crate.
        profile.UseForcedLoot = true;
        profile.ForcedLoot = ForcedLoot;
        profile.AllowBossItems = true;
        profile.WeaponPresetCount = new MinMax<int>(0, 0);
        profile.ArmorPresetCount = new MinMax<int>(0, 0);
        profile.ItemCount = new MinMax<int>(0, 0);
        profile.WeaponCrateCount = new MinMax<int>(0, 0);

        CurseLootProfile = profile;
        fileLog.Info(
            $"{Tag} Curse loot profile '{key}' configured: UseForcedLoot=true, "
                + $"ForcedLoot={ForcedLoot.Count}, random counts zeroed, AllowBossItems=true."
        );
        return true;
    }

    private static Dictionary<MongoId, MinMax<int>> BuildForcedLoot(ModConfig config)
    {
        var result = new Dictionary<MongoId, MinMax<int>>();
        foreach (var (tpl, range) in config.ForcedLoot)
        {
            if (string.IsNullOrWhiteSpace(tpl) || tpl.Length != 24)
            {
                ModFileLogger.Instance?.Warning($"{Tag} Skipping invalid ForcedLoot tpl '{tpl}'.");
                continue;
            }

            result[new MongoId(tpl)] = range.ToMinMax();
        }

        return result;
    }
}

using System.Reflection;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Generators.Loot;
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
    public SemanticVersioning.Version Version { get; init; } = new("1.3.0");
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
    LootGenerator lootGenerator,
    PatchManager patchManager
) : IOnLoad
{
    public const string Tag = "[YellowFlareCurse]";

    /// <summary>
    /// Mapped type is only a fallback if the Harmony replace-prefix fails.
    /// barter → SUPPLY crate («Ящик техобеспечения»), not COMMON («общей поддержки»).
    /// </summary>
    public static readonly SptAirdropTypeEnum CurseAirdropType = SptAirdropTypeEnum.barter;

    public static ModConfig Config { get; private set; } = new();
    public static MongoId CurseContainerId { get; private set; } = new(CurseIds.DefaultContainerId);
    public static string CurseContainerIdString { get; private set; } = CurseIds.DefaultContainerId;
    public static Dictionary<MongoId, MinMax<int>> ForcedLoot { get; private set; } = new();
    public static LootGenerator? LootGenerator { get; private set; }

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        LootGenerator = lootGenerator;

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

        // Fallback mapping if prefix somehow doesn't run (barter → SUPPLY crate, not COMMON).
        airdropConfig.CustomAirdropMapping[CurseContainerId] = CurseAirdropType;
        fileLog.Info($"{Tag} CustomAirdropMapping[{CurseContainerId}] = {CurseAirdropType}.");

        patchManager.PatcherName = "YellowFlareCurse";
        patchManager.AddPatch(new CurseAirdropLootPatch());
        patchManager.EnablePatches();

        fileLog.Success(
            $"{Tag} Loaded v1.3.0. Container={CurseContainerId}, Type={CurseAirdropType}, "
                + $"ForcedLoot={ForcedLoot.Count}, crate=SUPPLY/техобеспечения. "
                + $"FileLog={fileLog.LogFilePath}"
        );

        return Task.CompletedTask;
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

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
    public SemanticVersioning.Version Version { get; init; } = new("1.1.0");
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0");
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

    public static ModConfig Config { get; private set; } = new();
    public static MongoId CurseContainerId { get; private set; } = new(CurseIds.DefaultContainerId);
    public static Dictionary<MongoId, MinMax<int>> ForcedLoot { get; private set; } = new();
    public static AirdropLoot? MixedLootProfile { get; private set; }

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var pathToMod = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var configPath = Path.Combine(pathToMod, "config.json");
        Config = File.Exists(configPath)
            ? modHelper.GetJsonDataFromFile<ModConfig>(pathToMod, "config.json")
            : new ModConfig();

        if (!Config.Enabled)
        {
            logger.Warning($"{Tag} Disabled via config.");
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(Config.CurseContainerId) || Config.CurseContainerId.Length != 24)
        {
            logger.Error($"{Tag} Invalid CurseContainerId; expected 24-char MongoId.");
            return Task.CompletedTask;
        }

        CurseContainerId = new MongoId(Config.CurseContainerId);
        ForcedLoot = BuildForcedLoot(Config);

        airdropConfig.CustomAirdropMapping[CurseContainerId] = SptAirdropTypeEnum.mixed;

        // Ensure mixed loot profile can carry forced stacks when our patch runs.
        if (airdropConfig.Loot.TryGetValue(nameof(SptAirdropTypeEnum.mixed), out var mixedLoot)
            || airdropConfig.Loot.TryGetValue("mixed", out mixedLoot))
        {
            mixedLoot.AllowBossItems = true;
            MixedLootProfile = mixedLoot;
        }
        else
        {
            logger.Warning($"{Tag} Could not resolve mixed airdrop loot profile; forced loot patch may no-op.");
        }

        patchManager.PatcherName = "YellowFlareCurse";
        patchManager.EnablePatch(new CurseAirdropLootPatch());

        logger.Success(
            $"{Tag} Loaded. Container={CurseContainerId}, ForcedLoot entries={ForcedLoot.Count}, "
                + $"DelayHint={Config.AirdropDelaySeconds}s."
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
                continue;
            }

            result[new MongoId(tpl)] = range.ToMinMax();
        }

        return result;
    }
}

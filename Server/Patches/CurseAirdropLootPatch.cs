using System.Reflection;
using HarmonyLib;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Generators.Loot;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Location;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Services.InRaid;

namespace YellowFlareCurse.Patches;

/// <summary>
/// Fully replaces curse-container loot generation.
/// toiletPaper/mixed both resolve to COMMON crate («Ящик общей поддержки») in GetAirdropCrateItem —
/// so we build the response ourselves with SUPPLY crate («Ящик техобеспечения») + ForcedLoot.
/// </summary>
public class CurseAirdropLootPatch : AbstractPatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(AirdropService), nameof(AirdropService.GenerateCustomAirdropLoot));
    }

    [PatchPrefix]
    public static bool Prefix(GetAirdropLootRequest request, ref GetAirdropLootResponse __result)
    {
        var log = ModFileLogger.Instance;
        var container = request?.ContainerId.ToString() ?? "<null>";
        var empty = request?.ContainerId.IsEmpty == true;
        log?.Info($"{YellowFlareCurseMod.Tag} GenerateCustomAirdropLoot. ContainerId={container}, empty={empty}.");

        if (request is null || empty || !IsCurseContainer(container))
        {
            log?.Info(
                $"{YellowFlareCurseMod.Tag} Not curse container (want={YellowFlareCurseMod.CurseContainerIdString}) — pass-through."
            );
            return true; // run original
        }

        var lootGen = YellowFlareCurseMod.LootGenerator;
        if (lootGen is null)
        {
            log?.Error($"{YellowFlareCurseMod.Tag} LootGenerator is null — cannot build curse loot.");
            return true;
        }

        if (YellowFlareCurseMod.ForcedLoot.Count == 0)
        {
            log?.Warning($"{YellowFlareCurseMod.Tag} ForcedLoot empty — pass-through.");
            return true;
        }

        try
        {
            __result = BuildCurseAirdrop(lootGen);
            var itemCount = __result.Container?.Count() ?? 0;
            log?.Success(
                $"{YellowFlareCurseMod.Tag} Built curse airdrop: icon={__result.Icon}, "
                    + $"items={itemCount}, forcedEntries={YellowFlareCurseMod.ForcedLoot.Count}, "
                    + $"crate=LOOTCONTAINER_AIRDROP_SUPPLY_CRATE (техобеспечения)."
            );
            return false; // skip original
        }
        catch (Exception ex)
        {
            log?.Error($"{YellowFlareCurseMod.Tag} Failed to build curse airdrop: {ex}");
            return true;
        }
    }

    private static GetAirdropLootResponse BuildCurseAirdrop(LootGenerator lootGenerator)
    {
        var crateId = new MongoId();
        var crate = new Item
        {
            Id = crateId,
            // NOT common/«общей поддержки» — that is LOOTCONTAINER_AIRDROP_COMMON_SUPPLY_CRATE.
            Template = ItemTpl.LOOTCONTAINER_AIRDROP_SUPPLY_CRATE,
            Upd = new Upd { SpawnedInSession = true, StackObjectsCount = 1 },
        };

        var forcedStacks = lootGenerator.CreateForcedLoot(YellowFlareCurseMod.ForcedLoot);
        var containerItems = new List<Item> { crate };

        foreach (var stack in forcedStacks)
        {
            if (stack == null || stack.Count == 0)
            {
                continue;
            }

            foreach (var item in stack)
            {
                if (string.IsNullOrEmpty(item.ParentId))
                {
                    item.ParentId = crateId;
                    item.SlotId = "main";
                }

                containerItems.Add(item);
            }
        }

        return new GetAirdropLootResponse
        {
            Icon = AirdropTypeEnum.Supply,
            Container = containerItems,
        };
    }

    private static bool IsCurseContainer(string container)
    {
        return string.Equals(container, YellowFlareCurseMod.CurseContainerIdString, StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                container,
                YellowFlareCurseMod.CurseContainerId.ToString(),
                StringComparison.OrdinalIgnoreCase
            );
    }
}

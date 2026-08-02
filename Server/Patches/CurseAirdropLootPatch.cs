using System.Reflection;
using HarmonyLib;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Models.Eft.Location;
using SPTarkov.Server.Core.Services.InRaid;

namespace YellowFlareCurse.Patches;

/// <summary>
/// Safety net: when the client requests loot for the curse container, ensure UseForcedLoot is on.
/// Primary setup is done at load by rewriting the toiletPaper profile + CustomAirdropMapping.
/// </summary>
public class CurseAirdropLootPatch : AbstractPatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(AirdropService), nameof(AirdropService.GenerateCustomAirdropLoot));
    }

    [PatchPrefix]
    public static void Prefix(GetAirdropLootRequest request)
    {
        var log = ModFileLogger.Instance;
        var container = request?.ContainerId.ToString() ?? "<null>";
        log?.Info($"{YellowFlareCurseMod.Tag} GenerateCustomAirdropLoot. ContainerId={container}.");

        if (request is null || !IsCurseContainer(container))
        {
            log?.Info(
                $"{YellowFlareCurseMod.Tag} Not curse container (want={YellowFlareCurseMod.CurseContainerIdString}) — pass-through."
            );
            return;
        }

        var loot = YellowFlareCurseMod.CurseLootProfile;
        if (loot is null)
        {
            log?.Warning($"{YellowFlareCurseMod.Tag} CurseLootProfile is null — cannot force loot.");
            return;
        }

        if (YellowFlareCurseMod.ForcedLoot.Count == 0)
        {
            log?.Warning($"{YellowFlareCurseMod.Tag} Curse container matched but ForcedLoot is empty.");
            return;
        }

        loot.UseForcedLoot = true;
        loot.ForcedLoot = YellowFlareCurseMod.ForcedLoot;
        loot.WeaponPresetCount = new SPTarkov.Server.Core.Models.Common.MinMax<int>(0, 0);
        loot.ArmorPresetCount = new SPTarkov.Server.Core.Models.Common.MinMax<int>(0, 0);
        loot.ItemCount = new SPTarkov.Server.Core.Models.Common.MinMax<int>(0, 0);
        loot.WeaponCrateCount = new SPTarkov.Server.Core.Models.Common.MinMax<int>(0, 0);

        log?.Success(
            $"{YellowFlareCurseMod.Tag} Forcing curse loot on {container} "
                + $"({YellowFlareCurseMod.ForcedLoot.Count} entries, type={YellowFlareCurseMod.CurseAirdropType})."
        );
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

using System.Reflection;
using HarmonyLib;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Models.Eft.Location;
using SPTarkov.Server.Core.Services.InRaid;

namespace YellowFlareCurse.Patches;

/// <summary>
/// When the client requests loot for the curse container id, force the configured high-value loot table.
/// </summary>
public class CurseAirdropLootPatch : AbstractPatch
{
    private static bool _active;
    private static bool _previousUseForced;
    private static Dictionary<SPTarkov.Server.Core.Models.Common.MongoId, SPTarkov.Server.Core.Models.Common.MinMax<int>>? _previousForced;

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(AirdropService), nameof(AirdropService.GenerateCustomAirdropLoot));
    }

    [PatchPrefix]
    public static void Prefix(GetAirdropLootRequest request)
    {
        _active = false;
        _previousForced = null;

        var loot = YellowFlareCurseMod.MixedLootProfile;
        if (loot is null
            || request.ContainerId != YellowFlareCurseMod.CurseContainerId
            || YellowFlareCurseMod.ForcedLoot.Count == 0)
        {
            return;
        }

        _previousUseForced = loot.UseForcedLoot;
        _previousForced = loot.ForcedLoot;
        loot.UseForcedLoot = true;
        loot.ForcedLoot = YellowFlareCurseMod.ForcedLoot;
        _active = true;
    }

    [PatchPostfix]
    public static void Postfix()
    {
        if (!_active || YellowFlareCurseMod.MixedLootProfile is null)
        {
            return;
        }

        YellowFlareCurseMod.MixedLootProfile.UseForcedLoot = _previousUseForced;
        YellowFlareCurseMod.MixedLootProfile.ForcedLoot = _previousForced;
        _active = false;
        _previousForced = null;
    }
}

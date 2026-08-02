using System.Reflection;
using EFT;
using EFT.InventoryLogic;
using SPT.Reflection.Patching;
using UnityEngine;

namespace YellowFlareCurse.Client.Patches;

/// <summary>
/// Fires when a handheld flare / flare-gun cartridge reaches successful height.
/// </summary>
public class FlareSuccessPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return typeof(Player).GetMethod(
            nameof(Player.HandleFlareSuccessEvent),
            BindingFlags.Public | BindingFlags.Instance
        )!;
    }

    [PatchPostfix]
    public static void PatchPostfix(Player __instance, Vector3 position, AmmoTemplate ammoTemplate)
    {
        if (__instance == null)
        {
            ModLogger.Debug("FlareSuccess: instance null.");
            return;
        }

        var isYours = __instance.IsYourPlayer;
        var templateId = ammoTemplate?._id ?? "<null>";
        var nickname = __instance.Profile?.Nickname ?? "?";

        ModLogger.Debug(
            $"FlareSuccess fired. player={nickname}, IsYourPlayer={isYours}, tpl={templateId}, pos={position}."
        );

        if (!YellowFlareCursePlugin.Enabled.Value)
        {
            ModLogger.Info("FlareSuccess ignored — mod Enabled=false.");
            return;
        }

        if (!isYours)
        {
            ModLogger.Debug("FlareSuccess ignored — not local player.");
            return;
        }

        if (string.IsNullOrEmpty(templateId) || templateId != YellowFlareCursePlugin.YellowFlareTemplateId)
        {
            ModLogger.Info(
                $"Ignoring non-yellow flare success (ammoTpl={templateId}). "
                    + $"Need RSP-30 Yellow ammo={YellowFlareCursePlugin.YellowFlareTemplateId} "
                    + $"(weapon item={YellowFlareCursePlugin.YellowFlareWeaponId})."
            );
            return;
        }

        ModLogger.Info(
            $"Yellow RSP-30 success detected at {position} "
                + $"(ammoTpl={templateId}, weapon={YellowFlareCursePlugin.YellowFlareWeaponId})."
        );

        var component = CurseEventComponent.Instance;
        if (component == null)
        {
            ModLogger.Warning("No raid component attached; cannot start event.");
            return;
        }

        component.TryStartCurse(position);
    }
}

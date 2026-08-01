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
        if (!YellowFlareCursePlugin.Enabled.Value)
        {
            return;
        }

        if (__instance == null || !__instance.IsYourPlayer)
        {
            return;
        }

        var templateId = ammoTemplate?._id;
        if (string.IsNullOrEmpty(templateId) || templateId != YellowFlareCursePlugin.YellowFlareTemplateId)
        {
            if (YellowFlareCursePlugin.Debug.Value)
            {
                YellowFlareCursePlugin.Log.LogInfo(
                    $"[YellowFlareCurse] Ignoring non-yellow flare success (tpl={templateId})."
                );
            }

            return;
        }

        var component = CurseEventComponent.Instance;
        if (component == null)
        {
            YellowFlareCursePlugin.Log.LogWarning("[YellowFlareCurse] No raid component; cannot start event.");
            return;
        }

        component.TryStartCurse(position);
    }
}

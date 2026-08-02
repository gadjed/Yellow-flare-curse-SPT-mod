using System.Reflection;
using EFT;
using SPT.Reflection.Patching;
using UnityEngine;

namespace YellowFlareCurse.Client.Patches;

public class GameWorldPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return typeof(GameWorldUnityTickListener).GetMethod(
            nameof(GameWorldUnityTickListener.Create),
            BindingFlags.Public | BindingFlags.Static
        )!;
    }

    [PatchPostfix]
    public static void PatchPostfix(GameObject gameObject, GameWorld gameWorld)
    {
        if (gameWorld is HideoutGameWorld)
        {
            ModLogger.Debug("Skip attach — HideoutGameWorld.");
            return;
        }

        if (gameObject.GetComponent<CurseEventComponent>() != null)
        {
            ModLogger.Debug("Raid component already present.");
            return;
        }

        var component = gameObject.AddComponent<CurseEventComponent>();
        component.Init(gameWorld);
        ModLogger.Info("Raid event component attached.");
    }
}

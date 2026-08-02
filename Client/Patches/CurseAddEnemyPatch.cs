using System.Reflection;
using EFT;
using SPT.Reflection.Patching;

namespace YellowFlareCurse.Client.Patches;

/// <summary>
/// While the curse hunt-pack is active, block AI↔AI hostility so bots only hunt players.
/// </summary>
public class CurseAddEnemyPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return typeof(BotsGroup).GetMethod(
            nameof(BotsGroup.AddEnemy),
            BindingFlags.Public | BindingFlags.Instance
        )!;
    }

    [PatchPrefix]
    public static bool PatchPrefix(IPlayer person)
    {
        if (!CurseEventComponent.AllianceActive)
        {
            return true;
        }

        if (person == null || !person.IsAI)
        {
            return true;
        }

        return false;
    }
}

using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;

namespace YellowFlareCurse;

public class ModConfig
{
    [JsonPropertyName("Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Informational — actual delay is controlled by the client F12 setting.</summary>
    [JsonPropertyName("AirdropDelaySeconds")]
    public int AirdropDelaySeconds { get; set; } = 600;

    /// <summary>
    /// Synthetic container id passed by the client into GameWorld.InitAirdrop.
    /// Mapped via SPT CustomAirdropMapping + ForcedLoot patch.
    /// </summary>
    [JsonPropertyName("CurseContainerId")]
    public string CurseContainerId { get; set; } = CurseIds.DefaultContainerId;

    [JsonPropertyName("ForcedLoot")]
    public Dictionary<string, MinMaxConfig> ForcedLoot { get; set; } = new();
}

public class MinMaxConfig
{
    [JsonPropertyName("Min")]
    public int Min { get; set; }

    [JsonPropertyName("Max")]
    public int Max { get; set; }

    public MinMax<int> ToMinMax() => new(Math.Min(Min, Max), Math.Max(Min, Max));
}

public static class CurseIds
{
    public const string DefaultContainerId = "674a0fc0000000000000c001";

    /// <summary>Ammo fired by RSP-30 Yellow (patron_rsp_yellow) — what HandleFlareSuccessEvent reports.</summary>
    public const string YellowFlareTemplateId = "624c09e49b98e019a3315b66";

    /// <summary>Handheld RSP-30 Yellow weapon/item id.</summary>
    public const string YellowFlareWeaponId = "624c0b3340357b5f566e8766";
}

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

public class DamageTakenRoot
{
    [JsonPropertyName("data")]
    public DamageTakenDataContainer Data { get; set; } = null;
}

public class DamageTakenDataContainer
{
    [JsonPropertyName("reportData")]
    public DamageTakenReportData ReportData { get; set; } = null;
}

public class DamageTakenReportData
{
    [JsonPropertyName("report")]
    public DamageTakenReport Report { get; set; } = null;
}

public class DamageTakenReport
{
    [JsonPropertyName("events")]
    public DamageTakenEvents Events { get; set; } = null;
}

public class DamageTakenEvents
{
    [JsonPropertyName("data")]
    public List<DamageEvent> Data { get; set; } = new List<DamageEvent>();

    [JsonPropertyName("nextPageTimestamp")]
    public long? NextPageTimestamp { get; set; }
}

public class DamageEvent
{
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("sourceID")]
    public int? SourceID { get; set; }

    [JsonPropertyName("sourceInstance")]
    public int? SourceInstance { get; set; }

    [JsonPropertyName("targetID")]
    public int? TargetID { get; set; }

    [JsonPropertyName("abilityGameID")]
    public int? AbilityGameID { get; set; }

    [JsonPropertyName("fight")]
    public int? Fight { get; set; }

    [JsonPropertyName("hitType")]
    public int? HitType { get; set; }

    [JsonPropertyName("amount")]
    public int? Amount { get; set; }

    [JsonPropertyName("mitigated")]
    public int? Mitigated { get; set; }

    [JsonPropertyName("unmitigatedAmount")]
    public int? UnmitigatedAmount { get; set; }

    [JsonPropertyName("absorbed")]
    public int? Absorbed { get; set; }

    [JsonPropertyName("buffs")]
    public string Buffs { get; set; }

    [JsonPropertyName("isAoE")]
    public bool? IsAoE { get; set; }

    [JsonPropertyName("resourceActor")]
    public int? ResourceActor { get; set; }

    [JsonPropertyName("classResources")]
    public List<DamageTakenClassResource> ClassResources { get; set; }

    [JsonPropertyName("hitPoints")]
    public int? HitPoints { get; set; }

    [JsonPropertyName("maxHitPoints")]
    public int? MaxHitPoints { get; set; }

    [JsonPropertyName("attackPower")]
    public int? AttackPower { get; set; }

    [JsonPropertyName("spellPower")]
    public int? SpellPower { get; set; }

    [JsonPropertyName("armor")]
    public int? Armor { get; set; }

    [JsonPropertyName("absorb")]
    public int? Absorb { get; set; }

    [JsonPropertyName("x")]
    public int? X { get; set; }

    [JsonPropertyName("y")]
    public int? Y { get; set; }

    [JsonPropertyName("facing")]
    public int? Facing { get; set; }

    [JsonPropertyName("mapID")]
    public int? MapID { get; set; }

    [JsonPropertyName("versatility")]
    public int? Versatility { get; set; }

    [JsonPropertyName("avoidance")]
    public int? Avoidance { get; set; }

    [JsonPropertyName("itemLevel")]
    public int? ItemLevel { get; set; }

    [JsonPropertyName("sourceMarker")]
    public int? SourceMarker { get; set; }
}

public class DamageTakenClassResource
{
    [JsonPropertyName("amount")]
    public int Amount { get; set; }

    [JsonPropertyName("max")]
    public int Max { get; set; }

    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> Extra { get; set; }
}

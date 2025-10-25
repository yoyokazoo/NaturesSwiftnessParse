using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class HealEventRoot
{
    [JsonPropertyName("data")]
    public DataNode Data { get; set; } = null;
}

public sealed class HealEventDataNode
{
    [JsonPropertyName("reportData")]
    public HealEventReportDataNode ReportData { get; set; } = null;
}

public sealed class HealEventReportDataNode
{
    [JsonPropertyName("report")]
    public HealEventReportNode Report { get; set; } = null;
}

public sealed class HealEventReportNode
{
    [JsonPropertyName("events")]
    public HealEventEventsNode Events { get; set; } = null;
}

public sealed class HealEventEventsNode
{
    [JsonPropertyName("data")]
    public List<ResourceEvent> Data { get; set; } = new List<ResourceEvent>();

    // When not null, pass this back as startTime to fetch the next page.
    [JsonPropertyName("nextPageTimestamp")]
    public long? NextPageTimestamp { get; set; }
}

// One row from events.data (resourcechange in your sample)
public sealed class ResourceEvent
{
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // "resourcechange" in this page

    [JsonPropertyName("sourceID")]
    public int? SourceID { get; set; }

    [JsonPropertyName("targetID")]
    public int? TargetID { get; set; }

    [JsonPropertyName("abilityGameID")]
    public int? AbilityGameID { get; set; }

    [JsonPropertyName("fight")]
    public int? Fight { get; set; }

    // Resource deltas / metadata
    [JsonPropertyName("resourceChange")]
    public int? ResourceChange { get; set; }

    // 0 = health, 1 = mana, 2 = energy, 3 = combo points, etc. (engine-defined)
    [JsonPropertyName("resourceChangeType")]
    public int? ResourceChangeType { get; set; }

    [JsonPropertyName("otherResourceChange")]
    public int? OtherResourceChange { get; set; }

    [JsonPropertyName("maxResourceAmount")]
    public int? MaxResourceAmount { get; set; }

    [JsonPropertyName("waste")]
    public int? Waste { get; set; }

    // 1 indicates the snapshot is for the target; 0 for source (per API semantics)
    [JsonPropertyName("resourceActor")]
    public int? ResourceActor { get; set; }

    [JsonPropertyName("classResources")]
    public List<ClassResource> ClassResources { get; set; } = new List<ClassResource>();

    // Resource snapshot (present when includeResources: true)
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

    // Often -1 when unknown
    [JsonPropertyName("absorb")]
    public int? Absorb { get; set; }

    // Position/orientation snapshot
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

    // Future-proof: capture any extra fields without breaking
    [JsonExtensionData]
    public Dictionary<string, JsonElement> Extra { get; set; }
}
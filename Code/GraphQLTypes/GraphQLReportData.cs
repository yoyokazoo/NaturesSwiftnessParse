using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

public class ReportDataRoot
{
    [JsonPropertyName("data")]
    public ReportDataContainer Data { get; set; } = null;
}

public class ReportDataContainer
{
    [JsonPropertyName("reportData")]
    public ReportData ReportData { get; set; } = null;
}

public class ReportData
{
    [JsonPropertyName("report")]
    public Report Report { get; set; } = null;
}

public class Report
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("fights")]
    public List<Fight> Fights { get; set; } = new List<Fight>();

    [JsonPropertyName("events")]
    public EventsPage Events { get; set; } = null;

    [JsonPropertyName("masterData")]
    public MasterDataActors MasterData { get; set; } = null;
}

public sealed class MasterDataActors
{
    [JsonPropertyName("actors")]
    public List<Actor> Actors { get; set; } = new List<Actor>();
}

public sealed class Actor
{
    // Note: can be negative (e.g., -1 for Environment)
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    // "Player", "NPC", "Pet" (keep string for forward-compatibility)
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    // Null for player/NPC; actor id for pet owner
    [JsonPropertyName("petOwner")]
    public int? PetOwner { get; set; }

    // If the API includes these later, ExtensionData will capture them
    [JsonExtensionData]
    public Dictionary<string, JsonElement> Extra { get; set; }
}

public class Fight
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("startTime")]
    public int StartTime { get; set; } = 0;

    [JsonPropertyName("endTime")]
    public int EndTime { get; set; } = 0;
}

public sealed class EventsPage
{
    // The actual list of events
    [JsonPropertyName("data")]
    public List<EventRow> Data { get; set; } = new List<EventRow>();

    // When not null, use this to paginate: pass as startTime in the next request
    [JsonPropertyName("nextPageTimestamp")]
    public long? NextPageTimestamp { get; set; }
}

public sealed class EventRow
{
    // Example fields present in your sample (resource events)
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // e.g., "resourcechange"

    [JsonPropertyName("sourceID")]
    public int? SourceID { get; set; }

    [JsonPropertyName("targetID")]
    public int? TargetID { get; set; }

    [JsonPropertyName("abilityGameID")]
    public int? AbilityGameID { get; set; }

    [JsonPropertyName("fight")]
    public int? Fight { get; set; }

    [JsonPropertyName("resourceChange")]
    public int? ResourceChange { get; set; }

    [JsonPropertyName("resourceChangeType")]
    public int? ResourceChangeType { get; set; } // 0=health, 1=mana, etc. (engine-defined)

    [JsonPropertyName("otherResourceChange")]
    public int? OtherResourceChange { get; set; }

    [JsonPropertyName("maxResourceAmount")]
    public int? MaxResourceAmount { get; set; }

    [JsonPropertyName("waste")]
    public int? Waste { get; set; }

    // Which actor the resource snapshot pertains to (0=source, 1=target, etc., per API semantics)
    [JsonPropertyName("resourceActor")]
    public int? ResourceActor { get; set; }

    [JsonPropertyName("classResources")]
    public List<ClassResource> ClassResources { get; set; } = new List<ClassResource>();

    // Snapshot stats included when includeResources: true
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

    // Absorb can be -1 in samples; keep it as int?
    [JsonPropertyName("absorb")]
    public int? Absorb { get; set; }

    // Positioning/orientation
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

    [JsonPropertyName("amount")]
    public int? Amount { get; set; }

    // Capture any unexpected/extra fields without breaking deserialization
    [JsonExtensionData]
    public Dictionary<string, JsonElement> Extra { get; set; }
}

public sealed class ClassResource
{
    [JsonPropertyName("amount")]
    public int? Amount { get; set; }

    [JsonPropertyName("max")]
    public int? Max { get; set; }

    [JsonPropertyName("type")]
    public int? Type { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> Extra { get; set; }
}
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Text.Json;

public sealed class MasterDataRoot
{
    [JsonPropertyName("data")]
    public MasterDataNode Data { get; set; } = null;
}

public sealed class MasterDataNode
{
    [JsonPropertyName("reportData")]
    public MasterDataReportDataNode ReportData { get; set; } = null;
}

public sealed class MasterDataReportDataNode
{
    [JsonPropertyName("report")]
    public MasterDataReportNode Report { get; set; } = null;
}

public sealed class MasterDataReportNode
{
    [JsonPropertyName("masterData")]
    public MasterDataActorsNode MasterData { get; set; } = null;
}

public sealed class MasterDataActorsNode
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
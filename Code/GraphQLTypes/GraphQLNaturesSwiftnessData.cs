using System.Collections.Generic;
using System.Text.Json.Serialization;

public sealed class NaturesSwiftnessRoot
{
    [JsonPropertyName("data")]
    public NaturesSwiftnessDataNode Data { get; set; } = null;
}

public sealed class NaturesSwiftnessDataNode
{
    [JsonPropertyName("reportData")]
    public NaturesSwiftnessReportDataNode ReportData { get; set; } = null;
}

public sealed class NaturesSwiftnessReportDataNode
{
    [JsonPropertyName("report")]
    public NaturesSwiftnessReportNode Report { get; set; } = null;
}

public sealed class NaturesSwiftnessReportNode
{
    [JsonPropertyName("events")]
    public NaturesSwiftnessEventsNode Events { get; set; } = null;
}

public sealed class NaturesSwiftnessEventsNode
{
    [JsonPropertyName("data")]
    public List<NaturesSwiftnessEventRow> Data { get; set; } = new List<NaturesSwiftnessEventRow>();

    [JsonPropertyName("nextPageTimestamp")]
    public long? NextPageTimestamp { get; set; }
}

public sealed class NaturesSwiftnessEventRow
{
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;  // e.g., "cast"

    [JsonPropertyName("sourceID")]
    public int? SourceID { get; set; }

    [JsonPropertyName("targetID")]
    public int? TargetID { get; set; }

    [JsonPropertyName("abilityGameID")]
    public int? AbilityGameID { get; set; }

    [JsonPropertyName("fight")]
    public int? Fight { get; set; }

    // Optional fields (may not always be present)
    [JsonPropertyName("sourceMarker")]
    public int? SourceMarker { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object> Extra { get; set; }
}
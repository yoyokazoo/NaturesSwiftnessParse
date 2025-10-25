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
}

public class Fight
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
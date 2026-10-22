namespace GrandPrix.Application;

public sealed class GameSessionSaveData
{
    public int ContentSchemaVersion { get; set; }
    public string SeriesId { get; set; } = string.Empty;
    public string RaceStrategyPolicyId { get; set; } = string.Empty;
    public List<SessionCommandRecord> Commands { get; set; } = new();
}

public sealed class SessionCommandRecord
{
    public string Type { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string? Text1 { get; set; }
    public string? Text2 { get; set; }
    public int Int1 { get; set; }
    public int Int2 { get; set; }
    public long Long1 { get; set; }
    public double Double1 { get; set; }
    public bool Bool1 { get; set; }
}

namespace Coiny.API.Data.Hybrid;
public sealed class LegacyImportState
{
    public int Id { get; set; } = 1;
    public string SourceSha256 { get; set; } = "";
    public DateTime? CompletedAt { get; set; }
}

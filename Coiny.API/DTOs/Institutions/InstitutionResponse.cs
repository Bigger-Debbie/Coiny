namespace Coiny.API.DTOs.Institutions;

public class InstitutionResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Website { get; set; }
    public bool IsActive { get; set; }
}
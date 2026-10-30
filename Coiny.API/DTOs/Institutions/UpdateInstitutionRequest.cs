using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace Coiny.API.DTOs.Institutions;

public class UpdateInstitutionRequest : CreateInstitutionRequest
{
    [JsonRequired, Required, MaxLength(100)] public string ExpectedName { get; set; } = string.Empty;
    [JsonRequired, MaxLength(250)] public string? ExpectedWebsite { get; set; }
}

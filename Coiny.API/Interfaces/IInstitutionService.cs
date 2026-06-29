using Coiny.API.DTOs.Institutions;

namespace Coiny.API.Interfaces;

public interface IInstitutionService
{
    Task<List<InstitutionResponse>> GetInstitutionsAsync();
    Task<InstitutionResponse> CreateInstitutionAsync(CreateInstitutionRequest request);
}
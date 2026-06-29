using Coiny.API.Data;
using Coiny.API.DTOs.Institutions;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Services;

public class InstitutionService : IInstitutionService
{
    private readonly CoinyContext _context;
    private readonly ICurrentUserService _currentUserService;

    public InstitutionService(CoinyContext context, ICurrentUserService currentUserService)
    {
        _context = context; 
        _currentUserService = currentUserService;
    }

    public async Task<List<InstitutionResponse>> GetInstitutionsAsync()
    {
        var householdId = await _currentUserService.GetCurrentHouseholdIdAsync();

        return await _context.Institutions
            .Where(i => i.HouseholdId == householdId)
            .OrderBy(i => i.Name)
            .Select(i => new InstitutionResponse
            {
                Id = i.Id,
                Name = i.Name,
                Website = i.Website,
                IsActive = i.IsActive
            })
            .ToListAsync();
    }

    public async Task<InstitutionResponse> CreateInstitutionAsync(CreateInstitutionRequest request)
    {
        var householdId = await _currentUserService.GetCurrentHouseholdIdAsync();

        var institution = new Institution
        {
            HouseholdId = householdId,
            Name = request.Name,
            Website = request.Website,
            IsActive = true
        };

        _context.Institutions.Add(institution);
        await _context.SaveChangesAsync();

        return new InstitutionResponse
        {
            Id = institution.Id,
            Name = institution.Name,
            Website = institution.Website,
            IsActive = institution.IsActive
        };
    }
}
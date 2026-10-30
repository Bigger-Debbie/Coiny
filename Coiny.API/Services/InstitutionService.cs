using Coiny.API.Data;
using Coiny.API.DTOs.Institutions;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Services;

public sealed class InstitutionConflictException() : Exception("This institution changed. Reload it before saving.");

public class InstitutionService : IInstitutionService
{
    private readonly IFinancialContext _context;
    private readonly ICurrentUserService _currentUserService;

    public InstitutionService(IFinancialContext context, ICurrentUserService currentUserService)
    {
        _context = context; 
        _currentUserService = currentUserService;
    }

    private static (string Name, string? Website) Validate(CreateInstitutionRequest request)
    {
        var name = request.Name?.Trim() ?? "";
        var website = string.IsNullOrWhiteSpace(request.Website) ? null : request.Website.Trim();
        if (name.Length is < 1 or > 100) throw new ArgumentException("Enter an institution name of up to 100 characters.");
        if (website is not null && (website.Length > 250 || !Uri.TryCreate(website, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("https" or "http") || string.IsNullOrWhiteSpace(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo)))
            throw new ArgumentException("Enter a website starting with https:// or http://, or leave it blank.");
        return (name, website);
    }
    public async Task<InstitutionResponse> UpdateInstitutionAsync(int id, UpdateInstitutionRequest request)
    {
        var household = await _currentUserService.GetCurrentHouseholdIdAsync();
        var (name, website) = Validate(request);
        var owned = _context.Institutions.Where(i => i.Id == id && i.HouseholdId == household);
        var changed = await owned.Where(i => i.Name == request.ExpectedName && i.Website == request.ExpectedWebsite)
            .ExecuteUpdateAsync(set => set.SetProperty(i => i.Name, name).SetProperty(i => i.Website, website));
        if (changed == 0) {
            if (!await owned.AnyAsync()) throw new KeyNotFoundException("Institution not found in your household.");
            throw new InstitutionConflictException();
        }
        return await owned.AsNoTracking().Select(i => new InstitutionResponse { Id=i.Id, Name=i.Name, Website=i.Website, IsActive=i.IsActive }).SingleAsync();
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

        var (name, website) = Validate(request);
        var institution = new Institution
        {
            HouseholdId = householdId,
            Name = name,
            Website = website,
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
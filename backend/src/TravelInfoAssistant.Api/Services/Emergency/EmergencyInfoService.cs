using Microsoft.EntityFrameworkCore;
using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Infrastructure;

namespace TravelInfoAssistant.Api.Services.Emergency;

public sealed class EmergencyInfoService(AppDbContext dbContext) : IEmergencyInfoService
{
    public async Task<EmergencyInfoResponse?> GetAsync(
        Guid cityId,
        CancellationToken cancellationToken)
    {
        var city = await dbContext.Cities
            .AsNoTracking()
            .Where(item => item.IsActive)
            .FirstOrDefaultAsync(item => item.Id == cityId, cancellationToken);
        if (city is null)
        {
            return null;
        }

        var contacts = await dbContext.EmergencyContacts
            .AsNoTracking()
            .Where(item => item.CountryCode == city.CountryCode)
            .Where(item => item.CityCode == null || item.CityCode == city.Code)
            .OrderBy(item => item.SortOrder)
            .ToListAsync(cancellationToken);

        var office = await dbContext.OverseasOffices
            .AsNoTracking()
            .Where(item => item.CountryCode == city.CountryCode)
            .Where(item => item.CityCode == null || item.CityCode == city.Code)
            .FirstOrDefaultAsync(cancellationToken);

        var guides = await dbContext.EmergencyGuides
            .AsNoTracking()
            .Where(item => item.CountryCode == "*" || item.CountryCode == city.CountryCode)
            .OrderBy(item => item.SortOrder)
            .ToListAsync(cancellationToken);

        var verifiedDates = contacts.Select(item => item.VerifiedOn)
            .Concat(guides.Select(item => item.VerifiedOn));
        if (office is not null)
        {
            verifiedDates = verifiedDates.Append(office.VerifiedOn);
        }

        return new EmergencyInfoResponse(
            city.NameZh,
            city.CountryCode,
            contacts.Select(item => new EmergencyContactResponse(
                item.Id,
                item.Category,
                item.DisplayName,
                item.PhoneNumber,
                item.Note,
                item.SourceName,
                item.SourceUrl,
                item.VerifiedOn)).ToList(),
            office is null
                ? null
                : new OverseasOfficeResponse(
                    office.Id,
                    office.NameZh,
                    office.Address,
                    office.MainPhone,
                    office.EmergencyPhone,
                    office.Note,
                    office.SourceName,
                    office.SourceUrl,
                    office.VerifiedOn),
            guides.Select(item => new EmergencyGuideResponse(
                item.Id,
                item.Slug,
                item.Title,
                item.Summary,
                EmergencyGuideParser.ParseSteps(item.StepsJson),
                item.SourceName,
                item.SourceUrl,
                item.VerifiedOn)).ToList(),
            verifiedDates.DefaultIfEmpty(DateOnly.FromDateTime(DateTime.UtcNow)).Max());
    }
}

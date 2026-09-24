using Microsoft.EntityFrameworkCore;
using TravelInfoAssistant.Api.Domain;

namespace TravelInfoAssistant.Api.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<City> Cities => Set<City>();
    public DbSet<CityServiceCapability> CityServiceCapabilities => Set<CityServiceCapability>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var city = modelBuilder.Entity<City>();
        city.ToTable("cities");
        city.HasKey(item => item.Id);
        city.HasIndex(item => item.Code).IsUnique();
        city.Property(item => item.Code).HasMaxLength(50);
        city.Property(item => item.NameZh).HasMaxLength(100);
        city.Property(item => item.NameEn).HasMaxLength(100);
        city.Property(item => item.CountryCode).HasMaxLength(2);
        city.Property(item => item.TimeZone).HasMaxLength(100);

        var capability = modelBuilder.Entity<CityServiceCapability>();
        capability.ToTable("city_service_capabilities");
        capability.HasKey(item => item.Id);
        capability.HasIndex(item => new { item.CityId, item.ServiceKey }).IsUnique();
        capability.Property(item => item.ServiceKey).HasMaxLength(50);
        capability.Property(item => item.DisplayName).HasMaxLength(100);
        capability.Property(item => item.IntegrationStatus).HasConversion<string>().HasMaxLength(40);
        capability.Property(item => item.AvailabilityStatus).HasConversion<string>().HasMaxLength(40);
        capability.Property(item => item.Message).HasMaxLength(300);
        capability.HasOne(item => item.City)
            .WithMany(item => item.ServiceCapabilities)
            .HasForeignKey(item => item.CityId)
            .OnDelete(DeleteBehavior.Cascade);

        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        var taipeiId = Guid.Parse("ffb8f976-2fd5-46c7-b543-ebdcd3283973");
        var tokyoId = Guid.Parse("9f9e554f-a968-4986-8d78-766f31cc8ae2");

        modelBuilder.Entity<City>().HasData(
            new City
            {
                Id = taipeiId,
                Code = "taipei",
                NameZh = "台北",
                NameEn = "Taipei",
                CountryCode = "TW",
                TimeZone = "Asia/Taipei",
                CenterLatitude = 25.0375,
                CenterLongitude = 121.5637,
                CoverageRadiusKilometers = 70,
                IsActive = true,
                SortOrder = 1
            },
            new City
            {
                Id = tokyoId,
                Code = "tokyo",
                NameZh = "東京",
                NameEn = "Tokyo",
                CountryCode = "JP",
                TimeZone = "Asia/Tokyo",
                CenterLatitude = 35.6762,
                CenterLongitude = 139.6503,
                CoverageRadiusKilometers = 100,
                IsActive = true,
                SortOrder = 2
            });

        modelBuilder.Entity<CityServiceCapability>().HasData(
            Capability(
                "aaecb725-b02c-47c9-a09f-456f2a58733f",
                taipeiId,
                "bus",
                "公車",
                1,
                IntegrationStatus.Integrated),
            Capability(
                "828f124b-a580-48e1-a5f1-e751e48dc679",
                taipeiId,
                "metro",
                "捷運",
                2,
                IntegrationStatus.Integrated),
            Capability(
                "c95f5dbc-b3dc-4d5b-be90-3720d5b9f535",
                taipeiId,
                "rail",
                "台鐵",
                3,
                IntegrationStatus.Integrated),
            Capability(
                "2ad9a7d4-b49b-4da5-a554-f046f00689b8",
                taipeiId,
                "weather",
                "天氣",
                4,
                IntegrationStatus.Integrated),
            Capability("09120313-ac73-4b78-a50d-524ff51087c7", tokyoId, "metro", "地鐵", 1),
            Capability("86a6de06-35c0-4ff2-92f7-9ebd3f0594bb", tokyoId, "bus", "都營巴士", 2),
            Capability(
                "bf60bfe0-0e19-43a3-86dc-434b3f6bba5d",
                tokyoId,
                "weather",
                "天氣",
                3,
                IntegrationStatus.Integrated));
    }

    private static CityServiceCapability Capability(
        string id,
        Guid cityId,
        string serviceKey,
        string displayName,
        int sortOrder,
        IntegrationStatus integrationStatus = IntegrationStatus.NotIntegrated) =>
        new()
        {
            Id = Guid.Parse(id),
            CityId = cityId,
            ServiceKey = serviceKey,
            DisplayName = displayName,
            IntegrationStatus = integrationStatus,
            AvailabilityStatus = AvailabilityStatus.Available,
            SortOrder = sortOrder
        };
}

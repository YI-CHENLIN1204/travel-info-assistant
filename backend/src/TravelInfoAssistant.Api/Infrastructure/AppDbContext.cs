using Microsoft.EntityFrameworkCore;
using TravelInfoAssistant.Api.Domain;

namespace TravelInfoAssistant.Api.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<City> Cities => Set<City>();
    public DbSet<CityServiceCapability> CityServiceCapabilities => Set<CityServiceCapability>();
    public DbSet<EmergencyContact> EmergencyContacts => Set<EmergencyContact>();
    public DbSet<OverseasOffice> OverseasOffices => Set<OverseasOffice>();
    public DbSet<EmergencyGuide> EmergencyGuides => Set<EmergencyGuide>();

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

        var emergencyContact = modelBuilder.Entity<EmergencyContact>();
        emergencyContact.ToTable("emergency_contacts");
        emergencyContact.HasKey(item => item.Id);
        emergencyContact.HasIndex(item => new { item.CountryCode, item.CityCode, item.SortOrder });
        emergencyContact.Property(item => item.CountryCode).HasMaxLength(2);
        emergencyContact.Property(item => item.CityCode).HasMaxLength(50);
        emergencyContact.Property(item => item.Category).HasMaxLength(50);
        emergencyContact.Property(item => item.DisplayName).HasMaxLength(150);
        emergencyContact.Property(item => item.PhoneNumber).HasMaxLength(100);
        emergencyContact.Property(item => item.Note).HasMaxLength(500);
        emergencyContact.Property(item => item.SourceName).HasMaxLength(150);
        emergencyContact.Property(item => item.SourceUrl).HasMaxLength(500);

        var overseasOffice = modelBuilder.Entity<OverseasOffice>();
        overseasOffice.ToTable("overseas_offices");
        overseasOffice.HasKey(item => item.Id);
        overseasOffice.HasIndex(item => new { item.CountryCode, item.CityCode }).IsUnique();
        overseasOffice.Property(item => item.CountryCode).HasMaxLength(2);
        overseasOffice.Property(item => item.CityCode).HasMaxLength(50);
        overseasOffice.Property(item => item.NameZh).HasMaxLength(200);
        overseasOffice.Property(item => item.Address).HasMaxLength(500);
        overseasOffice.Property(item => item.MainPhone).HasMaxLength(100);
        overseasOffice.Property(item => item.EmergencyPhone).HasMaxLength(200);
        overseasOffice.Property(item => item.Note).HasMaxLength(600);
        overseasOffice.Property(item => item.SourceName).HasMaxLength(150);
        overseasOffice.Property(item => item.SourceUrl).HasMaxLength(500);

        var emergencyGuide = modelBuilder.Entity<EmergencyGuide>();
        emergencyGuide.ToTable("emergency_guides");
        emergencyGuide.HasKey(item => item.Id);
        emergencyGuide.HasIndex(item => new { item.CountryCode, item.Slug }).IsUnique();
        emergencyGuide.Property(item => item.CountryCode).HasMaxLength(2);
        emergencyGuide.Property(item => item.Slug).HasMaxLength(80);
        emergencyGuide.Property(item => item.Title).HasMaxLength(150);
        emergencyGuide.Property(item => item.Summary).HasMaxLength(600);
        emergencyGuide.Property(item => item.SourceName).HasMaxLength(150);
        emergencyGuide.Property(item => item.SourceUrl).HasMaxLength(500);

        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        var taipeiId = Guid.Parse("ffb8f976-2fd5-46c7-b543-ebdcd3283973");
        var tokyoId = Guid.Parse("9f9e554f-a968-4986-8d78-766f31cc8ae2");
        var hongKongId = Guid.Parse("3f44e734-7f45-4a20-a6df-56e2498341a0");
        var singaporeId = Guid.Parse("c8c1f1a4-2a8b-4a40-9f10-8e5b4f19ab23");

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
            },
            new City
            {
                Id = hongKongId,
                Code = "hong-kong",
                NameZh = "香港",
                NameEn = "Hong Kong",
                CountryCode = "HK",
                TimeZone = "Asia/Hong_Kong",
                CenterLatitude = 22.3193,
                CenterLongitude = 114.1694,
                CoverageRadiusKilometers = 60,
                IsActive = true,
                SortOrder = 3
            },
            new City
            {
                Id = singaporeId,
                Code = "singapore",
                NameZh = "新加坡",
                NameEn = "Singapore",
                CountryCode = "SG",
                TimeZone = "Asia/Singapore",
                CenterLatitude = 1.3521,
                CenterLongitude = 103.8198,
                CoverageRadiusKilometers = 45,
                IsActive = true,
                SortOrder = 4
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
                "46a76690-81bf-451e-83b7-4af992c74f6f",
                taipeiId,
                "high-speed-rail",
                "高鐵",
                4,
                IntegrationStatus.Integrated),
            Capability(
                "2ad9a7d4-b49b-4da5-a554-f046f00689b8",
                taipeiId,
                "weather",
                "天氣",
                5,
                IntegrationStatus.Integrated),
            Capability(
                "a93d797c-d305-4a82-a082-f2bc5279d1fb",
                taipeiId,
                "alerts",
                "旅遊警示",
                6,
                IntegrationStatus.Integrated),
            Capability(
                "f3166896-c438-4db1-840e-84b881685899",
                taipeiId,
                "emergency",
                "應急資訊",
                7,
                IntegrationStatus.Integrated),
            Capability(
                "09120313-ac73-4b78-a50d-524ff51087c7",
                tokyoId,
                "metro",
                "地鐵",
                1,
                IntegrationStatus.Integrated),
            Capability("86a6de06-35c0-4ff2-92f7-9ebd3f0594bb", tokyoId, "bus", "都營巴士", 2),
            Capability(
                "bf60bfe0-0e19-43a3-86dc-434b3f6bba5d",
                tokyoId,
                "weather",
                "天氣",
                3,
                IntegrationStatus.Integrated),
            Capability(
                "05c4a5b2-5c5e-477d-a968-95f96276bb31",
                tokyoId,
                "alerts",
                "旅遊警示",
                4,
                IntegrationStatus.Integrated),
            Capability(
                "ffcd687d-e144-4a4b-b21b-e30f394ae10a",
                tokyoId,
                "emergency",
                "應急資訊",
                5,
                IntegrationStatus.Integrated),
            Capability(
                "b03f799c-5db9-4abc-9f55-06a2923dc0f4",
                hongKongId,
                "metro",
                "港鐵",
                1,
                IntegrationStatus.Integrated),
            Capability(
                "30dbf5b9-52a8-4c44-8932-4fd1b764c63f",
                hongKongId,
                "bus",
                "巴士",
                2,
                IntegrationStatus.Integrated),
            Capability(
                "a2f6ee4e-1c76-4930-bbc9-68e4bcfa82df",
                hongKongId,
                "tram",
                "電車",
                3,
                IntegrationStatus.Integrated),
            Capability(
                "06b1dc30-72c1-4f1a-a060-a9e0438b11d4",
                hongKongId,
                "ferry",
                "渡輪",
                4,
                IntegrationStatus.Integrated),
            Capability(
                "2856bd80-c98a-4a68-b881-2f4d4f1375fe",
                hongKongId,
                "weather",
                "天氣",
                5,
                IntegrationStatus.Integrated),
            Capability(
                "5f99d5c8-f259-45c5-a611-30f4dd02d1c3",
                hongKongId,
                "alerts",
                "旅遊警示",
                6,
                IntegrationStatus.Integrated),
            Capability(
                "d85f5a65-eeba-42fb-9c23-c5728c05a607",
                singaporeId,
                "metro",
                "地鐵",
                1,
                IntegrationStatus.Integrated),
            Capability(
                "7a11e47e-a9a1-4d7b-94d9-bb65016d8670",
                singaporeId,
                "bus",
                "巴士",
                2,
                IntegrationStatus.Integrated),
            Capability(
                "96fde83b-840b-4472-9c76-589a545d3db4",
                singaporeId,
                "weather",
                "天氣",
                3,
                IntegrationStatus.Integrated),
            Capability(
                "d0398dbe-8ebc-46f8-aad8-c0131a9269ec",
                singaporeId,
                "alerts",
                "旅遊警示",
                4,
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

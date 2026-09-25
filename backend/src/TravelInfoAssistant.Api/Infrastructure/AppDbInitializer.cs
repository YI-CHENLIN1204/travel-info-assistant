using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TravelInfoAssistant.Api.Domain;

namespace TravelInfoAssistant.Api.Infrastructure;

public static class AppDbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseInitialization");
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        const int maximumAttempts = 5;
        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            try
            {
                await dbContext.Database.EnsureCreatedAsync();
                await EnsureEmergencySchemaAsync(dbContext);
                await ApplyCurrentCapabilityStateAsync(dbContext);
                await SeedEmergencyDataAsync(dbContext);
                return;
            }
            catch (Exception exception) when (attempt < maximumAttempts)
            {
                logger.LogWarning(
                    exception,
                    "Database initialization attempt {Attempt}/{MaximumAttempts} failed.",
                    attempt,
                    maximumAttempts);
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
            }
        }
    }

    private static async Task ApplyCurrentCapabilityStateAsync(AppDbContext dbContext)
    {
        var taipeiTransit = await dbContext.CityServiceCapabilities
            .Where(item => item.City.Code == "taipei")
            .Where(item => item.ServiceKey == "bus" || item.ServiceKey == "metro" ||
                           item.ServiceKey == "rail")
            .ToListAsync();

        var changed = false;
        foreach (var capability in taipeiTransit)
        {
            if (capability.IntegrationStatus == IntegrationStatus.Integrated)
            {
                continue;
            }

            capability.IntegrationStatus = IntegrationStatus.Integrated;
            changed = true;
        }

        var integratedDefinitions = new[]
        {
            new
            {
                CityCode = "tokyo",
                Id = Guid.Parse("09120313-ac73-4b78-a50d-524ff51087c7"),
                ServiceKey = "metro",
                DisplayName = "地鐵",
                SortOrder = 1
            },
            new
            {
                CityCode = "taipei",
                Id = Guid.Parse("2ad9a7d4-b49b-4da5-a554-f046f00689b8"),
                ServiceKey = "weather",
                DisplayName = "天氣",
                SortOrder = 4
            },
            new
            {
                CityCode = "tokyo",
                Id = Guid.Parse("bf60bfe0-0e19-43a3-86dc-434b3f6bba5d"),
                ServiceKey = "weather",
                DisplayName = "天氣",
                SortOrder = 3
            },
            new
            {
                CityCode = "taipei",
                Id = Guid.Parse("a93d797c-d305-4a82-a082-f2bc5279d1fb"),
                ServiceKey = "alerts",
                DisplayName = "旅遊警示",
                SortOrder = 5
            },
            new
            {
                CityCode = "tokyo",
                Id = Guid.Parse("05c4a5b2-5c5e-477d-a968-95f96276bb31"),
                ServiceKey = "alerts",
                DisplayName = "旅遊警示",
                SortOrder = 4
            },
            new
            {
                CityCode = "taipei",
                Id = Guid.Parse("f3166896-c438-4db1-840e-84b881685899"),
                ServiceKey = "emergency",
                DisplayName = "應急資訊",
                SortOrder = 6
            },
            new
            {
                CityCode = "tokyo",
                Id = Guid.Parse("ffcd687d-e144-4a4b-b21b-e30f394ae10a"),
                ServiceKey = "emergency",
                DisplayName = "應急資訊",
                SortOrder = 5
            }
        };
        foreach (var definition in integratedDefinitions)
        {
            var city = await dbContext.Cities
                .FirstOrDefaultAsync(item => item.Code == definition.CityCode);
            if (city is null)
            {
                continue;
            }

            var capability = await dbContext.CityServiceCapabilities
                .FirstOrDefaultAsync(item =>
                    item.CityId == city.Id && item.ServiceKey == definition.ServiceKey);
            if (capability is null)
            {
                dbContext.CityServiceCapabilities.Add(new CityServiceCapability
                {
                    Id = definition.Id,
                    CityId = city.Id,
                    ServiceKey = definition.ServiceKey,
                    DisplayName = definition.DisplayName,
                    IntegrationStatus = IntegrationStatus.Integrated,
                    AvailabilityStatus = AvailabilityStatus.Available,
                    SortOrder = definition.SortOrder
                });
                changed = true;
                continue;
            }

            if (capability.IntegrationStatus != IntegrationStatus.Integrated ||
                capability.AvailabilityStatus != AvailabilityStatus.Available)
            {
                capability.IntegrationStatus = IntegrationStatus.Integrated;
                capability.AvailabilityStatus = AvailabilityStatus.Available;
                changed = true;
            }
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync();
        }
    }

    private static Task EnsureEmergencySchemaAsync(AppDbContext dbContext) =>
        dbContext.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS emergency_contacts (
                "Id" uuid NOT NULL,
                "CountryCode" character varying(2) NOT NULL,
                "CityCode" character varying(50),
                "Category" character varying(50) NOT NULL,
                "DisplayName" character varying(150) NOT NULL,
                "PhoneNumber" character varying(100) NOT NULL,
                "Note" character varying(500),
                "SourceName" character varying(150) NOT NULL,
                "SourceUrl" character varying(500) NOT NULL,
                "VerifiedOn" date NOT NULL,
                "SortOrder" integer NOT NULL,
                CONSTRAINT "PK_emergency_contacts" PRIMARY KEY ("Id")
            );

            CREATE INDEX IF NOT EXISTS "IX_emergency_contacts_CountryCode_CityCode_SortOrder"
                ON emergency_contacts ("CountryCode", "CityCode", "SortOrder");

            CREATE TABLE IF NOT EXISTS overseas_offices (
                "Id" uuid NOT NULL,
                "CountryCode" character varying(2) NOT NULL,
                "CityCode" character varying(50),
                "NameZh" character varying(200) NOT NULL,
                "Address" character varying(500) NOT NULL,
                "MainPhone" character varying(100) NOT NULL,
                "EmergencyPhone" character varying(200) NOT NULL,
                "Note" character varying(600),
                "SourceName" character varying(150) NOT NULL,
                "SourceUrl" character varying(500) NOT NULL,
                "VerifiedOn" date NOT NULL,
                CONSTRAINT "PK_overseas_offices" PRIMARY KEY ("Id")
            );

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_overseas_offices_CountryCode_CityCode"
                ON overseas_offices ("CountryCode", "CityCode");

            CREATE TABLE IF NOT EXISTS emergency_guides (
                "Id" uuid NOT NULL,
                "CountryCode" character varying(2) NOT NULL,
                "Slug" character varying(80) NOT NULL,
                "Title" character varying(150) NOT NULL,
                "Summary" character varying(600) NOT NULL,
                "StepsJson" text NOT NULL,
                "SourceName" character varying(150) NOT NULL,
                "SourceUrl" character varying(500) NOT NULL,
                "VerifiedOn" date NOT NULL,
                "SortOrder" integer NOT NULL,
                CONSTRAINT "PK_emergency_guides" PRIMARY KEY ("Id")
            );

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_emergency_guides_CountryCode_Slug"
                ON emergency_guides ("CountryCode", "Slug");
            """);

    private static async Task SeedEmergencyDataAsync(AppDbContext dbContext)
    {
        var verifiedOn = new DateOnly(2026, 9, 24);
        const string taiwanEmergencySource =
            "https://www.nfa.gov.tw/cht/index.php?article_id=795&code=list&flag=detail&ids=66";
        const string japanHotlineSource = "https://www.japan.travel/en/plan/hotline/";
        const string mofaEmergencySource = "https://www.boca.gov.tw/cp-87-2121-7d8de-1.html";

        EmergencyContact[] contacts =
        [
            new()
            {
                Id = Guid.Parse("1fa937ad-9e7c-4271-838b-98bab0eed6e2"),
                CountryCode = "TW",
                CityCode = "taipei",
                Category = "police",
                DisplayName = "警察",
                PhoneNumber = "110",
                Note = "報案、治安與人身安全緊急狀況。",
                SourceName = "內政部消防署",
                SourceUrl = taiwanEmergencySource,
                VerifiedOn = verifiedOn,
                SortOrder = 1
            },
            new()
            {
                Id = Guid.Parse("6eb0455c-5b74-40f4-9029-3dbbe34f33fe"),
                CountryCode = "TW",
                CityCode = "taipei",
                Category = "fireMedical",
                DisplayName = "消防與救護車",
                PhoneNumber = "119",
                Note = "火災、救護與緊急救援。",
                SourceName = "內政部消防署",
                SourceUrl = taiwanEmergencySource,
                VerifiedOn = verifiedOn,
                SortOrder = 2
            },
            new()
            {
                Id = Guid.Parse("168d923a-0068-46d2-93f4-f4b8e54bbd80"),
                CountryCode = "TW",
                CityCode = "taipei",
                Category = "mobileFallback",
                DisplayName = "行動電話緊急備援",
                PhoneNumber = "112",
                Note = "行動電話在原電信網路無訊號時，可嘗試轉接 110 或 119。",
                SourceName = "內政部消防署",
                SourceUrl = taiwanEmergencySource,
                VerifiedOn = verifiedOn,
                SortOrder = 3
            },
            new()
            {
                Id = Guid.Parse("9c991118-1941-41f1-85f9-a07758e8e9ef"),
                CountryCode = "JP",
                CityCode = "tokyo",
                Category = "police",
                DisplayName = "警察",
                PhoneNumber = "110",
                Note = "犯罪、事故或人身安全的緊急報案。",
                SourceName = "日本政府觀光局 JNTO",
                SourceUrl = japanHotlineSource,
                VerifiedOn = verifiedOn,
                SortOrder = 1
            },
            new()
            {
                Id = Guid.Parse("49994946-4cce-4ed9-b8e1-7e01ca9e734e"),
                CountryCode = "JP",
                CityCode = "tokyo",
                Category = "fireMedical",
                DisplayName = "消防與救護車",
                PhoneNumber = "119",
                Note = "火災或需要緊急醫療救護時使用。",
                SourceName = "日本政府觀光局 JNTO",
                SourceUrl = japanHotlineSource,
                VerifiedOn = verifiedOn,
                SortOrder = 2
            },
            new()
            {
                Id = Guid.Parse("46d57531-81df-4871-9e1f-4cab4f17eea5"),
                CountryCode = "JP",
                CityCode = "tokyo",
                Category = "visitorSupport",
                DisplayName = "Japan Visitor Hotline",
                PhoneNumber = "+81-50-3816-2787",
                Note = "日本境內可撥 050-3816-2787；全年 24 小時，提供英語、中文與韓語服務。",
                SourceName = "日本政府觀光局 JNTO",
                SourceUrl = japanHotlineSource,
                VerifiedOn = verifiedOn,
                SortOrder = 3
            },
            new()
            {
                Id = Guid.Parse("f0b93ad5-b57b-431e-8942-2980e264e152"),
                CountryCode = "JP",
                CityCode = "tokyo",
                Category = "policeInformation",
                DisplayName = "警視廳一般資訊",
                PhoneNumber = "+81-3-3503-8484",
                Note = "非緊急警察資訊服務；緊急狀況仍請撥 110。",
                SourceName = "日本政府觀光局 JNTO",
                SourceUrl = japanHotlineSource,
                VerifiedOn = verifiedOn,
                SortOrder = 4
            },
            new()
            {
                Id = Guid.Parse("df7cc755-66a2-4982-baba-66dfecf26d20"),
                CountryCode = "JP",
                CityCode = "tokyo",
                Category = "mofaEmergency",
                DisplayName = "外交部緊急聯絡中心",
                PhoneNumber = "+886-800-085-095",
                Note = "自海外聯絡台灣外交部的全年無休緊急專線。",
                SourceName = "外交部領事事務局",
                SourceUrl = mofaEmergencySource,
                VerifiedOn = verifiedOn,
                SortOrder = 5
            }
        ];

        OverseasOffice[] offices =
        [
            new()
            {
                Id = Guid.Parse("4fabd440-6672-469a-b2b1-386c6b6e8358"),
                CountryCode = "JP",
                CityCode = "tokyo",
                NameZh = "駐日本代表處（台北駐日經濟文化代表處）",
                Address = "東京都港區白金台 5-20-2",
                MainPhone = "(03) 3280-7811",
                EmergencyPhone = "080-1009-7179 / 080-1009-7436",
                Note = "日間緊急聯絡可撥 (81-3) 3280-7811 分機 4；夜間警衛室 (81-3) 3280-7917。",
                SourceName = "中華民國外交部",
                SourceUrl = "https://www.mofa.gov.tw/OverseasOffice_Content.aspx?n=168&os=79&s=26&sms=87",
                VerifiedOn = verifiedOn
            }
        ];

        EmergencyGuide[] guides =
        [
            new()
            {
                Id = Guid.Parse("dd84f539-7e4d-4d5e-b302-645900201c02"),
                CountryCode = "*",
                Slug = "lost-passport",
                Title = "護照遺失",
                Summary = "先取得當地警察機關的遺失證明，再聯絡最近的駐外館處補發證件。",
                StepsJson = JsonSerializer.Serialize(new[]
                {
                    "立即向當地警察機關報案，取得護照遺失或被竊證明。",
                    "聯絡最近的台灣駐外館處，確認申請地點、受理時間與所需文件。",
                    "準備申請書、照片、台灣身分證明及警察證明；費用與補件依館處通知。",
                    "若行程緊急且無法等待新護照，詢問是否符合入國證明書申請條件。"
                }),
                SourceName = "外交部領事事務局",
                SourceUrl = "https://www.boca.gov.tw/cp-30-24-de07f-1.html",
                VerifiedOn = verifiedOn,
                SortOrder = 1
            },
            new()
            {
                Id = Guid.Parse("7e2bcf15-e3a2-426e-b69a-8c7de6976353"),
                CountryCode = "*",
                Slug = "theft",
                Title = "財物或證件遭竊",
                Summary = "保障人身安全後報警並保留證明，同步停用金融工具及通知保險公司。",
                StepsJson = JsonSerializer.Serialize(new[]
                {
                    "先離開危險環境；仍有立即威脅時使用當地緊急電話。",
                    "向當地警察機關報案，索取正式報案或失竊證明。",
                    "立即停用遭竊信用卡、金融卡與電信門號，並通知旅遊保險公司。",
                    "護照或重要證件遭竊時，聯絡最近的台灣駐外館處。"
                }),
                SourceName = "外交部領事事務局",
                SourceUrl = "https://www.boca.gov.tw/fp-85-244-cdfdb-1.html",
                VerifiedOn = verifiedOn,
                SortOrder = 2
            },
            new()
            {
                Id = Guid.Parse("05ee8e94-528e-4c71-ac13-edb716082075"),
                CountryCode = "*",
                Slug = "medical-incident",
                Title = "急病、受傷或重大事故",
                Summary = "有生命危險時先聯絡當地救護單位，再通知保險公司、親友與駐外館處。",
                StepsJson = JsonSerializer.Serialize(new[]
                {
                    "有立即危險時先撥當地消防或救護電話，清楚說明位置與狀況。",
                    "攜帶護照、保險資料及用藥資訊就醫；無法自行處理時請旅館或警方協助。",
                    "儘快通知旅遊保險公司與家人，保留診斷、收據及事故紀錄。",
                    "重大事故或無親友可協助時，聯絡台灣駐外館處或外交部緊急聯絡中心。"
                }),
                SourceName = "外交部領事事務局",
                SourceUrl = "https://www.boca.gov.tw/fp-85-244-cdfdb-1.html",
                VerifiedOn = verifiedOn,
                SortOrder = 3
            }
        ];

        await UpsertAsync(dbContext, contacts, offices, guides);
    }

    private static async Task UpsertAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<EmergencyContact> contacts,
        IReadOnlyCollection<OverseasOffice> offices,
        IReadOnlyCollection<EmergencyGuide> guides)
    {
        var existingContacts = await dbContext.EmergencyContacts
            .ToDictionaryAsync(item => item.Id);
        foreach (var item in contacts)
        {
            if (existingContacts.TryGetValue(item.Id, out var existing))
            {
                dbContext.Entry(existing).CurrentValues.SetValues(item);
            }
            else
            {
                dbContext.EmergencyContacts.Add(item);
            }
        }

        var existingOffices = await dbContext.OverseasOffices
            .ToDictionaryAsync(item => item.Id);
        foreach (var item in offices)
        {
            if (existingOffices.TryGetValue(item.Id, out var existing))
            {
                dbContext.Entry(existing).CurrentValues.SetValues(item);
            }
            else
            {
                dbContext.OverseasOffices.Add(item);
            }
        }

        var existingGuides = await dbContext.EmergencyGuides
            .ToDictionaryAsync(item => item.Id);
        foreach (var item in guides)
        {
            if (existingGuides.TryGetValue(item.Id, out var existing))
            {
                dbContext.Entry(existing).CurrentValues.SetValues(item);
            }
            else
            {
                dbContext.EmergencyGuides.Add(item);
            }
        }

        await dbContext.SaveChangesAsync();
    }
}

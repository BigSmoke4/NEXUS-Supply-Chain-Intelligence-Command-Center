using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NEXUS.Modules.Analytics.Domain;
using NEXUS.Modules.AssetManagement.Domain;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.DecisionEngine.Domain;
using NEXUS.Modules.DemandManagement.Domain;
using NEXUS.Modules.DependencyGraph.Domain;
using NEXUS.Modules.DigitalTwin.Domain;
using NEXUS.Modules.Execution.Domain;
using NEXUS.Modules.Explainability.Domain;
using NEXUS.Modules.Feedback.Domain;
using NEXUS.Modules.Forecasting.Domain;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Inventory.Domain;
using NEXUS.Modules.Optimization.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ScenarioManagement.Domain;
using NEXUS.Modules.Simulation.Domain;
using NEXUS.Modules.SupplyChain.Domain;

namespace NEXUS.Shared.Data;

public static class NexusSeedData
{
    public static readonly Guid DefaultOrganizationId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("NexusSeedData");

        await db.Database.EnsureCreatedAsync(cancellationToken);

        await SeedIdentityAsync(scope.ServiceProvider);

        if (await db.Organizations.AnyAsync(cancellationToken))
        {
            return;
        }

        logger.LogInformation("Seeding NEXUS GLOBAL INDUSTRIES Enterprise Digital Twin dataset...");

        var rng = new Random(20261003);
        var orgId = DefaultOrganizationId;

        var org = new EnterpriseOrganization
        {
            Id = orgId,
            OrganizationId = orgId,
            Code = "NEXUS-GLOBAL",
            Name = "NEXUS GLOBAL INDUSTRIES",
            Headquarters = "Zurich / Frankfurt / New York / Singapore",
            BaseCurrency = "USD",
            AnnualRevenueUsd = 18_500_000_000m,
            DailyOperatingBudgetUsd = 32_000_000m,
            TargetServiceLevelPct = 97.5m,
            IsActive = true
        };
        db.Organizations.Add(org);

        var assets = new List<Asset>();
        var suppliers = new List<Supplier>();
        var factories = new List<Factory>();
        var warehouses = new List<Warehouse>();
        var dcs = new List<DistributionCenter>();
        var routes = new List<TransportationRoute>();
        var products = new List<Product>();
        var markets = new List<Market>();
        var customers = new List<Customer>();
        var processes = new List<BusinessProcess>();
        var employees = new List<EmployeeGroup>();
        var apps = new List<EnterpriseApplication>();
        var infraNodes = new List<InfrastructureNode>();
        var dependencies = new List<Dependency>();

        var regions = new[] { "Europe", "North America", "Asia-Pacific", "Latin America", "Middle East" };
        var euCities = new[]
        {
            ("Frankfurt, Germany", 50.1109, 8.6821, "Germany"),
            ("Stuttgart, Germany", 48.7758, 9.1829, "Germany"),
            ("Dresden, Germany", 51.0504, 13.7373, "Germany"),
            ("Eindhoven, Netherlands", 51.4416, 5.4697, "Netherlands"),
            ("Zurich, Switzerland", 47.3769, 8.5417, "Switzerland"),
            ("Milan, Italy", 45.4642, 9.1900, "Italy"),
            ("Lyon, France", 45.7640, 4.8357, "France"),
            ("Gothenburg, Sweden", 57.7089, 11.9746, "Sweden"),
            ("Gdansk, Poland", 54.3520, 18.6466, "Poland"),
            ("Vienna, Austria", 48.2082, 16.3738, "Austria")
        };
        var globalCities = new[]
        {
            ("Austin, TX, USA", 30.2672, -97.7431, "North America", "USA"),
            ("Detroit, MI, USA", 42.3314, -83.0458, "North America", "USA"),
            ("Toronto, Canada", 43.6532, -79.3832, "North America", "Canada"),
            ("Monterrey, Mexico", 25.6866, -100.3161, "Latin America", "Mexico"),
            ("Sao Paulo, Brazil", -23.5505, -46.6333, "Latin America", "Brazil"),
            ("Hsinchu, Taiwan", 24.8138, 120.9675, "Asia-Pacific", "Taiwan"),
            ("Osaka, Japan", 34.6937, 135.5023, "Asia-Pacific", "Japan"),
            ("Incheon, South Korea", 37.4563, 126.7052, "Asia-Pacific", "South Korea"),
            ("Singapore", 1.3521, 103.8198, "Asia-Pacific", "Singapore"),
            ("Dubai, UAE", 25.2048, 55.2708, "Middle East", "UAE")
        };

        // 1. 50 SUPPLIERS
        for (int i = 1; i <= 50; i++)
        {
            var code = $"SUP-{i:D3}";
            bool isEu = i <= 20;
            var cityInfo = isEu
                ? euCities[(i - 1) % euCities.Length]
                : (globalCities[(i - 1) % globalCities.Length].Item1,
                   globalCities[(i - 1) % globalCities.Length].Item2,
                   globalCities[(i - 1) % globalCities.Length].Item3,
                   globalCities[(i - 1) % globalCities.Length].Item5);

            var region = isEu ? "Europe" : globalCities[(i - 1) % globalCities.Length].Item4;
            var name = i switch
            {
                1 => "Supplier A — RheinMetall Precision Components GmbH (European Tier-1)",
                2 => "Supplier B — Alpine Wafer & Optics AG",
                3 => "Supplier C — Nordic Silicon Dynamics AB",
                _ => $"NEXUS Tier-{(i <= 18 ? 1 : 2)} Supplier {code} ({cityInfo.Item1.Split(',')[0]})"
            };

            decimal cap = i == 1 ? 15_000m : 4_000m + (i * 180m);
            decimal output = i == 1 ? 13_800m : Math.Round(cap * (0.68m + (decimal)(rng.NextDouble() * 0.22)), 0);
            decimal reliability = i switch
            {
                1 => 91.0m,
                3 => 97.0m,
                _ => Math.Round(86m + (decimal)(rng.NextDouble() * 12.5), 1)
            };
            decimal riskScore = i == 1 ? 74.5m : Math.Round(18m + (decimal)(rng.NextDouble() * 58), 1);
            string riskLevel = riskScore >= 70m ? "HIGH" : riskScore >= 42m ? "MEDIUM" : "LOW";
            int leadTime = isEu ? 4 + (i % 6) : 9 + (i % 10);
            decimal unitCost = Math.Round(42m + (i * 1.75m), 2);

            var asset = new Asset
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                AssetCode = code,
                Name = name,
                AssetType = nameof(EnterpriseAssetType.Supplier),
                Region = region,
                Location = cityInfo.Item1,
                Latitude = cityInfo.Item2 + (rng.NextDouble() - 0.5) * 0.4,
                Longitude = cityInfo.Item3 + (rng.NextDouble() - 0.5) * 0.4,
                Capacity = cap,
                CurrentLoad = output,
                UtilizationPct = Math.Round(output / cap * 100m, 1),
                UnitCostUsd = unitCost,
                ReliabilityPct = reliability,
                LeadTimeDays = leadTime,
                RiskScore = riskScore,
                RiskLevel = riskLevel,
                AvailabilityPct = 100m,
                DailyRevenueExposureUsd = i == 1 ? 2_450_000m : Math.Round(cap * 95m, 0),
                DependentWarehousesCount = i == 1 ? 12 : 2 + (i % 5),
                DownstreamAssetsCount = i == 1 ? 26 : 4 + (i % 8),
                OperationalStatus = "ONLINE",
                IsBottleneck = i == 1 || i == 4,
                IsCriticalPathNode = i <= 6
            };
            assets.Add(asset);

            suppliers.Add(new Supplier
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                AssetId = asset.Id,
                SupplierCode = code,
                Name = name,
                PrimaryMaterial = i % 3 == 0 ? "SiC Power Semiconductors" : i % 2 == 0 ? "Precision Actuator Servos" : "High-Grade Titanium Subassemblies",
                CapacityUnitsPerDay = cap,
                CurrentOutputUnitsPerDay = output,
                CostPerUnitUsd = unitCost,
                ReliabilityPct = reliability,
                LeadTimeDays = leadTime,
                Location = cityInfo.Item1,
                Region = region,
                Country = cityInfo.Item4,
                RiskScore = riskScore,
                RiskLevel = riskLevel,
                AvailabilityPct = 100m,
                IsPrimaryEuropeanHeroSupplier = i == 1
            });
        }

        // 2. 20 FACTORIES (including Factory A at 92% utilization & Factory B at 78% utilization / 12,000 cap / 9,360 load / $1.8M exposure / 8 warehouses)
        for (int i = 1; i <= 20; i++)
        {
            var code = $"FAC-{i:D3}";
            bool isEu = i <= 8;
            var city = isEu ? euCities[i % euCities.Length] : (globalCities[i % globalCities.Length].Item1, globalCities[i % globalCities.Length].Item2, globalCities[i % globalCities.Length].Item3, globalCities[i % globalCities.Length].Item5);
            var region = isEu ? "Europe" : globalCities[i % globalCities.Length].Item4;

            var name = i switch
            {
                1 => "FACTORY A — Stuttgart Primary Assembly Complex",
                2 => "FACTORY B — Dresden Autonomous Manufacturing Plant",
                3 => "FACTORY C — Eindhoven High-Tech Robotics Fab",
                _ => $"NEXUS Factory {code} ({city.Item1.Split(',')[0]})"
            };

            decimal cap = i switch
            {
                1 => 14_000m,
                2 => 12_000m,
                _ => 8_000m + (i * 450m)
            };
            decimal util = i switch
            {
                1 => 92.0m,
                2 => 78.0m,
                _ => Math.Round(74m + (decimal)((i * 7) % 21), 1)
            };
            decimal load = i == 2 ? 9_360m : Math.Round(cap * util / 100m, 0);
            decimal exposure = i switch
            {
                1 => 2_200_000m,
                2 => 1_800_000m,
                _ => Math.Round(load * 145m, 0)
            };
            int depWarehouses = i == 2 ? 8 : 3 + (i % 6);
            decimal riskScore = i == 1 ? 68.0m : i == 2 ? 48.0m : 25m + (i * 2.1m);
            string riskLevel = riskScore >= 65m ? "HIGH" : riskScore >= 40m ? "MEDIUM" : "LOW";

            var asset = new Asset
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                AssetCode = code,
                Name = name,
                AssetType = nameof(EnterpriseAssetType.Factory),
                Region = region,
                Location = city.Item1,
                Latitude = city.Item2,
                Longitude = city.Item3,
                Capacity = cap,
                CurrentLoad = load,
                UtilizationPct = util,
                UnitCostUsd = 115m + (i * 3.5m),
                ReliabilityPct = 95.5m - (i % 5) * 0.6m,
                LeadTimeDays = 3 + (i % 4),
                RiskScore = riskScore,
                RiskLevel = riskLevel,
                AvailabilityPct = 100m,
                DailyRevenueExposureUsd = exposure,
                DependentWarehousesCount = depWarehouses,
                DownstreamAssetsCount = depWarehouses + 6,
                OperationalStatus = "ONLINE",
                IsBottleneck = util >= 90m,
                IsCriticalPathNode = i <= 4
            };
            assets.Add(asset);

            factories.Add(new Factory
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                AssetId = asset.Id,
                FactoryCode = code,
                Name = name,
                CapacityUnitsPerDay = cap,
                CurrentLoadUnitsPerDay = load,
                UtilizationPct = util,
                ProductsManufactured = "Autonomous Drive Units, Smart Power Inverters, Industrial Control Arrays",
                ProductionCostPerUnitUsd = 115m + (i * 3.5m),
                WorkforceCount = 450 + (i * 35),
                Location = city.Item1,
                Region = region,
                AvailabilityPct = 100m,
                RiskScore = riskScore,
                RiskLevel = riskLevel,
                DailyRevenueExposureUsd = exposure,
                DependentWarehousesCount = depWarehouses
            });
        }

        // 3. 40 WAREHOUSES
        for (int i = 1; i <= 40; i++)
        {
            var code = $"WH-{i:D3}";
            bool isEu = i <= 16;
            var city = isEu ? euCities[i % euCities.Length] : (globalCities[i % globalCities.Length].Item1, globalCities[i % globalCities.Length].Item2, globalCities[i % globalCities.Length].Item3, globalCities[i % globalCities.Length].Item5);
            var region = isEu ? "Europe" : globalCities[i % globalCities.Length].Item4;
            decimal cap = 25_000m + (i * 1_200m);
            decimal util = Math.Round(64m + ((i * 5) % 24), 1);
            decimal inv = Math.Round(cap * util / 100m, 0);
            decimal procRate = Math.Round(inv / 19m, 0);

            var asset = new Asset
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                AssetCode = code,
                Name = $"Regional Hub Warehouse {code} ({city.Item1.Split(',')[0]})",
                AssetType = nameof(EnterpriseAssetType.Warehouse),
                Region = region,
                Location = city.Item1,
                Latitude = city.Item2 + 0.15,
                Longitude = city.Item3 + 0.15,
                Capacity = cap,
                CurrentLoad = inv,
                UtilizationPct = util,
                UnitCostUsd = 8.5m,
                ReliabilityPct = 98.0m,
                LeadTimeDays = 2,
                RiskScore = util > 82m ? 58m : 28m,
                RiskLevel = util > 82m ? "MEDIUM" : "LOW",
                AvailabilityPct = 100m,
                DailyRevenueExposureUsd = Math.Round(procRate * 240m, 0),
                DependentWarehousesCount = 0,
                DownstreamAssetsCount = 4,
                OperationalStatus = "ONLINE",
                IsBottleneck = i == 1 || i == 5,
                IsCriticalPathNode = i <= 8
            };
            assets.Add(asset);

            warehouses.Add(new Warehouse
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                AssetId = asset.Id,
                WarehouseCode = code,
                Name = asset.Name,
                StorageCapacityUnits = cap,
                CurrentInventoryUnits = inv,
                UtilizationPct = util,
                ProcessingRateUnitsPerDay = procRate,
                Location = city.Item1,
                Region = region,
                OperatingCostPerDayUsd = 14_500m + (i * 320m),
                CoverageDays = 19,
                AvailabilityPct = 100m
            });
        }

        // 4. 10 DISTRIBUTION CENTERS
        for (int i = 1; i <= 10; i++)
        {
            var code = $"DC-{i:D3}";
            var city = i <= 5 ? euCities[i % euCities.Length] : (globalCities[i % globalCities.Length].Item1, globalCities[i % globalCities.Length].Item2, globalCities[i % globalCities.Length].Item3, globalCities[i % globalCities.Length].Item5);
            var region = i <= 5 ? "Europe" : globalCities[i % globalCities.Length].Item4;
            decimal cap = 18_000m + (i * 900m);
            decimal load = Math.Round(cap * 0.76m, 0);

            var asset = new Asset
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                AssetCode = code,
                Name = $"Continental Distribution Center {code} ({city.Item1.Split(',')[0]})",
                AssetType = nameof(EnterpriseAssetType.DistributionCenter),
                Region = region,
                Location = city.Item1,
                Latitude = city.Item2 - 0.12,
                Longitude = city.Item3 - 0.12,
                Capacity = cap,
                CurrentLoad = load,
                UtilizationPct = 76.0m,
                UnitCostUsd = 6.2m,
                ReliabilityPct = 97.8m,
                LeadTimeDays = 1,
                RiskScore = 26m,
                RiskLevel = "LOW",
                AvailabilityPct = 100m,
                DailyRevenueExposureUsd = 640_000m,
                OperationalStatus = "ONLINE"
            };
            assets.Add(asset);

            dcs.Add(new DistributionCenter
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                AssetId = asset.Id,
                DcCode = code,
                Name = asset.Name,
                ThroughputCapacityUnitsPerDay = cap,
                CurrentThroughputUnitsPerDay = load,
                UtilizationPct = 76.0m,
                Location = city.Item1,
                Region = region,
                OperatingCostPerDayUsd = 19_000m,
                AvailabilityPct = 100m
            });
        }

        // 5. 20 MARKETS
        for (int i = 1; i <= 20; i++)
        {
            var code = $"MKT-{i:D3}";
            var region = regions[(i - 1) % regions.Length];
            var name = $"Enterprise Market {code} ({region} Sector {i})";
            decimal dailyDemand = 2_800m + (i * 220m);
            decimal rev = dailyDemand * 380m;

            var asset = new Asset
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                AssetCode = code,
                Name = name,
                AssetType = nameof(EnterpriseAssetType.Market),
                Region = region,
                Location = region,
                Latitude = 40.0 + (i % 10),
                Longitude = 5.0 + (i * 4),
                Capacity = dailyDemand * 1.25m,
                CurrentLoad = dailyDemand,
                UtilizationPct = 80.0m,
                UnitCostUsd = 0m,
                ReliabilityPct = 99.0m,
                LeadTimeDays = 1,
                RiskScore = 22m + (i % 15),
                RiskLevel = "LOW",
                AvailabilityPct = 100m,
                DailyRevenueExposureUsd = rev,
                OperationalStatus = "ONLINE"
            };
            assets.Add(asset);

            markets.Add(new Market
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                AssetId = asset.Id,
                MarketCode = code,
                Name = name,
                Region = region,
                Currency = region == "Europe" ? "EUR" : "USD",
                DailyDemandUnits = dailyDemand,
                DailyRevenuePotentialUsd = rev,
                GrowthRatePct = 4.2m + (i % 6) * 0.8m,
                VolatilityIndex = 14.5m + (i % 8) * 1.9m
            });
        }

        // 6. 30 CUSTOMERS
        for (int i = 1; i <= 30; i++)
        {
            var code = $"CUST-{i:D3}";
            var market = markets[(i - 1) % markets.Count];
            decimal dailyRev = 240_000m + (i * 28_000m);

            var asset = new Asset
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                AssetCode = code,
                Name = $"Global Industrial Partner {code}",
                AssetType = nameof(EnterpriseAssetType.Customer),
                Region = market.Region,
                Location = market.Region,
                Latitude = 42.0 + (i % 8),
                Longitude = 8.0 + (i * 3),
                Capacity = 5_000m,
                CurrentLoad = 4_100m,
                UtilizationPct = 82.0m,
                ReliabilityPct = 99.0m,
                RiskScore = 20m,
                RiskLevel = "LOW",
                AvailabilityPct = 100m,
                DailyRevenueExposureUsd = dailyRev,
                OperationalStatus = "ONLINE"
            };
            assets.Add(asset);

            customers.Add(new Customer
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                AssetId = asset.Id,
                CustomerCode = code,
                Name = asset.Name,
                Segment = i <= 10 ? "Strategic Aerospace & Energy OEM" : "Industrial Automation Tier-1",
                Region = market.Region,
                Location = market.Region,
                MarketCode = market.MarketCode,
                ContractSlaPct = 96.5m + (i % 3) * 1.0m,
                DailyRevenueUsd = dailyRev,
                SlaBreachPenaltyPerDayUsd = Math.Round(dailyRev * 0.12m, 0)
            });
        }

        // 7. 100 TRANSPORTATION ROUTES
        for (int i = 1; i <= 100; i++)
        {
            var code = $"TRN-{i:D3}";
            var fac = factories[(i - 1) % factories.Count];
            var wh = warehouses[(i - 1) % warehouses.Count];
            var mode = (i % 4) switch
            {
                0 => "Air Freight Express Corridor",
                1 => "Dedicated Rail Freight Line",
                2 => "Maritime Container Corridor",
                _ => "Heavy Intermodal Highway"
            };
            decimal cap = 4_500m + (i * 65m);
            decimal util = 81.0m;
            decimal load = Math.Round(cap * util / 100m, 0);

            var asset = new Asset
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                AssetCode = code,
                Name = $"Corridor {code}: {fac.FactoryCode} -> {wh.WarehouseCode} ({mode})",
                AssetType = nameof(EnterpriseAssetType.TransportationRoute),
                Region = fac.Region,
                Location = $"{fac.Location} -> {wh.Location}",
                Capacity = cap,
                CurrentLoad = load,
                UtilizationPct = util,
                UnitCostUsd = 14.2m + (i % 9) * 1.8m,
                ReliabilityPct = 93.5m,
                LeadTimeDays = 2 + (i % 6),
                RiskScore = 34m + (i % 35),
                RiskLevel = (34 + (i % 35)) >= 60 ? "HIGH" : "MEDIUM",
                AvailabilityPct = 100m,
                DailyRevenueExposureUsd = 390_000m,
                OperationalStatus = "ONLINE"
            };
            assets.Add(asset);

            routes.Add(new TransportationRoute
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                AssetId = asset.Id,
                RouteCode = code,
                Name = asset.Name,
                OriginAssetCode = fac.FactoryCode,
                DestinationAssetCode = wh.WarehouseCode,
                TransportMode = mode,
                CapacityUnitsPerDay = cap,
                CurrentLoadUnitsPerDay = load,
                UtilizationPct = util,
                TransitLeadTimeDays = asset.LeadTimeDays,
                CostPerUnitUsd = asset.UnitCostUsd,
                ReliabilityPct = asset.ReliabilityPct,
                RiskScore = asset.RiskScore,
                AvailabilityPct = 100m
            });
        }

        // 8. 500 PRODUCTS
        var categories = new[]
        {
            "Autonomous Control Modules",
            "High-Voltage SiC Inverters",
            "Precision Robotic Actuators",
            "Industrial Sensor Arrays",
            "Grid Stabilization Turbines"
        };
        for (int i = 1; i <= 500; i++)
        {
            var sku = $"PRD-{i:D4}";
            var facPrimary = factories[(i - 1) % factories.Count];
            var facSecondary = factories[i % factories.Count];
            decimal price = 320m + (i % 40) * 24m;
            decimal cost = Math.Round(price * 0.58m, 2);
            decimal demand = 85m + (i % 30) * 6m;

            products.Add(new Product
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                Sku = sku,
                Name = $"NEXUS {categories[i % categories.Length]} Series-{i:D4}",
                Category = categories[i % categories.Length],
                UnitPriceUsd = price,
                UnitManufacturingCostUsd = cost,
                DailyDemandUnits = demand,
                TargetSafetyStockDays = 14 + (i % 10),
                CriticalityScore = 70m + (i % 30),
                PrimaryFactoryCode = facPrimary.FactoryCode,
                SecondaryFactoryCode = facSecondary.FactoryCode
            });
        }

        // 9. 100 BUSINESS PROCESSES
        for (int i = 1; i <= 100; i++)
        {
            processes.Add(new BusinessProcess
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                ProcessCode = $"PRC-{i:D3}",
                Name = $"Enterprise Operational Process PRC-{i:D3}",
                Category = i % 3 == 0 ? "Procurement & Supplier QA" : i % 2 == 0 ? "Autonomous Production Scheduling" : "Global Logistics & Customs Clearance",
                OwnerDepartment = "Global Operations Control",
                CriticalityScore = 75m + (i % 25),
                TargetSlaHours = 12 + (i % 24),
                HourlyFailureCostUsd = 28_000m + (i * 450m),
                AutomationPct = 82m,
                AvailabilityPct = 99.4m
            });
        }

        // 10. 20 EMPLOYEE GROUPS, 10 APPLICATIONS, 10 INFRASTRUCTURE NODES
        for (int i = 1; i <= 20; i++)
        {
            employees.Add(new EmployeeGroup
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                GroupCode = $"EMP-{i:D3}",
                Department = "Manufacturing & Robotics Engineering",
                RoleSpecialization = "Precision Assembly & Automation Operations",
                AssignedFacilityCode = factories[(i - 1) % factories.Count].FactoryCode,
                Location = factories[(i - 1) % factories.Count].Location,
                Headcount = 180 + (i * 15),
                ActiveShiftUtilizationPct = 86m,
                AvailabilityPct = 96.5m,
                DailyLaborCostUsd = 54_000m + (i * 2_200m)
            });
        }

        for (int i = 1; i <= 10; i++)
        {
            apps.Add(new EnterpriseApplication
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                AppCode = $"APP-{i:D3}",
                Name = $"NEXUS Enterprise Core System APP-{i:D3}",
                Tier = "Tier-1 Mission Critical",
                AvailabilityPct = 99.96m,
                RtoHours = 0.5m,
                DependentProcessesCount = 10 + i,
                DailyOperatingCostUsd = 9_500m + (i * 800m)
            });

            infraNodes.Add(new InfrastructureNode
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                InfraCode = $"INF-{i:D3}",
                Name = $"Industrial Telemetry & Compute Cluster INF-{i:D3}",
                NodeType = "Hybrid Cloud / Factory Edge Cluster",
                Region = regions[(i - 1) % regions.Length],
                CapacityUtilizationPct = 71m,
                RedundancyLevel = "N+2 Active-Active",
                HealthScorePct = 98.8m
            });
        }

        // 11. 1,050+ DEPENDENCIES (Supplier -> Factory -> Warehouse -> DC -> Route -> Market -> Customer + cross links)
        var assetByCode = assets.ToDictionary(a => a.AssetCode);

        void AddDep(string srcCode, string dstCode, string depType, decimal strength, decimal cap, decimal risk, decimal cost, int leadDays, bool spof = false, bool crit = false)
        {
            var src = assetByCode[srcCode];
            var dst = assetByCode[dstCode];
            dependencies.Add(new Dependency
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                SourceAssetId = src.Id,
                SourceAssetCode = src.AssetCode,
                SourceAssetName = src.Name,
                SourceAssetType = src.AssetType,
                TargetAssetId = dst.Id,
                TargetAssetCode = dst.AssetCode,
                TargetAssetName = dst.Name,
                TargetAssetType = dst.AssetType,
                DependencyType = depType,
                Strength = strength,
                CapacityUnitsPerDay = cap,
                CurrentFlowUnitsPerDay = Math.Round(cap * 0.82m, 0),
                RiskScore = risk,
                CostPerUnitUsd = cost,
                LeadTimeDays = leadDays,
                IsSinglePointOfFailure = spof,
                IsOnCriticalPath = crit
            });
        }

        // Hero critical path: SUP-001 -> FAC-001 & FAC-002 -> WH-001..WH-008 -> DC-001..DC-004 -> MKT-001..MKT-006 -> CUST-001..CUST-012
        AddDep("SUP-001", "FAC-001", "PrimaryComponentSupply", 0.96m, 8_500m, 78m, 44m, 4, spof: true, crit: true);
        AddDep("SUP-001", "FAC-002", "SecondaryComponentSupply", 0.84m, 5_500m, 64m, 45m, 5, spof: false, crit: true);
        AddDep("SUP-003", "FAC-002", "BackupQualifiedSupply", 0.91m, 7_200m, 22m, 47m, 4, spof: false, crit: true);
        AddDep("SUP-002", "FAC-001", "WaferSubassemblySupply", 0.88m, 6_000m, 52m, 49m, 5, spof: false, crit: true);

        // Connect all 50 suppliers to factories (300 edges)
        for (int s = 1; s <= 50; s++)
        {
            var supCode = $"SUP-{s:D3}";
            for (int k = 0; k < 6; k++)
            {
                var facIdx = ((s + k * 3 - 1) % 20) + 1;
                var facCode = $"FAC-{facIdx:D3}";
                if (s == 1 && (facIdx == 1 || facIdx == 2)) continue;
                AddDep(supCode, facCode, "RawMaterialAndSubassembly", 0.72m + (k * 0.04m), 3_200m + (s * 40m), 32m + (s % 40), 38m + k, 4 + (s % 6), spof: s <= 3 && k == 0, crit: s <= 5 && k <= 1);
            }
        }

        // Connect all 20 factories to warehouses (240 edges)
        for (int f = 1; f <= 20; f++)
        {
            var facCode = $"FAC-{f:D3}";
            for (int k = 0; k < 12; k++)
            {
                var whIdx = ((f * 2 + k - 1) % 40) + 1;
                var whCode = $"WH-{whIdx:D3}";
                AddDep(facCode, whCode, "FinishedGoodsReplenishment", 0.85m, 2_800m, f <= 2 ? 65m : 35m, 18m, 3, spof: f == 1 && k == 0, crit: f <= 3 && k <= 2);
            }
        }

        // Connect 40 warehouses to 10 DCs (160 edges)
        for (int w = 1; w <= 40; w++)
        {
            var whCode = $"WH-{w:D3}";
            for (int k = 0; k < 4; k++)
            {
                var dcIdx = ((w + k - 1) % 10) + 1;
                var dcCode = $"DC-{dcIdx:D3}";
                AddDep(whCode, dcCode, "RegionalDistributionFlow", 0.80m, 4_100m, 30m, 9.5m, 2, spof: false, crit: w <= 6 && k == 0);
            }
        }

        // Connect 10 DCs to 20 Markets (80 edges)
        for (int d = 1; d <= 10; d++)
        {
            var dcCode = $"DC-{d:D3}";
            for (int k = 0; k < 8; k++)
            {
                var mktIdx = ((d * 2 + k - 1) % 20) + 1;
                var mktCode = $"MKT-{mktIdx:D3}";
                AddDep(dcCode, mktCode, "MarketFulfillmentAllocation", 0.86m, 3_600m, 28m, 11m, 2, spof: false, crit: d <= 3 && k <= 1);
            }
        }

        // Connect 20 Markets to 30 Customers (120 edges)
        for (int m = 1; m <= 20; m++)
        {
            var mktCode = $"MKT-{m:D3}";
            for (int k = 0; k < 6; k++)
            {
                var custIdx = ((m + k * 3 - 1) % 30) + 1;
                var custCode = $"CUST-{custIdx:D3}";
                AddDep(mktCode, custCode, "CustomerOrderDelivery", 0.90m, 1_900m, 25m, 7.5m, 1, spof: false, crit: m <= 4 && k == 0);
            }
        }

        // Connect 100 TransportationRoutes between Factories and Warehouses (200 edges)
        for (int r = 1; r <= 100; r++)
        {
            var trn = routes[r - 1];
            AddDep(trn.OriginAssetCode, trn.RouteCode, "OutboundFreightDispatch", 0.78m, trn.CapacityUnitsPerDay, trn.RiskScore, trn.CostPerUnitUsd, trn.TransitLeadTimeDays);
            AddDep(trn.RouteCode, trn.DestinationAssetCode, "InboundFreightArrival", 0.78m, trn.CapacityUnitsPerDay, trn.RiskScore, trn.CostPerUnitUsd, trn.TransitLeadTimeDays);
        }

        db.Assets.AddRange(assets);
        db.Suppliers.AddRange(suppliers);
        db.Factories.AddRange(factories);
        db.Warehouses.AddRange(warehouses);
        db.DistributionCenters.AddRange(dcs);
        db.TransportationRoutes.AddRange(routes);
        db.Products.AddRange(products);
        db.Markets.AddRange(markets);
        db.Customers.AddRange(customers);
        db.BusinessProcesses.AddRange(processes);
        db.EmployeeGroups.AddRange(employees);
        db.EnterpriseApplications.AddRange(apps);
        db.InfrastructureNodes.AddRange(infraNodes);
        db.Dependencies.AddRange(dependencies);

        // Snapshots (Inventory, Demand, Capacity)
        for (int i = 0; i < 40; i++)
        {
            var wh = warehouses[i];
            var prd = products[i];
            db.InventorySnapshots.Add(new InventorySnapshot
            {
                OrganizationId = orgId,
                WarehouseId = wh.Id,
                WarehouseCode = wh.WarehouseCode,
                ProductId = prd.Id,
                ProductSku = prd.Sku,
                OnHandUnits = wh.CurrentInventoryUnits,
                SafetyStockUnits = Math.Round(wh.CurrentInventoryUnits * 0.35m, 0),
                ReorderPointUnits = Math.Round(wh.CurrentInventoryUnits * 0.50m, 0),
                DailyConsumptionUnits = wh.ProcessingRateUnitsPerDay,
                CoverageDays = 19m,
                HoldingCostPerDayUsd = wh.OperatingCostPerDayUsd
            });
        }

        for (int i = 0; i < 20; i++)
        {
            var mkt = markets[i];
            var prd = products[i];
            db.DemandSnapshots.Add(new DemandSnapshot
            {
                OrganizationId = orgId,
                MarketId = mkt.Id,
                MarketCode = mkt.MarketCode,
                ProductId = prd.Id,
                ProductSku = prd.Sku,
                ForecastedDemandUnits = mkt.DailyDemandUnits,
                ActualOrderVolumeUnits = Math.Round(mkt.DailyDemandUnits * 0.98m, 0),
                FulfilledUnits = Math.Round(mkt.DailyDemandUnits * 0.962m, 0),
                BackorderUnits = Math.Round(mkt.DailyDemandUnits * 0.018m, 0),
                ServiceLevelPct = 96.8m,
                RevenueUsd = mkt.DailyRevenuePotentialUsd
            });

            var fac = factories[i];
            db.CapacitySnapshots.Add(new CapacitySnapshot
            {
                OrganizationId = orgId,
                AssetId = fac.AssetId,
                AssetCode = fac.FactoryCode,
                AssetType = nameof(EnterpriseAssetType.Factory),
                NominalCapacity = fac.CapacityUnitsPerDay,
                EffectiveCapacity = fac.CapacityUnitsPerDay,
                CurrentLoad = fac.CurrentLoadUnitsPerDay,
                UtilizationPct = fac.UtilizationPct,
                SpareCapacityUnits = fac.CapacityUnitsPerDay - fac.CurrentLoadUnitsPerDay
            });
        }

        // 14 Active Risks (matching Section 27 Command Center display: ACTIVE RISKS = 14)
        var riskTitles = new (string Code, string Title, string Cat, string Sev, string AssetCode, decimal Prob, decimal Impact, decimal Exposure)[]
        {
            ("RSK-001", "European Tier-1 Supplier Capacity Degradation (RheinMetall SUP-001)", "Supplier Continuity", "CRITICAL", "SUP-001", 68m, 92m, 8_700_000m),
            ("RSK-002", "Stuttgart Primary Assembly (FACTORY A) 92% Saturation Bottleneck", "Manufacturing Capacity", "HIGH", "FAC-001", 62m, 85m, 2_200_000m),
            ("RSK-003", "SiC Wafer Lead-Time Volatility in Alpine Corridor", "Raw Material Shortage", "HIGH", "SUP-002", 54m, 76m, 1_850_000m),
            ("RSK-004", "Rhine Intermodal Rail Freight Congestion", "Transportation", "MEDIUM", "TRN-001", 49m, 64m, 920_000m),
            ("RSK-005", "Frankfurt Hub Warehouse WH-001 Safety Buffer Drawdown", "Inventory", "HIGH", "WH-001", 57m, 71m, 1_420_000m),
            ("RSK-006", "Industrial Grid Energy Tariff Spike (+25% European Winter Forecast)", "Energy & Utilities", "MEDIUM", "FAC-002", 46m, 60m, 780_000m),
            ("RSK-007", "Precision Robotics Specialist Shift Overtime Limit", "Workforce", "MEDIUM", "FAC-003", 41m, 55m, 540_000m),
            ("RSK-008", "Red Sea Maritime Container Rerouting Transit Delay (+6 Days)", "Geopolitical", "HIGH", "TRN-003", 64m, 78m, 1_650_000m),
            ("RSK-009", "Customer SLA Penalty Threshold Exposure (CUST-001 Aerospace OEM)", "SLA Compliance", "HIGH", "CUST-001", 48m, 82m, 1_120_000m),
            ("RSK-010", "East Asia Foundry Sub-Tier Packaging Bottleneck", "Supplier Continuity", "MEDIUM", "SUP-022", 44m, 66m, 890_000m),
            ("RSK-011", "EUR/USD Foreign Exchange Volatility on Component Contracts", "Financial", "LOW", "MKT-001", 38m, 45m, 410_000m),
            ("RSK-012", "Cold-Chain Telemetry Latency in Edge Cluster INF-002", "Infrastructure", "LOW", "WH-004", 29m, 42m, 310_000m),
            ("RSK-013", "Single-Point-of-Failure Dependency SUP-001 -> FAC-001", "Dependency Topology", "CRITICAL", "SUP-001", 72m, 90m, 4_200_000m),
            ("RSK-014", "Q4 Demand Surge (+30%) on Autonomous Control Units", "Demand Volatility", "MEDIUM", "MKT-002", 52m, 69m, 1_250_000m)
        };

        foreach (var r in riskTitles)
        {
            assetByCode.TryGetValue(r.AssetCode, out var affAsset);
            db.Risks.Add(new Risk
            {
                OrganizationId = orgId,
                RiskCode = r.Code,
                Title = r.Title,
                Category = r.Cat,
                Severity = r.Sev,
                ProbabilityPct = r.Prob,
                ImpactScore = r.Impact,
                CompositeRiskScore = Math.Round((r.Prob * r.Impact) / 100m, 1),
                AffectedAssetId = affAsset?.Id,
                AffectedAssetCode = r.AssetCode,
                AffectedAssetName = affAsset?.Name ?? r.AssetCode,
                RevenueExposureUsd = r.Exposure,
                Status = "ACTIVE",
                RecommendedMitigation = "Reallocate production load to Factory B (Dresden) and activate Supplier C (Nordic Silicon Dynamics)."
            });
        }

        // 3 Active Scenarios (matching Section 27 Command Center display: ACTIVE SCENARIOS = 3)
        var heroSupplierAsset = assetByCode["SUP-001"];
        var heroScenario = new Scenario
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            ScenarioCode = "SCN-HERO-001",
            Name = "Hero Scenario: European Supplier 40% Capacity Loss (14 Days)",
            ScenarioType = ScenarioTypes.SupplierFailure,
            Description = "European Tier-1 supplier (RheinMetall Precision Components GmbH, SUP-001) loses 40% capacity (100% -> 60%) for 14 days due to furnace & cleanroom sub-system outage.",
            TargetAssetId = heroSupplierAsset.Id,
            TargetAssetCode = "SUP-001",
            TargetAssetName = heroSupplierAsset.Name,
            DurationDays = 14,
            SupplierCapacityMultiplierPct = 60m,
            DemandDeltaPct = 0m,
            TransportCostDeltaPct = 10m,
            FactoryCapacityDeltaPct = 0m,
            EnergyCostDeltaPct = 0m,
            IsHeroScenario = true,
            IsBlackSwanScenario = false,
            Status = "ACTIVE",
            CreatedByUser = "Chief Supply Chain Officer",
            Parameters = new List<ScenarioParameter>
            {
                new()
                {
                    OrganizationId = orgId,
                    ParameterName = "Supplier Capacity",
                    TargetAssetCode = "SUP-001",
                    BaselineValue = 100m,
                    ScenarioValue = 60m,
                    DeltaPercent = -40m,
                    Unit = "%"
                },
                new()
                {
                    OrganizationId = orgId,
                    ParameterName = "Disruption Duration",
                    TargetAssetCode = "SUP-001",
                    BaselineValue = 0m,
                    ScenarioValue = 14m,
                    DeltaPercent = 100m,
                    Unit = "days"
                }
            }
        };

        var scenario2 = new Scenario
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            ScenarioCode = "SCN-002",
            Name = "Supplier A 50% Capacity + 30% Demand Spike + 20% Transport Cost",
            ScenarioType = ScenarioTypes.DemandSpike,
            Description = "Compound stress scenario from Section 12: Supplier capacity 100% -> 50%, Demand +30%, Transportation Cost +20%.",
            TargetAssetId = heroSupplierAsset.Id,
            TargetAssetCode = "SUP-001",
            TargetAssetName = heroSupplierAsset.Name,
            DurationDays = 21,
            SupplierCapacityMultiplierPct = 50m,
            DemandDeltaPct = 30m,
            TransportCostDeltaPct = 20m,
            Status = "ACTIVE",
            Parameters = new List<ScenarioParameter>
            {
                new() { OrganizationId = orgId, ParameterName = "Supplier Capacity", TargetAssetCode = "SUP-001", BaselineValue = 100m, ScenarioValue = 50m, DeltaPercent = -50m, Unit = "%" },
                new() { OrganizationId = orgId, ParameterName = "Demand", TargetAssetCode = "MKT-001", BaselineValue = 100m, ScenarioValue = 130m, DeltaPercent = 30m, Unit = "%" },
                new() { OrganizationId = orgId, ParameterName = "Transportation Cost", TargetAssetCode = "TRN-001", BaselineValue = 100m, ScenarioValue = 120m, DeltaPercent = 20m, Unit = "%" }
            }
        };

        var scenario3 = new Scenario
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            ScenarioCode = "SCN-BLACKSWAN-003",
            Name = "Black Swan Multi-Vector Stress Test (Simultaneous Global Shock)",
            ScenarioType = ScenarioTypes.GeopoliticalDisruption,
            Description = "Simultaneous disruption across Supplier Capacity (-40%), Demand (+35%), Transport Capacity (-20%), Energy Cost (+25%), Factory Capacity (-15%).",
            TargetAssetId = heroSupplierAsset.Id,
            TargetAssetCode = "SUP-001",
            TargetAssetName = heroSupplierAsset.Name,
            DurationDays = 30,
            SupplierCapacityMultiplierPct = 60m,
            DemandDeltaPct = 35m,
            TransportCostDeltaPct = 20m,
            FactoryCapacityDeltaPct = -15m,
            EnergyCostDeltaPct = 25m,
            IsBlackSwanScenario = true,
            Status = "ACTIVE"
        };

        db.Scenarios.AddRange(heroScenario, scenario2, scenario3);

        // Enterprise State Snapshot (matching Section 11 & Section 27 Command Center metrics)
        db.EnterpriseStateSnapshots.Add(new EnterpriseStateSnapshot
        {
            OrganizationId = orgId,
            DailyDemandUnits = 94_500m,
            TotalInventoryUnits = 1_795_500m,
            InventoryCoverageDays = 19.0m,
            TotalCapacityUnits = 232_000m,
            SupplierHealthPct = 91.0m,
            FactoryUtilizationPct = 87.0m,
            WarehouseUtilizationPct = 72.0m,
            TransportCapacityPct = 81.0m,
            DailyOrderVolumeUnits = 92_800m,
            DailyRevenueUsd = 50_680_000m,
            DailyCostUsd = 31_420_000m,
            RevenueAtRiskUsd = 12_400_000m,
            OverallRiskScore = 44.2m,
            ServiceLevelPct = 96.8m,
            ResilienceScore = 87.0m,
            ActiveRisksCount = 14,
            ActiveScenariosCount = 3,
            CapturedAtUtc = DateTime.UtcNow
        });

        // Resilience Score (Section 25)
        db.ResilienceScores.Add(new ResilienceScore
        {
            OrganizationId = orgId,
            OverallScore = 87.0m,
            SupplierDiversityScore = 84.0m,
            CapacityRedundancyScore = 88.0m,
            InventoryBufferScore = 85.0m,
            DependencyRiskScore = 81.0m,
            TransportationRedundancyScore = 89.0m,
            RecoveryCapabilityScore = 86.0m,
            ServiceLevelScore = 96.0m,
            OperationalFlexibilityScore = 87.0m,
            SimulatedCurrentDisruptedScore = 82.0m,
            SimulatedAfterMitigationScore = 94.0m,
            CalculatedAtUtc = DateTime.UtcNow
        });

        // Optimal Mitigation Planner Investments (Section 26)
        var mitigations = new[]
        {
            new Mitigation
            {
                OrganizationId = orgId,
                MitigationCode = "MIT-001",
                Name = "Qualify Dual-Source SiC Supplier in Sweden (Nordic Silicon Tier-1 Expansion)",
                InvestmentType = "Add Supplier",
                Description = "Onboard and reserve 6,500 units/day guaranteed capacity with Supplier C (SUP-003) to eliminate single-point dependency on SUP-001.",
                InvestmentCostUsd = 680_000m,
                RiskReductionPct = 48.5m,
                AnnualRevenueProtectedUsd = 6_400_000m,
                ResiliencePointsGain = 6.2m,
                RoiMultiple = 9.41m,
                IsOptimalSelection = true
            },
            new Mitigation
            {
                OrganizationId = orgId,
                MitigationCode = "MIT-002",
                Name = "Activate Dresden Factory B Modular Line +4,000 Units/Day Flex Capacity",
                InvestmentType = "Add Factory Capacity",
                Description = "Commission automated SMT & calibration line at Factory B (Dresden) to absorb 42% production shift from Stuttgart Factory A.",
                InvestmentCostUsd = 850_000m,
                RiskReductionPct = 44.0m,
                AnnualRevenueProtectedUsd = 7_900_000m,
                ResiliencePointsGain = 5.8m,
                RoiMultiple = 9.29m,
                IsOptimalSelection = true
            },
            new Mitigation
            {
                OrganizationId = orgId,
                MitigationCode = "MIT-003",
                Name = "Increase Strategic Semiconductor Safety Buffer (+7 Days Coverage)",
                InvestmentType = "Increase Inventory",
                Description = "Expand critical component buffer across WH-001 through WH-008 from 19 days to 26 days coverage.",
                InvestmentCostUsd = 520_000m,
                RiskReductionPct = 36.0m,
                AnnualRevenueProtectedUsd = 4_100_000m,
                ResiliencePointsGain = 4.1m,
                RoiMultiple = 7.88m,
                IsOptimalSelection = true
            },
            new Mitigation
            {
                OrganizationId = orgId,
                MitigationCode = "MIT-004",
                Name = "Establish Dedicated Alpine Rail-Air Intermodal Bypass Corridor",
                InvestmentType = "Add Transportation Route",
                Description = "Contract priority rail-air freight capacity reducing transit lead time by 2.5 days during European corridor disruptions.",
                InvestmentCostUsd = 390_000m,
                RiskReductionPct = 29.0m,
                AnnualRevenueProtectedUsd = 2_850_000m,
                ResiliencePointsGain = 3.2m,
                RoiMultiple = 7.31m,
                IsOptimalSelection = true
            },
            new Mitigation
            {
                OrganizationId = orgId,
                MitigationCode = "MIT-005",
                Name = "Lease High-Throughput Bonded Warehouse Facility in Eindhoven",
                InvestmentType = "Add Warehouse",
                Description = "Add 35,000 units regional buffer capacity adjacent to Factory C.",
                InvestmentCostUsd = 1_150_000m,
                RiskReductionPct = 24.5m,
                AnnualRevenueProtectedUsd = 3_200_000m,
                ResiliencePointsGain = 2.6m,
                RoiMultiple = 2.78m,
                IsOptimalSelection = false
            },
            new Mitigation
            {
                OrganizationId = orgId,
                MitigationCode = "MIT-006",
                Name = "N+2 Cross-Plant Tooling & Die Redundancy Program",
                InvestmentType = "Increase Redundancy",
                Description = "Replicate proprietary tooling dies across Stuttgart, Dresden, and Eindhoven factories.",
                InvestmentCostUsd = 740_000m,
                RiskReductionPct = 33.0m,
                AnnualRevenueProtectedUsd = 4_400_000m,
                ResiliencePointsGain = 3.9m,
                RoiMultiple = 5.95m,
                IsOptimalSelection = false
            },
            new Mitigation
            {
                OrganizationId = orgId,
                MitigationCode = "MIT-007",
                Name = "Deploy Sub-Tier IoT Vibration & Cleanroom Telemetry Sensors",
                InvestmentType = "Improve Monitoring",
                Description = "Real-time predictive anomaly detection on Tier-1 and Tier-2 European supplier furnace lines.",
                InvestmentCostUsd = 260_000m,
                RiskReductionPct = 27.5m,
                AnnualRevenueProtectedUsd = 2_550_000m,
                ResiliencePointsGain = 3.0m,
                RoiMultiple = 9.81m,
                IsOptimalSelection = true
            }
        };
        db.Mitigations.AddRange(mitigations);

        // Initial Hero SimulationRun, OptimizationRun, Decision, Explanation, Execution, Outcome, Quality
        var simRun = new SimulationRun
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            ScenarioId = heroScenario.Id,
            RunCode = "SIM-HERO-001",
            ScenarioName = heroScenario.Name,
            TargetAssetCode = "SUP-001",
            HorizonDays = 14,
            MonteCarloIterations = 10_000,
            Status = "COMPLETED",
            DeterministicRevenueLossUsd = 9_120_000m,
            DeterministicServiceLevelPct = 88.4m,
            FirstStockoutDay = 6,
            RecoveryTimeDays = 14.0m,
            OperationalCostImpactUsd = 1_480_000m,
            MonteCarloMeanRevenueLossUsd = 9_120_000m,
            MonteCarloP50RevenueLossUsd = 8_940_000m,
            MonteCarloP95RevenueLossUsd = 11_680_000m,
            MonteCarloP99RevenueLossUsd = 13_150_000m,
            ProbabilityOfStockoutPct = 8.4m,
            ProbabilityRevenueLossOver1MPct = 5.7m,
            ProbabilitySlaBelow95Pct = 3.2m,
            ExpectedServiceLevelPct = 91.4m,
            ExpectedRecoveryDays = 11.5m,
            ExpectedOperationalCostUsd = 1_500_000m,
            HistogramBucketsJson = JsonSerializer.Serialize(new[]
            {
                new { Bucket = "$0M-$2M", Count = 920, ProbabilityPct = 9.2 },
                new { Bucket = "$2M-$4M", Count = 1840, ProbabilityPct = 18.4 },
                new { Bucket = "$4M-$6M", Count = 2610, ProbabilityPct = 26.1 },
                new { Bucket = "$6M-$8M", Count = 2290, ProbabilityPct = 22.9 },
                new { Bucket = "$8M-$10M", Count = 1470, ProbabilityPct = 14.7 },
                new { Bucket = "$10M-$12M", Count = 620, ProbabilityPct = 6.2 },
                new { Bucket = ">$12M", Count = 250, ProbabilityPct = 2.5 }
            }),
            PropagationChainJson = JsonSerializer.Serialize(new[]
            {
                new { Step = 1, AssetCode = "SUP-001", AssetName = "Supplier A (RheinMetall)", Impact = "Capacity reduced by 40% (15,000 -> 9,000 units/day)", LossUsd = 0 },
                new { Step = 2, AssetCode = "FAC-001", AssetName = "Factory A (Stuttgart)", Impact = "Material deficit of 3,400 units/day; utilization strained at 92%", LossUsd = 2_200_000 },
                new { Step = 3, AssetCode = "WH-001", AssetName = "Warehouse WH-001 (Frankfurt)", Impact = "Safety stock depleted by Day 6; coverage drops from 19d to 4d", LossUsd = 3_150_000 },
                new { Step = 4, AssetCode = "MKT-001", AssetName = "European Industrial Market", Impact = "Order fulfillment delayed by 4.5 days across 8 warehouses", LossUsd = 3_770_000 }
            }),
            Results = new List<SimulationResult>
            {
                new()
                {
                    OrganizationId = orgId,
                    DayNumber = 1,
                    AffectedAssetCode = "SUP-001",
                    AffectedAssetName = "Supplier A — RheinMetall Precision Components GmbH",
                    AssetType = "Supplier",
                    HopDepth = 0,
                    EffectiveCapacityPct = 60.0m,
                    InventoryRemainingUnits = 42_000m,
                    UnfulfilledDemandUnits = 0m,
                    DailyRevenueLossUsd = 180_000m,
                    ServiceLevelPct = 97.2m,
                    CascadeDescription = "Supplier A capacity reduced by 40% (6,000 units/day shortfall)."
                },
                new()
                {
                    OrganizationId = orgId,
                    DayNumber = 4,
                    AffectedAssetCode = "FAC-001",
                    AffectedAssetName = "FACTORY A — Stuttgart Primary Assembly Complex",
                    AssetType = "Factory",
                    HopDepth = 1,
                    EffectiveCapacityPct = 68.5m,
                    InventoryRemainingUnits = 24_500m,
                    UnfulfilledDemandUnits = 1_450m,
                    DailyRevenueLossUsd = 640_000m,
                    ServiceLevelPct = 93.4m,
                    CascadeDescription = "Factory A receives less material; assembly output decreases by 31.5%."
                },
                new()
                {
                    OrganizationId = orgId,
                    DayNumber = 7,
                    AffectedAssetCode = "WH-001",
                    AffectedAssetName = "Regional Hub Warehouse WH-001 (Frankfurt)",
                    AssetType = "Warehouse",
                    HopDepth = 2,
                    EffectiveCapacityPct = 54.0m,
                    InventoryRemainingUnits = 8_200m,
                    UnfulfilledDemandUnits = 2_890m,
                    DailyRevenueLossUsd = 1_120_000m,
                    ServiceLevelPct = 89.8m,
                    CascadeDescription = "Warehouse inventory buffer breached; customer orders delayed."
                },
                new()
                {
                    OrganizationId = orgId,
                    DayNumber = 14,
                    AffectedAssetCode = "CUST-001",
                    AffectedAssetName = "Global Industrial Partner CUST-001",
                    AssetType = "Customer",
                    HopDepth = 3,
                    EffectiveCapacityPct = 61.0m,
                    InventoryRemainingUnits = 3_900m,
                    UnfulfilledDemandUnits = 3_620m,
                    DailyRevenueLossUsd = 1_480_000m,
                    ServiceLevelPct = 88.4m,
                    CascadeDescription = "Cumulative unmitigated revenue exposure reaches $9.12M over 14-day window."
                }
            }
        };
        db.SimulationRuns.Add(simRun);

        var optRun = new OptimizationRun
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            ScenarioId = heroScenario.Id,
            SimulationRunId = simRun.Id,
            RunCode = "OPT-HERO-001",
            SolverEngine = "Google OR-Tools GLOP Linear & Mixed-Integer Solver",
            SolverStatus = "OPTIMAL",
            ObjectiveValue = 96.42m,
            OptimalCostUsd = 1_500_000m,
            RevenueProtectedUsd = 8_700_000m,
            ResultingServiceLevelPct = 98.2m,
            RiskReductionPct = 63.0m,
            ResultingResilienceScore = 94.0m,
            SolveTimeMs = 48,
            Constraints = new List<OptimizationConstraint>
            {
                new() { OrganizationId = orgId, ConstraintName = "Factory B Spare Capacity Limit", Category = "Factory Capacity", LowerBound = 0m, UpperBound = 4_680m, EvaluatedActivity = 4_200m, SlackValue = 480m, ShadowPrice = 142.5m, IsBinding = false, Unit = "units/day" },
                new() { OrganizationId = orgId, ConstraintName = "Supplier C (SUP-003) Max Emergency Allocation", Category = "Supplier Capacity", LowerBound = 0m, UpperBound = 4_500m, EvaluatedActivity = 4_500m, SlackValue = 0m, ShadowPrice = 215.0m, IsBinding = true, Unit = "units/day" },
                new() { OrganizationId = orgId, ConstraintName = "Enterprise Minimum SLA Floor", Category = "SLA", LowerBound = 95.0m, UpperBound = 100.0m, EvaluatedActivity = 98.2m, SlackValue = 3.2m, ShadowPrice = 0m, IsBinding = false, Unit = "%" },
                new() { OrganizationId = orgId, ConstraintName = "Emergency Mitigation Budget Ceiling", Category = "Budget", LowerBound = 0m, UpperBound = 2_200_000m, EvaluatedActivity = 1_500_000m, SlackValue = 700_000m, ShadowPrice = 0m, IsBinding = false, Unit = "USD" }
            },
            Results = new List<OptimizationResult>
            {
                new()
                {
                    OrganizationId = orgId,
                    StrategyCode = "Strategy A",
                    StrategyName = "Strategy A — Minimum Cost Buffer Drawdown",
                    CandidateActionType = CandidateActionTypes.ReallocateInventory,
                    PrimaryAssetCode = "WH-001",
                    SecondaryAssetCode = "FAC-001",
                    ReallocationPct = 20.0m,
                    AllocatedUnitsPerDay = 2_000m,
                    TotalStrategyCostUsd = 1_200_000m,
                    AdditionalCostUsd = 180_000m,
                    RevenueProtectedUsd = 5_400_000m,
                    RiskReductionPct = 31.0m,
                    RiskLevel = "HIGH",
                    ResultingServiceLevelPct = 88.0m,
                    ResilienceScore = 84.0m,
                    IsParetoFrontierMember = true,
                    IsRecommended = false
                },
                new()
                {
                    OrganizationId = orgId,
                    StrategyCode = "Strategy B",
                    StrategyName = "Strategy B — Move 42% Production to Factory B + Activate Supplier C",
                    CandidateActionType = CandidateActionTypes.MoveProduction,
                    PrimaryAssetCode = "FAC-002",
                    SecondaryAssetCode = "SUP-003",
                    ReallocationPct = 42.0m,
                    AllocatedUnitsPerDay = 4_200m,
                    TotalStrategyCostUsd = 1_500_000m,
                    AdditionalCostUsd = 420_000m,
                    RevenueProtectedUsd = 8_700_000m,
                    RiskReductionPct = 63.0m,
                    RiskLevel = "MEDIUM",
                    ResultingServiceLevelPct = 98.2m,
                    ResilienceScore = 94.0m,
                    IsParetoFrontierMember = true,
                    IsRecommended = true
                },
                new()
                {
                    OrganizationId = orgId,
                    StrategyCode = "Strategy C",
                    StrategyName = "Strategy C — Full Multi-Plant Overdrive + Dedicated Air Freight",
                    CandidateActionType = CandidateActionTypes.ExpediteShipment,
                    PrimaryAssetCode = "FAC-002",
                    SecondaryAssetCode = "FAC-003",
                    ReallocationPct = 65.0m,
                    AllocatedUnitsPerDay = 6_000m,
                    TotalStrategyCostUsd = 1_900_000m,
                    AdditionalCostUsd = 890_000m,
                    RevenueProtectedUsd = 8_980_000m,
                    RiskReductionPct = 78.0m,
                    RiskLevel = "LOW",
                    ResultingServiceLevelPct = 99.0m,
                    ResilienceScore = 96.5m,
                    IsParetoFrontierMember = true,
                    IsRecommended = false
                }
            }
        };
        db.OptimizationRuns.Add(optRun);

        var replayTimeline = new[]
        {
            new { Time = "09:00", Event = "Supplier failure detected", Detail = "European Supplier A (SUP-001) reports 40% capacity loss for 14 days." },
            new { Time = "09:02", Event = "Enterprise impact calculated", Detail = "Dependency graph traversal identified 26 downstream nodes and $9.12M unmitigated revenue exposure." },
            new { Time = "09:04", Event = "147 candidate strategies generated", Detail = "Autonomous Decision Engine enumerated supplier switches, production shifts, and buffer reallocations." },
            new { Time = "09:06", Event = "10,000 simulations completed", Detail = "Monte Carlo engine evaluated stochastic demand, lead-time, and reliability distributions." },
            new { Time = "09:07", Event = "Optimization completed", Detail = "Google OR-Tools solved multi-objective MILP across Pareto frontier (Strategies A, B, C)." },
            new { Time = "09:08", Event = "Strategy B recommended", Detail = "Move 42% of affected production to Factory B (Dresden) and source from Supplier C (97% reliability)." },
            new { Time = "09:10", Event = "Manager approved", Detail = "Operations Manager verified explainability proof and approved Strategy B." },
            new { Time = "09:20", Event = "Execution initiated", Detail = "ERP/MES work orders dispatched to Factory B and Supplier C; resilience increased 82 -> 94." }
        };

        var heroDecision = new Decision
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            DecisionCode = "DEC-HERO-001",
            Title = "Mitigate European Supplier A 40% Capacity Disruption (14-Day Horizon)",
            ScenarioId = heroScenario.Id,
            SimulationRunId = simRun.Id,
            OptimizationRunId = optRun.Id,
            RecommendedStrategyCode = "Strategy B",
            ProblemDetected = "Supplier A (SUP-001) lost 40% capacity for 14 days, starving Factory A (92% utilization) of 3,400 units/day and exposing $9.12M in downstream revenue.",
            WhatRecommendation = "Move 42% of production to Factory B and source 4,500 units/day from Supplier C (Nordic Silicon Dynamics).",
            WhyExplanation = "Factory A utilization: 92% (saturated). Factory B utilization: 61% effective post-shift window (78% nominal, 4,680 units/day spare capacity). Supplier C reliability: 97%. Factory B provides sufficient spare capacity with minimal unit cost premium.",
            ExpectedResultSummary = "Revenue Protected: $8.7M | Additional Cost: $420K | Service Level: 98.2% | Risk Reduction: 63%",
            CandidateStrategiesGenerated = 147,
            SimulationsCompleted = 10_000,
            RevenueProtectedUsd = 8_700_000m,
            AdditionalCostUsd = 420_000m,
            ProjectedServiceLevelPct = 98.2m,
            RiskReductionPct = 63.0m,
            ResilienceScoreBefore = 82.0m,
            ResilienceScoreAfter = 94.0m,
            Status = "APPROVED",
            RequiresHumanApproval = true,
            ReplayTimelineJson = JsonSerializer.Serialize(replayTimeline),
            Options = new List<DecisionOption>
            {
                new()
                {
                    OrganizationId = orgId,
                    StrategyCode = "Strategy A",
                    StrategyName = "Strategy A — Minimum Cost Buffer Drawdown",
                    ActionType = CandidateActionTypes.ReallocateInventory,
                    Description = "Draw down regional safety stock in WH-001..WH-004 without shifting factory production.",
                    CostUsd = 1_200_000m,
                    RiskLabel = "HIGH",
                    RiskScore = 68.0m,
                    ServiceLevelPct = 88.0m,
                    ResilienceScore = 84.0m,
                    RevenueProtectedUsd = 5_400_000m,
                    AdditionalCostUsd = 180_000m,
                    RiskReductionPct = 31.0m,
                    IsRecommended = false
                },
                new()
                {
                    OrganizationId = orgId,
                    StrategyCode = "Strategy B",
                    StrategyName = "Strategy B — Move 42% of production to Factory B",
                    ActionType = CandidateActionTypes.MoveProduction,
                    Description = "Move 42% of production to Factory B (Dresden) and source emergency allocation from Supplier C (97% reliability).",
                    CostUsd = 1_500_000m,
                    RiskLabel = "MEDIUM",
                    RiskScore = 29.0m,
                    ServiceLevelPct = 96.0m,
                    ResilienceScore = 94.0m,
                    RevenueProtectedUsd = 8_700_000m,
                    AdditionalCostUsd = 420_000m,
                    RiskReductionPct = 63.0m,
                    IsRecommended = true
                },
                new()
                {
                    OrganizationId = orgId,
                    StrategyCode = "Strategy C",
                    StrategyName = "Strategy C — Maximum Resilience Multi-Fab + Air Charter",
                    ActionType = CandidateActionTypes.ExpediteShipment,
                    Description = "Shift 65% load across Factory B & Factory C with dedicated air freight charter.",
                    CostUsd = 1_900_000m,
                    RiskLabel = "LOW",
                    RiskScore = 14.0m,
                    ServiceLevelPct = 99.0m,
                    ResilienceScore = 96.5m,
                    RevenueProtectedUsd = 8_980_000m,
                    AdditionalCostUsd = 890_000m,
                    RiskReductionPct = 78.0m,
                    IsRecommended = false
                }
            },
            Approvals = new List<DecisionApproval>
            {
                new()
                {
                    OrganizationId = orgId,
                    ReviewerName = "Marcus Vance (VP Global Operations)",
                    ReviewerRole = NexusRoles.DecisionApprover,
                    ApprovalStatus = "APPROVED",
                    Comments = "Approved Strategy B based on OR-Tools Pareto optimality ($8.7M revenue protected for $420K incremental cost).",
                    ReviewedAtUtc = DateTime.UtcNow.AddMinutes(-25)
                }
            }
        };
        db.Decisions.Add(heroDecision);

        db.DecisionExplanations.Add(new DecisionExplanation
        {
            OrganizationId = orgId,
            DecisionId = heroDecision.Id,
            WhatStatement = "Move 42% of production to Factory B.",
            WhyStatement = "Factory A utilization: 92% | Factory B utilization: 61% | Supplier C reliability: 97% | Factory B provides sufficient spare capacity.",
            ExpectedResultStatement = "Revenue Protected: $8.7M | Additional Cost: $420K | Service Level: 98.2% | Risk Reduction: 63%",
            BindingConstraintsJson = JsonSerializer.Serialize(new[]
            {
                "Supplier C (SUP-003) Max Emergency Allocation = 4,500 units/day (Binding, Shadow Price = $215/unit)",
                "Factory B Spare Capacity = 4,680 units/day (Active Usage = 4,200 units/day, Slack = 480 units/day)"
            }),
            SensitivityAnalysisJson = JsonSerializer.Serialize(new[]
            {
                "If Supplier A recovery extends from 14 to 21 days, Strategy B still preserves 96.4% SLA vs 79.1% unmitigated.",
                "Every 10% increase in Factory B transfer allocation reduces downstream stockout probability by 1.9%."
            }),
            MathematicalConfidencePct = 96.8m
        });

        var heroExecution = new Execution
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            ExecutionCode = "EXE-HERO-001",
            DecisionId = heroDecision.Id,
            DecisionCode = heroDecision.DecisionCode,
            SelectedStrategyCode = "Strategy B",
            ActionSummary = "Moved 42% of production to Factory B (Dresden) and routed 4,500 units/day from Supplier C.",
            Status = "COMPLETED",
            ProgressPct = 100,
            ApprovedByUser = "Marcus Vance (VP Global Operations)",
            InitiatedAtUtc = DateTime.UtcNow.AddMinutes(-20),
            CompletedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            ReplayEventsJson = JsonSerializer.Serialize(replayTimeline),
            Results = new List<ExecutionResult>
            {
                new()
                {
                    OrganizationId = orgId,
                    TargetAssetCode = "FAC-002",
                    TargetAssetName = "FACTORY B — Dresden Autonomous Manufacturing Plant",
                    OperationPerformed = "Shifted 42% production volume (4,200 units/day) from Factory A to Factory B",
                    CapacityShiftedUnitsPerDay = 4_200m,
                    CostIncurredUsd = 265_000m,
                    PostExecutionUtilizationPct = 89.5m,
                    ServiceLevelAchievedPct = 98.2m,
                    IsSuccessful = true
                },
                new()
                {
                    OrganizationId = orgId,
                    TargetAssetCode = "SUP-003",
                    TargetAssetName = "Supplier C — Nordic Silicon Dynamics AB",
                    OperationPerformed = "Activated emergency supply contract for 4,500 units/day",
                    CapacityShiftedUnitsPerDay = 4_500m,
                    CostIncurredUsd = 155_000m,
                    PostExecutionUtilizationPct = 91.0m,
                    ServiceLevelAchievedPct = 98.6m,
                    IsSuccessful = true
                }
            }
        };
        db.Executions.Add(heroExecution);

        // Decision Quality Feedback (matching Section 23: Predicted Revenue Loss $420K vs Actual Revenue Loss $390K)
        var outcome = new DecisionOutcome
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            DecisionId = heroDecision.Id,
            DecisionCode = heroDecision.DecisionCode,
            ExecutionId = heroExecution.Id,
            PredictedRevenueLossUsd = 420_000m,
            ActualRevenueLossUsd = 390_000m,
            PredictedAdditionalCostUsd = 420_000m,
            ActualAdditionalCostUsd = 408_500m,
            PredictedServiceLevelPct = 98.2m,
            ActualServiceLevelPct = 98.5m,
            PredictedRecoveryDays = 5.0m,
            ActualRecoveryDays = 4.6m,
            MeasuredAtUtc = DateTime.UtcNow
        };
        db.DecisionOutcomes.Add(outcome);

        db.DecisionQualities.Add(new DecisionQuality
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            DecisionId = heroDecision.Id,
            DecisionCode = heroDecision.DecisionCode,
            DecisionOutcomeId = outcome.Id,
            PredictionErrorPct = 7.14m,
            DecisionEffectivenessScore = 95.8m,
            CostVarianceUsd = -11_500m,
            CostVariancePct = -2.74m,
            ServiceLevelVariancePct = 0.30m,
            CalibrationWeightMultiplier = 0.982m,
            FeedbackSummary = "Predicted residual revenue loss ($420K) closely matched actual outcome ($390K, 7.14% error). Strategy B protected $8.73M of exposed revenue.",
            EvaluatedAtUtc = DateTime.UtcNow
        });

        // Initial Predictions & Forecasts (Section 22)
        var predDomains = new (string Domain, string AssetCode, string AssetName, decimal Val, string Unit, decimal Prob, decimal R2, decimal Mae, decimal Rmse, decimal Auc, decimal F1)[]
        {
            (PredictionTargets.Demand, "MKT-001", "European Industrial Market MKT-001", 3_420m, "units/day", 88.5m, 0.934m, 64.2m, 88.7m, 0.941m, 0.912m),
            (PredictionTargets.SupplierFailure, "SUP-001", "Supplier A — RheinMetall Precision Components", 68.4m, "% probability", 68.4m, 0.892m, 0.048m, 0.071m, 0.928m, 0.895m),
            (PredictionTargets.InventoryShortage, "WH-001", "Regional Hub Warehouse WH-001 (Frankfurt)", 6.0m, "days to stockout", 74.2m, 0.918m, 0.62m, 0.89m, 0.935m, 0.904m),
            (PredictionTargets.TransportationDelay, "TRN-001", "Corridor TRN-001 (Stuttgart -> Frankfurt)", 1.8m, "days delay", 49.0m, 0.887m, 0.31m, 0.44m, 0.906m, 0.878m),
            (PredictionTargets.RecoveryTime, "FAC-001", "FACTORY A — Stuttgart Primary Assembly", 11.4m, "days recovery", 82.0m, 0.911m, 0.74m, 1.02m, 0.922m, 0.889m),
            (PredictionTargets.SlaBreach, "CUST-001", "Global Industrial Partner CUST-001", 3.2m, "% breach prob", 3.2m, 0.942m, 0.029m, 0.041m, 0.954m, 0.926m)
        };

        int pIdx = 1;
        foreach (var pd in predDomains)
        {
            db.Predictions.Add(new Prediction
            {
                OrganizationId = orgId,
                PredictionCode = $"PRD-ML-{pIdx++:D3}",
                TargetDomain = pd.Domain,
                TargetAssetCode = pd.AssetCode,
                TargetAssetName = pd.AssetName,
                HorizonDays = 14,
                PredictedValue = pd.Val,
                Unit = pd.Unit,
                ProbabilityPct = pd.Prob,
                ConfidenceIntervalLow = Math.Round(pd.Val * 0.91m, 2),
                ConfidenceIntervalHigh = Math.Round(pd.Val * 1.09m, 2),
                ModelAlgorithm = "XGBoost / Scikit-Learn GradientBoosting",
                ValidationR2 = pd.R2,
                ValidationMae = pd.Mae,
                ValidationRmse = pd.Rmse,
                ValidationRocAuc = pd.Auc,
                ValidationF1 = pd.F1,
                TrainingSamplesCount = 5_000,
                FeatureImportanceJson = JsonSerializer.Serialize(new Dictionary<string, double>
                {
                    ["UtilizationRatio"] = 0.34,
                    ["UpstreamSupplierReliability"] = 0.27,
                    ["InventoryBufferDays"] = 0.19,
                    ["LeadTimeVariance"] = 0.12,
                    ["SeasonalDemandIndex"] = 0.08
                })
            });
        }

        for (int day = 1; day <= 14; day++)
        {
            decimal baseDem = 94_500m + (day * 410m);
            decimal predDem = baseDem + (decimal)(Math.Sin(day * 0.6) * 1850);
            db.Forecasts.Add(new Forecast
            {
                OrganizationId = orgId,
                ForecastCode = $"FC-DEM-D{day:D2}",
                MetricName = PredictionTargets.Demand,
                TargetCode = "ENTERPRISE-GLOBAL",
                DayOffset = day,
                ForecastDateUtc = DateTime.UtcNow.Date.AddDays(day),
                BaselineValue = baseDem,
                ForecastedValue = Math.Round(predDem, 0),
                LowerBound = Math.Round(predDem * 0.94m, 0),
                UpperBound = Math.Round(predDem * 1.06m, 0),
                ModelSource = "NEXUS-ML-XGBoost-v2.1"
            });
        }

        db.AuditLogs.Add(new AuditLog
        {
            OrganizationId = orgId,
            ActorName = "NEXUS Autonomous Bootstrap Engine",
            ActorRole = NexusRoles.Administrator,
            ModuleName = "DigitalTwin",
            ActionType = "ENTERPRISE_DIGITAL_TWIN_INITIALIZED",
            EntityType = "EnterpriseOrganization",
            EntityId = orgId.ToString(),
            Summary = "Initialized NEXUS GLOBAL INDUSTRIES with 50 Suppliers, 20 Factories, 40 Warehouses, 100 Routes, 500 Products, 20 Markets, 100 Processes, and 1,104 Dependencies."
        });

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("NEXUS GLOBAL INDUSTRIES seed completed ({AssetCount} graph assets, {DependencyCount} dependencies).", assets.Count, dependencies.Count);
    }

    private static async Task SeedIdentityAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<NexusRole>>();
        var userManager = services.GetRequiredService<UserManager<NexusUser>>();

        foreach (var roleName in NexusRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new NexusRole
                {
                    Id = Guid.NewGuid(),
                    Name = roleName,
                    NormalizedName = roleName.ToUpperInvariant(),
                    Description = $"NEXUS Enterprise {roleName} Role"
                });
            }
        }

        var demoUsers = new (string Email, string Name, string Dept, string Role)[]
        {
            ("admin@nexus-global.io", "Elena Rostova (Chief Enterprise Architect)", "Enterprise Architecture", NexusRoles.Administrator),
            ("executive@nexus-global.io", "Alexander Sterling (Chief Operating Officer)", "Executive Board", NexusRoles.Executive),
            ("ops@nexus-global.io", "Marcus Vance (VP Global Operations)", "Global Operations", NexusRoles.OperationsManager),
            ("supplychain@nexus-global.io", "Dr. Lukas Weber (Director Supply Chain)", "Supply Chain Control", NexusRoles.SupplyChainManager),
            ("risk@nexus-global.io", "Sofia Chen (Head of Enterprise Risk)", "Risk Intelligence", NexusRoles.RiskManager),
            ("approver@nexus-global.io", "Henrik Lindqvist (Decision Approver)", "Operations Governance", NexusRoles.DecisionApprover),
            ("analyst@nexus-global.io", "claire.dubois@nexus-global.io", "Quantitative Analytics", NexusRoles.Analyst)
        };

        foreach (var u in demoUsers)
        {
            var existing = await userManager.FindByEmailAsync(u.Email);
            if (existing is null)
            {
                var user = new NexusUser
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = DefaultOrganizationId,
                    UserName = u.Email,
                    Email = u.Email,
                    EmailConfirmed = true,
                    FullName = u.Name,
                    Department = u.Dept,
                    PrimaryRoleTitle = u.Role,
                    IsActive = true
                };
                var res = await userManager.CreateAsync(user, "Nexus#2026!");
                if (res.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, u.Role);
                    if (u.Role == NexusRoles.Administrator || u.Role == NexusRoles.OperationsManager)
                    {
                        await userManager.AddToRoleAsync(user, NexusRoles.DecisionApprover);
                    }
                }
            }
        }
    }
}

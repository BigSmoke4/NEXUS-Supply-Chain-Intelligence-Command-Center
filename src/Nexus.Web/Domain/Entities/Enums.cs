namespace Nexus.Web.Domain.Entities;

public enum NodeType
{
    Supplier, Factory, Warehouse, DistributionCenter, Port,
    TransportationHub, RawMaterial, Component, Product, Customer, Market
}

public enum EdgeType
{
    Supplies, Produces, Contains, Stores, ShipsTo, DistributesTo,
    DependsOn, Substitutes, SoldTo, LocatedAt, TransportedVia
}

public enum DisruptionType
{
    SupplierShutdown, FactoryShutdown, FactoryCapacityReduction, WarehouseFailure,
    TransportationFailure, PortClosure, ComponentShortage, RawMaterialShortage,
    DemandSpike, DemandCollapse, QualityFailure, GeopoliticalEvent,
    NaturalDisaster, CyberIncident, LaborDisruption, EnergyShortage
}

public enum MitigationType
{
    ActivateAlternativeSupplier, IncreaseSupplierAllocation, ReduceLowPriorityProduction,
    RedirectInventory, ExpediteTransportation, UseAirFreight, MoveInventoryBetweenWarehouses,
    PrioritizeHighValueCustomers, SubstituteComponents, IncreaseProductionElsewhere,
    DelayLowPriorityOrders, IncreaseSafetyStock
}

public enum ApprovalStatus { Pending, Approved, Rejected }

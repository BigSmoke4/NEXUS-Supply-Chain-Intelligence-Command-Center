# NEXUS Architecture

## 1. Layering

```
Controller  →  Application service (Modules/*)  →  Domain entities  →  EF Core  →  PostgreSQL
```

Controllers stay thin: they load data, call one or two application services,
build a view model, and return a view. Business logic — inventory math, graph
traversal, risk scoring, mitigation generation — lives in `Modules/`, not in
controllers or Razor views.

## 2. Module boundaries

| Module | Owns | Depends on |
|---|---|---|
| `SupplyNetwork` | `SupplyNode`/`SupplyEdge` graph traversal | `NexusDbContext` |
| `Simulation` | Deterministic disruption propagation | `SupplyNetwork`, `NexusDbContext` |
| `Risk` | Composite supplier risk scoring | nothing (pure function of `Supplier` + weights) |
| `Optimization` | Mitigation strategy generation | `ScenarioResult` (output of `Simulation`) |
| `AI` | Vendor-agnostic LLM abstraction + orchestrator | `Simulation`, `Optimization` |

No module reaches into another module's tables directly for anything beyond
read access via `NexusDbContext`; cross-module coordination happens through
the interfaces (`IGraphService`, `ISimulationEngine`, `IMitigationEngine`,
`ILLMProvider`, `IAgentOrchestrator`), so any of these can be extracted into a
separate service later without rewriting callers.

## 3. The supply-chain graph

`SupplyNode` is a generic graph vertex; concrete business entities (Supplier,
Factory, Warehouse, Product, Customer, Component) each have their own
strongly-typed table and are linked to exactly one `SupplyNode` via
`NodeRefId`. This keeps:

- Graph algorithms (`GraphService`) generic and fast — they only ever touch
  `SupplyNode`/`SupplyEdge`, not six different entity tables.
- Business entities free to carry rich, type-specific columns (a `Supplier`
  has `ReliabilityPercent`; a `Product` has `UnitRevenue`) without polluting
  the graph schema.

Edges (`SupplyEdge`) carry relationship metadata — lead time, capacity, unit
cost, reliability, MOQ, contract priority — because those vary per
relationship (Supplier A → Component X might have a 21-day lead time while
Supplier B → Component X has 14), not per node.

## 4. The deterministic simulation engine

`SimulationEngine.RunAsync`:

1. Loads the scenario's `Disruption`s and builds a per-node, per-day
   `capacityFactor` array (1.0 = normal, 0.0 = fully down) covering a
   120-day horizon.
2. Uses `GraphService.GetDownstreamAsync` from every disrupted node to find
   which `Component`s are affected.
3. Walks each affected component's `InventoryRecord` day by day, subtracting
   daily consumption and only replenishing at the disrupted inbound rate,
   recording the first day quantity hits zero as a `StockoutEvent`.
4. Joins stockouts to `BillOfMaterial` to find affected `Product`s, and
   multiplies lost production days by `DailyDemandUnits * UnitRevenue` to get
   `RevenueAtRisk`.
5. Estimates `RecoveryDays` from the disruption's end date plus a ramp-up
   term proportional to how many stockouts occurred, and `ServiceLevelPercent`
   from the fraction of the product catalog affected.
6. Produces a single `OverallRiskScore` blending normalized revenue exposure
   and service-level degradation.

Every field on `ScenarioResult` is produced by this arithmetic — nothing here
calls an LLM, and nothing here is guessed.

This is intentionally a legible, inspectable implementation, not a
full discrete-event simulation with stochastic replenishment, multi-echelon
lead-time queues, or Monte Carlo confidence intervals. Extending it in that
direction (see §9-10 of the original brief) is the natural next step for a
production system; the current version establishes the correct architecture
(deterministic core, graph-driven propagation, evidence-based AI) for that
extension to build on.

## 5. AI orchestration

`AgentOrchestrator.InvestigateScenarioAsync`:

1. Runs `SimulationEngine` (never the LLM) to get calculated facts.
2. Runs `MitigationEngine` (never the LLM) to get candidate strategies.
3. Converts both into a flat `List<Evidence>` — each item tagged with its
   source and a confidence value.
4. Sends *only* that evidence list, plus the user's question, to
   `ILLMProvider.CompleteAsync`, with a system prompt that explicitly forbids
   inventing numbers.
5. Returns an `AiInvestigationResult` containing the agent trace, the
   evidence, the simulation result, the strategies, and the LLM's narrative —
   the UI renders all of these, so a user can always see exactly which facts
   are calculated vs. which sentence is the model's phrasing of them.

This is a **sequential** two-agent-equivalent pipeline (Simulation +
Optimization → Explanation → Decision), not yet the full parallel
Inventory/Supplier/Transportation/Financial fan-out described in the original
brief's §16. The trace/evidence data structures are already shaped to support
that: adding a new specialist agent means adding another step that appends to
the same `evidence` list before the LLM call, and/or running independent
steps with `Task.WhenAll` instead of sequentially.

## 6. Why a modular monolith, not microservices

Supply-chain domains are highly relational (a disruption's blast radius
depends on live joins across suppliers → components → factories →
warehouses → products → customers). Splitting that into services from day
one would mean either chatty synchronous calls across the network for every
simulation, or duplicating the graph in each service. The modular monolith
keeps those joins as in-process, transactionally consistent queries while
still enforcing the same boundaries (interfaces, no cross-module table
access) that would let any one module become its own service later if its
load profile genuinely diverges from the rest (e.g. `Simulation` becoming
CPU-bound enough to need independent scaling).

## 7. What a hardening pass still needs

This scaffold deliberately stops short of several things the original brief
calls for, so they don't ship as fake/placeholder implementations:

- **Multi-tenancy enforcement**: `OrganizationId` is on every entity, but no
  `ICurrentTenant`-driven global query filter is wired into
  `NexusDbContext.OnModelCreating` yet.
- **Authorization**: `NexusPermissions` constants exist; no
  `[Authorize(Policy = ...)]` attributes are applied to controllers yet.
- **Observability**: OpenTelemetry packages are referenced but not configured
  in `Program.cs`.
- **Testing**: no test project exists yet. Priority order for a first pass:
  unit tests on `SimulationEngine` (inventory depletion math, revenue
  exposure), `GraphService` (SPOF detection, traversal correctness), and
  `RiskScoringService` (weight normalization).
- **CI/CD**: no pipeline is defined.
- **Seed scale**: 3 suppliers instead of 25, 3 components instead of 40, etc.
  — enough to exercise every code path with a legible, inspectable dataset,
  not enough to be a convincing large-scale demo yet.

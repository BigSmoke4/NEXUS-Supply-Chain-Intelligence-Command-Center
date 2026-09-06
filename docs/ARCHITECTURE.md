# NEXUS Architecture

NEXUS is a modular ASP.NET Core MVC reference implementation for supply-chain
crisis decision support. The key invariant is simple: **the deterministic
engines own every number; the AI layer explains evidence that already exists.**

## Runtime flow

```text
Browser
  │
  ▼
Razor MVC + SignalR
  │
  ▼
Application modules (Modules/*)
  │
  ├── SupplyNetwork → graph traversal / SPOF / alternate paths
  ├── Simulation    → deterministic day-by-day disruption arithmetic
  ├── Risk          → composite supplier risk score
  ├── Optimization  → transparent mitigation heuristics
  ├── Knowledge     → lexical retrieval + query expansion
  ├── BackgroundJobs→ durable single-process simulation queue
  └── AI            → specialist evidence fan-out → LLM narrative
  │
  ▼
EF Core / PostgreSQL
```

## Deterministic simulation

`SimulationEngine` loads a tenant-scoped scenario, traverses downstream graph
relationships, models inventory depletion over a fixed horizon, identifies
stockouts, maps affected components to products, and calculates revenue
exposure, recovery, service level, and overall risk. `CascadeStep` records the
actual BFS depth so the UI animation replays the calculated propagation path.

The simulation is deliberately deterministic and inspectable. It is not a
Monte Carlo or discrete-event optimizer; those are future extensions rather
than claims hidden behind a UI.

## AI evidence boundary

`AgentOrchestrator` runs the simulation and mitigation engines first, then fans
out the registered specialist agents with `Task.WhenAll`. Their output is
merged into an evidence list. Retrieved contract text is inserted as quoted
data, never as system instructions. Only that evidence list and the user's
question are passed to `ILLMProvider`.

The default `MockLlmProvider` is intentionally explicit when no live model is
configured. `AnthropicLlmProvider` demonstrates the vendor adapter shape; the
rest of the application depends only on `ILLMProvider`.

## Multi-tenancy and authorization

Tenant identity comes from the signed-in user's `org_id` claim. `NexusDbContext`
uses global query filters for all tenant-owned entities, including Identity
users and persisted simulation artifacts. A `SaveChanges` boundary also
auto-populates an empty organization on new tenant-owned records and rejects
cross-tenant writes. Background jobs establish an `AsyncLocal` tenant scope
before resolving a tenant-scoped DbContext.

RBAC is policy-based: controllers use `NexusPermissions` policies rather than
scattered role checks. Human approval of mitigation strategies is explicit and
audited.

## Background processing

Every simulation enqueue persists a `SimulationJobRecord` before writing to
the in-memory `Channel`. The hosted worker recovers `Queued` and `Running`
records on startup, runs the simulation outside the request thread, persists
the result, generates deterministic alerts, and publishes SignalR completion
events. This is durable across restarts but intentionally single-process; a
distributed queue can replace `ISimulationJobQueue` later.

## UI / graph rendering

The dependency graph is a zero-dependency SVG renderer. Small networks use a
readable type-column layout; networks above 60 nodes use a deterministic
force-directed layout based on the actual edges. The same renderer provides
pan/zoom, search, neighbor focus, a live minimap, keyboard activation, and an
accessible relationship text fallback.

Primary navigation keeps semantic HTML links and adds the requested neural
network visual treatment with CSS, so keyboard and screen-reader behavior is
not sacrificed for decoration.

## Persistence bootstrap

The repository currently has no checked-in EF migration snapshot. For the
reference/demo path, `Database:InitializeOnStartup=true` applies available
migrations and otherwise falls back to `EnsureCreated`. Docker enables this
flag automatically. Production deployments should generate and review a normal
EF migration and apply it in the release pipeline rather than relying on
automatic startup DDL.

## Observability

OpenTelemetry tracing covers ASP.NET Core and EF Core. `NexusMetrics` exposes
real counters/histograms for simulation duration, queue depth, generated alerts,
approved mitigations, and failed jobs. Console export is the default so the
reference build requires no collector.

## Security posture

The project includes anti-forgery validation on state-changing MVC actions,
local-return-url validation on login, Identity password/lockout handling,
policy-based authorization, tenant query/write isolation, prompt-injection
regression tests, and immutable audit records. This is a strong reference
implementation baseline, not a substitute for a production penetration test,
threat model, or formal compliance certification.

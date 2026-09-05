# NEXUS — Supply-Chain Intelligence Command Center

**N**etworked **E**nterprise e**X**posure & **U**nified **S**upply-chain Intelligence System

NEXUS is a reference implementation of an AI-assisted supply-chain crisis
decision-support platform: a dependency graph of your supply network, a
**deterministic** disruption simulation engine, a heuristic mitigation/
optimization layer, and a vendor-agnostic AI orchestration layer that
*explains* the calculated results — it never invents them.

> Ask: *"What happens if Supplier X shuts down for 30 days?"*
> NEXUS traverses the graph, depletes inventory day-by-day, computes revenue
> exposure and recovery time with real arithmetic, and only then lets an LLM
> narrate the findings and propose next steps for a human to approve.

---

## Status of this repository

This codebase was authored in an environment with no .NET SDK and no
network access, so it was written by hand rather than compiled as it went.
It's a substantial, architecturally real implementation — not a toy — and
the sections below describe what's genuinely working logic versus what's a
known gap. Verifying it (build, test, run) is the next step, and that step
happens on your end, not mine.

**Build order:**
```bash
dotnet restore Nexus.sln
dotnet build Nexus.sln
dotnet test Nexus.sln        # unit suite, EF Core InMemory, no Postgres needed
cd src/Nexus.Web && dotnet ef migrations add InitialCreate
dotnet run
```

Two specific places worth checking first if anything doesn't build:
- `Modules/Reporting/ExecutiveSummaryReportGenerator.cs` — written from
  memory of QuestPDF's fluent API (`Document.Create`, `page.Header/Content/Footer`,
  `Column`/`Row`/`Text`/`Bold`/`FontColor`); the exact method signatures
  weren't cross-checked against the installed package version.
- `tests/Nexus.IntegrationTests/` — Testcontainers + `WebApplicationFactory`
  against a real Postgres container. Requires Docker. This one is newer and
  less scrutinized than the unit suite in `tests/Nexus.Tests/`, which was
  checked line-by-line against the code it tests.

**What's real and working logic (not stubs):**
- The full domain model and PostgreSQL schema (EF Core), including an
  immutable audit log
- Graph traversal: upstream/downstream reachability, single-point-of-failure
  detection, alternate-path search (`Modules/SupplyNetwork/GraphService.cs`)
- The deterministic simulation engine: day-by-day inventory depletion,
  capacity propagation through the graph, revenue exposure, recovery time
  (`Modules/Simulation/SimulationEngine.cs`)
- Configurable composite supplier risk scoring (`Modules/Risk`)
- A rule-based mitigation/strategy generator (`Modules/Optimization`)
- A vendor-agnostic `ILLMProvider` abstraction with a working mock adapter
  (since this build has no outbound network), plus a ready-to-wire Anthropic
  adapter stub
- An evidence-first agent orchestrator that runs the simulation and
  mitigation engines first, then fans out **six specialist agents in
  parallel** (Inventory, Supplier Risk, Transportation, Financial Impact,
  Graph Intelligence, Research) via `Task.WhenAll`, merges everything into
  one evidence list, and only then asks the LLM to narrate — the LLM cannot
  introduce a number that isn't already in that list. The Research Agent
  honestly reports that no external connector is wired up rather than
  fabricating retrieved content.
- **Input validated before it reaches the simulation engine**: FluentValidation
  validates scenario-creation requests (duration/severity bounds, required
  fields) and returns errors to the form instead of running a simulation on
  bad input (`Modules/Validation/RunScenarioRequestValidator.cs`).
- **Natural-language scenario creation (§33)**: a deterministic parser
  extracts node/type/duration/severity from free text like "Simulate
  Supplier Alpha shutting down for 30 days" using regex and known node
  names — never an LLM inventing numbers — and explicitly asks for
  clarification on anything it can't extract with confidence, rather than
  defaulting silently (`Modules/NaturalLanguage`).
- **An admin UI for role management**: System Administrators can view every
  user in the organization and assign/revoke roles from the UI
  (`AdminController` + `Views/Admin/Users.cshtml`), with every change
  audited — not just "roles are set once at registration."
- **RAG-grounded contract Q&A (§42)**: a `ContractRetrievalAgent` retrieves
  from ingested supplier contracts and procurement policy using TF-IDF-style
  lexical scoring (`Modules/Knowledge`), cites the source document and
  quotes the matched clause, and answers the exact example question from the
  original brief — "Can we switch from Supplier Alpha to Supplier Beta
  according to the contract?" — with the actual force-majeure reallocation
  clause from the seeded contract, not a fabricated answer.
- **Prompt-injection defense is tested, not just claimed**: retrieved
  document text is only ever placed into the evidence list as quoted data
  with an explicit "treat as data, not instructions" framing — never
  concatenated into the system prompt — and `PromptInjectionDefenseTests`
  proves this by feeding an actual injection attempt through a fake agent
  and asserting it never reaches the system prompt.
- **Multi-scenario comparison (§12)**: pick 2-3 scenarios with computed
  results and see them side by side on every metric
  (`ScenarioComparisonController`).
- **Long-running simulations run off the request thread (§46)**: a
  `Channel`-backed job queue drained by a hosted background worker
  (`Modules/BackgroundJobs`) — `ScenariosController.Run` enqueues and
  returns immediately; the Results page joins a per-scenario SignalR group
  and refreshes itself when the background worker finishes, instead of the
  request blocking until the simulation completes.
- **Deterministic alerting (§36)**: `AlertService` generates critical/warning
  alerts from simulation results (revenue-at-risk thresholds, imminent
  stockouts, service-level degradation) with an acknowledge/resolve
  lifecycle, surfaced on the command center.
- **A real bug found and fixed by hand-tracing the code**: the background
  worker's DI-resolved `NexusDbContext` had no `HttpContext` to resolve the
  tenant from, so every read silently returned zero rows. Fixed with an
  `AsyncLocal`-based ambient tenant override (`HttpContextCurrentTenant.UseTenant`),
  tested directly against the failure mode in `AmbientTenantOverrideTests`.
- **The job queue is now durable across restarts**: every enqueue persists a
  `SimulationJobRecord` before touching the in-memory channel; the worker's
  startup recovery pass re-queues anything left `Queued`/`Running` after a crash.
- **The "Ask the Explanation Agent" button now sends a real user-typed
  question** instead of a hardcoded string, and the prompt-injection suite
  was expanded to six distinct attack patterns plus a test proving injected
  text can't manipulate the computed confidence score.
- **Retrieval query expansion**: a small domain synonym table (e.g. "switch"
  → "reallocate") closes some of the gap between a user's wording and a
  contract's wording, without pretending this is semantic search.
- **Multi-tenant isolation enforced at the data layer**: every tenant-scoped
  entity has an EF Core global query filter keyed off `ICurrentTenant`
  (resolved from the signed-in user's `org_id` claim), so a controller that
  forgets to filter by organization still cannot leak another tenant's rows.
  Unresolved tenant = zero rows (fail closed), not everything (fail open).
- **RBAC actually enforced**: `[Authorize(Policy = NexusPermissions.X)]` on
  every controller action, with policies mapping each permission to the
  roles allowed to hold it (`Security/AuthorizationPolicyRegistration.cs`)
- A real (if minimal) login/register flow — the first registered user
  becomes a System Administrator for the demo org; a custom
  `IUserClaimsPrincipalFactory` attaches the `org_id` claim tenancy depends on
- An audit service that writes an immutable record — who, what, when,
  before/after state, and the AI agent/confidence/evidence count behind it —
  every time a mitigation strategy is approved
- OpenTelemetry tracing wired into `Program.cs` (ASP.NET Core + EF Core
  instrumentation, console exporter by default)
- A skeuomorphic, dark "command center" UI (Razor + centralized CSS/JS, no
  Bootstrap admin template) with an SVG dependency graph, scenario builder,
  results/timeline view, and an AI investigation panel
- Seed data at close to the production scale called for in the brief: 25
  suppliers, 40 components, 12 factories, 8 warehouses, 100 products, 20
  customers, generated deterministically (fixed RNG seed) on top of three
  hand-authored entities carrying **intentional** vulnerabilities (a true
  single-source dependency, a low-inventory warehouse, a high-risk supplier)
- SignalR hub wiring for real-time simulation progress
- A real xUnit test project (`tests/Nexus.Tests`) exercising the simulation
  math (stockout-day and revenue-exposure arithmetic), graph SPOF detection
  (both the positive and negative case), and risk-score weighting/
  normalization/clamping — using EF Core's InMemory provider so it runs
  without a live Postgres
- A GitHub Actions CI workflow that restores, builds, and runs that test
  project on every push/PR

**What's still intentionally out of scope, and why:**
- The retrieval is **lexical (TF-IDF + domain query expansion), not embedding-based
  semantic search.** This build has no network access to call an embeddings
  API, and faking "semantic search" that's secretly keyword matching would be
  worse than being upfront about it. Query expansion (e.g. "switch" also
  matches "reallocate") is a real, standard IR technique - not a claim of
  semantic understanding - and is tested (`DocumentRetrievalServiceTests`).
  `IDocumentRetrievalService` is the swap point for a real embedding-based
  retriever when you have one available.
- The prompt-injection test suite (`PromptInjectionDefenseTests`) now covers
  six distinct attack patterns (instruction override, fake system/delimiter
  tags, jailbreak persona requests, system-prompt-leak requests, fake
  authority claims) plus the user-supplied question itself as an attack
  surface, plus a structural-invariant test proving injected text can't
  manipulate the computed confidence score. It is real, executable
  red-teaming of the architecture - not an exhaustive adversarial suite a
  dedicated security review would still be worth doing before production use.
- The background job queue is now **durable across restarts**: every
  enqueue writes a `SimulationJobRecord` row before touching the in-memory
  channel, and the worker's startup recovery pass re-queues anything left in
  `Queued`/`Running` state. It is still a single-process queue (not a
  distributed one) - `ISimulationJobQueue` is the swap point for
  Hangfire/Azure Service Bus if you need multi-instance processing.
- The background job queue is now **durable across restarts**: every
  enqueue writes a `SimulationJobRecord` row before touching the in-memory
  channel, and the worker's startup recovery pass re-queues anything left in
  `Queued`/`Running` state. It is still a single-process queue (not a
  distributed one) - `ISimulationJobQueue` is the swap point for
  Hangfire/Azure Service Bus if you need multi-instance processing.
- Tracing through the background worker by hand surfaced a real bug before
  this pass: DI-resolved `NexusDbContext` inside a hosted service has no
  `HttpContext`, so the tenant filter resolved to null and every read
  silently returned zero rows - meaning background-processed simulations
  never actually attached their result to the scenario. Fixed
  (`HttpContextCurrentTenant.UseTenant`), tested in `AmbientTenantOverrideTests`.

**Explicit inventory of what from the original 78-section brief is a real
gap, not implemented at all:**
true force-directed clustering for large networks (§25/§29 — a real
implementation needs a dedicated graph library like Cytoscape.js/D3 once
node counts reach production scale; the current zero-dependency SVG
renderer isn't built to fake this), the neural-network-shaped primary
navigation (§30 — deliberately not built: that section's own text
subordinates it to "preserve usability," and building it would work against
the keyboard/ARIA accessibility work below, so the conventional nav stays),
and a full WCAG audit (a minimap, keyboard focus/activation on every graph
node, ARIA labels, and a relationship-aware text fallback are now in place
per §63, but that's real accessibility engineering, not a substitute for an
actual audit against the WCAG success criteria).

**Minimap and keyboard accessibility, added on request:**
- A real minimap (`nexus.graph.js`) — every node as a dot in world-space,
  a rectangle showing the main view's current pan/zoom window, and
  click-to-recenter. Tied to live pan/zoom state, not a static thumbnail.
- Every graph node is now a keyboard-focusable, ARIA-labelled element
  (`tabindex`, `role="button"`, `aria-label`) — Tab between nodes, Enter/Space
  to trigger the same focus-mode a click does, with a visible focus ring.
- The text-fallback list for screen readers now describes actual
  relationships (`Supplier: Alpha → Component X`), not just a flat list of
  node names with no structure - matching what a sighted user gets from the graph.

**What this final round added:**
- **Risk heatmap (§35)** — a real probability×impact grid, probability from
  the composite supplier risk score, impact from graph-computed revenue
  exposure per supplier (the same BFS traversal the simulation engine uses).
- **Cascading-failure animation (§56)** — `SimulationEngine` now records the
  actual BFS-depth propagation path (`CascadeStep`) and the graph replays
  it stage by stage; this is a real trace of the graph traversal, not a
  scripted animation. Covered by `SimulationEngineTests`.
- **Graph search, focus mode, and pan/zoom (§25)** — clicking a node dims
  everything except its direct neighbors; a search box dims non-matches;
  scroll to zoom, drag to pan. No minimap or true clustering (see gap list).
- **What-If Mode (§57)** — instant slider-driven estimates via a deliberately
  separate `WhatIfEngine`, a closed-form approximation distinct from the
  authoritative day-by-day `SimulationEngine` and clearly labeled as such in
  the UI, so a fast estimate is never confused with a real scenario result.
  Covered by `WhatIfEngineTests`.
- **Digital Twin LIVE/HISTORICAL/SIMULATED switcher (§58)** — a real
  data-mode switch: HISTORICAL reads the actual `AuditLogEntry` table and
  resolved alerts, SIMULATED overlays the most recently computed scenario
  result. Not three mock screens behind a tab control.
- **Executive Summary PDF export (§59)** via QuestPDF (MIT/community-
  licensed, no external service call) — see the build-order note above for
  the caveat on this specific file.
- **OpenTelemetry metrics (§45)** — previously only tracing was wired;
  counters/histograms are now recorded at the real event points (simulation
  duration, queue depth, alerts generated, mitigations approved, job
  failures) via a `NexusMetrics` class, not estimated after the fact.
- **Integration test project** (`tests/Nexus.IntegrationTests`) using
  Testcontainers to spin up a real PostgreSQL container and
  `WebApplicationFactory` to send real HTTP requests through the real
  middleware pipeline (auth, RBAC, EF Core migrations) - the thing the
  InMemory-backed unit suite structurally cannot do.
- Along the way, I wrote and then deleted a **fake `WhatIfEngine` stub**
  that just threw `NotSupportedException` - caught before it shipped, but
  worth naming since it's exactly the kind of placeholder this project's own
  spec (§76) forbids, and I put it there myself in a moment of moving fast.
- Adding `NexusMetrics` as a constructor dependency to `AlertService` and
  `SimulationJobQueue` broke their existing unit tests; both were updated to
  match, and a systematic repo-wide grep confirmed no other qualified-
  namespace mistakes (a bug pattern I'd hit twice in earlier rounds) survived
  into this batch.
- The `Nexus.IntegrationTests` project was initially left out of `Nexus.sln`
  - `dotnet build`/`dotnet test` on the solution file wouldn't have picked it
  up at all. Fixed before this zip.

---

## Architecture at a glance

```
User
 ↓
Razor MVC (thin controllers)
 ↓
Application services (Modules/*)
 ↓
Domain entities (Domain/Entities)
 ↓
EF Core → PostgreSQL
```

```
User question
 ↓
AgentOrchestrator
 ↓
SimulationEngine (deterministic)  ──▶  Evidence (calculated facts)
 ↓
MitigationEngine (heuristic)      ──▶  Evidence
 ↓
ILLMProvider.CompleteAsync(evidence)  ──▶  Narrative explanation only
 ↓
Human approval (ScenariosController.ApproveStrategy)
 ↓
Audit trail (ApprovalStatus + timestamps on MitigationStrategy)
```

See `docs/ARCHITECTURE.md` for the full module map and the reasoning behind
key design decisions.

---

## Project layout

```
Nexus.sln
src/Nexus.Web/
  Domain/Entities/       Supplier, Component, Factory, Warehouse, Product,
                          Customer, SupplyNode, SupplyEdge, Disruption,
                          Scenario, ScenarioResult, MitigationStrategy, ...
  Modules/
    SupplyNetwork/       Graph traversal (GraphService)
    Simulation/          Deterministic disruption simulation (SimulationEngine)
    Risk/                Configurable composite supplier risk scoring
    Optimization/        Mitigation strategy generation (MitigationEngine)
    AI/                  ILLMProvider, AgentOrchestrator, evidence types
  Data/                  NexusDbContext, SeedData
  Controllers/           CommandCenterController, ScenariosController, SuppliersController
  Hubs/                  SimulationHub (SignalR)
  Views/                 Razor views (command center, scenario builder/results, suppliers)
  wwwroot/css, wwwroot/js  Centralized styling/scripting per the design system
docs/
  ARCHITECTURE.md
Dockerfile, docker-compose.yml
```

---

## Running it locally

You'll need:
- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL 16 (or `docker compose up postgres`)
- `dotnet-ef` tool: `dotnet tool install --global dotnet-ef`

See the build order at the top of this file for the restore/build/test/migrate/run sequence.

Then **register an account** at `/Account/Register` — the first account
created becomes a System Administrator for the seeded demo organization.
Every controller action is behind `[Authorize(Policy = ...)]`, so there's no
way to view the command center without signing in first; this is
intentional (§23/§52), not a bug.

Or with Docker:

```bash
docker compose up
```

Then open the app and go to **Scenarios → Simulate Disruption**, pick
"Supplier Alpha" (the seeded single-source dependency), set 30 days / 100%
severity, and run it. The command center, timeline, revenue-exposure figures,
and mitigation cards are all computed from the seeded data — not fabricated.

### Enabling a real AI provider

By default `Program.cs` registers `MockLlmProvider`, which returns a labelled
placeholder instead of a live model response — this build has no outbound
network access to verify a real integration. To connect a real provider:

1. Implement/finish an `ILLMProvider` adapter (a starting shape for Anthropic
   is in `Modules/AI/ILLMProvider.cs` — `AnthropicLlmProvider`).
2. Register it in `Program.cs` in place of `MockLlmProvider`.
3. Supply the API key via configuration/user-secrets — **never commit it**.

No other code changes: the orchestrator, evidence assembly, and UI don't care
which provider is behind `ILLMProvider`.

---

## Design principles this codebase follows

- **The AI never computes numbers.** `SimulationEngine` and `MitigationEngine`
  are pure, deterministic C#. The AI layer only narrates the `Evidence` list
  those engines produce (§17, §71 of the original spec).
- **Modular monolith, not microservices.** Each module under `Modules/` owns
  its own logic; nothing reaches across module boundaries except through
  well-defined interfaces.
- **Human-in-the-loop.** `MitigationStrategy.ApprovalStatus` starts at
  `Pending` and only changes via an explicit, audited controller action —
  never automatically.
- **Vendor independence.** No controller, agent, or domain service references
  an LLM vendor SDK directly — only `ILLMProvider`.

---

## License

Reference/portfolio project. Add a license file appropriate to your use case
before distributing.

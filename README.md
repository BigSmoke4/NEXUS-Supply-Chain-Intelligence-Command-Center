# NEXUS — Supply-Chain Intelligence Command Center

**Networked Enterprise eXposure & Unified Supply-chain Intelligence System**

NEXUS is a reference implementation of an AI-assisted supply-chain crisis
decision-support platform. It combines a dependency graph, deterministic
disruption simulation, transparent mitigation heuristics, contract retrieval,
role-based access control, tenant isolation, real-time job processing, and a
vendor-neutral AI explanation layer.

> **Core invariant:** NEXUS calculates first and explains second.
>
> Ask: *“What happens if Supplier Alpha shuts down for 30 days?”*
>
> The deterministic engine traverses the network, models inventory depletion,
> calculates exposure and recovery, and records the actual cascade path. Only
> after those facts exist does the AI layer receive them as evidence and produce
> a narrative. The LLM is never the source of a calculated number.

## What is included

### Decision-support engine

- Supply-chain dependency graph backed by PostgreSQL/EF Core.
- Upstream/downstream traversal and single-point-of-failure detection.
- Alternate-path discovery.
- Deterministic day-by-day disruption simulation.
- Inventory depletion and stockout-day calculation.
- Downstream component/product/customer impact calculation.
- Revenue-at-risk, service-level, recovery-time, and composite risk scoring.
- Actual BFS cascade-depth recording for replay in the UI.
- Rule-based mitigation generation with explicit confidence/evidence fields.
- Fast **What-If** estimates kept separate from authoritative simulations.
- Probability × impact supplier risk heatmap.
- Multi-scenario comparison.

### AI and knowledge layer

- `ILLMProvider` abstraction: no controller or domain service depends on a
  vendor SDK.
- Evidence-first `AgentOrchestrator`.
- Parallel specialist fan-out with `Task.WhenAll` across inventory, supplier
  risk, transportation, financial impact, graph intelligence, contract
  retrieval, and research.
- Mock provider by default, so the reference build works without an API key.
- Anthropic adapter shape ready for configuration when network/API access is
  available.
- Lexical TF-IDF-style contract/policy retrieval with tested domain query
  expansion.
- Retrieved clauses are quoted as data, never promoted into system
  instructions.
- Prompt-injection regression tests covering multiple attack patterns and a
  confidence-score invariant.
- Natural-language scenario parsing is deterministic and asks for clarification
  rather than silently inventing missing duration/severity/type values.

### Security and governance

- ASP.NET Core Identity login/register flow with a branded NEXUS UI.
- Password policy and lockout-on-failure through Identity.
- First-user bootstrap as System Administrator; later registrations default to
  Viewer.
- Policy-based RBAC using `NexusPermissions`.
- Administrative role assignment/revocation UI with audit records.
- Multi-tenant EF global query filters across business data, Identity users,
  and persisted simulation artifacts.
- Tenant-aware write boundary: new records inherit the resolved tenant and
  cross-tenant writes are rejected.
- Background workers use an explicit ambient tenant scope.
- Anti-forgery validation on state-changing MVC actions.
- Local-return-url validation on login.
- Immutable audit records containing actor, action, before/after state, and AI
  evidence metadata where applicable.

### Operations and UI

- Razor MVC command-center UI with centralized CSS/JS and no admin template.
- LIVE / HISTORICAL / SIMULATED Digital Twin modes.
- Real SignalR simulation progress and completion notifications.
- Durable single-process simulation queue: database job record first, in-memory
  `Channel` for fast dispatch, startup recovery for queued/running jobs.
- Deterministic alert generation with acknowledge/resolve lifecycle.
- Executive Summary PDF export using QuestPDF.
- OpenTelemetry tracing and real application metrics.
- `/health/live` and `/health/ready` endpoints.
- Production-safe exception endpoint instead of relying on a nonexistent
  `Home/Error` controller.
- Docker and Docker Compose deployment path with PostgreSQL readiness gating.
- Accessible graph interaction: keyboard-focusable nodes, ARIA labels,
  relationship text fallback, visible focus, skip link, and reduced-motion
  support.

## Large-network graph rendering

The graph renderer is intentionally zero-dependency, but it is no longer tied
to a simple column layout for large networks.

- Up to 60 nodes: deterministic type-column layout for maximum readability.
- More than 60 nodes: deterministic force-directed layout using the actual
  graph edges.
- Pan and zoom.
- Node search.
- Neighbor focus mode.
- Live minimap with viewport recentering.
- Keyboard activation with Enter/Space.
- Screen-reader relationship fallback.
- Cascade animation replays the simulation engine's actual BFS stages.

The public renderer API is isolated so a future Cytoscape.js/D3 implementation
can replace only the layout/rendering internals if production-scale graph
requirements justify an external graph library.

## Architecture

```text
Browser
  │
  ▼
Razor MVC + SignalR
  │
  ▼
Application modules (Modules/*)
  │
  ├── SupplyNetwork   graph traversal / SPOF / alternate paths
  ├── Simulation      deterministic disruption arithmetic
  ├── Risk            supplier risk scoring
  ├── Optimization    mitigation heuristics
  ├── Knowledge       contract/policy retrieval
  ├── NaturalLanguage deterministic scenario parsing
  ├── BackgroundJobs  durable simulation queue
  ├── Alerts          deterministic alert lifecycle
  ├── Reporting       PDF generation
  └── AI              specialist evidence + LLM narration
  │
  ▼
EF Core → PostgreSQL
```

See [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) for the module boundaries,
data flow, tenancy model, background processing, AI evidence boundary, and
persistence strategy.

## Project structure

```text
Nexus.sln
├── src/Nexus.Web/
│   ├── Controllers/              thin MVC controllers
│   ├── Data/                     EF Core context + deterministic seed
│   ├── Domain/Entities/          domain and persistence models
│   ├── Modules/
│   │   ├── AI/                   evidence-first orchestration
│   │   ├── Alerts/               deterministic alerts
│   │   ├── Audit/                immutable audit service
│   │   ├── BackgroundJobs/       durable simulation worker
│   │   ├── Knowledge/            contract retrieval
│   │   ├── NaturalLanguage/      deterministic parser
│   │   ├── Observability/        OpenTelemetry metrics
│   │   ├── Optimization/         mitigation heuristics
│   │   ├── Reporting/            QuestPDF report generator
│   │   ├── Risk/                 supplier risk scoring
│   │   ├── Simulation/           deterministic simulation engine
│   │   ├── SupplyNetwork/        graph algorithms
│   │   ├── Validation/            scenario validation
│   │   └── WhatIf/               fast approximation engine
│   ├── Security/                 tenancy + authorization
│   ├── Hubs/                     SignalR
│   ├── Views/                    Razor UI
│   └── wwwroot/                  centralized CSS/JS
├── tests/Nexus.Tests/            unit/regression tests
├── tests/Nexus.IntegrationTests/ real PostgreSQL + WebApplicationFactory
├── docs/ARCHITECTURE.md
├── docs/ACCESSIBILITY.md
├── Dockerfile
├── docker-compose.yml
└── Nexus.sln
```

## Requirements

- .NET 9 SDK
- PostgreSQL 16, or Docker Desktop
- Docker is required for the PostgreSQL Testcontainers integration suite.
- `dotnet-ef` is optional for normal startup, but recommended for maintaining
  migrations:

```bash
dotnet tool install --global dotnet-ef
```

## Run locally

1. Restore and build:

```bash
dotnet restore Nexus.sln
dotnet build Nexus.sln --configuration Release
```

2. Run the unit and integration test suites:

```bash
dotnet test Nexus.sln --configuration Release
```

The integration project starts a real PostgreSQL 16 Testcontainer and exercises
real HTTP middleware, Identity, authorization, EF Core, database creation, and
health endpoints.

3. Start the web application:

```bash
cd src/Nexus.Web
dotnet run
```

Development configuration enables database initialization. If no EF migration
exists yet, NEXUS falls back to `EnsureCreated` so a fresh reference checkout
can boot; if migrations exist, they are applied.

4. Register at `/Account/Register`.

The first account in the seeded demo organization becomes a System
Administrator. Later accounts start as Viewer and can be promoted by an
administrator.

5. Open the command center and run:

```text
Scenarios → Simulate Disruption
Supplier Alpha → 30 days → 100% severity
```

The seeded Alpha dependency is intentionally vulnerable, so the scenario
produces an observable cascade, inventory depletion, revenue exposure, and
mitigation candidates.

## Run with Docker Compose

From the repository root:

```bash
docker compose down -v
docker compose build --no-cache
docker compose up
```

Then open:

```text
http://localhost:8080
```

Compose waits for PostgreSQL readiness before starting the web service and
enables the reference database bootstrap automatically. It also disables HTTPS
redirection because the container intentionally exposes plain HTTP on port
8080.

## Database and migrations

The repository is designed to be bootable as a reference implementation even
when a migration snapshot has not been generated. `Database:InitializeOnStartup`
controls the bootstrap behavior:

- `true` + migrations present → apply migrations.
- `true` + no migrations → `EnsureCreated`.
- `false` → application startup does not modify database schema.

For a production deployment, generate and review a normal EF Core migration,
commit the migration files, and apply them in the release pipeline. Do not use
automatic startup DDL as a substitute for a controlled production migration
process.

## AI provider configuration

The default provider is `MockLlmProvider`. It intentionally reports that a
live provider is unavailable instead of fabricating an answer.

To use Anthropic, configure the provider and API key through user secrets or
deployment secret storage. The adapter is selected automatically from
configuration; no controller changes are required. Do **not** commit API keys.

```json
{
  "AI": {
    "Provider": "Anthropic",
    "Model": "<model-id>"
  }
}
```

Set `AI:AnthropicApiKey` through user secrets/environment configuration. The
adapter sends the required Anthropic API headers and calls the Messages API
through `HttpClient`.

The orchestration contract remains unchanged:

```text
Simulation → Mitigation → Specialist evidence → LLM narrative
```

The LLM receives calculated evidence and the user's question; it does not
execute the simulation or manufacture financial figures.

## Testing

### Unit/regression suite

`tests/Nexus.Tests` covers:

- Simulation stockout-day arithmetic.
- Revenue-exposure arithmetic.
- Cascade/BFS stage ordering.
- Graph traversal and SPOF positive/negative cases.
- Supplier risk weighting, normalization, and clamping.
- What-If approximation behavior.
- Natural-language parser behavior.
- Retrieval and query expansion.
- Prompt-injection defense.
- Durable job queue behavior.
- Alert generation/lifecycle.
- Ambient tenant behavior.
- Cross-tenant query and write isolation.

### Integration suite

`tests/Nexus.IntegrationTests` uses Testcontainers PostgreSQL plus
`WebApplicationFactory` to exercise:

- Liveness/readiness endpoints.
- Database initialization against real PostgreSQL.
- Registration and authentication.
- Authorization redirect behavior.
- Real antiforgery-protected form submission.

## CI

GitHub Actions restores, builds, and tests the complete solution on pushes and
pull requests to `main`.

The integration tests use Testcontainers and therefore require the Docker
runtime available on the GitHub-hosted runner.

## Accessibility

The repository includes an accessibility engineering checklist at
[`docs/ACCESSIBILITY.md`](docs/ACCESSIBILITY.md). The implementation includes
semantic landmarks, skip navigation, visible focus, keyboard graph controls,
ARIA labels, a relationship-aware graph text fallback, and reduced-motion
support.

A deployed production application should still receive browser-based axe/
Lighthouse/WAVE checks and a manual keyboard/screen-reader pass. This README
does not claim formal WCAG certification.

## Design principles

### 1. AI does not calculate

All numeric simulation outcomes are produced by deterministic C# services.
The AI layer is an explanation and reasoning surface over evidence that already
exists.

### 2. Human approval is explicit

Mitigation recommendations begin as `Pending`. Only an authorized human action
can approve a strategy, and the decision is audited.

### 3. Tenant isolation is enforced below the UI

Controllers may still express organization constraints for clarity, but EF
Core global filters and the write boundary prevent a controller-level omission
from becoming a cross-tenant data leak.

### 4. Vendor independence

The application depends on `ILLMProvider`, not an LLM vendor SDK. The same
orchestrator can sit above Anthropic, OpenAI, Azure-hosted models, local models,
or a future provider adapter.

### 5. Transparent approximations

What-If mode is explicitly labelled as an approximation and remains separate
from the authoritative simulation path. Lexical retrieval is described as
lexical retrieval rather than being marketed as semantic search.

## Reference-data scale

The deterministic seed creates a network close to the requested demonstration
scale:

- 25 suppliers.
- 40 components.
- 12 factories.
- 8 warehouses.
- 100 products.
- 20 customers.
- Curated vulnerabilities including a true single-source dependency, a
  low-inventory warehouse, and a high-risk supplier.
- Deterministic generated data using a fixed random seed.

## Known production extensions

These are architectural extensions, not missing demo functionality:

- Replace the single-process queue with a distributed worker/queue when running
  multiple application instances.
- Replace lexical retrieval with an embedding/vector retriever when semantic
  search infrastructure is available; `IDocumentRetrievalService` is the swap
  point.
- Replace heuristic mitigation multipliers with a constrained optimization
  solver when real capacity, cost, contractual, and transportation constraints
  are available.
- Add a reviewed EF migration to the repository and manage schema changes in a
  controlled release pipeline.
- Perform deployment-specific threat modeling, penetration testing, and formal
  accessibility/compliance assessment before production use.

## License

This repository is a reference/portfolio implementation. Add a license file
appropriate to your intended distribution before publishing it as an open
source project.

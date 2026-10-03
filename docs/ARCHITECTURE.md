# NEXUS — Architecture & Engineering Reference

**Autonomous Enterprise Decision Engine**

This document contains the complete architectural specification and all 11 required Mermaid diagrams for **NEXUS**.

---

## 1. System Architecture

```mermaid
flowchart TB
    subgraph Client["Enterprise Control Room UI (Server-Rendered Razor MVC)"]
        Razor["ASP.NET Core Razor Views"]
        CSS["Centralized Skeuomorphic CSS (wwwroot/css/*)"]
        JS["Centralized Interactive JS + SVG/Canvas (wwwroot/js/*)"]
    end

    subgraph Monolith["NEXUS Modular Monolith (.NET 9 / ASP.NET Core MVC)"]
        Controllers["Thin MVC & REST API Controllers"]
        AppServices["18 Strongly-Isolated Business Modules"]
        OrTools["Google OR-Tools Multi-Objective Solver (GLOP / SCIP)"]
        MonteCarlo["10,000-Future Monte Carlo Engine"]
        BgWorker["BackgroundJobService (Queued / Running / Completed / Failed / Cancelled)"]
    end

    subgraph DataLayer["Authoritative Data & High-Speed Cache"]
        PG[("PostgreSQL 16 (36 Normalized Entities)")]
        Redis[("Redis 7 (State, Graph, Twin & Job Cache)")]
    end

    subgraph MLService["NEXUS Machine Learning Pipeline (Python 3.11)"]
        FastAPI["FastAPI Inference Service (:8000)"]
        Models["Serialized XGBoost & Scikit-Learn Models (.joblib)"]
    end

    Client <-->|HTTPS / Anti-Forgery / REST JSON| Controllers
    Controllers --> AppServices
    AppServices --> OrTools
    AppServices --> MonteCarlo
    AppServices --> BgWorker
    AppServices <-->|EF Core 9| PG
    AppServices <-->|Distributed Cache| Redis
    AppServices <-->|HTTP JSON Inference| FastAPI
    FastAPI --> Models
```

---

## 2. Modular Monolith Structure

```mermaid
flowchart LR
    subgraph NEXUS_Monolith["NEXUS (.NET 9 Single Deployment Unit)"]
        direction TB
        subgraph CoreTwin["Digital Twin & Supply Chain"]
            M1["Organization"]
            M2["AssetManagement"]
            M3["SupplyChain"]
            M4["Inventory"]
            M5["DemandManagement"]
            M6["DependencyGraph"]
            M7["DigitalTwin"]
        end
        subgraph Engines["Simulation, Optimization & Decision"]
            M8["ScenarioManagement"]
            M9["Simulation"]
            M10["Optimization"]
            M11["DecisionEngine"]
            M12["Explainability"]
        end
        subgraph Intelligence["ML, Analytics & Governance"]
            M13["Forecasting"]
            M14["Analytics"]
            M15["Execution"]
            M16["Feedback"]
            M17["Audit"]
            M18["Identity"]
        end
        SharedKernel["Shared Kernel / Infrastructure / PostgreSQL + Redis"]
    end

    CoreTwin --> SharedKernel
    Engines --> SharedKernel
    Intelligence --> SharedKernel
```

---

## 3. Module Dependencies

```mermaid
graph TD
    DigitalTwin --> AssetManagement
    DigitalTwin --> DependencyGraph
    DigitalTwin --> SupplyChain
    DependencyGraph --> AssetManagement
    ScenarioManagement --> AssetManagement
    Simulation --> ScenarioManagement
    Simulation --> DependencyGraph
    Optimization --> ScenarioManagement
    Optimization --> Simulation
    DecisionEngine --> ScenarioManagement
    DecisionEngine --> DependencyGraph
    DecisionEngine --> Simulation
    DecisionEngine --> Optimization
    DecisionEngine --> Explainability
    Execution --> DecisionEngine
    Feedback --> DecisionEngine
    Feedback --> Execution
    Analytics --> Simulation
    Analytics --> Optimization
    Forecasting --> Audit
```

---

## 4. Database ERD

```mermaid
erDiagram
    EnterpriseOrganization ||--o{ Asset : owns
    EnterpriseOrganization ||--o{ NexusUser : employs
    Asset ||--o| Supplier : specializes
    Asset ||--o| Factory : specializes
    Asset ||--o| Warehouse : specializes
    Asset ||--o| DistributionCenter : specializes
    Asset ||--o| TransportationRoute : specializes
    Asset ||--o| Customer : specializes
    Asset ||--o| Market : specializes
    Asset ||--o{ Dependency : source_or_target
    Warehouse ||--o{ InventorySnapshot : tracks
    Market ||--o{ DemandSnapshot : records
    Asset ||--o{ CapacitySnapshot : monitors
    Asset ||--o{ Risk : exposes
    Scenario ||--o{ ScenarioParameter : configures
    Scenario ||--o{ SimulationRun : drives
    SimulationRun ||--o{ SimulationResult : produces
    Scenario ||--o{ OptimizationRun : optimizes
    OptimizationRun ||--o{ OptimizationConstraint : enforces
    OptimizationRun ||--o{ OptimizationResult : yields
    Decision ||--o{ DecisionOption : ranks
    Decision ||--o{ DecisionApproval : governs
    Decision ||--o| DecisionExplanation : explains
    Decision ||--o{ Execution : dispatches
    Execution ||--o{ ExecutionResult : records
    Decision ||--o{ DecisionOutcome : measures
    DecisionOutcome ||--|| DecisionQuality : evaluates
```

---

## 5. Digital Twin Model

```mermaid
flowchart LR
    Suppliers["50 Suppliers\n(Capacity, Cost, Reliability,\nLead Time, Risk, Availability)"]
    Factories["20 Factories\n(Capacity, Utilization, Load,\nProduction Cost, Workforce)"]
    Warehouses["40 Warehouses\n(Storage Cap, Inventory,\nProcessing Rate, Coverage)"]
    DCs["10 Distribution Centers\n(Throughput, Load, Region)"]
    Routes["100 Transport Routes\n(Mode, Capacity, Transit Days,\nReliability, Cost)"]
    Markets["20 Markets & 500 Products\n(Demand, Revenue, Volatility)"]
    Customers["30 Enterprise Customers\n(SLA Target, Daily Revenue,\nPenalty/Day)"]
    EnterpriseOps["100 Business Processes\n20 Employee Groups\n10 Apps & 10 Infra Nodes"]

    Suppliers --> Factories --> Routes --> Warehouses --> DCs --> Markets --> Customers
    EnterpriseOps -.->|Cross-Dependencies| Factories
    EnterpriseOps -.->|Cross-Dependencies| Warehouses
```

---

## 6. Dependency Graph Algorithms

```mermaid
flowchart TD
    GraphInput["Directed Enterprise Graph\n(Assets V, Dependencies E)"]
    BFS["Multi-Hop BFS / DFS Traversal\n(Upstream & Downstream Reachability)"]
    CriticalPath["DAG Dynamic Programming\nCritical Path Analysis"]
    Bottlenecks["Saturation + Out-Degree Centrality +\nSPOF Bottleneck Detection"]
    Propagation["Cascading Failure Propagation\n(Edge Strength Attenuation & Buffer Drawdown)"]
    ImpactOut["DependencyImpactAnalysisDto\n(Affected Nodes, Warehouses, Revenue at Risk, SLA)"]

    GraphInput --> BFS
    GraphInput --> CriticalPath
    GraphInput --> Bottlenecks
    BFS --> Propagation --> ImpactOut
    CriticalPath --> ImpactOut
    Bottlenecks --> ImpactOut
```

---

## 7. Simulation Pipeline (Deterministic Cascade + Monte Carlo)

```mermaid
flowchart LR
    Scn["What-If Scenario\n(e.g. SUP-001 Cap 100% -> 60%, 14d)"]
    Cascade["Deterministic Day-by-Day Cascade\n(Material Deficit -> Factory Drop ->\nInventory Drawdown -> Order Delay)"]
    MC["Monte Carlo Engine (10,000 Futures)\nBox-Muller Sampling:\nDemand, Reliability, Lead Time,\nDelay, Capacity, Duration"]
    Dist["Dynamic Probability Metrics:\n• Stockout Prob (8.4%)\n• Loss > $1M Prob (5.7%)\n• SLA < 95% Prob (3.2%)\n• 7-Bucket Histogram"]

    Scn --> Cascade
    Scn --> MC
    Cascade --> Dist
    MC --> Dist
```

---

## 8. Optimization Pipeline (Google OR-Tools)

```mermaid
flowchart TB
    Problem["Shortfall & Scenario Parameters"]
    Variables["Decision Variables:\nx_factoryB_shift, x_supplierC_alloc,\nx_warehouse_buffer, x_expedite_route, x_unfulfilled"]
    Constraints["9 Hard Constraints:\nSupplier Cap • Factory Cap • Warehouse Cap •\nTransport Cap • Workforce • Budget •\nInventory • SLA Floor • Lead Time"]
    Objective["Multi-Objective Function:\nMinimize (Cost + Risk + Delivery Time + Inv Cost + Recovery)\nMaximize (Revenue + Service Level + Resilience)"]
    Solver["Google OR-Tools GLOP / SCIP Solver"]
    Frontier["Pareto Decision Frontier:\n• Strategy A ($1.2M, High Risk, 88% SLA)\n• Strategy B ($1.5M, Medium Risk, 96%/98.2% SLA)\n• Strategy C ($1.9M, Low Risk, 99% SLA)"]

    Problem --> Variables --> Constraints --> Objective --> Solver --> Frontier
```

---

## 9. AI Decision Pipeline

```mermaid
sequenceDiagram
    participant User as Operator / Executive
    participant AI as Grounded AI Agent
    participant Twin as Digital Twin & Graph
    participant Sim as Monte Carlo Engine
    participant Opt as Google OR-Tools
    participant Gov as Human-in-the-Loop

    User->>AI: "Our European supplier will lose 40% capacity for two weeks. What should we do?"
    AI->>Twin: Identify SUP-001 & traverse 26 downstream dependencies
    Twin-->>AI: $9.12M unmitigated exposure across FAC-001, WH-001..WH-008
    AI->>Sim: Execute 10,000 stochastic simulations
    Sim-->>AI: Stockout Prob 8.4%, P95 Loss $11.68M
    AI->>Opt: Solve multi-objective LP/MILP across 147 candidates
    Opt-->>AI: Recommend Strategy B (Move 42% to Factory B + Supplier C)
    AI-->>User: Grounded Explainable Recommendation (WHAT / WHY / EXPECTED RESULT)
    User->>Gov: Manager Reviews & Approves Strategy B
```

---

## 10. Decision Workflow & Human-in-the-Loop

```mermaid
stateDiagram-v2
    [*] --> EnterpriseStateMonitoring
    EnterpriseStateMonitoring --> ProblemDetected: Disruption / Bottleneck
    ProblemDetected --> CandidateGeneration: 147 Strategies
    CandidateGeneration --> MonteCarloSimulation: 10,000 Futures
    MonteCarloSimulation --> OrToolsOptimization: Pareto Frontier (A, B, C)
    OrToolsOptimization --> PendingApproval: Explainable Recommendation
    PendingApproval --> Approved: Manager Approves
    PendingApproval --> Rejected: Manager Rejects
    Approved --> Executed: Dispatch Work Orders
    Executed --> OutcomeMeasured: Predicted ($420K) vs Actual ($390K)
    OutcomeMeasured --> DecisionQualityUpdated: Calibration Feedback Loop
```

---

## 11. Machine Learning Pipeline

```mermaid
flowchart LR
    Data["Enterprise Telemetry Dataset\n(Utilization, Reliability, Coverage,\nLead Time, Demand Momentum)"]
    Split["80/20 Train/Validation Split"]
    Train["Scikit-Learn & XGBoost Training\n(6 Domains: Demand, Supplier Failure,\nShortage, Delay, Recovery, SLA Breach)"]
    Eval["Real Validation Evaluation:\nR², MAE, RMSE, ROC-AUC, F1"]
    Serialize["Joblib Model Serialization\n(ML/models/*.joblib + evaluation_metrics.json)"]
    FastAPI["FastAPI Inference Server\n(/predict, /metrics, /train)"]
    DotNet["ASP.NET Core Forecasting Module"]

    Data --> Split --> Train --> Eval --> Serialize --> FastAPI --> DotNet
```

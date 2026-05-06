# ODIN API — Observational Debugging & Intervention Node

**ASP.NET Core 8.0 Backend for the ODIN Intelligent Tutoring System**

The Pedagogical Kernel that powers ODIN's "Stealth Assessment" — a Sequential Processing Pipeline that analyzes every student code submission through Behavioral Filtering, Structural Diagnosis, and Probabilistic Scoring before generating adaptive pedagogical interventions.

---

## Architecture Overview

```
┌──────────────────────────────────────────────────────────────┐
│                    GAME CLIENT (React/Godot)                  │
│         Keystroke Dynamics Monitor + Code Editor              │
└─────────────────────────┬────────────────────────────────────┘
                          │ HTTPS POST /api/submission
                          │ JSON: { sourceCode, keystrokeData }
                          ▼
┌──────────────────────────────────────────────────────────────┐
│              SUBMISSION ORCHESTRATOR (API Gateway)            │
│                   SubmissionController.cs                     │
└──┬──────────┬──────────┬──────────┬──────────┬───────────────┘
   │          │          │          │          │
   ▼          ▼          ▼          ▼          ▼
┌──────┐ ┌────────┐ ┌────────┐ ┌─────────┐ ┌────────────┐
│ HBDA │ │  AST   │ │  BKT   │ │Affective│ │Intervention│
│      │ │Roslyn  │ │Engine  │ │  State  │ │ Controller │
│5-state│ │Parser  │ │4-param │ │  Gate   │ │  + Hints   │
│filter│ │Off-by-1│ │P(L₀)   │ │Helpless │ │  Library   │
│      │ │OOB,etc │ │P(T,G,S)│ │Threshold│ │  NPC       │
└──────┘ └────────┘ └────────┘ └─────────┘ └────────────┘
                          │
                          ▼
             ┌──────────────────────┐
             │   PostgreSQL (Data)  │
             │ D1: Student Profiles │
             │ D2: Interaction Logs │
             │ Scaffolding Library  │
             └──────────────────────┘
```

## Sequential Processing Pipeline

Every submission flows through five stages in order:

| Stage | Module | File | Purpose |
|-------|--------|------|---------|
| 1 | **HBDA** | `Services/HbdaService.cs` | Classifies behavior into 5 states (Table 8 thresholds) |
| 2 | **AST Diagnosis** | `Services/DiagnosticEngine.cs` | Roslyn AST parsing for C# array misconceptions |
| 3 | **BKT Engine** | `Services/BktService.cs` | Four-parameter Bayesian Knowledge Tracing |
| 4 | **Affective State** | `Services/AffectiveStateService.cs` | Helplessness Decision Gate (HBDA + BKT) |
| 5 | **Intervention** | `Services/InterventionController.cs` | Fetches NPC hints from Scaffolding Library |

## HBDA Behavioral States (Table 8)

| State | Thresholds | Score Delta |
|-------|-----------|-------------|
| **Tinkering** | SI ≤ 10s, ED ≤ 2, Error | +10 |
| **Gaming the System** | SI ≤ 5s, HU ≥ 3, ED ≈ 0 | +20 |
| **Wheel-Spinning** | TT ≥ 120s, Attempts ≥ 10, Same Error | +15 |
| **Productive Failure** | Attempts ≥ 3, Error changes, ED ≥ 10 | -15 |
| **Active Thinking** | IL ≥ 30s, KF ≤ 200ms, Near-success | -10 |

## BKT Parameters

| Parameter | Value | Description |
|-----------|-------|-------------|
| P(L₀) | 0.10 | Initial prior probability of mastery |
| P(T) | 0.10 | Learning/transition rate per attempt |
| P(G) | 0.20 | Probability of guessing correctly |
| P(S) | 0.10 | Probability of slipping despite mastery |
| Warm-Up | 3 attempts | Suppress updates during cold start (W-11) |
| Mastery | P(L) ≥ 0.90 + 5 consecutive correct | Level unlock trigger (W-12) |

## AST Diagnostic Rules

The Roslyn-based engine detects these C# array misconceptions:

- **OffByOneError** — `i <= arr.Length` instead of `i < arr.Length`
- **IndexOutOfRange** — Hardcoded index exceeding declared array size
- **UninitializedArray** — Array declared but never `new`-initialized
- **InvalidArraySize** — Array created with zero or negative size
- **DimensionMismatch** — Wrong number of indices for multidimensional arrays
- **InfiniteLoop** — `while(true)` without break, or for-loops with no increment
- **SyntaxError** — Standard Roslyn compiler syntax errors

---

## Quick Start

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [PostgreSQL 15+](https://www.postgresql.org/download/)

### 1. Set up the database

```bash
# Create the PostgreSQL database and user
psql -U postgres -c "CREATE USER odin_user WITH PASSWORD 'your_password';"
psql -U postgres -c "CREATE DATABASE odin_db OWNER odin_user;"
```

### 2. Configure connection string

Edit `appsettings.json` if your PostgreSQL settings differ:
```json
{
  "ConnectionStrings": {
    "OdinDb": "Host=localhost;Port=5432;Database=odin_db;Username=odin_user;Password=your_password"
  }
}
```

### 3. Restore and run

```bash
cd ODIN.Api
dotnet restore
dotnet ef migrations add InitialCreate
dotnet ef database update
dotnet run
```

The API starts at `http://localhost:5000` with Swagger UI at the root.

### 4. Test the pipeline

Use `ODIN.Api.http` with VS Code's REST Client extension, or use Swagger UI to:

1. **Register** a player → `POST /api/player/register`
2. **Create a session** → `POST /api/session`
3. **Submit code** → `POST /api/submission` (triggers the full pipeline)
4. **Check results** — the response contains diagnostics, behavioral state, BKT mastery, and any NPC intervention

---

## API Endpoints

### Core Pipeline
| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/submission` | **Main endpoint** — runs full pipeline |

### Player Management
| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/player/register` | Register new student |
| `POST` | `/api/player/login` | Authenticate + retrieve save data |
| `GET` | `/api/player/{id}` | Get player profile + mastery states |
| `GET` | `/api/player/{id}/history` | Get interaction history |

### Session Management
| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/session` | Start new game session |
| `GET` | `/api/session/{id}` | Get session details |
| `PATCH` | `/api/session/{id}/end` | End a session |
| `GET` | `/api/session/player/{id}` | List player's sessions |

### Instructor Dashboard
| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/instructor/overview` | Class-wide summary |
| `GET` | `/api/instructor/bottlenecks` | Cognitive bottleneck analysis |
| `GET` | `/api/instructor/students` | Student list sorted by risk |
| `GET` | `/api/instructor/interventions` | Recent intervention log |
| `GET` | `/api/instructor/mastery-heatmap` | Mastery data across skills |

### Puzzles
| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/puzzle/level/{level}` | Get puzzles for dungeon level |
| `GET` | `/api/puzzle/{id}` | Get specific puzzle |

---

## Project Structure

```
ODIN.Api/
├── Controllers/
│   ├── SubmissionController.cs      # Submission Orchestrator (main pipeline)
│   ├── PlayerController.cs          # Player registration, login, profiles
│   ├── SessionController.cs         # Game session management
│   ├── InstructorController.cs      # Instructor Dashboard endpoints
│   └── PuzzleController.cs          # Puzzle/level management
├── Data/
│   └── OdinDbContext.cs             # EF Core context + seed data
├── Models/
│   ├── Domain/
│   │   ├── Player.cs                # Player entity + MasteryState
│   │   ├── GameSession.cs           # Session tracking
│   │   ├── CodeSubmission.cs        # Central data payload
│   │   ├── InteractionLog.cs        # D2 behavioral logs
│   │   ├── ScaffoldingHint.cs       # Pedagogical hint library
│   │   └── Puzzle.cs                # Abstract Puzzle + ArrayPuzzle
│   ├── DTOs/
│   │   ├── SubmissionRequest.cs     # Client → Server JSON contract
│   │   └── SubmissionResponse.cs    # Server → Client response
│   └── Enums/
│       ├── BehaviorState.cs         # Five HBDA states
│       ├── DiagnosticCategory.cs    # AST error categories
│       ├── InterventionType.cs      # Intervention actions
│       └── SkillType.cs             # Array skill categories
├── Services/
│   ├── Interfaces/
│   │   └── IServices.cs             # All service interfaces + result types
│   ├── HbdaService.cs               # Stage 1: Behavioral gatekeeper
│   ├── DiagnosticEngine.cs          # Stage 2: Roslyn AST parser
│   ├── BktService.cs                # Stage 3: Bayesian Knowledge Tracing
│   ├── AffectiveStateService.cs     # Helplessness Decision Gate
│   ├── InterventionController.cs    # Adaptive hint selection
│   └── EditDistanceCalculator.cs    # Levenshtein distance utility
├── Program.cs                       # DI wiring + middleware
├── appsettings.json                 # Configuration
├── ODIN.Api.http                    # Test request collection
└── README.md
```

## Calibration Notes

The following values are **configurable** and should be calibrated during Phase 1 (Algorithm Benchmarking) with expert psychologists:

- **HBDA thresholds** in `HbdaService.cs` (submission interval, edit distance, etc.)
- **Helplessness Score weights** in `HbdaService.cs` (+20, +15, +10, -10, -15)
- **Helplessness Decision Gate** threshold in `AffectiveStateService.cs` (currently 50.0)
- **BKT parameters** in `BktService.cs` (P(L₀), P(T), P(G), P(S))
- **Mastery threshold** in `BktService.cs` (currently P(L) ≥ 0.90)
- **Scaffolding hint text** in `OdinDbContext.cs` seed data (to be refined with psychologists)

## White Box Test Cases Covered

| ID | Test Case | Implementation |
|----|-----------|---------------|
| W-04 | AST: Syntax Validation | `DiagnosticEngine` → Roslyn parser |
| W-05 | AST: Semantic Analysis (Off-by-One) | `CheckOffByOneErrors()` |
| W-06 | HBDA: Gaming Detection | `IsGamingTheSystem()` |
| W-07 | HBDA: Wheel-Spinning | `IsWheelSpinning()` |
| W-08 | HBDA: Tinkering | `IsTinkering()` |
| W-09 | HBDA: Productive Failure | `IsProductiveFailure()` |
| W-10 | HBDA: Active Thinking | `IsActiveThinking()` |
| W-11 | BKT: Cold Start (Warm-Up) | `BktService.WarmUpAttempts = 3` |
| W-12 | BKT: Mastery Trigger | `P(L) ≥ 0.90 + 5 consecutive correct` |

## License

This project is part of an academic thesis — Observational Debugging & Intervention Node (ODIN).

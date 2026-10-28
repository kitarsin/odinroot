# ODIN Core API

The ODIN Core API is the ASP.NET Core backend for the ODIN intelligent tutoring system. It authenticates requests with Supabase JWTs, evaluates student code, records session and interaction data, tracks mastery, and returns feedback for the web and Godot clients.

## C# learning focus

ODIN's current learning domain is college-level C# arrays. The tasks focus on array problem-solving and common logic misconceptions, especially off-by-one indexing and out-of-bounds access. Collections and object-oriented constructs are possible future expansions, not current supported learning domains.

The API uses rule-based code diagnostics and observable behavior signals to guide support. It does not diagnose learned helplessness or any other clinical condition.

## Request pipeline

A code submission is processed by the backend services:

1. **Behavior classification** evaluates interaction and submission telemetry.
2. **Code diagnosis** parses and checks submitted C# array solutions.
3. **Mastery tracking** updates the student's skill estimate.
4. **Affective score tracking** records a research/admin signal.
5. **Intervention selection** returns puzzle hints, NPC dialogue, rewards, or progression updates.

The main submission endpoint is `POST /api/submission`. Its request and response contracts are defined in `Models/DTOs/SubmissionRequest.cs` and `Models/DTOs/SubmissionResponse.cs`.

## Stack

- ASP.NET Core 8
- Entity Framework Core with Npgsql
- PostgreSQL through the existing ODIN Supabase database
- Supabase JWT bearer authentication
- Roslyn C# parsing and diagnostics
- Swagger / OpenAPI in Development

## Run locally

### Prerequisites

- .NET 8 SDK
- Access to an ODIN Supabase PostgreSQL database and its existing schema
- Supabase project URL and JWT signing secret

### Configure secrets

The API reads the PostgreSQL connection string from `ConnectionStrings:SupabaseDb`, and Supabase authentication settings from `Supabase:Url` and `Supabase:JwtSecret`. For local development, store them with .NET user secrets:

```bash
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:SupabaseDb" "Host=your-db-host;Port=5432;Database=postgres;Username=your-user;Password=your-password;SSL Mode=Require"
dotnet user-secrets set "Supabase:Url" "https://your-project.supabase.co"
dotnet user-secrets set "Supabase:JwtSecret" "your-supabase-jwt-secret"
dotnet user-secrets set "CORS_ALLOWED_ORIGINS" "http://localhost:5173"
```

Keep database credentials and JWT secrets out of source control. The frontend uses `VITE_ODIN_API_URL` to choose this API's base URL.

### Restore and start

From the repository root:

```bash
dotnet restore
dotnet run --environment Development --urls http://localhost:5000
```

Swagger UI is available at [http://localhost:5000/swagger](http://localhost:5000/swagger) in Development. The web client defaults to this API URL.

The API connects to the existing Supabase schema; it does not create or migrate the database schema at startup. Ensure the database is provisioned before running the service.

## API routes

All routes use the `/api` prefix. Authenticated requests should include the Supabase access token as a bearer token.

### Submissions

| Method | Route | Purpose |
| --- | --- | --- |
| `POST` | `/api/submission` | Evaluate a code submission or record supported session-end telemetry |
| `GET` | `/api/submission/session/{sessionId}` | List submissions for a session |
| `POST` | `/api/submission/reevaluate` | Re-evaluate submissions using the controller's supported request |

### Sessions

| Method | Route | Purpose |
| --- | --- | --- |
| `POST` | `/api/session` | Create a game session |
| `GET` | `/api/session/{id}` | Get session details |
| `PATCH` | `/api/session/{id}/end` | End a session |
| `GET` | `/api/session/player/{userId}` | List a player's sessions |

### Players and puzzles

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/api/player/{userId}` | Get a player profile and progress |
| `GET` | `/api/player/{userId}/gamestate` | Read saved game state |
| `PUT` | `/api/player/{userId}/gamestate` | Save game state |
| `POST` | `/api/player/{userId}/reset` | Reset player progress |
| `GET` | `/api/player/{userId}/history` | Get player interaction history |
| `GET` | `/api/puzzle/level/{level}` | Get puzzles for a level |
| `GET` | `/api/puzzle/{id}` | Get a puzzle |

### Instructor views

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/api/instructor/overview` | Class overview |
| `GET` | `/api/instructor/bottlenecks` | Learning bottleneck summary |
| `GET` | `/api/instructor/students` | Student overview |
| `GET` | `/api/instructor/interventions` | Recent intervention records |
| `GET` | `/api/instructor/mastery-heatmap` | Mastery summary |

For the request collection, see `ODIN.Api.http`. Swagger is enabled only in Development.

## Configuration reference

| Setting | Purpose |
| --- | --- |
| `ConnectionStrings:SupabaseDb` | PostgreSQL connection string |
| `Supabase:Url` | Supabase project URL used to validate token issuer |
| `Supabase:JwtSecret` | JWT signing secret used for bearer-token validation |
| `Cors:AllowedOrigins`, `Cors:AllowedOriginsCsv`, or `CORS_ALLOWED_ORIGINS` | Allowed browser origins; comma-separated settings are supported |

## Notes on interpretation

Behavior labels and mastery estimates are generated by deterministic rules and statistical models. They describe system observations for instructional support; they are not clinical conclusions. Thresholds and intervention content should be reviewed for the intended study or deployment context.

## Related project

- [ODIN web client](https://github.com/kitarsin/ODIN)

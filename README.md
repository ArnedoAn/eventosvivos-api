# EventosVivos API

Backend for an event-reservation system. Prevents overselling, enforces venue availability rules, and exposes a secured REST API with JWT role-based auth.

---

## Run locally (Docker, recommended)

```bash
docker compose up --build
```

- API: http://localhost:8080/swagger
- Postgres: `localhost:5432` (database `eventosvivos`, user `postgres`, password `postgres`)
- The API container waits for the database healthcheck, then applies EF Core migrations and seeds dev data automatically.

## Run locally (no Docker)

1. Install .NET 10 SDK and PostgreSQL 15+.
2. Create a local database and update the connection string, e.g.:

```bash
dotnet user-secrets init --project src/Api
dotnet user-secrets set --project src/Api "ConnectionStrings:DefaultConnection" "Host=localhost;Database=eventosvivos;Username=postgres;Password=postgres"
```

3. Run the API:

```bash
dotnet run --project src/Api
```

Migrations and seed data run on startup. Swagger is available at `/swagger` in Development.

---

## Architecture & justification

This project uses **Clean Architecture** with strict layer dependencies:

| Layer | Project | Depends on |
|---|---|---|
| Domain | `src/Domain` | nothing |
| Application | `src/Application` | Domain |
| Infrastructure | `src/Infrastructure` | Application, Domain |
| API | `src/Api` | Application, Infrastructure |

### Key design decisions

- **Event as aggregate root.** `Event` is the consistency boundary for seat inventory. The invariant `SeatsTaken + SeatsLost <= Capacity` is enforced inside the aggregate; infrastructure adds a PostgreSQL `CHECK` constraint as a backstop.
- **Ordered rule chain.** Business rules implement `IReservationRule` and run by priority (`Order`): `LateReservationRule` → `Near24hRule` → `HighPriceRule` → `AvailabilityRule`. This makes rule precedence explicit and testable.
- **Optimistic concurrency.** EF Core maps PostgreSQL's `xmin` system column as a row version. Concurrent writes that would violate inventory retry up to three times before failing, eliminating overselling without long-lived locks.
- **Result-based errors.** Expected business failures (sold out, late reservation, etc.) return `Result<T>` with a structured `Error`. Exceptions are reserved for truly exceptional/infrastructure faults.
- **Feature flags.** `IReservationOptions` exposes runtime-configurable reservation behavior (pending inventory hold and expiration) without changing domain logic.
- **Auto-completion on read (RN-06).** Events past their end date are marked `Completado` when read, via a seam that can be moved to a background job later.
- **JWT + roles.** Authentication uses JWT bearer tokens; authorization uses role claims (`Admin`, `User`).

---

## Feature flags

Configure via environment variables (`Reservation__*` in Docker, `Reservation:` in appsettings).

| Variable | Type | Default | Effect |
|---|---|---|---|
| `Reservation__PendingHoldsInventory` | `bool` | `true` | If `true`, `PendientePago` reservations consume inventory immediately. If `false`, only `Confirmada` reservations consume inventory. |
| `Reservation__PendingExpirationMinutes` | `int` | `0` | `0` means pending reservations never expire. `>0` means reservations older than N minutes are released on read/sweep (only meaningful when `PendingHoldsInventory` is `true`). |

---

## Seeded accounts

> **Warning:** These accounts are seeded only in Development and are intended for local testing. Change or disable seed hashes in production.

| Role | Email | Password |
|---|---|---|
| Admin | `admin@eventosvivos.com` | `Admin123!` |
| User | `user@eventosvivos.com` | `User123!` |

Log in via `POST /api/auth/login` to obtain a JWT token.

---

## Tech stack & test commands

- **.NET 10** · ASP.NET Core Web API
- **EF Core 10** + **Npgsql** on **PostgreSQL**
- **MediatR** for CQRS-lite commands/queries
- **FluentValidation** for input validation
- **BCrypt.Net-Next** for password hashing
- **xUnit**, **FluentAssertions**, **Testcontainers.PostgreSql**

### Run tests

```bash
# All tests
dotnet test

# Individual test projects
dotnet test tests/Domain.Tests
dotnet test tests/Application.Tests
dotnet test tests/Infrastructure.Tests
dotnet test tests/Integration.Tests
```

> `Integration.Tests` spins up a PostgreSQL Testcontainer automatically and can take longer on first run.

---
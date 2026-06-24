# AGENTS.md — EventosVivos API

## Build / Test / Run

```bash
dotnet build                              # Build entire solution
rtk dotnet test                           # Run all tests
dotnet test tests/Domain.Tests            # Run Domain tests only
dotnet test tests/Application.Tests       # Run Application tests only
dotnet test tests/Infrastructure.Tests    # Run Infrastructure tests only
dotnet test tests/Integration.Tests       # Run Integration tests (needs Docker + Postgres)
dotnet run --project src/Api              # Start API (migrates + seeds on startup)
```

The connection string is read from `ConnectionStrings:DefaultConnection` (e.g., `ConnectionStrings__DefaultConnection` as an environment variable).

## Architecture

Clean Architecture, 8 projects in `EventosVivos.slnx` (new XML solution format):

| Layer | Project | Depends On |
|-------|---------|------------|
| Domain | `src/Domain` | nothing |
| Application | `src/Application` | Domain |
| Infrastructure | `src/Infrastructure` | Application, Domain |
| Api | `src/Api` | Application, Infrastructure |
| Domain.Tests | `tests/Domain.Tests` | Domain |
| Application.Tests | `tests/Application.Tests` | Application, Domain |
| Infrastructure.Tests | `tests/Infrastructure.Tests` | Infrastructure, Application, Domain |
| Integration.Tests | `tests/Integration.Tests` | Api, Infrastructure, Application, Domain |

Never invert these dependencies. No business exceptions — expected failures return `Result<T>`. All timestamps are UTC.

## Implementation Roadmap

`PLAN.md` defines 21 sequential TDD tasks (Task 0 through Task 20). Task 0 is done; the rest build the system in this order: Domain (1–8), Application (9–15), Infrastructure (16–17), API + integration tests (18), Docker + README (19–20).

## Key Constraints

- Target framework `net10.0`; `Nullable` and `ImplicitUsings` enabled everywhere
- TDD mandatory per task: failing test first, minimal implementation, all green, then commit
- Domain types: `Result`/`Result<T>` with `Error` record; exceptions only for infra/exceptional faults
- Aggregate root `Event` owns inventory invariant: `SeatsTaken + SeatsLost <= Capacity`
- Reservation states: `PendientePago`, `Confirmada`, `Cancelada`
- Reservation rule priority (lower `Order` runs first): `LateReservationRule` → `Near24hRule` → `HighPriceRule` → `AvailabilityRule`
- Feature flags via `IReservationOptions`: `PendingHoldsInventory` (default `true`), `PendingExpirationMinutes` (default `0`)
- Concurrency: Postgres `xmin` row version + optimistic retry (up to 3×)
- EF Core migration command: `dotnet ef migrations add <Name> -p src/Infrastructure -s src/Api`

## Test Projects

- `tests/Domain.Tests` — unit tests on domain (no external deps)
- `tests/Application.Tests` — handler tests with in-memory EF + mocked clock/options
- `tests/Infrastructure.Tests` — tests for Infrastructure services (DI, options, security, retry policy)
- `tests/Integration.Tests` — `WebApplicationFactory` + Testcontainers Postgres; concurrency oversell proof

Test packages installed: xUnit 2.9.3, coverlet 6.0.4, MS Test.Sdk 17.14.1, FluentAssertions 8.2.0.
Integration tests also use `Testcontainers.PostgreSql` 4.4.0 and `Microsoft.AspNetCore.Mvc.Testing` 10.0.9.

# EventosVivos API — Architecture Audit & Correction Plan (refined)

## Context

A senior-architect audit of the EventosVivos reservation API (against `PLAN.md`: RF-01..06, RN-01..07, concurrency, auth, Docker) found that although per-task TDD looks complete in `.superpowers/sdd/progress.md`, **composition-root defects mean spec-mandated behavior never runs in the assembled app**, and several security/operational issues remain.

Direct verification of every claim (grep/read of DI, handlers, `Program.cs`, `appsettings.json`, `PLAN.md`) confirmed the wiring findings — **and surfaced a blocking problem the draft missed: the uncommitted working tree on `feature/implementation` does not compile.** The in-progress expiration-sweep + seed rework added call sites, DI registration, domain methods, and test doubles for two types that were **never defined**:

- `IReservationExpirer` / `ReservationExpirer` — referenced in `Infrastructure/DependencyInjection.cs:37`, in all four reservation/report handlers, and implemented as test doubles in the test diff, but **no interface or class exists** anywhere in the repo.
- `DevDataSeeder` — referenced in `Program.cs:85`, but only `SeedData`/`SeedPasswords` exist.

The domain side of the feature *was* applied (`Reservation.Expire`, `Event.ReleasePendingHold` exist). Because the tree doesn't build, "write a failing test first" is impossible until compilation is restored. **Decisions taken with the user:** complete the missing types (don't revert); keep the expirer in write handlers within the retry policy but remove it from the read-only query; add `UserId` ownership to close the IDOR.

**Intended outcome:** a compiling, green solution where the business rules, validation, ownership checks, and expiration sweep that the spec mandates actually execute in the composed application — protected by a new composition-root / pipeline / authz test layer that the current by-hand handler tests structurally cannot provide.

## Confirmed findings (verified, not assumed)

| # | Finding | Status |
|---|---------|--------|
| **B0** | **Working tree does not compile** — `IReservationExpirer`/`ReservationExpirer` and `DevDataSeeder` referenced but undefined. | **Confirmed (new)** |
| **C1** | No `IReservationRule` impl registered; `ReservationRuleSet` (Singleton, `Application/DI:18`) resolves an **empty** `IEnumerable` → RN-04/RF-03/RN-05 silently not enforced. `AvailabilityRule` only backstopped by the DB CHECK + `xmin`. | Confirmed |
| **C2** | `AddValidatorsFromAssembly` present but **no `IPipelineBehavior`/`AddOpenBehavior`** → validators are dead code; `ResultExtensions` `validation.*`→400 branch (`ResultExtensions.cs:43`) is unreachable; `BuyerName`/`BuyerEmail` bounds never enforced. | Confirmed |
| **H1** | IDOR — `CreateReservationCommand` has no owner; `Reservation` has no `UserId`; Cancel loads by id only; JWT `sub` issued (`JwtTokenService.cs:23`) but never consumed → any `User` can cancel any reservation by GUID. | Confirmed |
| **H2** | `PendingExpirationMinutes = 0` (inert default); expirer is a **write called from the read-only `GetOccupancyHandler`** outside the retry policy. | Confirmed |
| **H3** | `Jwt:Key` is the committed placeholder; `Program.cs:20` checks presence only (no length/placeholder/entropy guard). (`ExpiryMinutes` is `60`, not `0` — only risky if omitted.) | Confirmed (corrected) |
| **H4** | `Program.cs:81` runs `MigrateAsync()` unconditionally in all environments. | Confirmed |
| **M1** | Venue seed deviates from `PLAN.md:23` (`Auditorio Central/200`, `Sala Norte/50/Bogotá`, `Arena Sur/500/Medellín`). Note: the **committed** seed already deviated (`Teatro Municipal/500`…); the rework deviated differently (`Estadio El Campín/60000`…). User `HasData` was removed → prod boots with no admin. | Confirmed (corrected) |
| **M2** | `Event.ReleaseOnCancel` (`EventAggregate.cs:130`) **throws** instead of returning `Result` (inconsistent with sibling `ReleasePendingHold` which returns `Result`). | Confirmed |
| **M3** | `LoginHandler` skips `passwordHasher.Verify` on unknown email → timing enumeration side-channel. | Confirmed |
| **M4** | `Reservation.Cancel` now nulls `Code` (audit loss); confirm uniqueness pre-check filters `Status == Confirmada` while DB index is `Code_Value IS NOT NULL` — aligned only by today's cancel-nulls-code invariant. | Confirmed |
| **LOW** | `Money.InvalidAmount` PascalCase vs kebab elsewhere; `ReservationRuleSet` Singleton captive risk; `Func<int>? randomSixDigits` DI seam → prefer `IRandomProvider`; `JwtTokenService` uses `DateTime.UtcNow` not `IClock`. | Confirmed |

**Verified correct (no defect):** Result/Error + clean-arch purity; value objects; `Event.Create` (RN-01/RN-03); inventory hold/consume/release incl. RN-07; rule classes' own logic/ordering; `xmin` + `ck_event_capacity` CHECK + owned types + filtered code index; `EfVenueScheduleChecker` predicate; thin controllers; no stack-trace leak; the concurrency oversell integration test is genuine.

**Root cause:** handler/unit tests construct dependencies by hand and bypass MediatR/DI, so DI-wiring bugs (C1, C2) and the missing types (B0) are structurally invisible. The fix includes a composition-root / pipeline test layer.

## Dependency order

```
B0 restore compile ── prerequisite for everything ──┐
   ├─ create IReservationExpirer + ReservationExpirer (uses existing Reservation.Expire + Event.ReleasePendingHold)
   └─ create DevDataSeeder (admin+user) replacing removed HasData
        │
        ▼
C1 register rules ──► C2 validation pipeline ──► H1 ownership/UserId (+migration)
        │                                              │
        ▼                                              ▼
H3 JWT fail-fast ──► H4 gate migrate ──► H2 expirer placement (keep in writes, drop from query)
        │
        ▼
M1 venues→spec + admin seed ──► M2/M3/M4 ──► LOW sweep + test-gap closure
        │
        ▼
   green: dotnet test (4 projects) + new composition-root/pipeline/authz tests
```

Each step: **failing test first → minimal fix → green → commit** (repo TDD convention). B0 is the exception — it restores buildability so the rest can be test-driven; cover it with the composition-root test in step 1.

## Correction plan

**0. B0 — restore compilation (prerequisite).**
- Add `IReservationExpirer` (with `Task ExpireOverduePendingReservationsAsync(Guid eventId, DateTime nowUtc, CancellationToken ct)`). Place the interface in `src/Application/Abstractions/` (it's consumed by Application handlers; mirror `IConcurrencyRetryPolicy`).
- Implement `ReservationExpirer` in `src/Infrastructure/Persistence/`: early-return when `IReservationOptions.PendingExpirationMinutes <= 0`; else load `PendientePago` reservations for `eventId` older than the cutoff, call existing `Reservation.Expire(now)` and `Event.ReleasePendingHold(qty)`, then `SaveChangesAsync`. Use the `EagerReservationExpirer` test double in `CreateReservationHandlerTests` (test diff ~line 612) as the behavioral contract.
- Add `DevDataSeeder` in `src/Infrastructure/Persistence/Seed/` with `static Task SeedAsync(AppDbContext)` that idempotently inserts the admin + user accounts (BCrypt hashes via existing `SeedPasswords`), replacing the `AppUserConfiguration` `HasData` that the rework removed.

**1. C1 — register the rules + composition-root test.** In `Application/DependencyInjection.cs` register the four `IReservationRule` impls (assembly-scan `Domain.Rules` for `IReservationRule`, or four explicit `AddScoped`); make `ReservationRuleSet` `Scoped` (drop Singleton — also resolves the LOW captive-dependency item). **New test:** build the real provider, assert `ReservationRuleSet` resolves with 4 ordered rules; integration test proving RN-04 / RF-03 / RN-05 reject through the real endpoint.

**2. C2 — wire validation.** Add `ValidationBehavior<TRequest,TResponse> : IPipelineBehavior<...>` in `src/Application/` that runs registered `IValidator`s and returns failures as `validation.*` errors (→ 400 via existing `ResultExtensions.cs:43`); register with `AddOpenBehavior(typeof(ValidationBehavior<,>))`. **New test:** post over-length `BuyerName` / malformed email → 400.

**3. H1 — ownership/UserId.** Add `UserId` to `Reservation` (+ new migration) and to `CreateReservationCommand`. In `ReservationsController`, bind a body DTO (`EventId,Quantity,BuyerName,BuyerEmail`) and construct the command with `UserId` from the JWT (`User.FindFirst(ClaimTypes.NameIdentifier)`/`sub`) — never from the body. Persist via `Reservation.Create`. Enforce ownership in `CancelReservationHandler` (and Confirm if needed), with Admin bypass. **New test:** User A cannot cancel User B's reservation (403/404).

**4. H3 — JWT hardening.** In `Program.cs`, fail fast if `Jwt:Key` is missing, `< 32 bytes`, or equals the placeholder; guard `ExpiryMinutes > 0`. Keep the real key in env/secrets. **New test:** app refuses to start with the placeholder key.

**5. H4 — gate migrate.** Run `MigrateAsync()` only outside Production (or behind a `RunMigrations` flag); document migrations as a deploy step in `README`.

**6. H2 — expiration sweep placement.** Set a sensible non-zero `PendingExpirationMinutes` default (and document `0` = disabled). **Remove the expirer call from `GetOccupancyHandler`** (queries must not write); keep it in Create/Cancel/Confirm where it already runs inside the retry delegate. **New test:** integration test for the sweep with `PendingExpirationMinutes > 0` releasing an overdue hold.

**7. M1 — venues→spec + admin seed.** Set `VenueConfiguration` `HasData` to the `PLAN.md:23` triplet (`Auditorio Central/200/Bogotá`, `Sala Norte/50/Bogotá`, `Arena Sur/500/Medellín`) and regenerate the seed migration (the snapshot was hand-edited without a matching migration — reconcile). Ensure `DevDataSeeder` (step 0) provides the admin so events can be created.

**8. M2 / M3 / M4.** Make `Event.ReleaseOnCancel` return `Result` (and have `CancelReservationHandler` honor it). Add a dummy BCrypt verify on the unknown-email path in `LoginHandler`. Reconcile the confirm uniqueness pre-check with the DB filtered index, and keep `Code` on cancel for audit (adjust the pre-check filter accordingly).

**9. LOW sweep + test-gap closure.** Kebab-case `Money.InvalidAmount`; replace the `Func<int>?` seam in `ConfirmReservationHandler` with an `IRandomProvider`; use `IClock` in `JwtTokenService`. Add integration tests for `GET /api/events` filters, occupancy 404, and the cancel/confirm role matrix.

## Verification

- `dotnet test` across all four projects green — **including** the new composition-root, validation-pipeline, and ownership/authz tests. (Note: `dotnet` is not on PATH in this planning sandbox; the implementer must run it where the SDK is available.)
- Manual via `docker compose up --build` → `/swagger`: a `<1h` / `>5-within-24h` / `>10 high-price` reservation is now **rejected** (proves C1); over-length `buyerName` → 400 (proves C2); User A cannot cancel User B's reservation (proves H1); app refuses to start with the placeholder key (proves H3); an overdue pending hold is released when `PendingExpirationMinutes > 0` (proves H2 + B0).

## Critical files

`src/Application/DependencyInjection.cs` (C1, C2) · `src/Application/Abstractions/IReservationExpirer.cs` *(new, B0)* · `src/Infrastructure/Persistence/ReservationExpirer.cs` *(new, B0)* · `src/Infrastructure/Persistence/Seed/DevDataSeeder.cs` *(new, B0)* · `src/Api/Program.cs` + `src/Api/appsettings.json` (H3, H4, H2 default) · `src/Api/Controllers/ReservationsController.cs` + `src/Application/Reservations/CreateReservation/*`, `CancelReservation/*`, `ConfirmReservation/*` (H1, M4) · `src/Application/Reports/GetOccupancy/GetOccupancyHandler.cs` (H2) · `src/Domain/Events/EventAggregate.cs` (M2) · `src/Domain/Reservations/Reservation.cs` (H1 UserId, M4) · `src/Infrastructure/Persistence/Configurations/VenueConfiguration.cs` + `Migrations/*` (M1) · `src/Application/Auth/Login/LoginHandler.cs` (M3) · `src/Infrastructure/Security/JwtTokenService.cs` (LOW).
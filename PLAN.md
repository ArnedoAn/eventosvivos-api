# EventosVivos API — Implementation Plan

> **For agentic workers:** Implement task-by-task. Each task ends with a green test suite and a commit. Steps use checkbox (`- [ ]`) syntax for tracking. TDD is mandatory: write the failing test first, watch it fail, implement the minimum, watch it pass, commit.

**Goal:** Build the backend core of an event-reservation system that prevents overselling, detects venue schedule conflicts, and enforces a set of explicit business rules, exposed as a secured RESTful API.

**Architecture:** Clean Architecture (Domain → Application → Infrastructure → Api). The **Event** is the aggregate root and the consistency boundary for seat inventory; it owns the invariant `SeatsTaken + SeatsLost <= Capacity`. Business rules are modeled as an ordered chain of rule objects. Business errors flow as a `Result<T>` (no exceptions for expected failures). CQRS-lite via MediatR; input validation via FluentValidation; auth via JWT + roles.

**Tech Stack:** .NET 10 · ASP.NET Core Web API · EF Core 10 + Npgsql/PostgreSQL · MediatR 14 · FluentValidation 12 · BCrypt.Net-Next · JwtBearer 10 · Swashbuckle 10 · xUnit + FluentAssertions + Testcontainers (PostgreSQL).

---

## Global Constraints

- **Target framework:** `net10.0` for every project. `Nullable` enabled. `ImplicitUsings` enabled.
- **Layer dependency rule:** Domain depends on nothing. Application depends only on Domain. Infrastructure depends on Application + Domain. Api depends on Application + Infrastructure. Never invert.
- **No business exceptions:** expected business failures return `Result`/`Result<T>`. Exceptions are for truly exceptional/infra faults only.
- **All timestamps are UTC.** Persist and compare in UTC; convert at the edge only.
- **Reservation states:** `PendientePago`, `Confirmada`, `Cancelada`. `Confirmada` ≡ "pagada".
- **Event types:** `Conferencia`, `Taller`, `Concierto`.
- **Event statuses:** `Activo`, `Cancelado`, `Completado`.
- **Reservation code format:** `EV-{6 digits}`, unique.
- **Venues are reference data** (seeded, read-only): `{1: Auditorio Central, cap 200, Bogotá}`, `{2: Sala Norte, cap 50, Bogotá}`, `{3: Arena Sur, cap 500, Medellín}`.
- **Feature flags (runtime-configurable, injected via `IReservationOptions`):**
  - `PendingHoldsInventory` (bool, default `true`): if true, `PendientePago` reservations increment `SeatsTaken` at reserve-time; if false, only `Confirmada` consumes inventory (incremented at confirm-time).
  - `PendingExpirationMinutes` (int, default `0`): `0` = pending never expires; `>0` = pending older than N minutes is released on read/sweep (only meaningful when `PendingHoldsInventory` is true).

---

## Requirement → Task Map (self-review checklist)

| Req | Where covered |
|-----|---------------|
| RF-01 Create event | Task 9 |
| RF-02 List + filters | Task 10 |
| RF-03 Reserve | Task 7, 11 |
| RF-04 Confirm payment | Task 12 |
| RF-05 Cancel | Task 13 |
| RF-06 Occupancy report | Task 14 |
| RN-01 cap ≤ venue | Task 4 |
| RN-02 venue overlap | Task 6, 9 |
| RN-03 weekend night start | Task 4 |
| RN-04 reserve <1h blocked | Task 7 |
| RN-05 price >$100 → max 10 | Task 7 |
| RF-03 <24h → max 5 (priority over RN-05) | Task 7 |
| RN-06 auto-completado on read | Task 5 |
| RN-07 cancel <48h confirmed → lost | Task 8, 13 |
| Concurrency / no oversell | Task 5, 18 |
| Auth JWT + roles | Task 15 |
| Tests | every task + Task 18 |
| Docker / deploy | Task 19, 20 |

---

## File Structure

```
src/
  Domain/
    Common/Result.cs                  Result, Result<T>, Error
    Common/Entity.cs                  base entity + Id
    Enums/EventType.cs  EventStatus.cs  ReservationStatus.cs
    ValueObjects/Money.cs  Email.cs  DateRange.cs  ReservationCode.cs
    Events/EventAggregate.cs          aggregate root: Event (inventory invariant)
    Reservations/Reservation.cs       reservation entity
    Venues/Venue.cs                   reference data
    Rules/IReservationRule.cs         ordered rule contract
    Rules/LateReservationRule.cs      RN-04
    Rules/Near24hRule.cs              RF-03 (priority)
    Rules/HighPriceRule.cs            RN-05
    Rules/AvailabilityRule.cs         RN-01 capacity at reserve
    Rules/ReservationRuleSet.cs       runs rules in priority order
    Abstractions/IReservationOptions.cs   feature-flag contract
    Abstractions/IClock.cs            testable "now"
  Application/
    Abstractions/IAppDbContext.cs     EF abstraction for handlers
    Abstractions/IJwtTokenService.cs
    Events/CreateEvent/*              command + handler + validator + DTO
    Events/ListEvents/*               query + handler + filter DTO
    Reservations/CreateReservation/*  command + handler + validator
    Reservations/ConfirmReservation/* command + handler
    Reservations/CancelReservation/*  command + handler
    Reports/GetOccupancy/*            query + handler + DTO
    Auth/Login/*                      command + handler
    Common/Mappings.cs
  Infrastructure/
    Persistence/AppDbContext.cs       DbSets, xmin concurrency, CHECK constraint
    Persistence/Configurations/*      EF entity configs
    Persistence/Seed/SeedData.cs      venues + admin/user accounts
    Persistence/Migrations/*          generated
    Security/JwtTokenService.cs
    Security/PasswordHasher.cs        BCrypt wrapper
    Options/ReservationOptions.cs     binds appsettings -> IReservationOptions
    SystemClock.cs
    DependencyInjection.cs
  Api/
    Controllers/EventsController.cs
    Controllers/ReservationsController.cs
    Controllers/AuthController.cs
    Middleware/ExceptionHandlingMiddleware.cs
    Common/ResultExtensions.cs        Result -> IActionResult/ProblemDetails
    Program.cs                        DI, auth, swagger, migrate+seed
    appsettings.json / appsettings.Development.json
tests/
  Domain.Tests/        rules, value objects, aggregate invariants, edge cases
  Application.Tests/   handler outcomes with in-memory/SQLite + mocked clock/options
  Integration.Tests/   WebApplicationFactory + Testcontainers Postgres; concurrency oversell proof
Dockerfile
docker-compose.yml
.dockerignore
.gitignore
README.md
```

---

## Task 0: Repo hygiene (git + gitignore)

**Files:** Create `.gitignore`, init git.

- [ ] **Step 1:** Create `.gitignore` with standard .NET entries (`bin/`, `obj/`, `*.user`, `appsettings.*.local.json`, `.vs/`, `TestResults/`).
- [ ] **Step 2:** `git init && git add . && git commit -m "chore: scaffold clean-architecture solution"`

---

## Task 1: Result type (Domain/Common)

**Files:** Create `src/Domain/Common/Result.cs`. Test: `tests/Domain.Tests/Common/ResultTests.cs`.

**Interfaces produced:** `Error(string Code, string Message)`; `Result` with `IsSuccess`, `Error`, static `Success()`, `Failure(Error)`; `Result<T>` with `Value`, static `Success(T)`, `Failure(Error)`, implicit from `T` and `Error`.

- [ ] **Step 1: Failing test**
```csharp
public class ResultTests
{
    [Fact]
    public void Failure_carries_error_and_is_not_success()
    {
        var err = new Error("rule.violated", "nope");
        Result r = Result.Failure(err);
        r.IsSuccess.Should().BeFalse();
        r.Error.Should().Be(err);
    }

    [Fact]
    public void SuccessT_exposes_value()
    {
        Result<int> r = 42;
        r.IsSuccess.Should().BeTrue();
        r.Value.Should().Be(42);
    }
}
```
- [ ] **Step 2:** Run `dotnet test tests/Domain.Tests` → FAIL (types missing).
- [ ] **Step 3: Implement**
```csharp
namespace EventosVivos.Domain.Common;

public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new("", "");
}

public class Result
{
    protected Result(bool ok, Error error)
    {
        if (ok && error != Error.None) throw new InvalidOperationException();
        if (!ok && error == Error.None) throw new InvalidOperationException();
        IsSuccess = ok; Error = error;
    }
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }
    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error e) => new(false, e);
    public static Result<T> Success<T>(T value) => new(value, true, Error.None);
    public static Result<T> Failure<T>(Error e) => new(default!, false, e);
}

public sealed class Result<T> : Result
{
    private readonly T _value;
    internal Result(T value, bool ok, Error error) : base(ok, error) => _value = value;
    public T Value => IsSuccess ? _value : throw new InvalidOperationException("No value on failure.");
    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error e) => Failure<T>(e);
}
```
- [ ] **Step 4:** Run tests → PASS.
- [ ] **Step 5:** `git add -A && git commit -m "feat(domain): add Result type"`

---

## Task 2: Enums (Domain/Enums)

**Files:** Create `EventType.cs`, `EventStatus.cs`, `ReservationStatus.cs`. Test: `tests/Domain.Tests/Enums/EnumTests.cs`.

**Interfaces produced:** `enum EventType { Conferencia, Taller, Concierto }`; `enum EventStatus { Activo, Cancelado, Completado }`; `enum ReservationStatus { PendientePago, Confirmada, Cancelada }`.

- [ ] **Step 1: Failing test** asserting each enum has the exact members above (e.g. `Enum.GetNames<EventType>().Should().BeEquivalentTo(new[]{"Conferencia","Taller","Concierto"})`).
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement the three enums.
- [ ] **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(domain): add core enums`.

---

## Task 3: Value Objects — Money, Email, DateRange (Domain/ValueObjects)

**Files:** `Money.cs`, `Email.cs`, `DateRange.cs`. Tests: `tests/Domain.Tests/ValueObjects/*`.

**Interfaces produced:**
- `Money` (record): `static Result<Money> Create(decimal amount)` — rejects ≤ 0; `decimal Amount`.
- `Email` (record): `static Result<Email> Create(string raw)` — trims, lowercases, validates RFC-ish regex; `string Value`.
- `DateRange` (record): `static Result<DateRange> Create(DateTime startUtc, DateTime endUtc)` — rejects end ≤ start; `bool Overlaps(DateRange other)`.

- [ ] **Step 1: Failing tests**
```csharp
[Theory]
[InlineData(0)] [InlineData(-1)]
public void Money_rejects_non_positive(decimal v) =>
    Money.Create(v).IsFailure.Should().BeTrue();

[Theory]
[InlineData("a@b.com", true)]
[InlineData("nope", false)]
[InlineData("", false)]
public void Email_validates_format(string raw, bool ok) =>
    Email.Create(raw).IsSuccess.Should().Be(ok);

[Fact]
public void DateRange_rejects_end_before_start() =>
    DateRange.Create(new DateTime(2030,1,2), new DateTime(2030,1,1)).IsFailure.Should().BeTrue();

[Fact]
public void DateRange_overlap_is_detected()
{
    var a = DateRange.Create(new(2030,1,1,10,0,0), new(2030,1,1,12,0,0)).Value;
    var b = DateRange.Create(new(2030,1,1,11,0,0), new(2030,1,1,13,0,0)).Value;
    a.Overlaps(b).Should().BeTrue();
}
```
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement the three value objects. Overlap rule: `startUtc < other.EndUtc && other.StartUtc < EndUtc` (touching edges do NOT overlap).
- [ ] **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(domain): add Money, Email, DateRange value objects`.

---

## Task 4: Event aggregate — creation + invariants (RN-01, RN-03)

**Files:** `src/Domain/Events/EventAggregate.cs`, `src/Domain/Abstractions/IClock.cs`. Test: `tests/Domain.Tests/Events/EventCreationTests.cs`.

**Interfaces produced:**
- `interface IClock { DateTime UtcNow { get; } }`
- `Event` (aggregate root): private ctor; `static Result<Event> Create(string title, string description, int venueId, int venueCapacity, int capacity, DateRange schedule, Money price, EventType type, IClock clock)`.
- Properties: `Guid Id`, `string Title`, `string Description`, `int VenueId`, `int Capacity`, `DateRange Schedule`, `Money Price`, `EventType Type`, `EventStatus Status`, `int SeatsTaken`, `int SeatsLost`, `uint Version` (xmin).
- Validation in `Create`: title 5–100, description 10–500, capacity > 0, **capacity ≤ venueCapacity (RN-01)**, start must be future (`schedule.StartUtc > clock.UtcNow`), **RN-03: if start is Sat/Sun and start time-of-day > 22:00 → reject**. Status initialized `Activo`, `SeatsTaken=0`, `SeatsLost=0`.

- [ ] **Step 1: Failing tests** — happy path returns success; capacity > venueCapacity fails (`RN-01`); start in past fails; Saturday 22:30 start fails (`RN-03`); Saturday 21:00 start passes; title length boundaries (4 fail, 5 pass, 100 pass, 101 fail).
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement `Event.Create` with each guard returning a distinct `Error` code (`event.title.length`, `event.capacity.exceedsVenue`, `event.start.past`, `event.start.weekendNight`).
- [ ] **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(domain): Event aggregate creation with RN-01, RN-03`.

---

## Task 5: Event status (RN-06) + seam for background updater

**Files:** Modify `EventAggregate.cs`. Create `src/Domain/Abstractions/IEventStatusUpdater.cs`. Test: `tests/Domain.Tests/Events/EventStatusTests.cs`.

**Interfaces produced:**
- `Event.RefreshStatus(DateTime nowUtc)`: if `Status == Activo && nowUtc > Schedule.EndUtc` → set `Completado`. Never changes `Cancelado`.
- `Event.Cancel()`: `Activo` → `Cancelado` (returns `Result`).
- `interface IEventStatusUpdater { Task MarkCompletedEventsAsync(CancellationToken ct); }` — **seam only**; documented that the on-read path (`RefreshStatus` called in queries) is the chosen implementation and a hosted background job is the alternative (left as a trace, not implemented).

- [ ] **Step 1: Failing tests** — active event past end → `RefreshStatus` makes it `Completado`; cancelled event past end stays `Cancelado`; active event before end stays `Activo`.
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement `RefreshStatus` + `Cancel`. Add XML doc comment on `IEventStatusUpdater` explaining the on-read vs background-job decision (decision #3).
- [ ] **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(domain): RN-06 auto-complete on read + status updater seam`.

---

## Task 6: Venue overlap check contract (RN-02)

**Files:** `src/Domain/Venues/Venue.cs`. Add `interface IVenueScheduleChecker` to `src/Domain/Abstractions/`. Test: covered at handler level (Task 9); here just the `Venue` record + contract.

**Interfaces produced:**
- `Venue` (record): `int Id`, `string Name`, `int Capacity`, `string City`.
- `interface IVenueScheduleChecker { Task<bool> HasOverlapAsync(int venueId, DateRange schedule, Guid? excludeEventId, CancellationToken ct); }` — true when another **active** event shares the venue with an overlapping `DateRange`.

- [ ] **Step 1:** Create the `Venue` record and the interface (no logic). Commit `feat(domain): Venue reference type + overlap checker contract`. (No test needed — pure declarations; the rule is exercised in Task 9.)

---

## Task 7: Reservation rules — priority chain (RN-04, RF-03 24h, RN-05)

**Files:** `src/Domain/Rules/IReservationRule.cs`, `LateReservationRule.cs`, `Near24hRule.cs`, `HighPriceRule.cs`, `AvailabilityRule.cs`, `ReservationRuleSet.cs`. Test: `tests/Domain.Tests/Rules/ReservationRuleTests.cs`.

**Interfaces produced:**
- `readonly record struct ReservationRequest(int Quantity, DateTime EventStartUtc, decimal EventPrice, int RemainingSeats, DateTime NowUtc)`.
- `interface IReservationRule { int Order { get; } Result Evaluate(ReservationRequest r); }`
- Rules and order (lower runs first; first failure wins):
  - `LateReservationRule` Order=10 — **RN-04**: `EventStartUtc - NowUtc < 1h` → fail `reserve.tooLate`.
  - `Near24hRule` Order=20 — **RF-03**: if `EventStartUtc - NowUtc < 24h` and `Quantity > 5` → fail `reserve.max5Near24h`. (Priority over price rule by lower Order.)
  - `HighPriceRule` Order=30 — **RN-05**: if `EventPrice > 100` and `Quantity > 10` → fail `reserve.max10HighPrice`.
  - `AvailabilityRule` Order=40 — **RN-01 at reserve**: `Quantity > RemainingSeats` → fail `reserve.soldOut`. Also `Quantity < 1` → fail `reserve.minQuantity`.
- `ReservationRuleSet(IEnumerable<IReservationRule> rules)` with `Result Evaluate(ReservationRequest r)` running rules ordered by `Order`, returning the first failure or success.

- [ ] **Step 1: Failing tests** (the heart of the suite — cover boundaries):
```csharp
// RN-04 boundary
[Theory]
[InlineData(59, false)]   // 59 min before start -> rejected
[InlineData(61, true)]    // 61 min -> allowed (subject to later rules)
public void Late_rule_blocks_under_1h(int minutesBefore, bool ok) { /* ... */ }

// Priority: <24h AND price>100 AND qty 6 -> the 24h rule (max 5) wins, NOT the price rule
[Fact]
public void Near24h_takes_priority_over_high_price()
{
    var set = new ReservationRuleSet(new IReservationRule[]
        { new LateReservationRule(), new Near24hRule(), new HighPriceRule(), new AvailabilityRule() });
    var req = new ReservationRequest(
        Quantity: 6,
        EventStartUtc: new DateTime(2030,1,1,10,0,0),
        EventPrice: 150m,
        RemainingSeats: 100,
        NowUtc: new DateTime(2030,1,1,0,0,0)); // 10h before -> <24h window
    var result = set.Evaluate(req);
    result.IsFailure.Should().BeTrue();
    result.Error.Code.Should().Be("reserve.max5Near24h");
}

// >100 price, far date, qty 11 -> price rule fires
[Fact]
public void High_price_limits_to_10_when_far_out() { /* qty 11, start in 10 days, price 150 -> reserve.max10HighPrice */ }

// sold out
[Fact]
public void Availability_blocks_when_quantity_exceeds_remaining() { /* qty 5, remaining 3 -> reserve.soldOut */ }
```
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement each rule + `ReservationRuleSet` (sort by `Order`, short-circuit on first failure).
- [ ] **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(domain): ordered reservation rule chain (RN-04, RF-03, RN-05, RN-01)`.

---

## Task 8: Event aggregate — reserve / confirm / cancel + inventory (RN-07, flags)

**Files:** Modify `EventAggregate.cs`, `src/Domain/Reservations/Reservation.cs`, `src/Domain/ValueObjects/ReservationCode.cs`, `src/Domain/Abstractions/IReservationOptions.cs`. Test: `tests/Domain.Tests/Events/InventoryTests.cs`, `tests/Domain.Tests/Reservations/ReservationTests.cs`.

**Interfaces produced:**
- `interface IReservationOptions { bool PendingHoldsInventory { get; } int PendingExpirationMinutes { get; } }`
- `ReservationCode` (record): `static ReservationCode New(Func<int> sixDigits)` → `EV-123456`; `string Value`.
- `Reservation`: `static Result<Reservation> Create(Guid eventId, int qty, string buyerName, Email email, DateTime nowUtc)` → status `PendientePago`; `Confirm(ReservationCode code)` (only from `PendientePago`, else fail `reservation.alreadyConfirmed`/`reservation.cancelled`); `Cancel(DateTime nowUtc, DateTime eventStartUtc)` (only from `Confirmada`, else fail) → sets `CancelledUtc`, returns whether penalty applies (`<48h` → lost). Fields: `Status`, `Code`, `Quantity`, `BuyerName`, `Email`, `CreatedUtc`, `CancelledUtc`, `IsLost`.
- `Event` inventory methods (mutate counter + enforce invariant `SeatsTaken + SeatsLost <= Capacity`):
  - `int Remaining => Capacity - SeatsTaken - SeatsLost;`
  - `Result HoldOnReserve(int qty, IReservationOptions opt)` → if `opt.PendingHoldsInventory` increment `SeatsTaken` (guarded by invariant); else no-op.
  - `Result ConsumeOnConfirm(int qty, IReservationOptions opt)` → if NOT `PendingHoldsInventory` increment `SeatsTaken` now; else no-op (already held).
  - `void ReleaseOnCancel(int qty, bool penalty)` → penalty (RN-07): `SeatsTaken -= qty; SeatsLost += qty` (held, not resold); no penalty: `SeatsTaken -= qty` (freed). Guard against negative.

- [ ] **Step 1: Failing tests** — reserve increments SeatsTaken when flag on; reserve does NOT increment when flag off; confirm increments when flag off; cancel with penalty moves qty to SeatsLost and reduces Remaining permanently; cancel without penalty restores Remaining; `Reservation.Confirm` twice → failure; `Cancel` on pending → failure; invariant: holding beyond capacity → failure (`event.capacity.exceeded`).
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement. Penalty decision lives in handler (Task 13) which passes `penalty` based on `<48h`; `Reservation.Cancel` returns the penalty flag computed from `nowUtc` vs `eventStartUtc`.
- [ ] **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(domain): inventory hold/consume/release with RN-07 penalty + feature flags`.

---

## Task 9: CreateEvent command (RF-01) + venue overlap (RN-02)

**Files:** `src/Application/Abstractions/IAppDbContext.cs`, `src/Application/Events/CreateEvent/{CreateEventCommand,CreateEventHandler,CreateEventValidator,EventResponse}.cs`. Test: `tests/Application.Tests/Events/CreateEventHandlerTests.cs`.

**Interfaces produced:**
- `interface IAppDbContext { DbSet<Event> Events; DbSet<Reservation> Reservations; DbSet<Venue> Venues; DbSet<AppUser> Users; Task<int> SaveChangesAsync(CancellationToken ct); }`
- `record CreateEventCommand(...) : IRequest<Result<EventResponse>>` with all RF-01 fields.
- `CreateEventValidator` (FluentValidation): title 5–100, description 10–500, capacity > 0, price > 0, start future, end > start, type in enum.
- Handler: loads `Venue` (404 `venue.notFound` if missing), checks `IVenueScheduleChecker.HasOverlapAsync` (RN-02 → fail `event.venueOverlap`), calls `Event.Create`, persists, returns `EventResponse`.

- [ ] **Step 1: Failing test** — valid command creates event; overlapping schedule on same venue returns `event.venueOverlap`; capacity > venue capacity returns `event.capacity.exceedsVenue`; unknown venue returns `venue.notFound`. Use SQLite in-memory or EF InMemory + fake `IVenueScheduleChecker` and fixed `IClock`.
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement command, validator, handler.
- [ ] **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(app): CreateEvent with RN-02 overlap`.

---

## Task 10: ListEvents query with filters (RF-02)

**Files:** `src/Application/Events/ListEvents/{ListEventsQuery,ListEventsHandler,EventFilter}.cs`. Test: `tests/Application.Tests/Events/ListEventsHandlerTests.cs`.

**Interfaces produced:** `record ListEventsQuery(EventType? Type, DateTime? FromUtc, DateTime? ToUtc, int? VenueId, EventStatus? Status, string? TitleContains) : IRequest<Result<IReadOnlyList<EventResponse>>>`. Handler applies each filter only when present; title match is case-insensitive partial (`ILIKE`); calls `RefreshStatus` projection so `Completado` reflects current time (RN-06 on read).

- [ ] **Step 1: Failing test** — seed mixed events; filter by type returns only that type; title "conf" matches "Conferencia X" case-insensitively; date-range filter bounds inclusive on start; status filter reflects auto-completed events.
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement query + handler (compose `IQueryable`).
- [ ] **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(app): ListEvents with optional filters (RF-02)`.

---

## Task 11: CreateReservation command (RF-03) — rules + inventory + concurrency

**Files:** `src/Application/Reservations/CreateReservation/{CreateReservationCommand,CreateReservationHandler,CreateReservationValidator,ReservationResponse}.cs`. Test: `tests/Application.Tests/Reservations/CreateReservationHandlerTests.cs`.

**Interfaces produced:** `record CreateReservationCommand(Guid EventId, int Quantity, string BuyerName, string BuyerEmail) : IRequest<Result<ReservationResponse>>`.
Handler flow: load event (404), `RefreshStatus` (reject if not `Activo`), build `ReservationRequest` (Remaining from event), run `ReservationRuleSet`, on success `Event.HoldOnReserve`, create `Reservation` (PendientePago), persist with **optimistic-concurrency retry** (catch `DbUpdateConcurrencyException`, reload, retry up to 3×).

**Interfaces produced (Infrastructure dependency):** retry handled via an injected `IConcurrencyRetryPolicy.ExecuteAsync(Func<Task<Result<T>>>)` (Task 17) so the handler stays clean.

- [ ] **Step 1: Failing test** — valid reserve creates PendientePago + decrements Remaining (flag on); reserve on completed event → `event.notActive`; reserve exceeding remaining → `reserve.soldOut`; invalid email → validator failure; qty 0 → failure.
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(app): CreateReservation (RF-03) with rules + inventory hold`.

---

## Task 12: ConfirmReservation command (RF-04)

**Files:** `src/Application/Reservations/ConfirmReservation/{ConfirmReservationCommand,ConfirmReservationHandler}.cs`. Test: `tests/Application.Tests/Reservations/ConfirmReservationHandlerTests.cs`.

**Interfaces produced:** `record ConfirmReservationCommand(Guid ReservationId) : IRequest<Result<ReservationResponse>>`. Handler: load reservation (404), generate unique `ReservationCode` (`EV-{6}` — retry on collision against DB), `Reservation.Confirm(code)`, `Event.ConsumeOnConfirm` (no-op if flag on), persist. Already confirmed → `reservation.alreadyConfirmed`; cancelled → `reservation.cancelled`.

- [ ] **Step 1: Failing test** — pending → confirmed with code matching `^EV-\d{6}$`; confirm twice → `reservation.alreadyConfirmed`; confirm cancelled → `reservation.cancelled`; code uniqueness (seed colliding code, expect a different one issued).
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(app): ConfirmReservation (RF-04) with unique code`.

---

## Task 13: CancelReservation command (RF-05, RN-07)

**Files:** `src/Application/Reservations/CancelReservation/{CancelReservationCommand,CancelReservationHandler}.cs`. Test: `tests/Application.Tests/Reservations/CancelReservationHandlerTests.cs`.

**Interfaces produced:** `record CancelReservationCommand(Guid ReservationId) : IRequest<Result<ReservationResponse>>`. Handler: load reservation + its event (404), `Reservation.Cancel(now, event.Schedule.StartUtc)` → returns penalty flag (`<48h`), `Event.ReleaseOnCancel(qty, penalty)`, persist. Only `Confirmada` cancellable; pending/cancelled → distinct errors.

- [ ] **Step 1: Failing test** — confirmed >48h before event → cancelled, seats freed (Remaining restored); confirmed <48h before → cancelled, seats become `SeatsLost`, Remaining NOT restored (RN-07); cancel pending → `reservation.notConfirmed`; cancel already cancelled → `reservation.cancelled`; `CancelledUtc` set.
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(app): CancelReservation (RF-05) with RN-07 penalty`.

---

## Task 14: Occupancy report query (RF-06)

**Files:** `src/Application/Reports/GetOccupancy/{GetOccupancyQuery,GetOccupancyHandler,OccupancyResponse}.cs`. Test: `tests/Application.Tests/Reports/GetOccupancyHandlerTests.cs`.

**Interfaces produced:** `record GetOccupancyQuery(Guid EventId) : IRequest<Result<OccupancyResponse>>`.
`record OccupancyResponse(Guid EventId, string Title, int Capacity, int SoldConfirmed, int AvailableRemaining, int RetainedByPenalty, double OccupancyPercent, decimal TotalRevenue, EventStatus Status)`.
Computation: `SoldConfirmed` = sum of confirmed quantities; `RetainedByPenalty` = `SeatsLost`; `AvailableRemaining` = `Capacity - SeatsTaken - SeatsLost`; `OccupancyPercent` = `SoldConfirmed / Capacity * 100`; `TotalRevenue` = `Price * SoldConfirmed`; `Status` via `RefreshStatus`. Report is **explicit** about retained-lost seats (decision #2).

- [ ] **Step 1: Failing test** — event cap 100, 30 confirmed, 10 lost → Sold 30, Retained 10, Available 60, Occupancy 30.0, Revenue = price×30; status reflects auto-complete.
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(app): occupancy report (RF-06) with explicit penalty-retained seats`.

---

## Task 15: Auth — users, password hashing, JWT, Login (security)

**Files:** `src/Domain/Users/AppUser.cs` (`Id`, `Email`, `PasswordHash`, `Role`), `src/Application/Abstractions/IJwtTokenService.cs`, `src/Application/Auth/Login/{LoginCommand,LoginHandler,LoginResponse}.cs`, `src/Infrastructure/Security/{JwtTokenService,PasswordHasher}.cs`. Test: `tests/Application.Tests/Auth/LoginHandlerTests.cs`, `tests/Domain.Tests` (role enum).

**Interfaces produced:**
- `enum Role { Admin, User }`
- `interface IJwtTokenService { string Generate(AppUser user); }`
- `interface IPasswordHasher { string Hash(string pw); bool Verify(string pw, string hash); }`
- `record LoginCommand(string Email, string Password) : IRequest<Result<LoginResponse>>`; `LoginResponse(string Token, string Role)`.

- [ ] **Step 1: Failing test** — correct creds → token + role; wrong password → `auth.invalidCredentials`; unknown email → same generic error (no user enumeration). Mock `IJwtTokenService`/`IPasswordHasher`.
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement handler + BCrypt hasher + JWT service (HS256, configurable issuer/audience/key/expiry from options).
- [ ] **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(auth): JWT login with BCrypt + roles`.

---

## Task 16: EF Core persistence — DbContext, configs, xmin, CHECK, seed

**Files:** `src/Infrastructure/Persistence/AppDbContext.cs`, `Configurations/*.cs`, `Seed/SeedData.cs`, `src/Infrastructure/Options/ReservationOptions.cs`, `src/Infrastructure/SystemClock.cs`, `src/Infrastructure/DependencyInjection.cs`. Test: deferred to Task 18 (integration).

**Key configuration to implement:**
- `Event`: map `Version` to Postgres system column `xmin` → `.IsRowVersion()` + `.HasColumnName("xmin")` + `.HasColumnType("xid")`. Owned types for `Money`, `DateRange`. **CHECK constraint** `ck_event_capacity` = `"SeatsTaken" + "SeatsLost" <= "Capacity"` via `ToTable(t => t.HasCheckConstraint(...))`.
- `Reservation`: owned `Email`, `ReservationCode` as string; unique index on code (filtered: where not null).
- `Venue`: seeded HasData (3 venues). `AppUser`: seeded one Admin + one User (BCrypt-hashed seed passwords from config, documented in README).
- `ReservationOptions : IReservationOptions` bound from `appsettings` section `Reservation`.
- `AppDbContext : IAppDbContext`.
- `DependencyInjection.AddInfrastructure(config)` registers DbContext (Npgsql), options, clock, jwt, hasher, `IVenueScheduleChecker` (EF impl), `IConcurrencyRetryPolicy`.

- [ ] **Step 1:** Implement all configs + DI.
- [ ] **Step 2:** `dotnet ef migrations add InitialCreate -p src/Infrastructure -s src/Api` → verify migration includes xmin mapping + CHECK constraint.
- [ ] **Step 3:** Commit `feat(infra): EF Core persistence, xmin concurrency, CHECK constraint, seed`.

---

## Task 17: Concurrency retry policy + venue checker (Infrastructure)

**Files:** `src/Infrastructure/Persistence/ConcurrencyRetryPolicy.cs`, `src/Infrastructure/Persistence/EfVenueScheduleChecker.cs`. Test: `tests/Application.Tests` uses fakes; real impl proven in Task 18.

**Interfaces produced:** `interface IConcurrencyRetryPolicy { Task<Result<T>> ExecuteAsync<T>(Func<Task<Result<T>>> action, int maxAttempts = 3); }` — re-invokes on `DbUpdateConcurrencyException`, reloading state each attempt; after max attempts returns `Result.Failure(new Error("concurrency.conflict", ...))`.

- [ ] **Step 1:** Implement retry policy + EF venue checker (`Events.AnyAsync(active && same venue && overlapping range && id != exclude)`).
- [ ] **Step 2:** Commit `feat(infra): optimistic-concurrency retry policy + venue overlap checker`.

---

## Task 18: API wiring + integration tests (incl. concurrency oversell proof)

**Files:** `src/Api/Program.cs`, `Controllers/*.cs`, `Middleware/ExceptionHandlingMiddleware.cs`, `Common/ResultExtensions.cs`, `appsettings*.json`. Tests: `tests/Integration.Tests/{ApiFactory.cs,ReservationConcurrencyTests.cs,EndpointSmokeTests.cs}`.

**Endpoints (RESTful):**
```
POST   /api/auth/login                          -> 200 {token,role}
POST   /api/events            [Admin]           -> 201 EventResponse
GET    /api/events?type&from&to&venueId&status&q -> 200 EventResponse[]
POST   /api/reservations      [User]            -> 201 ReservationResponse
POST   /api/reservations/{id}/confirm [Admin]   -> 200 ReservationResponse
POST   /api/reservations/{id}/cancel  [User|Admin] -> 200 ReservationResponse
GET    /api/events/{id}/occupancy               -> 200 OccupancyResponse
```
`ResultExtensions` maps `Result` → `IActionResult`: success→200/201; error codes →
`*.notFound`→404, `auth.*`→401, `concurrency.conflict`→409, everything else→`ProblemDetails` 422/400.

- [ ] **Step 1: Program.cs** — DI (Application MediatR + validators, Infrastructure), JWT auth + `[Authorize(Roles=...)]`, Swagger with bearer, `app.Run` after `db.Migrate()` + seed on startup.
- [ ] **Step 2: Controllers** — thin: send MediatR command, map Result.
- [ ] **Step 3: ExceptionHandlingMiddleware** — unhandled → 500 ProblemDetails (no stack leak).
- [ ] **Step 4: Failing concurrency test (the differentiator):**
```csharp
[Fact]
public async Task Parallel_reservations_never_oversell()
{
    // Arrange: event capacity = 10 (venue cap >=10), PendingHoldsInventory = true
    var eventId = await SeedEventWithCapacity(10);
    // Act: fire 50 concurrent reservations of qty 1
    var tasks = Enumerable.Range(0, 50)
        .Select(_ => Client.PostAsJsonAsync("/api/reservations",
            new { eventId, quantity = 1, buyerName = "x", buyerEmail = "x@y.com" }));
    var responses = await Task.WhenAll(tasks);
    // Assert: exactly 10 succeeded (201), rest rejected (422 soldOut / 409 conflict). Never >10 held.
    responses.Count(r => r.IsSuccessStatusCode).Should().Be(10);
    var occ = await GetOccupancy(eventId);
    (occ.SoldConfirmed + PendingCount(eventId)).Should().BeLessThanOrEqualTo(10);
}
```
Uses `Testcontainers.PostgreSql` (real Postgres → real xmin concurrency). `WebApplicationFactory` overrides connection string to the container.
- [ ] **Step 5:** Run integration tests → first FAIL (endpoints unwired), implement until PASS.
- [ ] **Step 6:** Commit `feat(api): endpoints, auth, error mapping + concurrency oversell integration test`.

---

## Task 19: Dockerfile + .dockerignore (local-first, provider-agnostic)

**Files:** `Dockerfile`, `.dockerignore`.

- [ ] **Step 1:** Multi-stage `Dockerfile` (`mcr.microsoft.com/dotnet/sdk:10.0` build → `aspnet:10.0` runtime), exposes 8080, `ENTRYPOINT dotnet EventosVivos.Api.dll`. Connection string + JWT key read from env vars (no secrets baked in).
- [ ] **Step 2:** `.dockerignore` (bin, obj, tests, .git, node_modules).
- [ ] **Step 3:** Commit `chore: containerize API`.

---

## Task 20: docker-compose (local) + README + deploy notes

**Files:** `docker-compose.yml`, `README.md`.

- [ ] **Step 1:** `docker-compose.yml` — services: `db` (postgres:17, volume, healthcheck), `api` (build ., depends_on db healthy, env: `ConnectionStrings__Default`, `Jwt__Key`, `Reservation__PendingHoldsInventory`, `Reservation__PendingExpirationMinutes`). One command up: `docker compose up --build`.
- [ ] **Step 2: README.md** sections:
  - **Run locally (Docker, recommended):** `docker compose up --build` → API at `http://localhost:8080/swagger`.
  - **Run locally (no Docker):** set connection string to a local Postgres, `dotnet run --project src/Api` (auto-migrate + seed).
  - **Architecture & justification:** Clean Architecture, Event-as-aggregate, ordered rule chain, optimistic concurrency (xmin) + CHECK backstop, feature flags, on-read RN-06 with background-job seam.
  - **Feature flags:** table of `Reservation__*` env vars and effects.
  - **Seeded accounts:** admin + user emails/passwords (dev only).
  - **Tech stack & test commands:** `dotnet test`.
  - **Deploy (provider-agnostic):** "Any container host. Build the image, supply env vars (`ConnectionStrings__Default`, `Jwt__Key`, `Reservation__*`), point at a managed PostgreSQL. Examples (interchangeable, not fixed): Render, Fly.io, Azure Container Apps, Railway + Neon/Supabase Postgres." Emphasize the image is the unit of deploy; provider is swappable.
- [ ] **Step 3:** Commit `docs: README with architecture, flags, deploy notes` + `chore: docker-compose for local`.

---

## Self-Review Notes

- Every RF (01–06) and RN (01–07) maps to a task (see table). Concurrency + auth + tests + docker covered.
- Type names are consistent across tasks (`Result`, `Event`, `Reservation`, `ReservationRequest`, `IReservationOptions`, `EventResponse`, `ReservationResponse`, `OccupancyResponse`).
- Priority rule ordering (RF-03 24h over RN-05 price) is locked via `Order` (20 < 30) and explicitly tested in Task 7.
- Decision log: (1) pending holds inventory via flag; (2) lost seats explicit in report; (3) RN-06 on-read + seam; (4) ordered rule pattern; (5) JWT+roles.

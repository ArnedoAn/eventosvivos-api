# EventosVivos API — REST Contract

> **Audience**: Frontend / API consumers.  
> **Source of truth**: compiled from source code as of commit `6d42230`.  
> Every claim cites the file where it was verified.

---

## Table of Contents

1. [Global Conventions](#1-global-conventions)
2. [Authentication & JWT](#2-authentication--jwt)
3. [Error Shape](#3-error-shape)
4. [Enum Reference](#4-enum-reference)
5. [Endpoints](#5-endpoints)
   - [POST /api/auth/login](#51-post-apiautlogin)
   - [POST /api/events](#52-post-apievents)
   - [GET /api/events](#53-get-apievents)
   - [GET /api/events/{id}/occupancy](#54-get-apieventsioccupancy)
   - [POST /api/reservations](#55-post-apireservations)
   - [POST /api/reservations/{id}/confirm](#56-post-apireservationsidconfirm)
   - [POST /api/reservations/{id}/cancel](#57-post-apireservationsidcancel)
6. [Endpoints that do NOT exist](#6-endpoints-that-do-not-exist)
7. [Error Code Reference](#7-error-code-reference)
8. [Seeded Data (Development)](#8-seeded-data-development)

---

## 1. Global Conventions

### Base URL
All endpoints are prefixed with `/api`.

### JSON naming policy
ASP.NET Core's default JSON options are in effect. No explicit `PropertyNamingPolicy` override is set in `Program.cs`, but ASP.NET Core pre-configures camelCase for its JSON serializer. All request and response **property names are camelCase**.

_Source: `src/Api/Program.cs` lines 16–18_

### Enum serialization
`JsonStringEnumConverter` is registered globally (no `JsonNamingPolicy` argument).  
Enums are serialized as their **C# member name strings, verbatim** (not integers, not lowercased).

_Source: `src/Api/Program.cs` line 18_

### Dates
All `DateTime` fields are UTC. The serializer emits ISO-8601 with a `Z` suffix, e.g.:

```
"2025-09-15T18:00:00Z"
```

Do **not** send local-time dates; parse responses as UTC.

### Money / Decimal
`price`, `totalRevenue` are JSON numbers with arbitrary decimal precision (C# `decimal`).  
Example: `75.50` or `100`.

### Content-Type
All requests with a body must send `Content-Type: application/json`.  
All responses are `application/json` (error responses use `application/problem+json`).

---

## 2. Authentication & JWT

### Flow
1. Call `POST /api/auth/login` → receive a JWT token.
2. Send the token in every protected request as:
   ```
   Authorization: Bearer <token>
   ```

### Token claims (verified in `src/Infrastructure/Security/JwtTokenService.cs`)

| JWT claim | C# source | Example value |
|-----------|-----------|---------------|
| `sub` | `user.Id.ToString()` (Guid) | `"a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d"` |
| `email` | `user.Email` | `"admin@eventosvivos.com"` |
| `http://schemas.microsoft.com/ws/2008/06/identity/claims/role` | `user.Role.ToString()` | `"Admin"` or `"User"` |

> The role claim uses the full `ClaimTypes.Role` URI as its key name inside the token.  
> ASP.NET Core's authorization middleware reads it correctly when checking `[Authorize(Roles = "Admin")]`.

### Token configuration (from `appsettings.json` / `appsettings.Development.json`)

| Parameter | Default (dev) |
|-----------|---------------|
| Issuer | `"EventosVivos"` |
| Audience | `"EventosVivos"` |
| Algorithm | HS256 |
| Expiry | 60 minutes (`ExpiryMinutes`) |
| Clock skew | 0 (strict, `ClockSkew = TimeSpan.Zero`) |

_Source: `src/Infrastructure/Security/JwtTokenService.cs`, `src/Api/appsettings.Development.json`_

---

## 3. Error Shape

### ProblemDetails (controller-generated errors — 400 / 401 / 404 / 409 / 422)

These errors are produced by `ResultExtensions.MapError` → `CreateProblemDetails` and returned through ASP.NET Core's `ObjectResult` pipeline (camelCase serialization active).

```json
{
  "type": "https://httpstatuses.com/422",
  "title": "Unprocessable Entity",
  "status": 422,
  "detail": "Event is not active."
}
```

**Field inventory:**

| JSON key | Type | Description |
|----------|------|-------------|
| `type` | string | URL: `https://httpstatuses.com/{status}` |
| `title` | string | Human-readable status name |
| `status` | number | HTTP status code (integer) |
| `detail` | string | The domain error message |

> **IMPORTANT — no `code` field in the body.**  
> The internal error code (e.g., `"event.notActive"`, `"validation.failed"`) is used **only** to select the HTTP status. It is **not** included in the response body.  
> _Source: `src/Api/Common/ResultExtensions.cs` `CreateProblemDetails` method, lines 53–62_

### HTTP-status → error-code mapping rules

The mapping is implemented in `ResultExtensions.MapError` (`src/Api/Common/ResultExtensions.cs` lines 34–51):

| Condition on error code | HTTP status |
|------------------------|-------------|
| Ends with `.notFound` (case-insensitive) | 404 |
| Starts with `auth.` (case-insensitive) | 401 |
| Equals `concurrency.conflict` (case-insensitive) | 409 |
| Starts with `validation.` (case-insensitive) | 400 |
| Anything else | 422 |

### 500 Internal Server Error (middleware-generated)

Unhandled exceptions are caught by `ExceptionHandlingMiddleware`. This middleware calls `JsonSerializer.Serialize(problemDetails)` **without** custom options, which means the default .NET serializer (PascalCase) is used.

> **Known inconsistency**: 500 error bodies use **PascalCase** keys while all other error bodies use camelCase.

```json
{
  "Type": "https://httpstatuses.com/500",
  "Title": "An unexpected error occurred",
  "Status": 500,
  "Detail": "An unexpected error occurred."
}
```

_Source: `src/Api/Middleware/ExceptionHandlingMiddleware.cs` lines 23–32_

### Authorization errors from ASP.NET Core middleware (not domain errors)

When a request lacks a valid JWT, or the authenticated user's role does not satisfy `[Authorize(Roles = "...")]`, ASP.NET Core returns standard 401/403 responses **before** the controller runs. These responses do **not** go through `ResultExtensions` and may not include a body.

---

## 4. Enum Reference

All enums serialized as strings by `JsonStringEnumConverter` with no casing transformation.

### `EventType` — `src/Domain/Enums/EventType.cs`

| String value | Notes |
|--------------|-------|
| `"Conferencia"` | Conference |
| `"Taller"` | Workshop |
| `"Concierto"` | Concert |

### `EventStatus` — `src/Domain/Enums/EventStatus.cs`

| String value | Notes |
|--------------|-------|
| `"Activo"` | Active — accepting reservations |
| `"Cancelado"` | Cancelled |
| `"Completado"` | Completed — event schedule has ended |

> Status is computed on read (`RefreshStatus`): an `Activo` event whose `endUtc` has passed is returned as `"Completado"` in list/occupancy responses without a DB write.

### `ReservationStatus` — `src/Domain/Enums/ReservationStatus.cs`

| String value | Notes |
|--------------|-------|
| `"PendientePago"` | Pending payment — initial state |
| `"Confirmada"` | Confirmed by Admin |
| `"Cancelada"` | Cancelled (by user, admin, or expiry) |

### `Role` — `src/Domain/Users/Role.cs`

| String value | Integer value | Notes |
|--------------|---------------|-------|
| `"User"` | 0 | Regular user |
| `"Admin"` | 1 | Administrator |

> Role is stored as a string in the database (`HasConversion<string>()`).  
> _Source: `src/Infrastructure/Persistence/Configurations/AppUserConfiguration.cs` line 18_

---

## 5. Endpoints

---

### 5.1 `POST /api/auth/login`

**Authorization:** Anonymous

**Purpose:** Exchange credentials for a JWT token.

#### Request body

_Source: `src/Application/Auth/Login/LoginCommand.cs`_

```json
{
  "email": "admin@eventosvivos.com",
  "password": "Admin123!"
}
```

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `email` | string | yes | Case-insensitive DB lookup |
| `password` | string | yes | BCrypt-verified |

#### Response — 200 OK

_Source: `src/Application/Auth/Login/LoginResponse.cs`_

```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "role": "Admin"
}
```

| Field | Type | Notes |
|-------|------|-------|
| `token` | string | Signed JWT — use in `Authorization: Bearer` |
| `role` | string | `"Admin"` or `"User"` |

#### Errors

| HTTP | `detail` (body) | Internal code | When |
|------|-----------------|---------------|------|
| 401 | `"Invalid credentials."` | `auth.invalidCredentials` | Email not found or wrong password |

> Timing-neutral: a dummy BCrypt verify runs even when the user is not found to prevent user-enumeration timing attacks.  
> _Source: `src/Application/Auth/Login/LoginHandler.cs` lines 25–26_

---

### 5.2 `POST /api/events`

**Authorization:** `Admin` role required

**Purpose:** Create a new event.

_Source: `src/Api/Controllers/EventsController.cs` lines 17–27_

#### Request body

_Source: `src/Application/Events/CreateEvent/CreateEventCommand.cs`_

```json
{
  "title": "Tech Conference 2025",
  "description": "Annual technology conference covering AI, cloud, and security.",
  "venueId": 1,
  "capacity": 150,
  "startUtc": "2025-09-15T18:00:00Z",
  "endUtc": "2025-09-15T22:00:00Z",
  "price": 75.00,
  "type": "Conferencia"
}
```

| Field | Type | Required | Constraints |
|-------|------|----------|-------------|
| `title` | string | yes | 5–100 characters |
| `description` | string | yes | 10–500 characters |
| `venueId` | number (int) | yes | Must be an existing venue ID |
| `capacity` | number (int) | yes | > 0, ≤ venue capacity |
| `startUtc` | string (ISO-8601 UTC) | yes | Must be in the future; Saturday/Sunday events cannot start after 22:00 UTC |
| `endUtc` | string (ISO-8601 UTC) | yes | Must be after `startUtc` |
| `price` | number (decimal) | yes | > 0 |
| `type` | string (EventType enum) | yes | `"Conferencia"`, `"Taller"`, or `"Concierto"` |

> FluentValidation runs first (`validation.failed` → 400), then domain rules run in the handler (`event.*` → 422).  
> _Validators: `src/Application/Events/CreateEvent/CreateEventValidator.cs`_  
> _Domain rules: `src/Domain/Events/EventAggregate.cs` `Create` method_

#### Response — 201 Created

`Location` header: `/api/events/{id}`

_Source: `src/Application/Events/CreateEvent/EventResponse.cs`_

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "title": "Tech Conference 2025",
  "description": "Annual technology conference covering AI, cloud, and security.",
  "venueId": 1,
  "capacity": 150,
  "startUtc": "2025-09-15T18:00:00Z",
  "endUtc": "2025-09-15T22:00:00Z",
  "price": 75.00,
  "type": "Conferencia",
  "status": "Activo"
}
```

| Field | Type | Notes |
|-------|------|-------|
| `id` | string (Guid) | Newly generated event ID |
| `title` | string | |
| `description` | string | |
| `venueId` | number (int) | |
| `capacity` | number (int) | Event capacity (not venue max) |
| `startUtc` | string (ISO-8601 UTC) | |
| `endUtc` | string (ISO-8601 UTC) | |
| `price` | number (decimal) | |
| `type` | string (EventType) | |
| `status` | string (EventStatus) | Always `"Activo"` on creation |

#### Errors

| HTTP | `detail` (body) | Internal code | When |
|------|-----------------|---------------|------|
| 400 | e.g. `"Title: The length of 'Title' must be at least 5 characters. You entered 3 characters."` | `validation.failed` | FluentValidation failure |
| 401 | — | — | Missing or invalid JWT (ASP.NET Core middleware) |
| 403 | — | — | Authenticated but role is not `Admin` |
| 404 | `"Venue not found."` | `venue.notFound` | `venueId` does not exist |
| 422 | `"End must be after start."` | `DateRange.InvalidRange` | `endUtc` ≤ `startUtc` |
| 422 | `"Amount must be greater than zero."` | `money.invalidAmount` | `price` ≤ 0 |
| 422 | `"Venue schedule overlaps with another event."` | `event.venueOverlap` | Venue already booked in that time window |
| 422 | `"Title must be between 5 and 100 characters."` | `event.title.length` | Domain-level title check |
| 422 | `"Description must be between 10 and 500 characters."` | `event.description.length` | Domain-level description check |
| 422 | `"Capacity must be greater than zero."` | `event.capacity.invalid` | `capacity` ≤ 0 |
| 422 | `"Capacity cannot exceed venue capacity."` | `event.capacity.exceedsVenue` | `capacity` > venue max |
| 422 | `"Start must be in the future."` | `event.start.past` | `startUtc` ≤ current UTC |
| 422 | `"Weekend events cannot start after 22:00."` | `event.start.weekendNight` | Saturday/Sunday after 22:00 UTC |

---

### 5.3 `GET /api/events`

**Authorization:** Anonymous

**Purpose:** List events with optional filters.

_Source: `src/Api/Controllers/EventsController.cs` lines 29–44_

#### Query parameters

_All parameters are optional._  
_Note: query parameter names in the URL differ from the internal `ListEventsQuery` record field names._

| Query param | URL name | Type | Maps to | Description |
|-------------|----------|------|---------|-------------|
| `type` | `type` | EventType enum string | `ListEventsQuery.Type` | Filter by event type |
| `from` | `from` | ISO-8601 UTC date-time | `ListEventsQuery.FromUtc` | `startUtc` ≥ this value |
| `to` | `to` | ISO-8601 UTC date-time | `ListEventsQuery.ToUtc` | `startUtc` ≤ this value |
| `venueId` | `venueId` | int | `ListEventsQuery.VenueId` | Filter by venue |
| `status` | `status` | EventStatus enum string | `ListEventsQuery.Status` | Filter by computed status |
| `q` | `q` | string | `ListEventsQuery.TitleContains` | Case-insensitive substring search on `title` |

> `status` filter is **not** applied at DB level. The handler fetches unfiltered events, calls `RefreshStatus` on each, then filters in memory. This means an event that just expired will be returned as `"Completado"` even if the DB still stores `"Activo"`.  
> _Source: `src/Application/Events/ListEvents/ListEventsHandler.cs` lines 14–38, `src/Application/Events/ListEvents/EventFilter.cs`_

**Example request:**
```
GET /api/events?type=Conferencia&status=Activo&q=tech
```

#### Response — 200 OK

Array of `EventResponse` objects (same shape as the 201 response from POST).

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "title": "Tech Conference 2025",
    "description": "Annual technology conference covering AI, cloud, and security.",
    "venueId": 1,
    "capacity": 150,
    "startUtc": "2025-09-15T18:00:00Z",
    "endUtc": "2025-09-15T22:00:00Z",
    "price": 75.00,
    "type": "Conferencia",
    "status": "Activo"
  }
]
```

Returns an empty array `[]` when no events match. Never returns 404.

#### Errors

This endpoint does not produce domain errors. Only 500 is possible on infrastructure failure.

---

### 5.4 `GET /api/events/{id}/occupancy`

**Authorization:** Anonymous

**Purpose:** Get seat occupancy and revenue stats for a specific event.

_Source: `src/Api/Controllers/EventsController.cs` lines 46–54_

#### Path parameter

| Parameter | Type | Notes |
|-----------|------|-------|
| `id` | Guid | Event ID |

#### Response — 200 OK

_Source: `src/Application/Reports/GetOccupancy/OccupancyResponse.cs`_

```json
{
  "eventId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "title": "Tech Conference 2025",
  "capacity": 150,
  "soldConfirmed": 42,
  "availableRemaining": 100,
  "retainedByPenalty": 8,
  "occupancyPercent": 28.0,
  "totalRevenue": 3150.00,
  "status": "Activo"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `eventId` | string (Guid) | |
| `title` | string | |
| `capacity` | number (int) | Max seats for this event |
| `soldConfirmed` | number (int) | Count of seats in `Confirmada` reservations |
| `availableRemaining` | number (int) | `capacity - seatsTaken - seatsLost` |
| `retainedByPenalty` | number (int) | Seats permanently lost due to late-cancellation penalty (`seatsLost`) |
| `occupancyPercent` | number (double) | `soldConfirmed / capacity * 100` — not capped at 100 |
| `totalRevenue` | number (decimal) | `price × soldConfirmed` |
| `status` | string (EventStatus) | Refreshed on read |

> `soldConfirmed` counts only `Confirmada` reservations (not `PendientePago`).  
> `availableRemaining` accounts for both held pending seats and lost-to-penalty seats.  
> _Source: `src/Application/Reports/GetOccupancy/GetOccupancyHandler.cs` lines 24–32_

#### Errors

| HTTP | `detail` | Internal code | When |
|------|----------|---------------|------|
| 404 | `"Event not found."` | `event.notFound` | No event with that Guid |

---

### 5.5 `POST /api/reservations`

**Authorization:** `User` role required

**Purpose:** Create a reservation for an event. Seat hold and expiry logic runs before insertion.

_Source: `src/Api/Controllers/ReservationsController.cs` lines 21–38_

#### Request body

The request record is defined in the controller file: `src/Api/Controllers/ReservationsController.cs` line 11.

```json
{
  "eventId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "quantity": 2,
  "buyerName": "María García",
  "buyerEmail": "maria@example.com"
}
```

| Field | Type | Required | Constraints |
|-------|------|----------|-------------|
| `eventId` | string (Guid) | yes | Must be non-empty |
| `quantity` | number (int) | yes | > 0 |
| `buyerName` | string | yes | Non-empty, max 200 characters |
| `buyerEmail` | string | yes | Non-empty, valid email format, max 256 characters |

> The `userId` is NOT in the request body. It is extracted from the JWT `sub` claim via `ICurrentUser`.  
> _Source: `src/Application/Reservations/CreateReservation/CreateReservationHandler.cs` line 26_

#### Response — 201 Created

`Location` header: `/api/reservations/{id}`

_Source: `src/Application/Reservations/CreateReservation/ReservationResponse.cs`_

```json
{
  "id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "eventId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "userId": "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e",
  "quantity": 2,
  "buyerName": "María García",
  "buyerEmail": "maria@example.com",
  "status": "PendientePago",
  "createdUtc": "2025-06-24T14:32:00Z"
}
```

| Field | Type | Notes |
|-------|------|-------|
| `id` | string (Guid) | New reservation ID |
| `eventId` | string (Guid) | |
| `userId` | string (Guid) | Authenticated user's ID from JWT |
| `quantity` | number (int) | |
| `buyerName` | string | Trimmed |
| `buyerEmail` | string | Lowercased and trimmed by domain |
| `status` | string (ReservationStatus) | Always `"PendientePago"` on creation |
| `createdUtc` | string (ISO-8601 UTC) | |

#### Business rules (applied in order)

These rules run via `ReservationRuleSet` before the DB insert:

| Rule | Error code | `detail` | Condition |
|------|------------|---------|-----------|
| `LateReservationRule` (order=10) | `reserve.tooLate` | `"Reservations are closed within 1 hour of the event start."` | `eventStart - now < 1 hour` |
| `Near24hRule` (order=20) | `reserve.max5Near24h` | `"Maximum 5 seats within 24 hours of the event."` | `eventStart - now < 24 hours AND quantity > 5` |
| `HighPriceRule` (order=30) | `reserve.max10HighPrice` | `"Maximum 10 seats for high-price events."` | `price > 100 AND quantity > 10` |
| `AvailabilityRule` (order=40) | `reserve.soldOut` | `"Not enough seats available."` | `quantity > remainingSeats` |

_Source: `src/Domain/Rules/`_

#### Errors

| HTTP | `detail` | Internal code | When |
|------|----------|---------------|------|
| 400 | e.g. `"Quantity: 'Quantity' must be greater than '0'."` | `validation.failed` | FluentValidation failure |
| 401 | `"Authentication required."` | `auth.unauthenticated` | JWT missing `sub` claim |
| 401 | — | — | Missing/invalid JWT (ASP.NET Core middleware) |
| 403 | — | — | Role is not `User` |
| 404 | `"Event not found."` | `event.notFound` | Event Guid not found |
| 409 | — | `concurrency.conflict` | Concurrent update conflict (retried internally; surfaces only when retry budget exhausted) |
| 422 | `"Event is not active."` | `event.notActive` | Event status is not `Activo` |
| 422 | `"Email format is invalid."` | `Email.InvalidFormat` | `buyerEmail` fails domain email regex |
| 422 | `"Reservations are closed within 1 hour of the event start."` | `reserve.tooLate` | See business rules above |
| 422 | `"Maximum 5 seats within 24 hours of the event."` | `reserve.max5Near24h` | See business rules above |
| 422 | `"Maximum 10 seats for high-price events."` | `reserve.max10HighPrice` | See business rules above |
| 422 | `"Not enough seats available."` | `reserve.soldOut` | See business rules above |
| 422 | `"Not enough seats available."` | `event.capacity.exceeded` | Hold fails at event level (seat count exceeded) |

---

### 5.6 `POST /api/reservations/{id}/confirm`

**Authorization:** `Admin` role required

**Purpose:** Confirm a `PendientePago` reservation. Assigns a unique numeric code and permanently consumes inventory.

_Source: `src/Api/Controllers/ReservationsController.cs` lines 41–53_

#### Path parameter

| Parameter | Type | Notes |
|-----------|------|-------|
| `id` | Guid | Reservation ID |

No request body.

#### Response — 200 OK

Same `ReservationResponse` shape as `POST /api/reservations`, with `status` updated to `"Confirmada"`.

```json
{
  "id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "eventId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "userId": "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e",
  "quantity": 2,
  "buyerName": "María García",
  "buyerEmail": "maria@example.com",
  "status": "Confirmada",
  "createdUtc": "2025-06-24T14:32:00Z"
}
```

> The generated `ReservationCode` is **not** in the response body (it is stored in DB only).

#### Errors

| HTTP | `detail` | Internal code | When |
|------|----------|---------------|------|
| 401 | — | — | Missing/invalid JWT |
| 403 | — | — | Role is not `Admin` |
| 404 | `"Reservation not found."` | `reservation.notFound` | Reservation Guid not found |
| 404 | `"Event not found."` | `event.notFound` | Parent event missing (should not happen in normal operation) |
| 409 | — | `concurrency.conflict` | Concurrent conflict |
| 422 | `"Reservation is already confirmed."` | `reservation.alreadyConfirmed` | Already `Confirmada` |
| 422 | `"Reservation is cancelled."` | `reservation.cancelled` | Already `Cancelada` |
| 422 | `"Could not generate a unique reservation code."` | `reservation.codeCollision` | 10 consecutive code collision attempts failed |
| 422 | `"Not enough seats available."` | `event.capacity.exceeded` | Seat consume fails |

---

### 5.7 `POST /api/reservations/{id}/cancel`

**Authorization:** `User` OR `Admin` role required

**Purpose:** Cancel a `Confirmada` reservation. If cancelled with < 48 hours before the event start, a penalty applies and the seats are permanently lost (`seatsLost` increments).

_Source: `src/Api/Controllers/ReservationsController.cs` lines 55–66_

#### Path parameter

| Parameter | Type | Notes |
|-----------|------|-------|
| `id` | Guid | Reservation ID |

No request body.

#### Ownership rule
- A `User` can only cancel **their own** reservation (`reservation.UserId == currentUser.Id`).
- An `Admin` can cancel any reservation.

_Source: `src/Application/Reservations/CancelReservation/CancelReservationHandler.cs` line 38_

> **Note**: the ownership failure returns **422** (not 403), because `reservation.forbidden` does not match any of the special-case prefixes in `ResultExtensions.MapError`. The `[ProducesResponseType(403)]` attribute on the controller is misleading.

#### Response — 200 OK

Same `ReservationResponse` shape with `status` updated to `"Cancelada"`.

```json
{
  "id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "eventId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "userId": "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e",
  "quantity": 2,
  "buyerName": "María García",
  "buyerEmail": "maria@example.com",
  "status": "Cancelada",
  "createdUtc": "2025-06-24T14:32:00Z"
}
```

#### Errors

| HTTP | `detail` | Internal code | When |
|------|----------|---------------|------|
| 401 | `"Authentication required."` | `auth.unauthenticated` | JWT `sub` claim missing |
| 401 | — | — | Missing/invalid JWT |
| 403 | — | — | Role is not `User` or `Admin` |
| 404 | `"Reservation not found."` | `reservation.notFound` | Reservation Guid not found |
| 404 | `"Event not found."` | `event.notFound` | Parent event missing |
| 409 | — | `concurrency.conflict` | Concurrent conflict |
| 422 | `"You can only cancel your own reservations."` | `reservation.forbidden` | User tries to cancel another user's reservation |
| 422 | `"Reservation is cancelled."` | `reservation.cancelled` | Already `Cancelada` |
| 422 | `"Only confirmed reservations can be cancelled."` | `reservation.notConfirmed` | Status is `PendientePago` (not yet confirmed) |
| 422 | `"Cannot release more seats than are held."` | `event.capacity.overRelease` | Inconsistent seat count (should not happen in normal operation) |

---

## 6. Endpoints that do NOT exist

The following operations are **not implemented**. There are no routes for them:

| Operation | Notes |
|-----------|-------|
| `GET /api/events/{id}` | No single-event GET endpoint. Use `GET /api/events?q=...` to filter. |
| `GET /api/reservations` | No list-reservations endpoint. |
| `GET /api/reservations/{id}` | No get-reservation-by-id endpoint. |
| `PUT/PATCH /api/events/{id}` | No event update endpoint. |
| `DELETE /api/events/{id}` | No event deletion endpoint. |
| `DELETE /api/reservations/{id}` | No reservation deletion endpoint. Cancel via `POST /api/reservations/{id}/cancel`. |
| `POST /api/auth/register` | No user registration endpoint. |
| User management endpoints | No CRUD for users. |

---

## 7. Error Code Reference

Complete table of all domain error codes in the system.

| Code | HTTP status | `detail` message | Origin |
|------|-------------|-----------------|--------|
| `auth.invalidCredentials` | 401 | `"Invalid credentials."` | `LoginHandler` |
| `auth.unauthenticated` | 401 | `"Authentication required."` | `CreateReservationHandler`, `CancelReservationHandler` |
| `validation.failed` | 400 | `"{PropertyName}: {message}; ..."` (semicolon-joined) | `ValidationBehavior` (FluentValidation) |
| `venue.notFound` | 404 | `"Venue not found."` | `CreateEventHandler` |
| `event.notFound` | 404 | `"Event not found."` | `CreateReservationHandler`, `ConfirmReservationHandler`, `CancelReservationHandler`, `GetOccupancyHandler` |
| `reservation.notFound` | 404 | `"Reservation not found."` | `ConfirmReservationHandler`, `CancelReservationHandler` |
| `concurrency.conflict` | 409 | — | `ConcurrencyRetryPolicy` (EF concurrency token exhausted) |
| `DateRange.InvalidRange` | 422 | `"End must be after start."` | `DateRange.Create` |
| `money.invalidAmount` | 422 | `"Amount must be greater than zero."` | `Money.Create` |
| `Email.InvalidFormat` | 422 | `"Email format is invalid."` | `Email.Create` |
| `event.venueOverlap` | 422 | `"Venue schedule overlaps with another event."` | `CreateEventHandler` |
| `event.notActive` | 422 | `"Event is not active."` or `"Only active events can be cancelled."` | `CreateReservationHandler`, `Event.Cancel` |
| `event.title.length` | 422 | `"Title must be between 5 and 100 characters."` | `Event.Create` |
| `event.description.length` | 422 | `"Description must be between 10 and 500 characters."` | `Event.Create` |
| `event.capacity.invalid` | 422 | `"Capacity must be greater than zero."` | `Event.Create` |
| `event.capacity.exceedsVenue` | 422 | `"Capacity cannot exceed venue capacity."` | `Event.Create` |
| `event.capacity.exceeded` | 422 | `"Not enough seats available."` | `Event.HoldOnReserve`, `Event.ConsumeOnConfirm` |
| `event.capacity.overRelease` | 422 | `"Cannot release more seats than are held."` | `Event.ReleaseOnCancel`, `Event.ReleasePendingHold` |
| `event.quantity.invalid` | 422 | `"Quantity must be greater than zero."` | `Event.HoldOnReserve`, `Event.ConsumeOnConfirm`, `Event.ReleaseOnCancel` |
| `event.start.past` | 422 | `"Start must be in the future."` | `Event.Create` |
| `event.start.weekendNight` | 422 | `"Weekend events cannot start after 22:00."` | `Event.Create` |
| `reserve.tooLate` | 422 | `"Reservations are closed within 1 hour of the event start."` | `LateReservationRule` |
| `reserve.max5Near24h` | 422 | `"Maximum 5 seats within 24 hours of the event."` | `Near24hRule` |
| `reserve.max10HighPrice` | 422 | `"Maximum 10 seats for high-price events."` | `HighPriceRule` |
| `reserve.soldOut` | 422 | `"Not enough seats available."` | `AvailabilityRule` |
| `reserve.minQuantity` | 422 | `"At least one seat must be reserved."` | `AvailabilityRule` |
| `reservation.quantity.invalid` | 422 | `"Quantity must be greater than zero."` | `Reservation.Create` |
| `reservation.buyerName.required` | 422 | `"Buyer name is required."` | `Reservation.Create` |
| `reservation.email.required` | 422 | `"Email is required."` | `Reservation.Create` |
| `reservation.userId.required` | 422 | `"User identifier is required."` | `Reservation.Create` |
| `reservation.alreadyConfirmed` | 422 | `"Reservation is already confirmed."` | `Reservation.Confirm` |
| `reservation.cancelled` | 422 | `"Reservation is cancelled."` | `Reservation.Confirm`, `Reservation.Cancel` |
| `reservation.notConfirmed` | 422 | `"Only confirmed reservations can be cancelled."` | `Reservation.Cancel` |
| `reservation.codeCollision` | 422 | `"Could not generate a unique reservation code."` | `ConfirmReservationHandler` |
| `reservation.forbidden` | 422 | `"You can only cancel your own reservations."` | `CancelReservationHandler` |
| `reservation.notPending` | 422 | `"Only pending reservations can expire."` | `Reservation.Expire` (internal, used by expirer — not directly reachable via API) |

---

## 8. Seeded Data (Development)

These records are created automatically when the API starts in the Development environment.

### Users — `src/Infrastructure/Persistence/Seed/DevDataSeeder.cs`

| Email | Password | Role | ID |
|-------|----------|------|----|
| `admin@eventosvivos.com` | `Admin123!` | `Admin` | `a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d` |
| `user@eventosvivos.com` | `User123!` | `User` | `b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e` |

> Users are seeded at runtime (Development only) via `DevDataSeeder.SeedAsync`. They are idempotent (skipped if the email already exists).

### Venues — `src/Infrastructure/Persistence/Configurations/VenueConfiguration.cs`

Seeded via EF Core migrations (`HasData`), present in all environments.

| ID | Name | City | Max Capacity |
|----|------|------|--------------|
| `1` | Auditorio Central | Bogotá | 200 |
| `2` | Sala Norte | Bogotá | 50 |
| `3` | Arena Sur | Medellín | 500 |

> No events are seeded. Create events via `POST /api/events` using the admin credentials.

---

_Last verified against commit `6d42230` on 2026-06-24._

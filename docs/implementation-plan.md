# Implementation plan

Agreed design and step-by-step plan for the Hacker News Best Stories API. Each step is implemented
with **TDD** (failing test → minimal code → refactor) and delivered as **one pull request**.
Follow the folder conventions and rules in [`CLAUDE.md`](../CLAUDE.md).

## Requirements recap

- `GET /api/stories/best?count={n}` returns the best `n` stories, **ordered by score descending**:

  ```json
  [
    {
      "title": "A uBlock Origin update was rejected from the Chrome Web Store",
      "uri": "https://github.com/uBlockOrigin/uBlock-issues/issues/745",
      "postedBy": "ismaildonmez",
      "time": "2019-10-12T13:43:01+00:00",
      "score": 1716,
      "commentCount": 572
    }
  ]
  ```

- Must serve large numbers of requests efficiently **without overloading the Hacker News API**.
- Hacker News endpoints (base `https://hacker-news.firebaseio.com/v0/`):
  - `beststories.json` → array of up to **200** IDs (verified; `topstories`/`newstories` return 500
    but are different rankings, so they are not used).
  - `item/{id}.json` → item, or `null` when it does not exist.
- Field mapping: `title` ← `title`, `uri` ← `url`, `postedBy` ← `by`, `time` ← `time` (Unix seconds
  → `DateTimeOffset` UTC, serialized as `+00:00`), `score` ← `score`, `commentCount` ← `descendants`.
- Observed: ~250 ms per Hacker News call; many parallel TLS connections produced SSL errors
  locally, so outgoing concurrency must be bounded.

## Decisions

| Topic | Decision |
|---|---|
| API style | Minimal APIs, route groups, `TypedResults`, built-in OpenAPI |
| `count < 1` | `400 Bad Request` with `ProblemDetails` |
| `count > 200` | Returns all available stories (at most 200) |
| Ranking | All `beststories` items are ranked by `score` (the list is not guaranteed to be score-ordered), then `Take(n)` |
| Ties on score | Keep the original Hacker News `beststories` order (stable sort) |
| Story without `url` (e.g. Ask HN) | `uri` = `https://news.ycombinator.com/item?id={id}` |
| Missing `descendants` | `commentCount` = 0 |
| Items that are `null`, `deleted`, `dead` or not `type == "story"` | Excluded |
| Item that fails after retries | Rebuild: skipped. Refresh: keeps its cached copy, skipped if none. If no story at all can be obtained, the rebuild/refresh fails |
| Upstream failure contract | `BestStoriesService` wraps upstream failures (`HttpRequestException`, `JsonException`, Polly `ExecutionRejectedException`) in `HackerNewsUnavailableException` |
| Service lifetime | `BestStoriesService` is **transient** (the typed Hacker News client is transient so `IHttpClientFactory` can rotate handlers); singletons resolve it from a scope |
| Outgoing concurrency bound | Concurrency limiter of `AddStandardResilienceHandler()` (outermost strategy, shared by every client instance, retries included): `PermitLimit = MaxConcurrentRequests`, queue of 1000 |
| Inbound protection | Global concurrency limiter (configurable, generous) returning `429`; per-client rate limiting is a documented future enhancement |
| Defaults | Refresh interval **60 s**, cache TTL **3 min**, max **8** concurrent Hacker News calls |
| Cache warm-up | On start the refresher warms up the ranking through `GetBestStoriesAsync` (`GetOrCreateAsync`), so it shares the single rebuild with cold-cache requests instead of issuing a second one |
| Tests | xUnit v3 (MTP), NSubstitute, AwesomeAssertions, WireMock.Net, `FakeTimeProvider`, `FakeLogger`; never call the real Hacker News API |

## Caching design (hybrid)

`HybridCache` (in-memory L1) with three kinds of entries:

| Key | Content | Purpose |
|---|---|---|
| `hackernews:beststories` | Best story IDs | Universe of stories (≤ 200) |
| `hackernews:item:{id}` | Mapped story | Avoids re-fetching unchanged items when rebuilding |
| `beststories:ranked` | Stories sorted by score | **The only entry read by requests**: one lookup + `Take(n)` |

- **Background refresher** (`BackgroundService` + `PeriodicTimer` + `TimeProvider`): every refresh
  interval it re-fetches the IDs, refreshes the items with bounded concurrency, recomputes the
  ranking and overwrites the entries (`SetAsync`).
- **TTL (3 min) > refresh interval (60 s)**: if Hacker News fails during a refresh, the last good
  data keeps being served (the refresher only overwrites on success).
- **IDs leaving the list** are not deleted explicitly: their item entries simply expire.
- **Cold start / expired cache**: requests use `GetOrCreateAsync`; stampede protection guarantees a
  single rebuild even with thousands of concurrent requests, and only missing items are fetched.
- **Hacker News down and cache cold**: `503 Service Unavailable` with `ProblemDetails`.
- **Upstream load**: at most 1 + 200 calls per refresh interval, independent of incoming traffic
  (this is an SLO objective validated by the load test).
- Future enhancement: use `/v0/updates.json` to refresh only changed items.

## Configuration

`appsettings.json` section `HackerNews` (bound to `HackerNewsOptions`, validated on start):

| Key | Default |
|---|---|
| `BaseAddress` | `https://hacker-news.firebaseio.com/v0/` |
| `RefreshInterval` | `00:01:00` |
| `CacheExpiration` | `00:03:00` |
| `MaxConcurrentRequests` | `8` |

`HackerNews__BaseAddress` and `HackerNews__RefreshInterval` are already used by the load test
`compose.yaml`; keep those names.

Validation rules (the app fails to start otherwise): `BaseAddress` absolute, both intervals between
1 s and 1 day, `CacheExpiration` > `RefreshInterval`, `MaxConcurrentRequests` between 1 and 64.

The client throws `HttpRequestException` on error status codes (after the standard resilience
retries) and `JsonException` on malformed payloads; callers (step 3) decide how to degrade.

## Steps

### 1. Models and mapping
- `Models/StoryResponse` (public contract, `sealed record`).
- `Infrastructure/HackerNews/HackerNewsItem` (upstream DTO) and its mapping to `StoryResponse`.
- Tests: Unix time → `DateTimeOffset` UTC and JSON shape (`+00:00`), `uri` fallback (missing or
  invalid `url`), missing `descendants`, exclusion of deleted/dead/non-story items.
- Remove the temporary `--ignore-exit-code 8` from the unit test project (unit tests now exist).

### 2. Hacker News client
- `Infrastructure/HackerNews/IHackerNewsClient`, `HackerNewsClient` (typed `HttpClient`),
  `HackerNewsOptions` with validation, `AddStandardResilienceHandler()`.
- DI registration in `Extensions/`.
- Tests with a fake `HttpMessageHandler`: IDs, item, `null` item, HTTP errors, deserialization.

### 3. Ranking service with cache
- `Services/IBestStoriesService`, `BestStoriesService` using `HybridCache`:
  `GetBestStoriesAsync(count)` reads the ranking (`GetOrCreateAsync`, fetching only missing entries) and
  `RefreshAsync()` re-fetches everything and overwrites the entries (`SetAsync`) for the refresher.
- `RankedStories` and `StoryResponse` are sealed and `[ImmutableObject(true)]`, so `HybridCache`
  returns the cached instance instead of deserializing a copy per request.
- Stable sort by score descending, `Take(n)`.
- Shared concurrency bound configured on the resilience pipeline in `AddHackerNewsClient()`.
- DI registration (`AddBestStoriesService()`: `HybridCache` + transient service).
- Tests: ordering and ties, `n` bigger than available, cache hits avoid client calls, stampede
  protection (many concurrent calls → one rebuild), partial item failures, refresh (overwrite,
  cached copy fallback, last ranking kept on failure), concurrency bound shared across clients.

### 4. Background refresher
- `BackgroundServices/BestStoriesCacheRefresher`: on start warms up the ranking; then on each
  interval (`PeriodicTimer` + `TimeProvider`) creates a scope (`IServiceScopeFactory`) and calls
  `IBestStoriesService.RefreshAsync()`; a `HackerNewsUnavailableException` is logged and the last
  good data keeps being served. On stop the cancellation propagates (the host treats it as clean).
- Registered by `AddBestStoriesService()` together with `TimeProvider.System`.
- Tests with `FakeTimeProvider` and `FakeLogger`: warm-up, refreshes on each interval (new scope
  each time), failure is logged and refreshing continues, cancellation stops cleanly. Logging via
  `LoggerMessage` source generator. Component tests point `HackerNews:BaseAddress` to a closed local
  port so the refresher never reaches the real API.

### 5. Endpoint
- `Apis/BestStoriesApi.MapBestStoriesApi()`: `GET /api/stories/best?count=n`, OpenAPI metadata.
- Component tests (`WebApplicationFactory` + WireMock.Net): happy path and JSON contract, ordering,
  `count` validation (400), `count > 200`, Hacker News down with cold cache (503) and warm cache
  (stale data served), upstream calls bounded under concurrent requests.
- Resilience of the Hacker News client (deferred from step 2, where it is not unit tested because
  the retries use real backoff): transient upstream failures (e.g. `500` then `200`) are retried by
  `AddStandardResilienceHandler()` and the request succeeds. Shorten the retry delay in the test
  host configuration to keep the test fast.

### 6. Cross-cutting
- Health checks: `/health/live` and `/health/ready` (ready when the ranked cache is warm).
- Global concurrency limiter (`429` when exceeded).
- Load test `setup()` waits on `/health/ready`.

### 7. Load test and documentation
- Run the `Load test` workflow; calibrate the SLO targets with the measured results.
- Complete the README: usage, design, assumptions, future enhancements.

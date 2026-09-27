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
| Inbound protection | Global concurrency limiter (configurable, generous) returning `429`; per-client rate limiting is a documented future enhancement |
| Defaults | Refresh interval **60 s**, cache TTL **3 min**, max **8** concurrent Hacker News calls |
| Tests | xUnit v3 (MTP), NSubstitute, AwesomeAssertions, WireMock.Net, `FakeTimeProvider`; never call the real Hacker News API |

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

## Steps

### 1. Models and mapping
- `Models/StoryResponse` (public contract, `sealed record`).
- `Infrastructure/HackerNews/HackerNewsItem` (upstream DTO) and its mapping to `StoryResponse`.
- Tests: Unix time → `DateTimeOffset` UTC and JSON shape (`+00:00`), `uri` fallback, missing
  `descendants`, exclusion of deleted/dead/non-story items.

### 2. Hacker News client
- `Infrastructure/HackerNews/IHackerNewsClient`, `HackerNewsClient` (typed `HttpClient`),
  `HackerNewsOptions` with validation, `AddStandardResilienceHandler()`.
- DI registration in `Extensions/`.
- Tests with a fake `HttpMessageHandler`: IDs, item, `null` item, HTTP errors, deserialization.

### 3. Ranking service with cache
- `Services/IBestStoriesService`, `BestStoriesService` using `HybridCache` and bounded concurrency.
- Stable sort by score descending, `Take(n)`.
- Tests: ordering and ties, `n` bigger than available, cache hits avoid client calls, stampede
  protection (many concurrent calls → one rebuild), partial item failures.

### 4. Background refresher
- `BackgroundServices/BestStoriesCacheRefresher`.
- Tests with `FakeTimeProvider`: refreshes on each interval, failure keeps last good data,
  cancellation stops cleanly. Logging via `LoggerMessage` source generator.

### 5. Endpoint
- `Apis/BestStoriesApi.MapBestStoriesApi()`: `GET /api/stories/best?count=n`, OpenAPI metadata.
- Component tests (`WebApplicationFactory` + WireMock.Net): happy path and JSON contract, ordering,
  `count` validation (400), `count > 200`, Hacker News down with cold cache (503) and warm cache
  (stale data served), upstream calls bounded under concurrent requests.

### 6. Cross-cutting
- Health checks: `/health/live` and `/health/ready` (ready when the ranked cache is warm).
- Global concurrency limiter (`429` when exceeded).
- Remove the temporary `--ignore-exit-code 8` from the unit test project.
- Load test `setup()` waits on `/health/ready`.

### 7. Load test and documentation
- Run the `Load test` workflow; calibrate the SLO targets with the measured results.
- Complete the README: usage, design, assumptions, future enhancements.

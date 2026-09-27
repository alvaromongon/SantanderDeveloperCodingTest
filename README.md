# Hacker News Best Stories API

ASP.NET Core (.NET 10) RESTful API that returns the details of the best `n` stories from the
[Hacker News API](https://github.com/HackerNews/API), ordered by score (descending).

> **Status:** work in progress. Sections marked _TBD_ will be completed as the implementation lands.

## Requirements

- [.NET SDK 10.0](https://dotnet.microsoft.com/download/dotnet/10.0) (see `global.json`)
- [Docker](https://www.docker.com/) (optional)

## How to run

### With the .NET CLI

```bash
dotnet run --project src/HackerNews.BestStories.Api
```

### With Docker

```bash
docker build -t hackernews-beststories .
docker run --rm -p 8080:8080 hackernews-beststories
```

The OpenAPI document is available at `/openapi/v1.json`.

### Usage

_TBD_ – `GET /api/stories/best?count={n}`

## How to test

```bash
dotnet test
```

The full local quality gate (the same checks as CI) runs automatically before every `git push`
and can be run manually:

```bash
.githooks/pre-push
```

## Project structure

```
src/HackerNews.BestStories.Api/
├── Apis/                    Minimal API endpoint groups
├── BackgroundServices/      Hosted services (periodic cache refresh)
├── Extensions/              Service registration and pipeline extension methods
├── Infrastructure/
│   └── HackerNews/          Typed HttpClient, upstream DTOs and options
├── Models/                  Public API contracts
├── Services/                Application logic
└── Program.cs
tests/
├── HackerNews.BestStories.Api.UnitTests/       Unit tests (mirror the src folders)
├── HackerNews.BestStories.Api.ComponentTests/  In-process API tests, Hacker News stubbed (mirror the src folders)
└── HackerNews.BestStories.Api.LoadTests/       k6 load test validating the SLO
```

The layout follows Microsoft's reference Minimal API services (eShop): endpoints grouped with
route-group extension methods and dependency injection wired through extension methods.

## Design

_TBD_ – summary of the approach:

- **Hybrid caching** with `HybridCache`: the list of best story IDs, each story, and the ranked
  result are cached; a background service refreshes them periodically so Hacker News load is
  bounded and independent of incoming traffic. Cache misses (cold start) are rebuilt once thanks to
  stampede protection.
- **Resilience** on the outgoing `HttpClient` (retries, circuit breaker, timeouts) and bounded
  concurrency towards Hacker News.

## Service Level Objectives (SLO)

Measured for `GET /api/stories/best?count={n}` with a mix of `n` (30% of requests with `n = 200`,
the worst case) on the reference environment: a GitHub-hosted `ubuntu-latest` runner with the API
running in its Docker image and Hacker News replaced by a stub that adds 100 ms latency per call.

| Objective | Target |
|---|---|
| Sustained load | **500 requests/second** |
| Latency at that load | **p95 < 100 ms**, **p99 < 250 ms** |
| Error rate at that load | **< 0.1%** |
| Hacker News protection | At most one full refresh (1 + 200 requests) per refresh interval, **independent of the incoming load** |
| Data freshness | Stories are at most one refresh interval old (default 60 s) while Hacker News is available |

These targets are an initial proposal and will be calibrated with the first load test results.
They are enforced as k6 thresholds in
[`best-stories.js`](tests/HackerNews.BestStories.Api.LoadTests/scripts/best-stories.js).

### Load test

The [`Load test`](.github/workflows/load-test.yml) workflow runs **nightly at 03:00 UTC** and on
demand (with configurable rate and duration). It starts the API and a WireMock stub of Hacker News
with Docker Compose, drives a constant arrival rate with [k6](https://k6.io/), and fails when any
SLO threshold is breached. The SLO report is written to the workflow run summary, and the raw
results (JSON and HTML report) are uploaded as an artifact.

Run it locally:

```bash
docker compose -f tests/HackerNews.BestStories.Api.LoadTests/compose.yaml up -d --build api hackernews-stub
docker compose -f tests/HackerNews.BestStories.Api.LoadTests/compose.yaml run --rm k6
docker compose -f tests/HackerNews.BestStories.Api.LoadTests/compose.yaml down
```

`RATE`, `DURATION` and `REFRESH_INTERVAL` can be overridden as environment variables. Results are
written to `tests/HackerNews.BestStories.Api.LoadTests/results/`.

## Assumptions

- The Hacker News `beststories` endpoint returns at most **200** IDs, so `n` greater than 200
  returns all available stories; `n` lower than 1 is rejected with `400 Bad Request`.
- The best stories are ranked by `score` across the whole `beststories` list, not only the first
  `n` IDs, because Hacker News does not guarantee that list is ordered by score.
- Data may be slightly stale (up to the configured refresh interval).
- _TBD_

## Enhancements given more time

_TBD_

## Quality gates

| Gate | Local (`pre-push`) | CI |
|---|---|---|
| Formatting (`dotnet format --verify-no-changes`) | ✅ | ✅ |
| Build with analyzers, warnings as errors | ✅ | ✅ |
| Unit and component tests | ✅ | ✅ |
| Line coverage ≥ 80% | ✅ | ✅ |
| Locked NuGet restore and vulnerable package audit | | ✅ |
| Docker image build and Trivy scan | | ✅ |
| CodeQL static analysis | | ✅ |
| SLO load test | | ✅ (nightly) |

`main` is protected by a repository ruleset ([definition](.github/rulesets/main.json)): changes go
through pull requests, the `Build & test`, `Docker image` and `Analyze C#` checks must pass and the
branch must be up to date. Force pushes and deletion are blocked.

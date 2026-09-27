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

## Design

_TBD_ – summary of the approach:

- **Hybrid caching** with `HybridCache`: the list of best story IDs, each story, and the ranked
  result are cached; a background service refreshes them periodically so Hacker News load is
  bounded and independent of incoming traffic. Cache misses (cold start) are rebuilt once thanks to
  stampede protection.
- **Resilience** on the outgoing `HttpClient` (retries, circuit breaker, timeouts) and bounded
  concurrency towards Hacker News.

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

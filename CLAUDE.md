# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project

Santander backend coding test: an ASP.NET Core (.NET 10) Minimal API that returns the best `n`
Hacker News stories ordered by score, without overloading the Hacker News API.
See `README.md` for requirements, design and assumptions.

**Implementation plan:** [`docs/implementation-plan.md`](docs/implementation-plan.md) holds the agreed
design, decisions and steps. Read it before implementing and keep it updated if a decision changes.

## Layout

Source follows the conventions of Microsoft's reference Minimal API services (eShop):
endpoints grouped with route-group extension methods, DI wiring in extension methods, and
technical folders inside a single web project.

```
src/HackerNews.BestStories.Api/
├── Apis/                    Minimal API endpoint groups (e.g. BestStoriesApi.MapBestStoriesApi)
├── BackgroundServices/      Hosted services (periodic cache refresh)
├── Extensions/              IServiceCollection / WebApplication extension methods
├── Infrastructure/
│   └── HackerNews/          Typed HttpClient, upstream DTOs and options
├── Models/                  Public API contracts (response DTOs)
├── Services/                Application logic (ranking, cached best stories)
├── Properties/launchSettings.json
├── appsettings*.json
└── Program.cs
tests/
├── HackerNews.BestStories.Api.UnitTests/       Mirrors src folders, one `{Type}Tests` class per type
├── HackerNews.BestStories.Api.ComponentTests/  Mirrors src folders; in-process API via
│                                               WebApplicationFactory, Hacker News stubbed with WireMock.Net
└── HackerNews.BestStories.Api.LoadTests/       k6 script, WireMock stub mappings and compose file (SLO)
```

- Test folders and namespaces **mirror the source**: `src/.../Services/Foo.cs` →
  `tests/...UnitTests/Services/FooTests.cs`, namespace `HackerNews.BestStories.Api.UnitTests.Services`.
- Shared hand-written fakes/stubs (e.g. `StubHttpMessageHandler`) live in `TestDoubles/` at the root
  of each test project; it is the only test folder that does not mirror the source.
- No test may call the real Hacker News API.
- `build/coverage.sh` – merges coverage and enforces the threshold (80% lines).
- `.githooks/pre-push` – local gate (format, build, tests, coverage); enabled by the first build.
- `.github/rulesets/` – branch protection applied to GitHub (see README).

## Commands

```bash
dotnet build
dotnet test
dotnet format --verify-no-changes
.githooks/pre-push   # full local gate, same checks as CI

# Load test (SLO) with Docker
docker compose -f tests/HackerNews.BestStories.Api.LoadTests/compose.yaml up -d --build api hackernews-stub
docker compose -f tests/HackerNews.BestStories.Api.LoadTests/compose.yaml run --rm k6
docker compose -f tests/HackerNews.BestStories.Api.LoadTests/compose.yaml down
```

The SLO is defined in `README.md` and enforced by the thresholds in
`tests/HackerNews.BestStories.Api.LoadTests/scripts/best-stories.js`; keep both in sync.

## Rules

- **TDD**: write a failing test first, make it pass with the minimum code, then refactor.
- Follow Microsoft .NET naming and coding conventions; `.editorconfig` is enforced on build and
  warnings are errors (`Directory.Build.props`).
- Package versions live only in `Directory.Packages.props` (Central Package Management);
  never put `Version` on a `PackageReference`. Commit updated `packages.lock.json` files.
- Test names: `Method_Scenario_ExpectedResult`.
- Commits are authored by the repository owner: no `Co-Authored-By` trailers.
- **Only `git push` after an explicit confirmation from the owner** for that push; approval does not carry
  over to later pushes. `main` is protected: changes go through pull requests.

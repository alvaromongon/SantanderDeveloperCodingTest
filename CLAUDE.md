# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project

Santander backend coding test: an ASP.NET Core (.NET 10) Minimal API that returns the best `n`
Hacker News stories ordered by score, without overloading the Hacker News API.
See `README.md` for requirements, design and assumptions.

## Layout

- `src/HackerNews.BestStories.Api` – the API (Minimal APIs).
- `tests/HackerNews.BestStories.Api.UnitTests` – unit tests (xUnit v3, NSubstitute, AwesomeAssertions).
- `tests/HackerNews.BestStories.Api.ComponentTests` – in-process tests with `WebApplicationFactory`
  and WireMock.Net stubbing the Hacker News API. No test may call the real Hacker News API.
- `build/coverage.sh` – merges coverage and enforces the threshold (80% lines).
- `.githooks/pre-push` – local gate (format, build, tests, coverage); enabled by the first build.

## Commands

```bash
dotnet build
dotnet test
dotnet format --verify-no-changes
.githooks/pre-push   # full local gate, same checks as CI
```

## Rules

- **TDD**: write a failing test first, make it pass with the minimum code, then refactor.
- Follow Microsoft .NET naming and coding conventions; `.editorconfig` is enforced on build and
  warnings are errors (`Directory.Build.props`).
- Package versions live only in `Directory.Packages.props` (Central Package Management);
  never put `Version` on a `PackageReference`. Commit updated `packages.lock.json` files.
- Test names: `Method_Scenario_ExpectedResult`.
- Commits are authored by the repository owner: no `Co-Authored-By` trailers.
- **Never `git push`** – the owner pushes manually.

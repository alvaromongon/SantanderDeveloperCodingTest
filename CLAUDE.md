# CLAUDE.md

Guidance for Claude Code when working in this repository.

**`README.md` is the source of truth** for the project: requirements, how to run and test, project
structure, test and development conventions, design, SLO, assumptions and quality gates. Read the
relevant sections before working and keep them up to date; do not duplicate them here.

- Project: ASP.NET Core (.NET 10) Minimal API returning the best `n` Hacker News stories by score
  without overloading the Hacker News API.
- Design decisions and steps: [`docs/implementation-plan.md`](docs/implementation-plan.md). Read it
  before implementing and update it if a decision changes.

## Where to look in the README

| Topic | README section |
|---|---|
| Build, run and configuration | *How to run* |
| Test commands, local gate and test conventions | *How to test* |
| Folder layout and coding conventions | *Project structure*, *Development conventions* |
| Load test commands and SLO | *Service Level Objectives (SLO)* |
| CI checks and branch protection | *Quality gates* |

## Rules for Claude

- Follow the README conventions, in particular **TDD** (failing test first) and mirroring test
  folders. Never call the real Hacker News API from tests.
- Before finishing, run the local gate: `.githooks/pre-push`.
- The SLO in `README.md` and the thresholds in
  `tests/HackerNews.BestStories.Api.LoadTests/scripts/best-stories.js` must stay in sync.
- Commits are authored by the repository owner: no `Co-Authored-By` trailers.
- **Only `git push` after an explicit confirmation from the owner** for that push; approval does not
  carry over to later pushes.

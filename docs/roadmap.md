# Implementation Roadmap

## Phase 0 - Rule Lock

Goal: turn local rule knowledge into a testable rules profile.

Deliverables:

- Confirm open rule questions in `docs/product-spec.md`.
- Define default rule profile.
- Decide which variants are deferred.
- Write acceptance examples for bidding, trick play, marriages, trupa, and scoring.

Exit criteria:

- No major unresolved rule ambiguity blocks engine implementation.

## Phase 1 - Repository Scaffold

Goal: create the .NET solution and baseline projects.

Deliverables:

- `RuskaTrupa.sln`.
- `RuskaTrupa.Core`.
- `RuskaTrupa.AI`.
- `RuskaTrupa.Web`.
- `RuskaTrupa.Core.Tests`.
- `RuskaTrupa.AI.Tests`.
- CI build if hosted on GitHub.

Exit criteria:

- Solution builds.
- Empty test projects run.
- Web app starts.

## Phase 2 - Core Engine

Goal: implement deterministic game rules.

Deliverables:

- Card and deck model.
- Deal logic.
- Game phases.
- Legal action provider.
- Reducer/state transition engine.
- Trick resolution.
- Scoring.
- Game over detection.
- Unit tests for all completed rules.

Exit criteria:

- A complete hand can be played through code.
- Invalid actions are rejected.
- Tests cover core scoring and trick behavior.

## Phase 3 - Basic AI

Goal: make the game playable against two simple but legal bots.

Deliverables:

- Agent interface.
- Beginner heuristic bot.
- Basic bidding.
- Basic trump selection.
- Basic card play.
- Basic defender cooperation.
- AI legality tests.

Exit criteria:

- One human plus two bots can complete a full game in a non-polished debug UI.
- Bots never produce illegal actions in automated tests.

## Phase 4 - Web MVP

Goal: create the first usable Blazor PWA.

Deliverables:

- Main table screen.
- Player hand UI.
- Bidding UI.
- Trump selection UI.
- Card play UI.
- Scoreboard.
- Game log.
- Local save/resume.
- PWA manifest, icons, and service worker.

Exit criteria:

- A user can play a full game to 501 in the browser.
- PWA can be installed locally.
- Offline load works after installation.

## Phase 5 - AI Quality

Goal: improve bots from legal to enjoyable.

Deliverables:

- Standard heuristic bot.
- Self-play harness.
- AI statistics report.
- Monte Carlo prototype for Strong difficulty.
- Tuning data from thousands of hands.

Exit criteria:

- Bots make plausible bids.
- Defender bots use trupa signal meaningfully.
- Bidder success rate is within an acceptable range after tuning.

## Phase 6 - Polish and Store Readiness

Goal: prepare the app for public distribution.

Deliverables:

- Responsive visual polish.
- Keyboard and touch-friendly controls.
- Accessibility pass.
- Store screenshots.
- Privacy policy page if required.
- PWABuilder validation.
- Microsoft Store package.

Exit criteria:

- Hosted PWA passes installability checks.
- Store package can be submitted.

## Phase 7 - Native Windows Option

Goal: add a native Windows shell only if PWA distribution is not enough.

Deliverables:

- Evaluate PWA user experience after MVP.
- Decide between .NET MAUI Blazor Hybrid and WinUI shell.
- Reuse Core, AI, and Razor components where practical.

Exit criteria:

- Clear go/no-go decision for native Windows app.

## Initial Backlog

1. Confirm rule questions.
2. Scaffold .NET solution.
3. Implement card model.
4. Implement deck and seeded shuffle.
5. Implement deal.
6. Implement bidding state.
7. Implement talon reveal and card passing.
8. Implement legal card play.
9. Implement trick winner.
10. Implement hand scoring.
11. Implement game scoring to 501.
12. Implement beginner AI.
13. Implement basic Blazor table UI.
14. Enable PWA install support.
15. Add self-play test harness.

## Suggested First Milestone

The first useful milestone should be a non-polished but complete local game loop:

```text
Start game -> deal -> bid -> choose trump -> pass cards -> play tricks -> score hand -> next hand -> game over at 501
```

This milestone should prioritize correctness over presentation. Once that loop is stable, UI polish and AI strength can iterate safely.

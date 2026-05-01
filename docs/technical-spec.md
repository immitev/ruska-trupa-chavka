# Technical Specification

## Recommended Stack

- .NET 10 LTS.
- C# for all domain logic.
- Blazor WebAssembly for the web client.
- PWA support through the Blazor WebAssembly PWA template.
- xUnit or NUnit for tests.
- Optional later: .NET MAUI Blazor Hybrid for a native Windows shell.

## Solution Layout

```text
src/
  RuskaTrupa.Core/
  RuskaTrupa.AI/
  RuskaTrupa.Web/
tests/
  RuskaTrupa.Core.Tests/
  RuskaTrupa.AI.Tests/
docs/
```

Optional later projects:

```text
src/
  RuskaTrupa.Windows/
  RuskaTrupa.Server/
tests/
  RuskaTrupa.Integration.Tests/
```

## Project Responsibilities

### RuskaTrupa.Core

Owns deterministic game rules and state transitions.

Responsibilities:

- Card model.
- Deck generation and shuffling abstraction.
- Deal model.
- Public and private player state.
- Legal action generation.
- Game reducer/state machine.
- Trick resolution.
- Marriage detection and announcement validation.
- Score calculation.
- Game persistence DTOs.

The core must not reference Blazor, UI, browser APIs, storage APIs, or AI implementation details.

### RuskaTrupa.AI

Owns computer player strategy.

Responsibilities:

- AI agent interfaces.
- Bidding strategy.
- Trump selection.
- Card play strategy.
- Trupa signal strategy.
- Resignation strategy.
- Monte Carlo simulation support.
- Self-play simulation harness.

The AI may reference `RuskaTrupa.Core`, but `RuskaTrupa.Core` must not reference AI.

### RuskaTrupa.Web

Owns the user experience.

Responsibilities:

- Blazor routing and screens.
- Game table UI.
- Card rendering.
- Action controls.
- Local game orchestration.
- PWA manifest and service worker configuration.
- Local save/resume using browser storage.

### RuskaTrupa.Windows

Optional later Windows shell.

Responsibilities:

- Host shared Razor components in a desktop shell.
- Provide Windows-specific packaging if PWA distribution is insufficient.
- Avoid duplicating rules or AI logic.

## Core Architecture

The engine should be a reducer-style state machine:

```csharp
public interface IGameReducer
{
    GameState Apply(GameState state, GameAction action);
}
```

Every action must be validated against the current state before it mutates the game:

```csharp
public interface ILegalActionProvider
{
    IReadOnlyList<GameAction> GetLegalActions(GameState state, PlayerId player);
}
```

The UI and AI should both use the same legal action provider.

## Hidden Information Model

The system must distinguish full state from player-visible state.

```csharp
public sealed record GameState(
    GamePhase Phase,
    IReadOnlyList<PlayerState> Players,
    TalonState Talon,
    PublicHandState Public,
    ScoreState Score,
    RandomSeed Seed
);

public sealed record GameObservation(
    PlayerId Self,
    IReadOnlyList<Card> Hand,
    PublicGameState PublicState,
    IReadOnlyList<Bid> Bids,
    IReadOnlyList<PlayedTrick> CompletedTricks,
    CurrentTrick? CurrentTrick,
    TrumpSuit? Trump,
    IReadOnlyList<Announcement> Announcements
);
```

Only `GameObservation` is passed to AI agents.

## Game Phases

Recommended initial phases:

```text
NotStarted
Dealing
Bidding
RevealTalon
ChooseTrump
PassCards
PlayingTricks
ScoringHand
GameOver
```

Each phase should define:

- Legal actions.
- Required actor.
- State transition rules.
- Validation errors.

## Action Model

Recommended actions:

```text
StartGame
StartHand
Bid
PassBid
RevealTalon
ChooseTrump
PassCardToOpponent
PlayCard
AnnounceMarriage
SignalTrupa
ResignHand
AcceptScoring
StartNextHand
```

Actions should be serializable so games can be replayed and debugged.

## Randomness

Use a seedable random source.

Requirements:

- A game can be reproduced from a seed and action log.
- AI self-play can run deterministic test cases.
- Production games can use non-predictable seed generation.

## Persistence

MVP persistence can use browser local storage.

Save:

- Current game state.
- Player preferences.
- Last selected rule profile.
- Basic statistics.

Avoid persistence formats that depend on private implementation details. Prefer versioned DTOs.

## Testing Strategy

Core tests:

- Deck contains exactly 24 unique cards.
- Deal produces correct hand and talon sizes.
- Bidding validates pass and bid constraints.
- Talon pickup and card passing preserve card count.
- Legal card play rejects impossible cards.
- Trick winner is correct for each suit/trump scenario.
- Marriage announcements score correctly.
- Failed bid subtracts score.
- Resignation scoring is correct.
- Game ends at 501 or above.

AI tests:

- AI never returns illegal actions.
- AI uses only `GameObservation`.
- AI can complete 10,000 self-play hands without invalid state.
- AI bidding does not produce obviously impossible bids.
- Defender cooperation does not depend on hidden cards.

## Distribution

### Web

Deploy as static Blazor WebAssembly assets.

Hosting options:

- Azure Static Web Apps.
- GitHub Pages.
- Cloudflare Pages.
- Any static host with HTTPS.

### PWA

PWA requirements:

- Valid web app manifest.
- App icons.
- Service worker.
- Offline fallback.
- HTTPS hosting.
- Installable from Chromium-based browsers.

### Microsoft Store

Use PWABuilder to create the Store package from the hosted PWA URL.

Store submission needs:

- Partner Center app reservation.
- Package identity values.
- Generated MSIX bundle.
- Store screenshots and assets.
- Age rating.
- Privacy policy URL if required by Store policy.

Manifest changes require a new package submission. Normal app code and asset updates can usually be served from the web host without repackaging.

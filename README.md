# Ruska Trupa Chavka

A C# implementation of the Bulgarian card game **Руска трупа** (also known as чавка / руска тройка), built as a web-first game with a separate rules engine
and AI opponents.

The rules reference used by the project is the Bulgarian Wikipedia article:
[Руска трупа](https://bg.wikipedia.org/wiki/%D0%A0%D1%83%D1%81%D0%BA%D0%B0_%D1%82%D1%80%D1%83%D0%BF%D0%B0).

## Current Status

The project includes:

- Blazor WebAssembly UI for browser play.
- C# core rules engine in `RuskaTrupa.Core`.
- C# AI engine in `RuskaTrupa.AI`.
- Two configurable AI/algorithmic opponents.
- Bulgarian and English localization.
- Bidding, trump / no-trump play, talon handling, passing cards, trick play,  marriages, resigning a hand, scoring, and game-over flow.
- Replay/review of the last hand, including bids and card play.
- autoplay mode for letting quickly finishing the current hand.
- Bot levels: Beginner, Intermediate, Advanced.
- Bot play styles focused on bidding behavior: Balanced, Bold, Patient.

## Solution Layout

- `src/RuskaTrupa.Core` - card model, game state, legal actions, reducer, scoring,
  save/restore support.
- `src/RuskaTrupa.AI` - heuristic AI player implementation and bot difficulty/style
  behavior.
- `src/RuskaTrupa.Web` - Blazor WebAssembly app, UI, localization, persistence, and
  browser game flow.
- `tests/RuskaTrupa.Core.Tests` - rules and reducer tests.
- `tests/RuskaTrupa.AI.Tests` - AI behavior and benchmark/stress tests.
- `docs` - product, technical, AI, and roadmap notes from the original planning
  phase.

## Requirements

- .NET 10 SDK
- A modern browser
- Visual Studio 2026 or a compatible .NET CLI workflow

## Build and Test

```powershell
dotnet build RuskaTrupa.slnx
dotnet test RuskaTrupa.slnx
```

Run the web app:

```powershell
dotnet run --project src/RuskaTrupa.Web/RuskaTrupa.Web.csproj
```

Then open the local URL printed by `dotnet run`.

## Notes

The app is web-first and remains suitable for PWA packaging. A native Windows shell
can be added later through a .NET wrapper such as MAUI Blazor Hybrid or WinUI if
the project needs Microsoft Store distribution beyond the browser/PWA route.

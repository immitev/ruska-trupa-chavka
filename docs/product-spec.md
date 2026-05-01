# Ruska Trupa Product Specification

## Product Goal

Build a digital version of Ruska Trupa, also known as Chavka or Ruska Troyka, as a web-first card game with installable Windows distribution.

The first production target is a Blazor WebAssembly Progressive Web App. The app should also be suitable for Microsoft Store distribution as a PWA package. A native Windows shell can be added later by reusing the same C# game engine and, where practical, the same Razor UI components.

## Target Platforms

- Web browsers on desktop and mobile.
- Installable PWA on Windows through Microsoft Edge or Chrome.
- Microsoft Store PWA package.
- Optional later target: Windows desktop app using .NET MAUI Blazor Hybrid or WinUI.

## Primary Game Mode

The first playable mode is:

- One human player.
- Two computer-controlled players.
- Local single-device play.
- No account, server, or online multiplayer requirement for MVP.

The AI players must behave like constrained human players. They must only use information visible to a player in their position.

## Future Game Modes

- Three AI self-play simulation for balancing and regression testing.
- Pass-and-play local human mode.
- Online multiplayer.
- Ranked play and statistics.
- Daily challenge or preset deal mode.

## Player Experience

The MVP should let a player:

- Start a new game quickly.
- Choose seat/player name.
- Play against two AI opponents.
- See hand, bids, trump, current trick, taken tricks, announcements, and score.
- Make all legal actions through the UI.
- Receive clear feedback when an action is illegal or unavailable.
- Resume a local game after closing the app, if technically feasible in the MVP.
- Play offline after the PWA has been installed and cached.

## Game Summary

Ruska Trupa is a three-player trick-taking card game played with a 24-card deck:

- Ranks: 9, J, Q, K, 10, A.
- Suits: clubs, diamonds, hearts, spades.
- Card point values:
  - A: 11
  - 10: 10
  - K: 4
  - Q: 3
  - J: 2
  - 9: 0

Each hand has:

- Deal.
- Bidding.
- Talon pickup by the winning bidder.
- Trump selection.
- Discard or card passing to restore hand sizes.
- Trick play.
- Scoring.

Game score is cumulative. The first player to reach at least 501 points wins.

## Baseline Rules

These rules are the initial implementation baseline and must be confirmed before engine lock.

### Players

- Exactly three players.
- Dealer rotates after each hand.
- The player after the dealer starts bidding.

### Deck and Deal

- Use a 24-card deck.
- Deal seven cards to each player.
- Leave three cards face down as talon.
- Dealing pattern: first three cards, then two cards, then two cards.

### Bidding

- Minimum opening bid is 100.
- Players may pass.
- Highest bid wins.
- A bid above 120 requires visible proof of at least one king-queen marriage.
- A bid above 140 requires visible proof of at least two king-queen marriages.
- The winning bidder reveals and takes the talon.
- The winning bidder selects trump.
- The winning bidder gives one chosen card to each opponent, returning to seven cards.

### Trick Play

- The winning bidder tries to score at least the bid value.
- The other two players cooperate against the winning bidder.
- Raising is not mandatory.
- Legal play constraints around following suit and trumping must be confirmed.
- The winner of each trick leads the next trick unless the confirmed local rules say otherwise.

### Trupa Signal

Defenders may use a "trupa" signal to indicate that their partner should add a strong card when the signaling player expects to win the trick.

For implementation, "trupa" is modeled as an explicit public action available only to defender players when the rules allow it.

### Marriages

- A king and queen of the same suit form a marriage.
- Non-trump marriage: 20 points.
- Trump marriage: 40 points.
- A marriage may be announced when the suit is played.
- The player does not necessarily need to be leading the trick, pending rule confirmation.

### Scoring

- If the bidder reaches the bid, the bidder scores earned points.
- The defenders score their earned points.
- If the bidder fails, the bid amount is subtracted from the bidder's cumulative score.
- Defenders still add their earned points.
- If the bidder resigns by the second trick inclusive, the bid amount is subtracted from the bidder, and each defender receives 25 points.

## Rule Questions To Resolve

These must be answered before the rules engine is considered final:

1. Must a player follow suit when able?
2. If unable to follow suit, must a player play trump when able?
3. If playing trump, must a player overtrump when able?
4. Who leads the first trick after talon pickup and trump selection?
5. Are there fixed bid increments, or can any integer bid be made?
6. What is the maximum bid?
7. What exactly happens if all players pass?
8. Does the bidder score actual earned points on success or only the bid amount?
9. Can defenders announce marriages, or only the bidder?
10. Can a marriage be announced only when playing one of the two marriage cards?
11. Can a player announce more than one marriage in a hand?
12. When exactly is the "trupa" signal legal?
13. Is "trupa" a binding instruction, or only advisory table talk?
14. Are there local scoring variants that should be supported from the start?

## Non-Goals For MVP

- Real-money play.
- Online multiplayer.
- User accounts.
- Chat.
- Custom rule editor.
- Advanced animations.
- Native Windows-only features.

## Success Criteria

The MVP is acceptable when:

- A full game to 501 can be played by one human against two AI players.
- All legal actions are enforced by the engine.
- AI does not access hidden information.
- Rules are covered by unit tests.
- Self-play can run many hands without invalid states or crashes.
- PWA install works.
- The app can be prepared for Microsoft Store PWA packaging.

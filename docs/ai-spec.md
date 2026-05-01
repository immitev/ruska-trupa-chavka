# AI Specification

## AI Goal

The game must include two AI opponents that simulate human players well enough to make the game interesting. They should be competent, imperfect, explainable, and constrained by the same information a human player would have.

## Design Principles

- AI must never read hidden cards directly.
- AI decisions must be reproducible in tests.
- AI behavior should be tunable by difficulty level.
- AI should use the same legal action provider as the UI.
- AI should fail safely by choosing a legal fallback action if a strategy module cannot decide.

## Agent Interface

```csharp
public interface IPlayerAgent
{
    BidDecision DecideBid(GameObservation observation, IReadOnlyList<BidAction> legalBids);
    TrumpDecision DecideTrump(GameObservation observation, IReadOnlyList<Suit> legalTrumpSuits);
    CardPassDecision DecideCardsToPass(GameObservation observation, IReadOnlyList<CardPassAction> legalPasses);
    PlayDecision DecideCard(GameObservation observation, IReadOnlyList<PlayCardAction> legalPlays);
    TrupaDecision DecideTrupa(GameObservation observation, IReadOnlyList<TrupaAction> legalSignals);
    ResignationDecision DecideResignation(GameObservation observation, IReadOnlyList<ResignAction> legalResignations);
}
```

The exact method signatures can be simplified during implementation, but each decision type should remain conceptually separate.

## Difficulty Levels

### Beginner

- Uses simple heuristics.
- Avoids high-risk bids.
- Occasionally misses tactical opportunities.
- Suitable for first-time players.

### Standard

- Uses stronger bidding evaluation.
- Tracks played high cards.
- Cooperates as defender through trupa signal.
- Makes fewer wasteful point-card plays.

### Strong

- Uses Monte Carlo sampling for uncertain card distribution.
- Evaluates expected score across legal moves.
- Better at deciding when to preserve trump or force bidder.
- Better at bid success estimation.

## Heuristic MVP

The first AI implementation should be rule-based.

### Hand Evaluation

Evaluate:

- Raw card point value.
- Number of aces and tens.
- Trump potential by suit.
- Marriage availability.
- Suit length.
- Control cards by suit.
- Defensive strength if not bidder.

### Bidding

Beginner and Standard AI can estimate bid range from:

- Guaranteed card points.
- Marriage points.
- Expected talon improvement.
- Trump suit strength.
- Number of likely tricks.

The bot should avoid bidding above thresholds unless the visible marriage requirement is satisfied.

### Trump Selection

Choose trump by:

- Existing marriage worth 40.
- Suit length.
- High-card control in suit.
- Ability to capture point cards.

### Card Passing After Talon

When bidder passes cards to opponents:

- Avoid giving high point cards unless forced.
- Avoid strengthening defender marriages if visible.
- Prefer passing weak off-suit low cards.
- Consider void creation if rules make voids useful.

### Trick Play

General heuristics:

- Win cheap when the trick contains valuable points.
- Avoid wasting aces or tens into likely losing tricks.
- Preserve trump when future control matters.
- As defender, help partner win high-value tricks.
- As bidder, protect bid success over maximizing defender disruption.

### Trupa Signal

The trupa signal should be produced when:

- The signaling defender expects partner support to increase trick value.
- The signal is legal.
- The signaling defender has high confidence of winning the trick or enabling partner to win it.

The partner is not forced by engine logic unless final rules require it. The partner AI should treat the signal as strong evidence when choosing a legal card.

## Monte Carlo AI

After the heuristic MVP is stable, add Monte Carlo evaluation.

Process:

1. Build possible hidden-card worlds compatible with the observation.
2. For each legal move, simulate multiple continuations.
3. Use heuristic agents for rollout play.
4. Score each move by expected hand outcome.
5. Choose the move with best expected value, with controlled randomness.

Important constraints:

- Generated worlds must respect known cards, played cards, hand sizes, talon visibility, and public announcements.
- The AI must not choose a move that is only good because of one hidden-world assumption.
- Limit simulation time to keep UI responsive.

Suggested budget:

- Beginner: no simulations.
- Standard: no simulations or very small tactical sampling.
- Strong: 100 to 1,000 rollouts per decision depending on device performance.

## Self-Play Simulation

Create a console or test harness that can run:

```text
AI vs AI vs AI
Human seat replaced by AI
N hands or N full games
Deterministic seed range
Summary statistics
```

Track:

- Average score per seat.
- Bidder success rate.
- Average bid value.
- Pass rate.
- Resignation rate.
- Trupa signal frequency.
- Illegal action count.
- Hands ending due to engine errors.

Self-play is required for AI balancing and regression testing.

## Anti-Cheating Requirements

Tests should enforce:

- `IPlayerAgent` implementations only accept `GameObservation`.
- No AI code path can access full `GameState` except simulation internals that are explicitly constructed from sampled worlds.
- In sampled worlds, unknown cards are randomized from the AI perspective.
- Hidden talon cards are not visible before reveal.
- Opponent hands are never visible unless revealed by rules.

## Explainability

For debugging and future UI hints, AI decisions should optionally include a reason:

```csharp
public sealed record AgentDecision<TAction>(
    TAction Action,
    double Confidence,
    string ReasonCode
);
```

Examples:

- `BidStrongMarriage`
- `PassWeakOffSuit`
- `WinHighValueTrick`
- `SupportPartnerTrupa`
- `PreserveTrumpControl`

Reason codes should be stable enough for logs and tests, not necessarily user-facing.

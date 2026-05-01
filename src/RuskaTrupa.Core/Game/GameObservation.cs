using RuskaTrupa.Core.Cards;

namespace RuskaTrupa.Core.Game;

public sealed record GameObservation(
    PlayerId Self,
    IReadOnlyList<Card> Hand,
    PublicGameState PublicState);

public sealed record PublicGameState(
    GamePhase Phase,
    PlayerId Dealer,
    PlayerId CurrentPlayer,
    IReadOnlyList<Bid> Bids,
    PlayerId? Bidder,
    int? WinningBid,
    Suit? Trump,
    IReadOnlyList<CardPass> PassedCards,
    IReadOnlyList<PlayedCard> CurrentTrick,
    IReadOnlyList<CompletedTrick> CompletedTricks,
    IReadOnlyList<MarriageAnnouncement> MarriageAnnouncements,
    IReadOnlyDictionary<PlayerId, int> HandScores,
    int TargetScore);

using RuskaTrupa.Core.Cards;

namespace RuskaTrupa.Core.Game;

public sealed record GameState(
    GamePhase Phase,
    IReadOnlyList<PlayerSeat> Players,
    IReadOnlyDictionary<PlayerId, IReadOnlyList<Card>> Hands,
    IReadOnlyList<Card> Talon,
    PlayerId Dealer,
    PlayerId CurrentPlayer,
    IReadOnlyList<Bid> Bids,
    PlayerId? Bidder,
    int? WinningBid,
    int PassesSinceLastBid,
    Suit? Trump,
    IReadOnlyList<CardPass> PassedCards,
    IReadOnlyList<PlayedCard> CurrentTrick,
    IReadOnlyList<CompletedTrick> CompletedTricks,
    IReadOnlyList<MarriageAnnouncement> MarriageAnnouncements,
    IReadOnlyList<TrupaSignal> TrupaSignals,
    IReadOnlyDictionary<PlayerId, int> HandScores,
    IReadOnlyDictionary<PlayerId, int> GameScores,
    HandSettlement Settlement,
    RuleProfile Rules,
    int HandNumber,
    IReadOnlyList<GameAction> ActionLog,
    int InitialSeed,
    int Seed)
{
    public static GameState StartNewHand(int seed)
    {
        var players = new[]
        {
            new PlayerSeat(PlayerId.First, "You", PlayerKind.Human),
            new PlayerSeat(PlayerId.Second, "Bot 1", PlayerKind.Bot),
            new PlayerSeat(PlayerId.Third, "Bot 2", PlayerKind.Bot)
        };
        var dealer = PlayerId.Second;
        var gameScores = PlayerOrder.All.ToDictionary(player => player, _ => 0);

        return StartHand(seed, players, dealer, gameScores, RuleProfile.Default, 1);
    }

    public static GameState StartHand(
        int seed,
        IReadOnlyList<PlayerSeat> players,
        PlayerId dealer,
        IReadOnlyDictionary<PlayerId, int> gameScores,
        RuleProfile rules,
        int handNumber)
    {
        var deal = CardDealer.DealCards(Deck.Shuffle(seed), dealer);

        return new GameState(
            GamePhase.Bidding,
            players,
            deal.Hands,
            deal.Talon,
            dealer,
            PlayerOrder.Next(dealer),
            Array.Empty<Bid>(),
            null,
            null,
            0,
            null,
            Array.Empty<CardPass>(),
            Array.Empty<PlayedCard>(),
            Array.Empty<CompletedTrick>(),
            Array.Empty<MarriageAnnouncement>(),
            Array.Empty<TrupaSignal>(),
            PlayerOrder.All.ToDictionary(player => player, _ => 0),
            gameScores,
            HandSettlement.None,
            rules,
            handNumber,
            Array.Empty<GameAction>(),
            seed,
            seed);
    }

    public IReadOnlyList<Card> GetHand(PlayerId player) => Hands[player];

    public GameObservation CreateObservation(PlayerId player)
    {
        return new GameObservation(
            player,
            GetHand(player),
            new PublicGameState(
                Phase,
                Dealer,
                CurrentPlayer,
                Bids,
                Bidder,
                WinningBid,
                Trump,
                PassedCards,
                CurrentTrick,
                CompletedTricks,
                MarriageAnnouncements,
                HandScores,
                Rules.TargetScore));
    }
}

public sealed record CardPass(PlayerId From, PlayerId To, Card Card);

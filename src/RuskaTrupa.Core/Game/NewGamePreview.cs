using RuskaTrupa.Core.Cards;

namespace RuskaTrupa.Core.Game;

public sealed record NewGamePreview(
    IReadOnlyList<PlayerSeat> Players,
    Deal Deal,
    int Seed)
{
    public static NewGamePreview Create(int seed)
    {
        var players = new[]
        {
            new PlayerSeat(PlayerId.First, "You", PlayerKind.Human),
            new PlayerSeat(PlayerId.Second, "Bot 1", PlayerKind.Bot),
            new PlayerSeat(PlayerId.Third, "Bot 2", PlayerKind.Bot)
        };

        return new NewGamePreview(players, CardDealer.DealCards(Deck.Shuffle(seed), PlayerId.Third), seed);
    }

    public GameObservation CreateObservation(PlayerId player)
    {
        return new GameObservation(
            player,
            Deal.GetHand(player),
            new PublicGameState(
                GamePhase.Bidding,
                Deal.Dealer,
                PlayerId.First,
                Array.Empty<Bid>(),
                null,
                null,
                null,
                Array.Empty<CardPass>(),
                Array.Empty<PlayedCard>(),
                Array.Empty<CompletedTrick>(),
                Array.Empty<MarriageAnnouncement>(),
                PlayerOrder.All.ToDictionary(player => player, _ => 0),
                501));
    }

    public GameState ToGameState() => GameState.StartNewHand(Seed);
}

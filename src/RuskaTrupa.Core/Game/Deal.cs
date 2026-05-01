using RuskaTrupa.Core.Cards;

namespace RuskaTrupa.Core.Game;

public sealed record Deal(
    IReadOnlyDictionary<PlayerId, IReadOnlyList<Card>> Hands,
    IReadOnlyList<Card> Talon,
    PlayerId Dealer)
{
    public IReadOnlyList<Card> GetHand(PlayerId player) => Hands[player];
}

public static class CardDealer
{
    public static Deal DealCards(IReadOnlyList<Card> shuffledDeck, PlayerId dealer)
    {
        ArgumentNullException.ThrowIfNull(shuffledDeck);

        if (shuffledDeck.Count != 24)
        {
            throw new ArgumentException("Ruska Trupa requires a 24-card deck.", nameof(shuffledDeck));
        }

        if (shuffledDeck.Distinct().Count() != 24)
        {
            throw new ArgumentException("The deck must contain 24 unique cards.", nameof(shuffledDeck));
        }

        var players = PlayerOrder.All;
        var hands = players.ToDictionary(player => player, _ => new List<Card>(7));

        var index = 0;
        foreach (var batchSize in new[] { 3, 2, 2 })
        {
            foreach (var player in players)
            {
                for (var i = 0; i < batchSize; i++)
                {
                    hands[player].Add(shuffledDeck[index++]);
                }
            }
        }

        var talon = shuffledDeck.Skip(index).Take(3).ToArray();

        return new Deal(
            hands.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<Card>)pair.Value.ToArray()),
            talon,
            dealer);
    }
}

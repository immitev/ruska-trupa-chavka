namespace RuskaTrupa.Core.Cards;

public static class Deck
{
    public static IReadOnlyList<Card> Create()
    {
        var cards = new List<Card>(24);

        foreach (var suit in Enum.GetValues<Suit>())
        {
            foreach (var rank in Enum.GetValues<Rank>())
            {
                cards.Add(new Card(suit, rank));
            }
        }

        return cards;
    }

    public static IReadOnlyList<Card> Shuffle(int seed)
    {
        var cards = Create().ToArray();
        var random = new Random(seed);

        for (var i = cards.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }

        return cards;
    }
}

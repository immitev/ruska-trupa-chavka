using RuskaTrupa.Core.Cards;

namespace RuskaTrupa.Core.Tests;

public sealed class DeckTests
{
    [Fact]
    public void Create_ReturnsTwentyFourUniqueCards()
    {
        var deck = Deck.Create();

        Assert.Equal(24, deck.Count);
        Assert.Equal(24, deck.Distinct().Count());
        Assert.Equal(4, deck.Select(card => card.Suit).Distinct().Count());
        Assert.Equal(6, deck.Select(card => card.Rank).Distinct().Count());
    }

    [Fact]
    public void Shuffle_IsDeterministicForSeed()
    {
        var first = Deck.Shuffle(1234);
        var second = Deck.Shuffle(1234);

        Assert.Equal(first, second);
    }
}

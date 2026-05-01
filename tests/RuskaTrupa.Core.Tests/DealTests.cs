using RuskaTrupa.Core.Cards;
using RuskaTrupa.Core.Game;

namespace RuskaTrupa.Core.Tests;

public sealed class DealTests
{
    [Fact]
    public void DealCards_DealsSevenCardsToEachPlayerAndThreeToTalon()
    {
        var deal = CardDealer.DealCards(Deck.Shuffle(42), PlayerId.Third);

        Assert.Equal(7, deal.GetHand(PlayerId.First).Count);
        Assert.Equal(7, deal.GetHand(PlayerId.Second).Count);
        Assert.Equal(7, deal.GetHand(PlayerId.Third).Count);
        Assert.Equal(3, deal.Talon.Count);
    }

    [Fact]
    public void DealCards_UsesEveryCardExactlyOnce()
    {
        var deal = CardDealer.DealCards(Deck.Shuffle(42), PlayerId.Third);

        var allCards = deal.Hands.Values.SelectMany(hand => hand).Concat(deal.Talon).ToArray();

        Assert.Equal(24, allCards.Length);
        Assert.Equal(24, allCards.Distinct().Count());
    }
}

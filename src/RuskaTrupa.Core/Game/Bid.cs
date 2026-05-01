using RuskaTrupa.Core.Cards;

namespace RuskaTrupa.Core.Game;

public sealed record Bid(PlayerId Player, int? Amount, Suit? MarriageSuit = null)
{
    public bool IsPass => Amount is null;
}

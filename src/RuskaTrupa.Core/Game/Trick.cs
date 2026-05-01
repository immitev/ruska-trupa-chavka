using RuskaTrupa.Core.Cards;

namespace RuskaTrupa.Core.Game;

public sealed record PlayedCard(PlayerId Player, Card Card);

public sealed record CompletedTrick(
    IReadOnlyList<PlayedCard> Cards,
    PlayerId Winner)
{
    public int PointValue => Cards.Sum(played => played.Card.PointValue);
}

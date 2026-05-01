namespace RuskaTrupa.Core.Cards;

public enum Suit
{
    Clubs,
    Diamonds,
    Hearts,
    Spades
}

public enum Rank
{
    Nine,
    Jack,
    Queen,
    King,
    Ten,
    Ace
}

public readonly record struct Card(Suit Suit, Rank Rank)
{
    public int Strength => Rank switch
    {
        Rank.Ace => 6,
        Rank.Ten => 5,
        Rank.King => 4,
        Rank.Queen => 3,
        Rank.Jack => 2,
        Rank.Nine => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(Rank))
    };

    public int PointValue => Rank switch
    {
        Rank.Ace => 11,
        Rank.Ten => 10,
        Rank.King => 4,
        Rank.Queen => 3,
        Rank.Jack => 2,
        Rank.Nine => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(Rank))
    };

    public override string ToString() => $"{Rank} of {Suit}";
}

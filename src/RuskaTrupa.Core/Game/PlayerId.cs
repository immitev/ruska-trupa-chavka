namespace RuskaTrupa.Core.Game;

public readonly record struct PlayerId(int Value)
{
    public static PlayerId First { get; } = new(0);
    public static PlayerId Second { get; } = new(1);
    public static PlayerId Third { get; } = new(2);

    public override string ToString() => $"Player {Value + 1}";
}

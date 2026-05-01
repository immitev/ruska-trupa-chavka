namespace RuskaTrupa.Core.Game;

public static class PlayerOrder
{
    public static IReadOnlyList<PlayerId> All { get; } =
        new[] { PlayerId.First, PlayerId.Third, PlayerId.Second };

    public static PlayerId Next(PlayerId player)
    {
        var index = All.ToList().IndexOf(player);
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(player), player, "Unknown player.");
        }

        return All[(index + 1) % All.Count];
    }
}

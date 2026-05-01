namespace RuskaTrupa.Core.Game;

public sealed record PlayerSeat(PlayerId Id, string Name, PlayerKind Kind);

public enum PlayerKind
{
    Human,
    Bot
}

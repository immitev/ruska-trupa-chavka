using RuskaTrupa.Core.Cards;

namespace RuskaTrupa.Core.Game;

public sealed record MarriageAnnouncement(PlayerId Player, Suit Suit, int Points);

public sealed record TrupaSignal(PlayerId Player, int TrickNumber);

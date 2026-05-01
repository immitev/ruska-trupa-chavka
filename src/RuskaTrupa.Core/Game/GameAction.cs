using RuskaTrupa.Core.Cards;

namespace RuskaTrupa.Core.Game;

public abstract record GameAction(PlayerId Player);

public sealed record BidAction(PlayerId Player, int? Amount, Suit? MarriageSuit = null) : GameAction(Player);

public sealed record RevealTalonAction(PlayerId Player) : GameAction(Player);

public sealed record ChooseTrumpAction(PlayerId Player, Suit? Trump) : GameAction(Player);

public sealed record PassCardAction(PlayerId Player, PlayerId Recipient, Card Card) : GameAction(Player);

public sealed record PlayCardAction(PlayerId Player, Card Card) : GameAction(Player);

public sealed record AnnounceMarriageAction(PlayerId Player, Suit Suit) : GameAction(Player);

public sealed record SignalTrupaAction(PlayerId Player) : GameAction(Player);

public sealed record ResignHandAction(PlayerId Player) : GameAction(Player);

public sealed record SettleHandAction(PlayerId Player) : GameAction(Player);

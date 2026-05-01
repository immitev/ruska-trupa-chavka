using RuskaTrupa.Core.Cards;

namespace RuskaTrupa.Core.Game;

public sealed record SavedGame(int InitialSeed, IReadOnlyList<SavedGameAction> Actions)
{
    public static SavedGame FromState(GameState state)
    {
        return new SavedGame(state.InitialSeed, state.ActionLog.Select(SavedGameAction.FromAction).ToArray());
    }

    public GameState Restore()
    {
        var reducer = new GameReducer();
        var state = GameState.StartNewHand(InitialSeed);

        foreach (var action in Actions.Select(action => action.ToAction()))
        {
            state = reducer.Apply(state, action);
        }

        return state;
    }
}

public sealed record SavedGameAction(
    string Type,
    int Player,
    int? Amount = null,
    Suit? Suit = null,
    int? Recipient = null,
    Card? Card = null)
{
    public static SavedGameAction FromAction(GameAction action)
    {
        return action switch
        {
            BidAction bid => new(nameof(BidAction), bid.Player.Value, Amount: bid.Amount, Suit: bid.MarriageSuit),
            RevealTalonAction reveal => new(nameof(RevealTalonAction), reveal.Player.Value),
            ChooseTrumpAction trump => new(nameof(ChooseTrumpAction), trump.Player.Value, Suit: trump.Trump),
            PassCardAction pass => new(nameof(PassCardAction), pass.Player.Value, Recipient: pass.Recipient.Value, Card: pass.Card),
            PlayCardAction play => new(nameof(PlayCardAction), play.Player.Value, Card: play.Card),
            AnnounceMarriageAction marriage => new(nameof(AnnounceMarriageAction), marriage.Player.Value, Suit: marriage.Suit),
            SignalTrupaAction trupa => new(nameof(SignalTrupaAction), trupa.Player.Value),
            ResignHandAction resign => new(nameof(ResignHandAction), resign.Player.Value),
            SettleHandAction settle => new(nameof(SettleHandAction), settle.Player.Value),
            _ => throw new NotSupportedException($"Unsupported action type {action.GetType().Name}.")
        };
    }

    public GameAction ToAction()
    {
        var player = new PlayerId(Player);
        return Type switch
        {
            nameof(BidAction) => new BidAction(player, Amount, Suit),
            nameof(RevealTalonAction) => new RevealTalonAction(player),
            nameof(ChooseTrumpAction) => new ChooseTrumpAction(player, Suit),
            nameof(PassCardAction) => new PassCardAction(player, new PlayerId(Recipient ?? throw Missing(nameof(Recipient))), Card ?? throw Missing(nameof(Card))),
            nameof(PlayCardAction) => new PlayCardAction(player, Card ?? throw Missing(nameof(Card))),
            nameof(AnnounceMarriageAction) => new AnnounceMarriageAction(player, Suit ?? throw Missing(nameof(Suit))),
            nameof(SignalTrupaAction) => new SignalTrupaAction(player),
            nameof(ResignHandAction) => new ResignHandAction(player),
            nameof(SettleHandAction) => new SettleHandAction(player),
            _ => throw new NotSupportedException($"Unsupported action type {Type}.")
        };
    }

    private static InvalidOperationException Missing(string property)
    {
        return new InvalidOperationException($"Saved action is missing {property}.");
    }
}

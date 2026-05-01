namespace RuskaTrupa.Core.Game;

public sealed record RuleProfile(
    int TargetScore,
    int MinimumBid,
    int MaximumBid,
    int BidStep,
    bool MustFollowSuit,
    bool MustTrumpWhenVoid,
    bool MustOvertrump,
    bool AllPassRedealsWithSameDealer,
    int ResignationDefenderBonus)
{
    public static RuleProfile Default { get; } = new(
        TargetScore: 501,
        MinimumBid: 100,
        MaximumBid: 200,
        BidStep: 1,
        MustFollowSuit: true,
        MustTrumpWhenVoid: true,
        MustOvertrump: true,
        AllPassRedealsWithSameDealer: true,
        ResignationDefenderBonus: 25);
}

public enum SettlementKind
{
    None,
    BidderSucceeded,
    BidderFailed,
    BidderResigned
}

public sealed record HandSettlement(
    SettlementKind Kind,
    PlayerId? Bidder,
    int? Bid,
    IReadOnlyDictionary<PlayerId, int> ScoreDeltas)
{
    public static HandSettlement None { get; } = new(
        SettlementKind.None,
        null,
        null,
        PlayerOrder.All.ToDictionary(player => player, _ => 0));
}

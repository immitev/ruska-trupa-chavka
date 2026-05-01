using RuskaTrupa.Core.Cards;

namespace RuskaTrupa.Core.Game;

public interface IGameReducer
{
    GameState Apply(GameState state, GameAction action);
}

public sealed class GameReducer : IGameReducer
{
    private readonly ILegalActionProvider legalActions;

    public GameReducer()
        : this(new LegalActionProvider())
    {
    }

    public GameReducer(ILegalActionProvider legalActions)
    {
        this.legalActions = legalActions;
    }

    public GameState Apply(GameState state, GameAction action)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(action);

        var legal = legalActions.GetLegalActions(state, action.Player);
        if (!legal.Contains(action))
        {
            throw new InvalidOperationException($"Illegal action {action} in phase {state.Phase}.");
        }

        var next = action switch
        {
            BidAction bid => ApplyBid(state, bid),
            RevealTalonAction reveal => ApplyRevealTalon(state, reveal),
            ChooseTrumpAction chooseTrump => state with
            {
                Phase = GamePhase.PassCards,
                Trump = chooseTrump.Trump,
                CurrentPlayer = chooseTrump.Player
            },
            PassCardAction passCard => ApplyPassCard(state, passCard),
            PlayCardAction playCard => ApplyPlayCard(state, playCard),
            AnnounceMarriageAction marriage => ApplyAnnounceMarriage(state, marriage),
            SignalTrupaAction trupa => ApplySignalTrupa(state, trupa),
            ResignHandAction resign => ApplyResignHand(state, resign),
            SettleHandAction => ApplySettleHand(state),
            _ => throw new NotSupportedException($"Unsupported action type {action.GetType().Name}.")
        };

        return next with { ActionLog = state.ActionLog.Append(action).ToArray() };
    }

    private static GameState ApplyBid(GameState state, BidAction action)
    {
        var bids = state.Bids.Append(new Bid(action.Player, action.Amount, action.MarriageSuit)).ToArray();

        if (action.Amount is null)
        {
            var passesSinceLastBid = state.PassesSinceLastBid + 1;
            if (state.Bidder is null && passesSinceLastBid >= PlayerOrder.All.Count)
            {
                var redeal = GameState.StartHand(
                    state.Seed + 1,
                    state.Players,
                    state.Rules.AllPassRedealsWithSameDealer ? state.Dealer : PlayerOrder.Next(state.Dealer),
                    state.GameScores,
                    state.Rules,
                    state.HandNumber + 1);
                return redeal with { ActionLog = state.ActionLog, InitialSeed = state.InitialSeed };
            }

            if (state.Bidder is not null && NoEligibleChallengersRemain(bids, state.Bidder.Value))
            {
                return state with
                {
                    Phase = GamePhase.RevealTalon,
                    Bids = bids,
                    PassesSinceLastBid = passesSinceLastBid,
                    CurrentPlayer = state.Bidder.Value
                };
            }

            return state with
            {
                Bids = bids,
                PassesSinceLastBid = passesSinceLastBid,
                CurrentPlayer = NextEligibleBidder(bids, action.Player)
            };
        }

        if (NoEligibleChallengersRemain(bids, action.Player))
        {
            return state with
            {
                Phase = GamePhase.RevealTalon,
                Bids = bids,
                Bidder = action.Player,
                WinningBid = action.Amount.Value,
                PassesSinceLastBid = 0,
                CurrentPlayer = action.Player
            };
        }

        return state with
        {
            Bids = bids,
            Bidder = action.Player,
            WinningBid = action.Amount.Value,
            PassesSinceLastBid = 0,
            CurrentPlayer = NextEligibleBidder(bids, action.Player)
        };
    }

    private static bool NoEligibleChallengersRemain(IReadOnlyList<Bid> bids, PlayerId bidder)
    {
        return PlayerOrder.All
            .Where(player => player != bidder)
            .All(player => bids.Any(bid => bid.Player == player && bid.Amount is null));
    }

    private static PlayerId NextEligibleBidder(IReadOnlyList<Bid> bids, PlayerId player)
    {
        var next = PlayerOrder.Next(player);
        while (bids.Any(bid => bid.Player == next && bid.Amount is null))
        {
            next = PlayerOrder.Next(next);
        }

        return next;
    }

    private static GameState ApplyAnnounceMarriage(GameState state, AnnounceMarriageAction action)
    {
        var points = state.Trump == action.Suit ? 40 : 20;
        var handScores = state.HandScores.ToDictionary(pair => pair.Key, pair => pair.Value);
        handScores[action.Player] += points;

        return state with
        {
            HandScores = handScores,
            MarriageAnnouncements = state.MarriageAnnouncements
                .Append(new MarriageAnnouncement(action.Player, action.Suit, points))
                .ToArray()
        };
    }

    private static GameState ApplySignalTrupa(GameState state, SignalTrupaAction action)
    {
        return state with
        {
            TrupaSignals = state.TrupaSignals
                .Append(new TrupaSignal(action.Player, state.CompletedTricks.Count + 1))
                .ToArray()
        };
    }

    private static GameState ApplyResignHand(GameState state, ResignHandAction action)
    {
        var handScores = PlayerOrder.All.ToDictionary(player => player, _ => 0);
        var deltas = PlayerOrder.All.ToDictionary(player => player, _ => 0);
        deltas[action.Player] = -(state.WinningBid ?? 0);
        foreach (var defender in PlayerOrder.All.Where(player => player != action.Player))
        {
            handScores[defender] = state.Rules.ResignationDefenderBonus;
            deltas[defender] = state.Rules.ResignationDefenderBonus;
        }

        return state with
        {
            Phase = GamePhase.ScoringHand,
            CurrentTrick = Array.Empty<PlayedCard>(),
            HandScores = handScores,
            Settlement = new HandSettlement(
                SettlementKind.BidderResigned,
                action.Player,
                state.WinningBid,
                deltas)
        };
    }

    private static GameState ApplyRevealTalon(GameState state, RevealTalonAction action)
    {
        var hands = CloneHands(state);
        hands[action.Player].AddRange(state.Talon);

        return state with
        {
            Phase = GamePhase.ChooseTrump,
            Hands = FreezeHands(hands),
            Talon = Array.Empty<Card>(),
            CurrentPlayer = action.Player
        };
    }

    private static GameState ApplyPassCard(GameState state, PassCardAction action)
    {
        var hands = CloneHands(state);
        if (!hands[action.Player].Remove(action.Card))
        {
            throw new InvalidOperationException("The passed card is not in the player's hand.");
        }

        hands[action.Recipient].Add(action.Card);

        var passedCards = state.PassedCards.Append(new CardPass(action.Player, action.Recipient, action.Card)).ToArray();
        var phase = passedCards.Count(pass => pass.From == action.Player) == 2
            ? GamePhase.PlayingTricks
            : GamePhase.PassCards;

        return state with
        {
            Phase = phase,
            Hands = FreezeHands(hands),
            PassedCards = passedCards,
            CurrentPlayer = action.Player
        };
    }

    private static GameState ApplyPlayCard(GameState state, PlayCardAction action)
    {
        var hands = CloneHands(state);
        if (!hands[action.Player].Remove(action.Card))
        {
            throw new InvalidOperationException("The played card is not in the player's hand.");
        }

        var currentTrick = state.CurrentTrick.Append(new PlayedCard(action.Player, action.Card)).ToArray();
        if (currentTrick.Length < 3)
        {
            return state with
            {
                Hands = FreezeHands(hands),
                CurrentTrick = currentTrick,
                CurrentPlayer = PlayerOrder.Next(action.Player)
            };
        }

        var winner = DetermineTrickWinner(currentTrick, state.Trump);
        var completedTrick = new CompletedTrick(currentTrick, winner);
        var completedTricks = state.CompletedTricks.Append(completedTrick).ToArray();
        var handScores = state.HandScores.ToDictionary(pair => pair.Key, pair => pair.Value);
        handScores[winner] += completedTrick.PointValue;

        var phase = hands.Values.All(hand => hand.Count == 0)
            ? GamePhase.ScoringHand
            : GamePhase.PlayingTricks;

        return state with
        {
            Phase = phase,
            Hands = FreezeHands(hands),
            CurrentTrick = Array.Empty<PlayedCard>(),
            CompletedTricks = completedTricks,
            HandScores = handScores,
            CurrentPlayer = winner
        };
    }

    private static GameState ApplySettleHand(GameState state)
    {
        var gameScores = state.GameScores.ToDictionary(pair => pair.Key, pair => pair.Value);
        var bidder = state.Bidder;
        var winningBid = state.WinningBid;
        var deltas = PlayerOrder.All.ToDictionary(player => player, _ => 0);
        var kind = SettlementKind.BidderSucceeded;

        if (state.Settlement.Kind == SettlementKind.BidderResigned && bidder is not null && winningBid is not null)
        {
            kind = SettlementKind.BidderResigned;
            deltas[bidder.Value] = -winningBid.Value;
            gameScores[bidder.Value] += deltas[bidder.Value];
            foreach (var defender in PlayerOrder.All.Where(player => player != bidder.Value))
            {
                deltas[defender] = state.Rules.ResignationDefenderBonus;
                gameScores[defender] += deltas[defender];
            }
        }
        else if (bidder is not null && winningBid is not null && state.HandScores[bidder.Value] < winningBid.Value)
        {
            kind = SettlementKind.BidderFailed;
            deltas[bidder.Value] = -winningBid.Value;
            gameScores[bidder.Value] += deltas[bidder.Value];
            foreach (var defender in PlayerOrder.All.Where(player => player != bidder.Value))
            {
                deltas[defender] = state.HandScores[defender];
                gameScores[defender] += deltas[defender];
            }
        }
        else
        {
            foreach (var player in PlayerOrder.All)
            {
                deltas[player] = state.HandScores[player];
                gameScores[player] += deltas[player];
            }
        }

        var settlement = new HandSettlement(kind, bidder, winningBid, deltas);

        if (gameScores.Values.Any(score => score >= state.Rules.TargetScore))
        {
            return state with
            {
                Phase = GamePhase.GameOver,
                GameScores = gameScores,
                Settlement = settlement
            };
        }

        var nextHand = GameState.StartHand(
            state.Seed + 1,
            state.Players,
            PlayerOrder.Next(state.Dealer),
            gameScores,
            state.Rules,
            state.HandNumber + 1);
        return nextHand with { ActionLog = state.ActionLog, InitialSeed = state.InitialSeed };
    }

    private static PlayerId DetermineTrickWinner(IReadOnlyList<PlayedCard> playedCards, Suit? trump)
    {
        var leadSuit = playedCards[0].Card.Suit;
        var winningSuit = trump is not null && playedCards.Any(played => played.Card.Suit == trump)
            ? trump.Value
            : leadSuit;

        return playedCards
            .Where(played => played.Card.Suit == winningSuit)
            .OrderByDescending(played => played.Card.Strength)
            .First()
            .Player;
    }

    private static Dictionary<PlayerId, List<Card>> CloneHands(GameState state)
    {
        return state.Hands.ToDictionary(pair => pair.Key, pair => pair.Value.ToList());
    }

    private static IReadOnlyDictionary<PlayerId, IReadOnlyList<Card>> FreezeHands(
        Dictionary<PlayerId, List<Card>> hands)
    {
        return hands.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<Card>)pair.Value.ToArray());
    }
}

using RuskaTrupa.Core.Cards;

namespace RuskaTrupa.Core.Game;

public interface ILegalActionProvider
{
    IReadOnlyList<GameAction> GetLegalActions(GameState state, PlayerId player);
}

public sealed class LegalActionProvider : ILegalActionProvider
{
    private const int MarriageBidThreshold = 120;

    public IReadOnlyList<GameAction> GetLegalActions(GameState state, PlayerId player)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Phase is GamePhase.ScoringHand)
        {
            return new GameAction[] { new SettleHandAction(player) };
        }

        if (state.CurrentPlayer != player)
        {
            return Array.Empty<GameAction>();
        }

        return state.Phase switch
        {
            GamePhase.Bidding => GetBiddingActions(state, player),
            GamePhase.RevealTalon when state.Bidder == player => new GameAction[] { new RevealTalonAction(player) },
            GamePhase.ChooseTrump when state.Bidder == player => Enum.GetValues<Suit>()
                .Cast<Suit?>()
                .Append(null)
                .Select(trump => (GameAction)new ChooseTrumpAction(player, trump))
                .Append(new ResignHandAction(player))
                .ToArray(),
            GamePhase.PassCards when state.Bidder == player => GetPassCardActions(state, player),
            GamePhase.PlayingTricks => GetPlayingActions(state, player),
            _ => Array.Empty<GameAction>()
        };
    }

    private static IReadOnlyList<GameAction> GetBiddingActions(GameState state, PlayerId player)
    {
        if (state.Bidder is null && state.PassesSinceLastBid >= PlayerOrder.All.Count - 1)
        {
            return new GameAction[] { new BidAction(player, state.Rules.MinimumBid) };
        }

        var actions = new List<GameAction> { new BidAction(player, null) };
        if (HasPassed(state.Bids, player))
        {
            return actions;
        }

        var floor = Math.Max(
            state.Rules.MinimumBid,
            (state.WinningBid ?? state.Rules.MinimumBid - state.Rules.BidStep) + state.Rules.BidStep);

        var marriageSuits = GetMarriageSuits(state, player).ToArray();
        for (var bid = floor; bid <= state.Rules.MaximumBid; bid += state.Rules.BidStep)
        {
            if (bid <= MarriageBidThreshold)
            {
                actions.Add(new BidAction(player, bid));
                continue;
            }

            actions.AddRange(marriageSuits.Select(suit => new BidAction(player, bid, suit)));
        }

        return actions;
    }

    private static bool HasPassed(IEnumerable<Bid> bids, PlayerId player)
    {
        return bids.Any(bid => bid.Player == player && bid.Amount is null);
    }

    private static IReadOnlyList<GameAction> GetPassCardActions(GameState state, PlayerId player)
    {
        var recipients = PlayerOrder.All
            .Where(candidate => candidate != player)
            .Where(candidate => state.PassedCards.All(pass => pass.To != candidate))
            .ToArray();

        return recipients
            .SelectMany(recipient => state.GetHand(player)
                .Select(card => (GameAction)new PassCardAction(player, recipient, card)))
            .ToArray();
    }

    private static IReadOnlyList<GameAction> GetPlayingActions(GameState state, PlayerId player)
    {
        var actions = GetPlayCardActions(state, player).ToList();
        actions.AddRange(GetMarriageActions(state, player));

        if (CanSignalTrupa(state, player))
        {
            actions.Add(new SignalTrupaAction(player));
        }

        if (CanResign(state, player))
        {
            actions.Add(new ResignHandAction(player));
        }

        return actions;
    }

    private static IReadOnlyList<GameAction> GetPlayCardActions(GameState state, PlayerId player)
    {
        var hand = state.GetHand(player);
        if (state.CurrentTrick.Count == 0)
        {
            return hand.Select(card => (GameAction)new PlayCardAction(player, card)).ToArray();
        }

        var leadSuit = state.CurrentTrick[0].Card.Suit;
        var legalCards = state.Rules.MustFollowSuit
            ? FollowSuitCards(state, hand, leadSuit)
            : hand;

        return legalCards.Select(card => (GameAction)new PlayCardAction(player, card)).ToArray();
    }

    private static IReadOnlyList<Card> FollowSuitCards(GameState state, IReadOnlyList<Card> hand, Suit leadSuit)
    {
        var followSuitCards = hand.Where(card => card.Suit == leadSuit).ToArray();
        if (followSuitCards.Length > 0)
        {
            if (state.Rules.MustOvertrump && state.Trump == leadSuit)
            {
                var highestPlayedTrump = HighestPlayedTrumpStrength(state);
                var overtrumps = followSuitCards.Where(card => card.Strength > highestPlayedTrump).ToArray();
                if (overtrumps.Length > 0)
                {
                    return overtrumps;
                }
            }

            return followSuitCards;
        }

        if (state.Rules.MustTrumpWhenVoid && state.Trump is not null)
        {
            var trumpCards = hand.Where(card => card.Suit == state.Trump.Value).ToArray();
            if (trumpCards.Length > 0)
            {
                if (state.Rules.MustOvertrump)
                {
                    var highestPlayedTrump = HighestPlayedTrumpStrength(state);
                    var overtrumps = trumpCards.Where(card => card.Strength > highestPlayedTrump).ToArray();
                    if (overtrumps.Length > 0)
                    {
                        return overtrumps;
                    }
                }

                return trumpCards;
            }
        }

        return hand;
    }

    private static int HighestPlayedTrumpStrength(GameState state)
    {
        return state.Trump is null
            ? 0
            : state.CurrentTrick
                .Where(played => played.Card.Suit == state.Trump.Value)
                .Select(played => played.Card.Strength)
                .DefaultIfEmpty(0)
                .Max();
    }

    private static IEnumerable<GameAction> GetMarriageActions(GameState state, PlayerId player)
    {
        var currentLeadSuit = state.CurrentTrick.Count == 0
            ? (Suit?)null
            : state.CurrentTrick[0].Card.Suit;
        var playableMarriageSuits = GetPlayCardActions(state, player)
            .OfType<PlayCardAction>()
            .Where(action => action.Card.Rank is Rank.King or Rank.Queen)
            .Select(action => action.Card.Suit)
            .ToHashSet();

        foreach (var suit in GetMarriageSuits(state, player))
        {
            if (currentLeadSuit is not null && suit != currentLeadSuit)
            {
                continue;
            }

            if (!playableMarriageSuits.Contains(suit))
            {
                continue;
            }

            if (state.MarriageAnnouncements.Any(announcement => announcement.Player == player && announcement.Suit == suit))
            {
                continue;
            }

            yield return new AnnounceMarriageAction(player, suit);
        }
    }

    private static IEnumerable<Suit> GetMarriageSuits(GameState state, PlayerId player)
    {
        var hand = state.GetHand(player);
        foreach (var suit in Enum.GetValues<Suit>())
        {
            if (hand.Contains(new Card(suit, Rank.King)) && hand.Contains(new Card(suit, Rank.Queen)))
            {
                yield return suit;
            }
        }
    }

    private static bool CanSignalTrupa(GameState state, PlayerId player)
    {
        if (state.Bidder is null
            || state.Bidder.Value == player
            || state.GetHand(player).Count <= 1
            || state.CurrentTrick.Count == 0
            || state.CurrentTrick.Any(played => played.Player == player)
            || state.TrupaSignals.Any(signal => signal.Player == player && signal.TrickNumber == state.CompletedTricks.Count + 1))
        {
            return false;
        }

        var partner = PlayerOrder.All.Single(candidate => candidate != player && candidate != state.Bidder.Value);
        if (state.CurrentTrick.Any(played => played.Player == partner))
        {
            return false;
        }

        return GetPlayCardActions(state, player)
            .OfType<PlayCardAction>()
            .Any(action => WouldWinCurrentTrick(state, action));
    }

    private static bool WouldWinCurrentTrick(GameState state, PlayCardAction action)
    {
        var candidateCards = state.CurrentTrick
            .Append(new PlayedCard(action.Player, action.Card))
            .ToArray();
        var winner = DeterminePartialTrickWinner(candidateCards, state.Trump);
        return winner == action.Player;
    }

    private static PlayerId DeterminePartialTrickWinner(IReadOnlyList<PlayedCard> playedCards, Suit? trump)
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

    private static bool CanResign(GameState state, PlayerId player)
    {
        return state.Bidder == player && state.CompletedTricks.Count < 2;
    }
}

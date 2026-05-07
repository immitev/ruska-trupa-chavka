using RuskaTrupa.Core.Cards;
using RuskaTrupa.Core.Game;

namespace RuskaTrupa.AI;

public sealed class HeuristicPlayerAgent : IPlayerAgent
{
    private const int MarriageBidThreshold = 120;
    private static readonly IReadOnlyList<Card> FullDeck = Enum.GetValues<Suit>()
        .SelectMany(suit => Enum.GetValues<Rank>().Select(rank => new Card(suit, rank)))
        .ToArray();

    public HeuristicPlayerAgent()
        : this(BotSkillLevel.Advanced, BotPlayStyle.Balanced)
    {
    }

    public HeuristicPlayerAgent(BotSkillLevel skillLevel)
        : this(skillLevel, BotPlayStyle.Balanced)
    {
    }

    public HeuristicPlayerAgent(BotSkillLevel skillLevel, BotPlayStyle playStyle)
    {
        SkillLevel = skillLevel;
        PlayStyle = playStyle;
    }

    public BotSkillLevel SkillLevel { get; }

    public BotPlayStyle PlayStyle { get; }

    public AgentDecision<int> DecideOpeningBid(GameObservation observation, IReadOnlyList<int> legalBids)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(legalBids);

        if (legalBids.Count == 0)
        {
            throw new ArgumentException("At least one legal bid is required.", nameof(legalBids));
        }

        var nonPassBids = legalBids.Where(bid => bid > 0).Order().ToArray();
        if (nonPassBids.Length == 0)
        {
            return new AgentDecision<int>(0, 0.95, "NoBidAvailable");
        }

        if (!legalBids.Contains(0))
        {
            return new AgentDecision<int>(nonPassBids[0], 1.0, "ForcedMinimumBid");
        }

        if (SkillLevel == BotSkillLevel.Beginner)
        {
            return DecideBeginnerOpeningBid(observation, nonPassBids);
        }

        var minimumBid = nonPassBids[0];
        var profile = EvaluateHand(observation.Hand);

        if (minimumBid > MarriageBidThreshold && profile.MarriageCount == 0)
        {
            return new AgentDecision<int>(0, 0.82, "NoMarriageForHighBid");
        }

        var requiredMargin = minimumBid switch
        {
            <= 100 => -4,
            <= MarriageBidThreshold => 16,
            _ => 26
        };
        requiredMargin += PlayStyle switch
        {
            BotPlayStyle.Bold => minimumBid <= 100 ? -6 : -8,
            BotPlayStyle.Patient => minimumBid <= 100 ? 8 : 10,
            _ => 0
        };

        if (SkillLevel == BotSkillLevel.Intermediate)
        {
            requiredMargin += minimumBid <= 100 ? 4 : 8;
        }

        var hasMinimumBidControls = minimumBid <= 100
            && profile.ControlCount >= 2
            && profile.ContractEstimate >= minimumBid - 8;
        if (profile.ContractEstimate < minimumBid + requiredMargin && !hasMinimumBidControls)
        {
            return new AgentDecision<int>(0, 0.78, "InsufficientContractMargin");
        }

        var maxRaise = SkillLevel == BotSkillLevel.Intermediate
            ? 1
            : minimumBid <= 110 ? 3 : 1;
        maxRaise = PlayStyle switch
        {
            BotPlayStyle.Bold when SkillLevel == BotSkillLevel.Advanced => minimumBid <= 110 ? 7 : 3,
            BotPlayStyle.Patient => 1,
            _ => maxRaise
        };
        var ceiling = Math.Min(profile.ContractEstimate - requiredMargin, minimumBid + maxRaise);
        var selected = nonPassBids.Where(bid => bid <= ceiling).DefaultIfEmpty(minimumBid).Max();

        return new AgentDecision<int>(
            selected,
            selected == 0 ? 0.78 : 0.66,
            selected == 0 ? "HandBelowBidFloor" : "ContractEstimate");
    }

    public AgentDecision<Suit?> DecideTrump(GameObservation observation, IReadOnlyList<Suit?> legalTrumpSuits)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(legalTrumpSuits);

        if (legalTrumpSuits.Count == 0)
        {
            throw new ArgumentException("At least one legal trump suit is required.", nameof(legalTrumpSuits));
        }

        if (SkillLevel == BotSkillLevel.Beginner)
        {
            var beginnerTrump = legalTrumpSuits
                .OrderByDescending(suit => suit is null ? 0 : observation.Hand.Count(card => card.Suit == suit.Value))
                .ThenByDescending(suit => suit is null ? observation.Hand.Count(card => card.Rank == Rank.Ace) : 0)
                .First();
            return new AgentDecision<Suit?>(beginnerTrump, 0.42, "BeginnerSuitLength");
        }

        var selected = legalTrumpSuits
            .OrderByDescending(suit => TrumpChoiceScore(observation.Hand, suit))
            .ThenByDescending(suit => suit is null ? 0 : observation.Hand.Count(card => card.Suit == suit.Value))
            .First();

        return new AgentDecision<Suit?>(
            selected,
            0.68,
            selected is null ? "HighCardsAcrossSuits" : "BestTrumpFit");
    }

    public AgentDecision<Card> DecideCardToPass(GameObservation observation, IReadOnlyList<Card> legalCards)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(legalCards);

        if (legalCards.Count == 0)
        {
            throw new ArgumentException("At least one legal card is required.", nameof(legalCards));
        }

        if (SkillLevel == BotSkillLevel.Beginner)
        {
            var beginnerPass = legalCards
                .OrderBy(card => card.PointValue)
                .ThenBy(card => card.Strength)
                .First();
            return new AgentDecision<Card>(beginnerPass, 0.45, "BeginnerLowestCard");
        }

        var marriageCards = MarriageCards(observation.Hand).ToHashSet();
        var selected = legalCards
            .OrderBy(card => PassCardKeepPenalty(observation, card, marriageCards))
            .ThenBy(card => card.PointValue)
            .ThenBy(card => card.Strength)
            .First();

        return new AgentDecision<Card>(selected, 0.72, "DiscardWeakestLiability");
    }

    public AgentDecision<Card> DecideCard(GameObservation observation, IReadOnlyList<Card> legalCards)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(legalCards);

        if (legalCards.Count == 0)
        {
            throw new ArgumentException("At least one legal card is required.", nameof(legalCards));
        }

        if (legalCards.Count == 1)
        {
            return new AgentDecision<Card>(legalCards[0], 1.0, "ForcedCard");
        }

        var selected = SkillLevel == BotSkillLevel.Beginner
            ? legalCards
                .OrderBy(card => card.PointValue)
                .ThenBy(card => card.Strength)
                .First()
            : observation.PublicState.CurrentTrick.Count == 0
                ? ChooseLeadCard(observation, legalCards)
                : ChooseFollowCard(observation, legalCards);

        var protectedMarriage = AvoidBreakingUnannouncedMarriage(observation, legalCards, selected);
        if (protectedMarriage != selected)
        {
            return new AgentDecision<Card>(protectedMarriage, 0.78, "PreserveUnannouncedMarriage");
        }

        return new AgentDecision<Card>(selected, SkillLevel == BotSkillLevel.Beginner ? 0.4 : 0.7, SkillLevel == BotSkillLevel.Beginner ? "BeginnerConservePoints" : "TrickAwareCard");
    }

    private static AgentDecision<int> DecideBeginnerOpeningBid(GameObservation observation, IReadOnlyList<int> nonPassBids)
    {
        var minimumBid = nonPassBids[0];
        var rawPoints = observation.Hand.Sum(card => card.PointValue);
        var aces = observation.Hand.Count(card => card.Rank == Rank.Ace);
        var marriages = CountMarriages(observation.Hand);
        var beginnerEstimate = rawPoints + aces * 10 + marriages * 18;

        if (minimumBid > 100 || beginnerEstimate < 88)
        {
            return new AgentDecision<int>(0, 0.72, "BeginnerPass");
        }

        return new AgentDecision<int>(minimumBid, 0.48, "BeginnerMinimumBid");
    }

    private static Card ChooseLeadCard(GameObservation observation, IReadOnlyList<Card> legalCards)
    {
        var state = observation.PublicState;
        var bidder = state.Bidder;
        var selfIsBidder = bidder == observation.Self;
        var announcedMarriageSuits = state.MarriageAnnouncements
            .Where(announcement => announcement.Player == observation.Self)
            .Select(announcement => announcement.Suit)
            .ToHashSet();

        var unannouncedMarriageLead = legalCards
            .Where(card => card.Rank is Rank.King or Rank.Queen
                && !announcedMarriageSuits.Contains(card.Suit)
                && HasMarriage(observation.Hand, card.Suit))
            .OrderByDescending(card => state.Trump == card.Suit ? 40 : 20)
            .ThenBy(card => card.Rank == Rank.Queen ? 0 : 1)
            .Select(card => (Card?)card)
            .FirstOrDefault();
        if (unannouncedMarriageLead is { } marriageLead)
        {
            return marriageLead;
        }

        if (selfIsBidder)
        {
            var masterWinner = legalCards
                .Where(card => IsLeadControlCard(observation, card))
                .OrderByDescending(card => card.PointValue)
                .ThenByDescending(card => card.Strength)
                .Select(card => (Card?)card)
                .FirstOrDefault();
            if (masterWinner is { } winner)
            {
                return winner;
            }

            return legalCards
                .OrderByDescending(card => LeadWinnerLikelihood(observation, card))
                .ThenByDescending(card => card.PointValue)
                .ThenByDescending(card => card.Strength)
                .First();
        }

        var bidderVoidPressure = legalCards
            .Where(card => state.Bidder is { } bid && IsPlayerKnownVoidInSuit(state, bid, card.Suit))
            .OrderBy(card => card.PointValue)
            .ThenBy(card => card.Strength)
            .Select(card => (Card?)card)
            .FirstOrDefault();
        if (bidderVoidPressure is { } pressureCard)
        {
            return pressureCard;
        }

        return legalCards
            .OrderBy(card => DefensiveLeadRisk(observation, card))
            .ThenBy(card => card.PointValue)
            .ThenBy(card => card.Strength)
            .First();
    }

    private static Card ChooseFollowCard(GameObservation observation, IReadOnlyList<Card> legalCards)
    {
        var state = observation.PublicState;
        var currentWinner = DetermineTrickWinner(state.CurrentTrick, state.Trump);
        var partner = DefensivePartner(observation.Self, state.Bidder);
        var partnerWinning = partner is not null && currentWinner == partner;
        var bidderWinning = state.Bidder is not null && currentWinner == state.Bidder;
        var trickPoints = state.CurrentTrick.Sum(played => played.Card.PointValue);
        var bidderStillToPlay = state.Bidder is { } bidder && !HasPlayerPlayed(state, bidder);

        var winningCards = legalCards
            .Where(card => WouldWin(observation.Self, card, state.CurrentTrick, state.Trump))
            .ToArray();

        if (partnerWinning)
        {
            if (bidderStillToPlay && BidderMayStillOvertakeCurrentWinner(observation))
            {
                return legalCards
                    .OrderBy(card => ThrowawayCost(card, state.Trump))
                    .ThenBy(card => card.Strength)
                    .First();
            }

            return legalCards
                .OrderByDescending(card => SafePointContribution(card))
                .ThenBy(card => card.Strength)
                .First();
        }

        if (winningCards.Length > 0
            && IsLastToPlayInTrick(state)
            && (state.Bidder == observation.Self || bidderWinning || trickPoints >= 5))
        {
            return winningCards
                .OrderByDescending(card => card.PointValue)
                .ThenBy(card => ThrowawayCost(card, state.Trump))
                .ThenBy(card => card.Strength)
                .First();
        }

        if (winningCards.Length > 0 && (bidderWinning || trickPoints >= 10 || state.Bidder == observation.Self))
        {
            return winningCards
                .OrderBy(card => card.PointValue)
                .ThenBy(card => card.Strength)
                .First();
        }

        return legalCards
            .OrderBy(card => ThrowawayCost(card, state.Trump))
            .ThenBy(card => card.Strength)
            .First();
    }

    private static bool IsLastToPlayInTrick(PublicGameState state)
    {
        return state.CurrentTrick.Count >= PlayerOrder.All.Count - 1;
    }

    private static Card AvoidBreakingUnannouncedMarriage(GameObservation observation, IReadOnlyList<Card> legalCards, Card selected)
    {
        if (!BreaksUnannouncedMarriage(observation, selected))
        {
            return selected;
        }

        return legalCards
            .Where(card => !BreaksUnannouncedMarriage(observation, card))
            .OrderBy(card => ThrowawayCost(card, observation.PublicState.Trump))
            .ThenBy(card => card.Strength)
            .Select(card => (Card?)card)
            .FirstOrDefault() ?? selected;
    }

    private static bool BreaksUnannouncedMarriage(GameObservation observation, Card card)
    {
        return card.Rank is Rank.King or Rank.Queen
            && HasMarriage(observation.Hand, card.Suit)
            && !CanAnnounceMarriageByPlaying(observation.PublicState, card.Suit)
            && !observation.PublicState.MarriageAnnouncements.Any(announcement =>
                announcement.Player == observation.Self && announcement.Suit == card.Suit);
    }

    private static bool CanAnnounceMarriageByPlaying(PublicGameState state, Suit suit)
    {
        return state.CurrentTrick.Count == 0 || state.CurrentTrick[0].Card.Suit == suit;
    }

    private static bool BidderMayStillOvertakeCurrentWinner(GameObservation observation)
    {
        var state = observation.PublicState;
        if (state.Bidder is not { } bidder || HasPlayerPlayed(state, bidder) || state.CurrentTrick.Count == 0)
        {
            return false;
        }

        var currentWinner = DetermineTrickWinner(state.CurrentTrick, state.Trump);
        var winningCard = state.CurrentTrick.First(played => played.Player == currentWinner).Card;
        var leadSuit = state.CurrentTrick[0].Card.Suit;

        if (state.Trump == winningCard.Suit)
        {
            return UnknownCards(observation)
                .Any(card => card.Suit == winningCard.Suit && card.Strength > winningCard.Strength);
        }

        if (CountUnseenHigherCards(observation, winningCard) > 0)
        {
            return true;
        }

        return state.Trump is { } trump
            && IsPlayerKnownVoidInSuit(state, bidder, leadSuit)
            && UnknownCards(observation).Any(card => card.Suit == trump);
    }

    private static HandProfile EvaluateHand(IReadOnlyList<Card> hand)
    {
        var rawPoints = hand.Sum(card => card.PointValue);
        var marriages = CountMarriages(hand);
        var aces = hand.Count(card => card.Rank == Rank.Ace);
        var tens = hand.Count(card => card.Rank == Rank.Ten);
        var protectedTens = hand.Count(card => card.Rank == Rank.Ten && hand.Any(other => other.Suit == card.Suit && other.Rank == Rank.Ace));
        var bestSuit = Enum.GetValues<Suit>().Max(suit => SuitContractScore(hand, suit));
        var noTrump = rawPoints + aces * 13 + protectedTens * 8 + tens * 3;
        var estimate = Math.Max(bestSuit, noTrump) + marriages * 14;
        var controls = aces + protectedTens;
        return new HandProfile(Math.Clamp(estimate, 0, 180), marriages, controls);
    }

    private static int TrumpChoiceScore(IReadOnlyList<Card> hand, Suit? suit)
    {
        if (suit is null)
        {
            var aces = hand.Count(card => card.Rank == Rank.Ace);
            var protectedTens = hand.Count(card => card.Rank == Rank.Ten && hand.Any(other => other.Suit == card.Suit && other.Rank == Rank.Ace));
            var weakNines = hand.Count(card => card.Rank == Rank.Nine);
            return hand.Sum(card => card.PointValue) + aces * 12 + protectedTens * 8 - weakNines * 2;
        }

        return SuitContractScore(hand, suit.Value);
    }

    private static int SuitContractScore(IReadOnlyList<Card> hand, Suit suit)
    {
        var suitCards = hand.Where(card => card.Suit == suit).ToArray();
        var sideAces = hand.Count(card => card.Suit != suit && card.Rank == Rank.Ace);
        var hasMarriage = HasMarriage(hand, suit);
        var highTrumpScore = suitCards.Sum(card => card.Rank switch
        {
            Rank.Ace => 28,
            Rank.Ten => 22,
            Rank.King => 14,
            Rank.Queen => 12,
            Rank.Jack => 7,
            Rank.Nine => 2,
            _ => 0
        });

        return hand.Sum(card => card.PointValue)
            + highTrumpScore
            + suitCards.Length * 7
            + sideAces * 8
            + (hasMarriage ? 38 : 0);
    }

    private static int PassCardKeepPenalty(GameObservation observation, Card card, ISet<Card> marriageCards)
    {
        var trump = observation.PublicState.Trump;
        var remainingHand = observation.Hand.Where(candidate => candidate != card).ToArray();
        var suitLengthBefore = observation.Hand.Count(candidate => candidate.Suit == card.Suit);
        var suitLengthAfter = remainingHand.Count(candidate => candidate.Suit == card.Suit);
        var penalty = card.PointValue * 4 + card.Strength;

        if (trump == card.Suit)
        {
            penalty += 55 + card.Strength * 4;
        }

        if (card.Rank == Rank.Ace)
        {
            penalty += 45;
        }

        if (card.Rank == Rank.Ten)
        {
            penalty += ProtectedTenValue(observation, card, remainingHand);
        }

        if (card.Rank is Rank.King or Rank.Queen && HasMarriage(remainingHand, card.Suit))
        {
            penalty += 26;
        }

        if (marriageCards.Contains(card))
        {
            penalty += 65;
        }

        if (suitLengthBefore == 1 && trump != card.Suit)
        {
            penalty -= 8;
        }

        if (card.PointValue == 0 && trump != card.Suit)
        {
            penalty -= 8;
        }

        if (suitLengthAfter == 0 && trump != card.Suit)
        {
            penalty -= 6;
        }

        penalty -= RemainingHandControlValue(observation, remainingHand) / 6;

        return penalty;
    }

    private static int ProtectedTenValue(GameObservation observation, Card ten, IReadOnlyList<Card> remainingHand)
    {
        var trump = observation.PublicState.Trump;
        var hasAce = remainingHand.Any(card => card.Suit == ten.Suit && card.Rank == Rank.Ace);
        if (hasAce)
        {
            return 34;
        }

        if (trump == ten.Suit)
        {
            return 24;
        }

        var suitLengthAfter = remainingHand.Count(card => card.Suit == ten.Suit);
        var unseenHigher = CountUnseenHigherCards(observation, ten);
        if (suitLengthAfter == 0)
        {
            return -44;
        }

        if (suitLengthAfter <= 1 && unseenHigher > 0)
        {
            return -34;
        }

        return unseenHigher > 0 ? -18 : 10;
    }

    private static int RemainingHandControlValue(GameObservation observation, IReadOnlyList<Card> remainingHand)
    {
        var value = 0;
        foreach (var card in remainingHand)
        {
            if (card.Rank == Rank.Ace)
            {
                value += 18;
            }

            if (card.Rank == Rank.Ten && remainingHand.Any(other => other.Suit == card.Suit && other.Rank == Rank.Ace))
            {
                value += 12;
            }

            if (observation.PublicState.Trump == card.Suit)
            {
                value += 4 + card.Strength;
            }
        }

        return value;
    }

    private static int DefensiveLeadRisk(GameObservation observation, Card card)
    {
        var trump = observation.PublicState.Trump;
        var risk = card.PointValue * 3 + card.Strength;
        if (trump == card.Suit)
        {
            risk += 18;
        }

        if (card.Rank == Rank.Ace)
        {
            risk -= 20;
        }

        var unseenHigher = CountUnseenHigherCards(observation, card);
        if (IsLeadControlCard(observation, card) && card.PointValue >= 10)
        {
            risk -= 45 + card.PointValue;
        }

        return risk + unseenHigher * 8;
    }

    private static int LeadWinnerLikelihood(GameObservation observation, Card card)
    {
        var trump = observation.PublicState.Trump;
        var unseenHigher = CountUnseenHigherCards(observation, card);
        var remainingSuitCards = CountUnknownCardsInSuit(observation, card.Suit);
        var score = card.Strength * 12 + card.PointValue - unseenHigher * 16 - remainingSuitCards;
        if (trump == card.Suit)
        {
            score += 14;
        }

        if (IsLeadControlCard(observation, card))
        {
            score += 80 + card.PointValue * 2;
        }
        else if (IsKnownMasterCard(observation, card))
        {
            score += 38 + card.PointValue;
        }

        if (card.Rank == Rank.Ace)
        {
            score += 20;
        }

        return score;
    }

    private static int SafePointContribution(Card card) => card.PointValue switch
    {
        >= 10 => 100 + card.Strength,
        >= 3 => 40 + card.PointValue,
        _ => card.PointValue
    };

    private static int ThrowawayCost(Card card, Suit? trump)
    {
        var cost = card.PointValue * 5 + card.Strength;
        if (trump == card.Suit)
        {
            cost += 30;
        }

        if (card.Rank == Rank.Ace)
        {
            cost += 30;
        }

        return cost;
    }

    private static bool WouldWin(PlayerId player, Card card, IReadOnlyList<PlayedCard> currentTrick, Suit? trump)
    {
        var candidate = currentTrick.Append(new PlayedCard(player, card)).ToArray();
        return DetermineTrickWinner(candidate, trump) == player;
    }

    private static PlayerId DetermineTrickWinner(IReadOnlyList<PlayedCard> playedCards, Suit? trump)
    {
        var leadSuit = playedCards[0].Card.Suit;
        return playedCards
            .OrderByDescending(played => CardTrickRank(played.Card, leadSuit, trump))
            .First()
            .Player;
    }

    private static int CardTrickRank(Card card, Suit leadSuit, Suit? trump)
    {
        if (trump == card.Suit)
        {
            return 100 + card.Strength;
        }

        return card.Suit == leadSuit ? 50 + card.Strength : card.Strength;
    }

    private static PlayerId? DefensivePartner(PlayerId self, PlayerId? bidder)
    {
        return bidder is null || bidder == self
            ? null
            : PlayerOrder.All.Single(player => player != self && player != bidder.Value);
    }

    private static bool HasPlayerPlayed(PublicGameState state, PlayerId player)
    {
        return state.CurrentTrick.Any(played => played.Player == player);
    }

    private static bool IsKnownMasterCard(GameObservation observation, Card card)
    {
        return CountUnseenHigherCards(observation, card) == 0;
    }

    private static bool IsLeadControlCard(GameObservation observation, Card card)
    {
        if (!IsKnownMasterCard(observation, card))
        {
            return false;
        }

        var trump = observation.PublicState.Trump;
        if (trump is null || card.Suit == trump)
        {
            return true;
        }

        return !PlayerOrder.All
            .Where(player => player != observation.Self)
            .Any(player => IsPlayerKnownVoidInSuit(observation.PublicState, player, card.Suit));
    }

    private static int CountUnseenHigherCards(GameObservation observation, Card card)
    {
        return UnknownCards(observation)
            .Count(candidate => candidate.Suit == card.Suit && candidate.Strength > card.Strength);
    }

    private static int CountUnknownCardsInSuit(GameObservation observation, Suit suit)
    {
        return UnknownCards(observation).Count(card => card.Suit == suit);
    }

    private static IEnumerable<Card> UnknownCards(GameObservation observation)
    {
        var known = observation.Hand
            .Concat(observation.PublicState.CurrentTrick.Select(played => played.Card))
            .Concat(observation.PublicState.CompletedTricks.SelectMany(trick => trick.Cards.Select(played => played.Card)))
            .Concat(observation.PublicState.PassedCards.Select(pass => pass.Card))
            .ToHashSet();

        return FullDeck.Where(card => !known.Contains(card));
    }

    private static bool IsPlayerKnownVoidInSuit(PublicGameState state, PlayerId player, Suit suit)
    {
        return state.CompletedTricks.Any(trick =>
            trick.Cards.Count > 0
            && trick.Cards[0].Card.Suit == suit
            && trick.Cards.Any(played => played.Player == player && played.Card.Suit != suit));
    }

    private static IEnumerable<Card> MarriageCards(IEnumerable<Card> hand)
    {
        foreach (var group in hand.GroupBy(card => card.Suit))
        {
            var ranks = group.Select(card => card.Rank).ToHashSet();
            if (!ranks.Contains(Rank.King) || !ranks.Contains(Rank.Queen))
            {
                continue;
            }

            foreach (var card in group.Where(card => card.Rank is Rank.King or Rank.Queen))
            {
                yield return card;
            }
        }
    }

    private static bool HasMarriage(IEnumerable<Card> hand, Suit suit)
    {
        return hand.Contains(new Card(suit, Rank.King)) && hand.Contains(new Card(suit, Rank.Queen));
    }

    private static int CountMarriages(IEnumerable<Card> hand)
    {
        return hand.GroupBy(card => card.Suit)
            .Count(group =>
            {
                var ranks = group.Select(card => card.Rank).ToHashSet();
                return ranks.Contains(Rank.King) && ranks.Contains(Rank.Queen);
            });
    }

    private sealed record HandProfile(int ContractEstimate, int MarriageCount, int ControlCount);
}

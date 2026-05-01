using RuskaTrupa.Core.Cards;
using RuskaTrupa.Core.Game;

namespace RuskaTrupa.AI;

public sealed class HeuristicPlayerAgent : IPlayerAgent
{
    private const int MarriageBidThreshold = 120;

    public HeuristicPlayerAgent()
        : this(BotSkillLevel.Advanced)
    {
    }

    public HeuristicPlayerAgent(BotSkillLevel skillLevel)
    {
        SkillLevel = skillLevel;
    }

    public BotSkillLevel SkillLevel { get; }

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
            <= 100 => -8,
            <= MarriageBidThreshold => 16,
            _ => 26
        };

        if (SkillLevel == BotSkillLevel.Intermediate)
        {
            requiredMargin += minimumBid <= 100 ? 4 : 8;
        }

        if (profile.ContractEstimate < minimumBid + requiredMargin)
        {
            return new AgentDecision<int>(0, 0.78, "InsufficientContractMargin");
        }

        var maxRaise = SkillLevel == BotSkillLevel.Intermediate
            ? 1
            : minimumBid <= 110 ? 3 : 1;
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
            .OrderBy(card => PassCardPenalty(observation, card, marriageCards))
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

        if (SkillLevel == BotSkillLevel.Beginner)
        {
            var beginnerCard = legalCards
                .OrderBy(card => card.PointValue)
                .ThenBy(card => card.Strength)
                .First();
            return new AgentDecision<Card>(beginnerCard, 0.4, "BeginnerConservePoints");
        }

        var selected = observation.PublicState.CurrentTrick.Count == 0
            ? ChooseLeadCard(observation, legalCards)
            : ChooseFollowCard(observation, legalCards);

        return new AgentDecision<Card>(selected, 0.7, "TrickAwareCard");
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
        var bidder = observation.PublicState.Bidder;
        var selfIsBidder = bidder == observation.Self;
        var announcedMarriageSuits = observation.PublicState.MarriageAnnouncements
            .Where(announcement => announcement.Player == observation.Self)
            .Select(announcement => announcement.Suit)
            .ToHashSet();

        var unannouncedMarriageLead = legalCards
            .Where(card => card.Rank is Rank.King or Rank.Queen
                && !announcedMarriageSuits.Contains(card.Suit)
                && HasMarriage(observation.Hand, card.Suit))
            .OrderByDescending(card => observation.PublicState.Trump == card.Suit ? 40 : 20)
            .ThenBy(card => card.Rank == Rank.Queen ? 0 : 1)
            .FirstOrDefault();
        if (unannouncedMarriageLead != default)
        {
            return unannouncedMarriageLead;
        }

        if (selfIsBidder)
        {
            return legalCards
                .OrderByDescending(card => LeadWinnerLikelihood(observation.Hand, card, observation.PublicState.Trump))
                .ThenByDescending(card => card.PointValue)
                .ThenByDescending(card => card.Strength)
                .First();
        }

        return legalCards
            .OrderBy(card => DefensiveLeadRisk(card, observation.PublicState.Trump))
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

        var winningCards = legalCards
            .Where(card => WouldWin(observation.Self, card, state.CurrentTrick, state.Trump))
            .ToArray();

        if (partnerWinning)
        {
            return legalCards
                .OrderByDescending(card => SafePointContribution(card))
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
        return new HandProfile(Math.Clamp(estimate, 0, 180), marriages);
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

    private static int PassCardPenalty(GameObservation observation, Card card, ISet<Card> marriageCards)
    {
        var trump = observation.PublicState.Trump;
        var suitLength = observation.Hand.Count(candidate => candidate.Suit == card.Suit);
        var penalty = card.PointValue * 4 + card.Strength;

        if (trump == card.Suit)
        {
            penalty += 40 + card.Strength * 3;
        }

        if (card.Rank == Rank.Ace)
        {
            penalty += 45;
        }

        if (card.Rank == Rank.Ten && observation.Hand.Any(other => other.Suit == card.Suit && other.Rank == Rank.Ace))
        {
            penalty += 28;
        }

        if (marriageCards.Contains(card))
        {
            penalty += 65;
        }

        if (suitLength == 1 && trump != card.Suit)
        {
            penalty -= 8;
        }

        return penalty;
    }

    private static int DefensiveLeadRisk(Card card, Suit? trump)
    {
        var risk = card.PointValue * 3 + card.Strength;
        if (trump == card.Suit)
        {
            risk += 18;
        }

        if (card.Rank == Rank.Ace)
        {
            risk -= 20;
        }

        return risk;
    }

    private static int LeadWinnerLikelihood(IReadOnlyList<Card> hand, Card card, Suit? trump)
    {
        var sameSuitHigher = hand.Count(other => other.Suit == card.Suit && other.Strength > card.Strength);
        var score = card.Strength * 12 + card.PointValue - sameSuitHigher * 4;
        if (trump == card.Suit)
        {
            score += 14;
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

    private sealed record HandProfile(int ContractEstimate, int MarriageCount);
}

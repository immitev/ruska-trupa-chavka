using RuskaTrupa.AI;
using RuskaTrupa.Core.Cards;
using RuskaTrupa.Core.Game;

namespace RuskaTrupa.AI.Tests;

public sealed class HeuristicPlayerAgentTests
{
    [Fact]
    public void DecideCard_ReturnsOneOfTheLegalCards()
    {
        var preview = NewGamePreview.Create(7);
        var observation = preview.CreateObservation(PlayerId.Second);
        var legalCards = observation.Hand.Take(3).ToArray();
        var agent = new HeuristicPlayerAgent();

        var decision = agent.DecideCard(observation, legalCards);

        Assert.Contains(decision.Action, legalCards);
    }

    [Fact]
    public void DecideCardToPass_ReturnsOneOfTheLegalCards()
    {
        var preview = NewGamePreview.Create(7);
        var observation = preview.CreateObservation(PlayerId.Second);
        var legalCards = observation.Hand.Take(3).ToArray();
        var agent = new HeuristicPlayerAgent();

        var decision = agent.DecideCardToPass(observation, legalCards);

        Assert.Contains(decision.Action, legalCards);
    }

    [Fact]
    public void DecideCardToPass_DiscardsBareTenBeforeSafeFaceCard()
    {
        var observation = new GameObservation(
            PlayerId.Second,
            new[]
            {
                new Card(Suit.Clubs, Rank.Ten),
                new Card(Suit.Hearts, Rank.King),
                new Card(Suit.Diamonds, Rank.Queen),
                new Card(Suit.Spades, Rank.Jack)
            },
            EmptyPublicState(PlayerId.Second) with
            {
                Trump = Suit.Hearts,
                Bidder = PlayerId.Second
            });
        var agent = new HeuristicPlayerAgent();

        var decision = agent.DecideCardToPass(observation, new[] { new Card(Suit.Clubs, Rank.Ten), new Card(Suit.Diamonds, Rank.Queen) });

        Assert.Equal(new Card(Suit.Clubs, Rank.Ten), decision.Action);
    }

    [Fact]
    public void DecideCardToPass_KeepsTenProtectedByAce()
    {
        var observation = new GameObservation(
            PlayerId.Second,
            new[]
            {
                new Card(Suit.Clubs, Rank.Ace),
                new Card(Suit.Clubs, Rank.Ten),
                new Card(Suit.Diamonds, Rank.King),
                new Card(Suit.Spades, Rank.Nine)
            },
            EmptyPublicState(PlayerId.Second) with
            {
                Trump = Suit.Hearts,
                Bidder = PlayerId.Second
            });
        var agent = new HeuristicPlayerAgent();

        var decision = agent.DecideCardToPass(observation, new[] { new Card(Suit.Clubs, Rank.Ten), new Card(Suit.Spades, Rank.Nine) });

        Assert.Equal(new Card(Suit.Spades, Rank.Nine), decision.Action);
    }

    [Fact]
    public void DecideTrump_ReturnsOneOfTheLegalSuits()
    {
        var preview = NewGamePreview.Create(7);
        var observation = preview.CreateObservation(PlayerId.Second);
        var legalSuits = Enum.GetValues<Suit>().Cast<Suit?>().Append(null).ToArray();
        var agent = new HeuristicPlayerAgent();

        var decision = agent.DecideTrump(observation, legalSuits);

        Assert.Contains(decision.Action, legalSuits);
    }

    [Fact]
    public void DecideOpeningBid_ReturnsOneOfTheLegalBids()
    {
        var preview = NewGamePreview.Create(7);
        var observation = preview.CreateObservation(PlayerId.Second);
        var legalBids = new[] { 0, 100, 110, 120 };
        var agent = new HeuristicPlayerAgent();

        var decision = agent.DecideOpeningBid(observation, legalBids);

        Assert.Contains(decision.Action, legalBids);
    }

    [Fact]
    public void DecideOpeningBid_BidsMinimumWithModerateControls()
    {
        var observation = new GameObservation(
            PlayerId.Second,
            new[]
            {
                new Card(Suit.Clubs, Rank.Ace),
                new Card(Suit.Clubs, Rank.Ten),
                new Card(Suit.Hearts, Rank.King),
                new Card(Suit.Spades, Rank.Queen),
                new Card(Suit.Diamonds, Rank.Jack),
                new Card(Suit.Diamonds, Rank.Nine),
                new Card(Suit.Spades, Rank.Nine)
            },
            EmptyPublicState(PlayerId.Second));
        var agent = new HeuristicPlayerAgent();

        var decision = agent.DecideOpeningBid(observation, new[] { 0, 100, 110, 120 });

        Assert.Equal(100, decision.Action);
    }

    [Fact]
    public void DecideOpeningBid_RaisesModeratelyAfterExistingBid()
    {
        var observation = new GameObservation(
            PlayerId.Second,
            new[]
            {
                new Card(Suit.Clubs, Rank.Ace),
                new Card(Suit.Clubs, Rank.Ten),
                new Card(Suit.Clubs, Rank.King),
                new Card(Suit.Clubs, Rank.Queen),
                new Card(Suit.Hearts, Rank.Ace),
                new Card(Suit.Hearts, Rank.Ten),
                new Card(Suit.Spades, Rank.Ace)
            },
            EmptyPublicState(PlayerId.Second));
        var agent = new HeuristicPlayerAgent();

        var decision = agent.DecideOpeningBid(observation, Enumerable.Range(101, 60).Prepend(0).ToArray());

        Assert.InRange(decision.Action, 101, 104);
    }

    [Fact]
    public void DecideOpeningBid_PlayStyleChangesRiskTolerance()
    {
        var observation = new GameObservation(
            PlayerId.Second,
            new[]
            {
                new Card(Suit.Clubs, Rank.Ace),
                new Card(Suit.Clubs, Rank.Ten),
                new Card(Suit.Clubs, Rank.King),
                new Card(Suit.Clubs, Rank.Queen),
                new Card(Suit.Hearts, Rank.Ace),
                new Card(Suit.Hearts, Rank.Ten),
                new Card(Suit.Spades, Rank.Ace)
            },
            EmptyPublicState(PlayerId.Second));
        var legalBids = Enumerable.Range(101, 60).Prepend(0).ToArray();
        var balanced = new HeuristicPlayerAgent(BotSkillLevel.Advanced, BotPlayStyle.Balanced);
        var bold = new HeuristicPlayerAgent(BotSkillLevel.Advanced, BotPlayStyle.Bold);
        var patient = new HeuristicPlayerAgent(BotSkillLevel.Advanced, BotPlayStyle.Patient);

        var balancedBid = balanced.DecideOpeningBid(observation, legalBids).Action;
        var boldBid = bold.DecideOpeningBid(observation, legalBids).Action;
        var patientBid = patient.DecideOpeningBid(observation, legalBids).Action;

        Assert.True(boldBid > balancedBid);
        Assert.True(patientBid <= balancedBid);
    }

    [Fact]
    public void BeginnerSkill_PassesAfterExistingBid()
    {
        var observation = new GameObservation(
            PlayerId.Second,
            new[]
            {
                new Card(Suit.Clubs, Rank.Ace),
                new Card(Suit.Clubs, Rank.Ten),
                new Card(Suit.Clubs, Rank.King),
                new Card(Suit.Clubs, Rank.Queen),
                new Card(Suit.Hearts, Rank.Ace),
                new Card(Suit.Hearts, Rank.Ten),
                new Card(Suit.Spades, Rank.Ace)
            },
            EmptyPublicState(PlayerId.Second));
        var agent = new HeuristicPlayerAgent(BotSkillLevel.Beginner);

        var decision = agent.DecideOpeningBid(observation, new[] { 0, 101, 102, 103 });

        Assert.Equal(0, decision.Action);
    }

    [Fact]
    public void DecideOpeningBid_BidsMinimumWhenPassIsNotLegal()
    {
        var observation = new GameObservation(
            PlayerId.Second,
            new[]
            {
                new Card(Suit.Clubs, Rank.Nine),
                new Card(Suit.Diamonds, Rank.Nine),
                new Card(Suit.Hearts, Rank.Nine),
                new Card(Suit.Spades, Rank.Nine)
            },
            EmptyPublicState(PlayerId.Second));
        var agent = new HeuristicPlayerAgent(BotSkillLevel.Beginner);

        var decision = agent.DecideOpeningBid(observation, new[] { 100 });

        Assert.Equal(100, decision.Action);
    }

    [Fact]
    public void BeginnerSkill_PlaysLowestLegalCard()
    {
        var observation = new GameObservation(
            PlayerId.Second,
            new[]
            {
                new Card(Suit.Clubs, Rank.Ace),
                new Card(Suit.Clubs, Rank.Nine),
                new Card(Suit.Hearts, Rank.Ten)
            },
            EmptyPublicState(PlayerId.Second));
        var agent = new HeuristicPlayerAgent(BotSkillLevel.Beginner);

        var decision = agent.DecideCard(observation, new[] { new Card(Suit.Clubs, Rank.Ace), new Card(Suit.Clubs, Rank.Nine) });

        Assert.Equal(new Card(Suit.Clubs, Rank.Nine), decision.Action);
    }

    [Fact]
    public void DecideTrump_PrefersStrongMarriageSuit()
    {
        var observation = new GameObservation(
            PlayerId.Second,
            new[]
            {
                new Card(Suit.Hearts, Rank.Ace),
                new Card(Suit.Hearts, Rank.Ten),
                new Card(Suit.Hearts, Rank.King),
                new Card(Suit.Hearts, Rank.Queen),
                new Card(Suit.Clubs, Rank.Ace),
                new Card(Suit.Diamonds, Rank.Nine),
                new Card(Suit.Spades, Rank.Jack)
            },
            EmptyPublicState(PlayerId.Second));
        var agent = new HeuristicPlayerAgent();

        var decision = agent.DecideTrump(observation, Enum.GetValues<Suit>().Cast<Suit?>().Append(null).ToArray());

        Assert.Equal(Suit.Hearts, decision.Action);
    }

    [Fact]
    public void DecideCard_LeadsQueenWhenAnnouncingMarriage()
    {
        var observation = new GameObservation(
            PlayerId.Second,
            new[]
            {
                new Card(Suit.Spades, Rank.King),
                new Card(Suit.Spades, Rank.Queen),
                new Card(Suit.Clubs, Rank.Ace)
            },
            EmptyPublicState(PlayerId.Second) with
            {
                Trump = Suit.Spades,
                Bidder = PlayerId.Second,
                CurrentTrick = Array.Empty<PlayedCard>()
            });
        var agent = new HeuristicPlayerAgent();

        var decision = agent.DecideCard(observation, new[] { new Card(Suit.Spades, Rank.King), new Card(Suit.Spades, Rank.Queen) });

        Assert.Equal(new Card(Suit.Spades, Rank.Queen), decision.Action);
    }

    [Fact]
    public void DecideCard_ContributesPointsWhenPartnerWinning()
    {
        var observation = new GameObservation(
            PlayerId.Second,
            new[]
            {
                new Card(Suit.Clubs, Rank.Ten),
                new Card(Suit.Clubs, Rank.Nine),
                new Card(Suit.Hearts, Rank.Ace)
            },
            EmptyPublicState(PlayerId.Second) with
            {
                Bidder = PlayerId.First,
                CurrentTrick = new[]
                {
                    new PlayedCard(PlayerId.Third, new Card(Suit.Clubs, Rank.Ace)),
                    new PlayedCard(PlayerId.First, new Card(Suit.Clubs, Rank.King))
                }
            });
        var agent = new HeuristicPlayerAgent();

        var decision = agent.DecideCard(observation, new[] { new Card(Suit.Clubs, Rank.Ten), new Card(Suit.Clubs, Rank.Nine) });

        Assert.Equal(new Card(Suit.Clubs, Rank.Ten), decision.Action);
    }

    [Fact]
    public void DecideCard_DoesNotFeedPointsToPartnerWhenBidderStillCanPlay()
    {
        var observation = new GameObservation(
            PlayerId.Second,
            new[]
            {
                new Card(Suit.Clubs, Rank.Ten),
                new Card(Suit.Clubs, Rank.Nine),
                new Card(Suit.Hearts, Rank.Ace)
            },
            EmptyPublicState(PlayerId.Second) with
            {
                Bidder = PlayerId.First,
                CurrentTrick = new[]
                {
                    new PlayedCard(PlayerId.Third, new Card(Suit.Clubs, Rank.King))
                }
            });
        var agent = new HeuristicPlayerAgent();

        var decision = agent.DecideCard(observation, new[] { new Card(Suit.Clubs, Rank.Ten), new Card(Suit.Clubs, Rank.Nine) });

        Assert.Equal(new Card(Suit.Clubs, Rank.Nine), decision.Action);
    }

    [Fact]
    public void DecideCard_WinsCheaplyWhenBidderCurrentlyWinning()
    {
        var observation = new GameObservation(
            PlayerId.Second,
            new[]
            {
                new Card(Suit.Spades, Rank.Jack),
                new Card(Suit.Spades, Rank.Ace),
                new Card(Suit.Hearts, Rank.Nine)
            },
            EmptyPublicState(PlayerId.Second) with
            {
                Trump = Suit.Spades,
                Bidder = PlayerId.First,
                CurrentTrick = new[]
                {
                    new PlayedCard(PlayerId.First, new Card(Suit.Hearts, Rank.Ace))
                }
            });
        var agent = new HeuristicPlayerAgent();

        var decision = agent.DecideCard(observation, new[] { new Card(Suit.Spades, Rank.Jack), new Card(Suit.Spades, Rank.Ace) });

        Assert.Equal(new Card(Suit.Spades, Rank.Jack), decision.Action);
    }

    [Fact]
    public void DecideCard_LastToPlayTakesMaximumPointsWhenWinningIsGuaranteed()
    {
        var observation = new GameObservation(
            PlayerId.Second,
            new[]
            {
                new Card(Suit.Clubs, Rank.King),
                new Card(Suit.Clubs, Rank.Ten),
                new Card(Suit.Hearts, Rank.Nine)
            },
            EmptyPublicState(PlayerId.Second) with
            {
                Bidder = PlayerId.First,
                CurrentTrick = new[]
                {
                    new PlayedCard(PlayerId.First, new Card(Suit.Clubs, Rank.Jack)),
                    new PlayedCard(PlayerId.Third, new Card(Suit.Clubs, Rank.Queen))
                }
            });
        var agent = new HeuristicPlayerAgent();

        var decision = agent.DecideCard(observation, new[] { new Card(Suit.Clubs, Rank.King), new Card(Suit.Clubs, Rank.Ten) });

        Assert.Equal(new Card(Suit.Clubs, Rank.Ten), decision.Action);
    }

    [Fact]
    public void DecideCard_BidderLeadsRememberedMasterTen()
    {
        var observation = new GameObservation(
            PlayerId.Second,
            new[]
            {
                new Card(Suit.Clubs, Rank.Ten),
                new Card(Suit.Hearts, Rank.King),
                new Card(Suit.Diamonds, Rank.Nine)
            },
            EmptyPublicState(PlayerId.Second) with
            {
                Trump = Suit.Hearts,
                Bidder = PlayerId.Second,
                CurrentTrick = Array.Empty<PlayedCard>(),
                CompletedTricks = new[]
                {
                    new CompletedTrick(
                        new[]
                        {
                            new PlayedCard(PlayerId.First, new Card(Suit.Clubs, Rank.Ace)),
                            new PlayedCard(PlayerId.Third, new Card(Suit.Clubs, Rank.Queen)),
                            new PlayedCard(PlayerId.Second, new Card(Suit.Clubs, Rank.Jack))
                        },
                        PlayerId.First)
                }
            });
        var agent = new HeuristicPlayerAgent();

        var decision = agent.DecideCard(observation, new[] { new Card(Suit.Clubs, Rank.Ten), new Card(Suit.Hearts, Rank.King), new Card(Suit.Diamonds, Rank.Nine) });

        Assert.Equal(new Card(Suit.Clubs, Rank.Ten), decision.Action);
    }

    private static PublicGameState EmptyPublicState(PlayerId currentPlayer)
    {
        return new PublicGameState(
            GamePhase.Bidding,
            PlayerId.First,
            currentPlayer,
            Array.Empty<Bid>(),
            null,
            null,
            null,
            Array.Empty<CardPass>(),
            Array.Empty<PlayedCard>(),
            Array.Empty<CompletedTrick>(),
            Array.Empty<MarriageAnnouncement>(),
            PlayerOrder.All.ToDictionary(player => player, _ => 0),
            501);
    }
}

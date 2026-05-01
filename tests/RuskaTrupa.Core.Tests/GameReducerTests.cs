using RuskaTrupa.Core.Cards;
using RuskaTrupa.Core.Game;

namespace RuskaTrupa.Core.Tests;

public sealed class GameReducerTests
{
    [Fact]
    public void StartNewHand_BeginsInBiddingWithPlayerAfterDealer()
    {
        var state = GameState.StartNewHand(12);

        Assert.Equal(GamePhase.Bidding, state.Phase);
        Assert.Equal(PlayerId.Second, state.Dealer);
        Assert.Equal(PlayerId.First, state.CurrentPlayer);
        Assert.All(PlayerOrder.All, player => Assert.Equal(7, state.GetHand(player).Count));
        Assert.Equal(3, state.Talon.Count);
    }

    [Fact]
    public void LegalActions_DuringBidding_IncludePassAndValidBidsForCurrentPlayerOnly()
    {
        var state = GameState.StartNewHand(12);
        var legalActions = new LegalActionProvider();

        var currentPlayerActions = legalActions.GetLegalActions(state, PlayerId.First).OfType<BidAction>().ToArray();
        var otherPlayerActions = legalActions.GetLegalActions(state, PlayerId.Second);

        Assert.Contains(currentPlayerActions, action => action.Amount is null);
        Assert.Contains(currentPlayerActions, action => action.Amount == 100);
        Assert.Contains(currentPlayerActions, action => action.Amount == 200);
        Assert.Empty(otherPlayerActions);
    }

    [Fact]
    public void LegalActions_DuringBidding_MinimumNextBidIsOneAboveCurrentBid()
    {
        var state = WithHand(GameState.StartNewHand(12), PlayerId.Second, new[]
        {
            new Card(Suit.Clubs, Rank.King),
            new Card(Suit.Clubs, Rank.Queen),
            new Card(Suit.Hearts, Rank.Ace),
            new Card(Suit.Hearts, Rank.Ten),
            new Card(Suit.Spades, Rank.Nine),
            new Card(Suit.Diamonds, Rank.Jack),
            new Card(Suit.Diamonds, Rank.Nine)
        }) with
        {
            CurrentPlayer = PlayerId.Second,
            Bidder = PlayerId.First,
            WinningBid = 120
        };
        var legalActions = new LegalActionProvider();

        var bids = legalActions.GetLegalActions(state, PlayerId.Second).OfType<BidAction>().ToArray();

        Assert.Contains(bids, action => action.Amount is null);
        Assert.DoesNotContain(bids, action => action.Amount == 120);
        Assert.Contains(bids, action => action.Amount == 121 && action.MarriageSuit == Suit.Clubs);
    }

    [Fact]
    public void LegalActions_DuringBidding_RequireMarriageSuitAboveOneHundredTwenty()
    {
        var state = WithHand(GameState.StartNewHand(12), PlayerId.Second, new[]
        {
            new Card(Suit.Clubs, Rank.King),
            new Card(Suit.Hearts, Rank.Queen),
            new Card(Suit.Hearts, Rank.Ace),
            new Card(Suit.Hearts, Rank.Ten),
            new Card(Suit.Spades, Rank.Nine),
            new Card(Suit.Diamonds, Rank.Jack),
            new Card(Suit.Diamonds, Rank.Nine)
        }) with
        {
            CurrentPlayer = PlayerId.Second,
            Bidder = PlayerId.First,
            WinningBid = 120
        };
        var legalActions = new LegalActionProvider();

        var bids = legalActions.GetLegalActions(state, PlayerId.Second).OfType<BidAction>().ToArray();

        Assert.Contains(bids, action => action.Amount is null);
        Assert.DoesNotContain(bids, action => action.Amount > 120);
    }

    [Fact]
    public void LegalActions_DuringBidding_AfterPassOnlyAllowPass()
    {
        var state = GameState.StartNewHand(12) with
        {
            CurrentPlayer = PlayerId.First,
            Bids = new[] { new Bid(PlayerId.First, null), new Bid(PlayerId.Third, 101) },
            Bidder = PlayerId.Third,
            WinningBid = 101
        };
        var legalActions = new LegalActionProvider();

        var bids = legalActions.GetLegalActions(state, PlayerId.First).OfType<BidAction>().ToArray();

        Assert.Single(bids);
        Assert.Contains(bids, action => action.Amount is null);
    }

    [Fact]
    public void LegalActions_DuringBidding_ThirdPlayerMustBidMinimumAfterTwoOpeningPasses()
    {
        var reducer = new GameReducer();
        var state = GameState.StartNewHand(12);

        state = reducer.Apply(state, new BidAction(PlayerId.First, null));
        state = reducer.Apply(state, new BidAction(PlayerId.Third, null));

        var bids = new LegalActionProvider().GetLegalActions(state, PlayerId.Second).OfType<BidAction>().ToArray();

        Assert.Single(bids);
        Assert.Equal(100, bids[0].Amount);
    }

    [Fact]
    public void Reducer_SkipsPlayerWhoAlreadyPassedDuringBidding()
    {
        var reducer = new GameReducer();
        var state = GameState.StartNewHand(12);

        state = reducer.Apply(state, new BidAction(PlayerId.First, null));
        state = reducer.Apply(state, new BidAction(PlayerId.Third, 100));

        Assert.Equal(PlayerId.Second, state.CurrentPlayer);
        Assert.DoesNotContain(
            new LegalActionProvider().GetLegalActions(state with { CurrentPlayer = PlayerId.First }, PlayerId.First).OfType<BidAction>(),
            action => action.Amount is not null);
    }

    [Fact]
    public void Reducer_EndsBiddingWhenOnlyBidderRemainsAfterEarlierPass()
    {
        var reducer = new GameReducer();
        var state = GameState.StartNewHand(12);

        state = reducer.Apply(state, new BidAction(PlayerId.First, null));
        state = reducer.Apply(state, new BidAction(PlayerId.Third, 100));
        state = reducer.Apply(state, new BidAction(PlayerId.Second, null));

        Assert.Equal(GamePhase.RevealTalon, state.Phase);
        Assert.Equal(PlayerId.Third, state.Bidder);
        Assert.Equal(PlayerId.Third, state.CurrentPlayer);
    }

    [Fact]
    public void Reducer_AdvancesBidWinnerToTalonRevealAfterTwoPasses()
    {
        var reducer = new GameReducer();
        var state = GameState.StartNewHand(12);

        state = reducer.Apply(state, new BidAction(PlayerId.First, 100));
        state = reducer.Apply(state, new BidAction(PlayerId.Third, null));
        state = reducer.Apply(state, new BidAction(PlayerId.Second, null));

        Assert.Equal(GamePhase.RevealTalon, state.Phase);
        Assert.Equal(PlayerId.First, state.CurrentPlayer);
        Assert.Equal(PlayerId.First, state.Bidder);
        Assert.Equal(100, state.WinningBid);
    }

    [Fact]
    public void Reducer_RevealTalon_AddsTalonToBidderHand()
    {
        var reducer = new GameReducer();
        var state = WinOpeningBid(reducer);

        state = reducer.Apply(state, new RevealTalonAction(PlayerId.First));

        Assert.Equal(GamePhase.ChooseTrump, state.Phase);
        Assert.Equal(10, state.GetHand(PlayerId.First).Count);
        Assert.Empty(state.Talon);
    }

    [Fact]
    public void Reducer_ChooseTrump_MovesToPassCards()
    {
        var reducer = new GameReducer();
        var state = WinOpeningBid(reducer);
        state = reducer.Apply(state, new RevealTalonAction(PlayerId.First));

        state = reducer.Apply(state, new ChooseTrumpAction(PlayerId.First, Suit.Hearts));

        Assert.Equal(GamePhase.PassCards, state.Phase);
        Assert.Equal(Suit.Hearts, state.Trump);
    }

    [Fact]
    public void Reducer_ChooseNoTrump_MovesToPassCardsWithNoTrump()
    {
        var reducer = new GameReducer();
        var state = WinOpeningBid(reducer);
        state = reducer.Apply(state, new RevealTalonAction(PlayerId.First));

        state = reducer.Apply(state, new ChooseTrumpAction(PlayerId.First, null));

        Assert.Equal(GamePhase.PassCards, state.Phase);
        Assert.Null(state.Trump);
    }

    [Fact]
    public void LegalActions_ChooseTrump_IncludeNoTrump()
    {
        var reducer = new GameReducer();
        var state = WinOpeningBid(reducer);
        state = reducer.Apply(state, new RevealTalonAction(PlayerId.First));

        var trumpActions = new LegalActionProvider()
            .GetLegalActions(state, PlayerId.First)
            .OfType<ChooseTrumpAction>()
            .ToArray();

        Assert.Contains(trumpActions, action => action.Trump is null);
        Assert.Contains(trumpActions, action => action.Trump == Suit.Clubs);
        Assert.Equal(5, trumpActions.Length);
    }

    [Fact]
    public void Reducer_PassingOneCardToEachOpponent_MovesToPlayingTricks()
    {
        var reducer = new GameReducer();
        var state = StartPlayingTricks(reducer, Suit.Hearts);

        Assert.Equal(8, state.GetHand(PlayerId.First).Count);
        Assert.Equal(8, state.GetHand(PlayerId.Second).Count);
        Assert.Equal(8, state.GetHand(PlayerId.Third).Count);
        Assert.Equal(2, state.PassedCards.Count);
    }

    [Fact]
    public void LegalActions_DuringTrickPlay_RequireFollowingSuitWhenPossible()
    {
        var state = GameState.StartNewHand(1) with
        {
            Phase = GamePhase.PlayingTricks,
            CurrentPlayer = PlayerId.Second,
            Trump = Suit.Spades,
            CurrentTrick = new[] { new PlayedCard(PlayerId.First, new Card(Suit.Hearts, Rank.Ace)) },
            Hands = new Dictionary<PlayerId, IReadOnlyList<Card>>
            {
                [PlayerId.First] = Array.Empty<Card>(),
                [PlayerId.Second] = new[]
                {
                    new Card(Suit.Hearts, Rank.Nine),
                    new Card(Suit.Spades, Rank.Ace)
                },
                [PlayerId.Third] = Array.Empty<Card>()
            }
        };
        var legalActions = new LegalActionProvider();

        var plays = legalActions.GetLegalActions(state, PlayerId.Second).OfType<PlayCardAction>().ToArray();

        Assert.Single(plays);
        Assert.Equal(new Card(Suit.Hearts, Rank.Nine), plays[0].Card);
    }

    [Fact]
    public void Reducer_CompletesTrickAndAwardsHandPointsToTrumpWinner()
    {
        var reducer = new GameReducer();
        var state = GameState.StartNewHand(1) with
        {
            Phase = GamePhase.PlayingTricks,
            CurrentPlayer = PlayerId.First,
            Trump = Suit.Spades,
            Hands = new Dictionary<PlayerId, IReadOnlyList<Card>>
            {
                [PlayerId.First] = new[] { new Card(Suit.Hearts, Rank.Ace) },
                [PlayerId.Second] = new[] { new Card(Suit.Hearts, Rank.Ten) },
                [PlayerId.Third] = new[] { new Card(Suit.Spades, Rank.Nine) }
            },
            HandScores = PlayerOrder.All.ToDictionary(player => player, _ => 0),
            GameScores = PlayerOrder.All.ToDictionary(player => player, _ => 0)
        };

        state = reducer.Apply(state, new PlayCardAction(PlayerId.First, new Card(Suit.Hearts, Rank.Ace)));
        state = reducer.Apply(state, new PlayCardAction(PlayerId.Third, new Card(Suit.Spades, Rank.Nine)));
        state = reducer.Apply(state, new PlayCardAction(PlayerId.Second, new Card(Suit.Hearts, Rank.Ten)));

        Assert.Equal(GamePhase.ScoringHand, state.Phase);
        Assert.Equal(PlayerId.Third, state.CompletedTricks.Single().Winner);
        Assert.Equal(21, state.HandScores[PlayerId.Third]);
        Assert.Equal(0, state.GameScores[PlayerId.Third]);
        Assert.Equal(PlayerId.Third, state.CurrentPlayer);
    }

    [Fact]
    public void Reducer_SettleHand_WhenBidderSucceeds_AddsAllHandScoresAndStartsNextHand()
    {
        var reducer = new GameReducer();
        var state = GameState.StartNewHand(1) with
        {
            Phase = GamePhase.ScoringHand,
            Bidder = PlayerId.First,
            WinningBid = 20,
            HandScores = new Dictionary<PlayerId, int>
            {
                [PlayerId.First] = 31,
                [PlayerId.Second] = 10,
                [PlayerId.Third] = 15
            },
            GameScores = PlayerOrder.All.ToDictionary(player => player, _ => 0)
        };

        state = reducer.Apply(state, new SettleHandAction(PlayerId.First));

        Assert.Equal(GamePhase.Bidding, state.Phase);
        Assert.Equal(31, state.GameScores[PlayerId.First]);
        Assert.Equal(10, state.GameScores[PlayerId.Second]);
        Assert.Equal(15, state.GameScores[PlayerId.Third]);
        Assert.Equal(PlayerId.First, state.Dealer);
        Assert.Equal(PlayerId.Third, state.CurrentPlayer);
    }

    [Fact]
    public void Reducer_SettleHand_WhenBidderFails_SubtractsBidAndAddsDefenderScores()
    {
        var reducer = new GameReducer();
        var state = GameState.StartNewHand(1) with
        {
            Phase = GamePhase.ScoringHand,
            Bidder = PlayerId.First,
            WinningBid = 100,
            HandScores = new Dictionary<PlayerId, int>
            {
                [PlayerId.First] = 31,
                [PlayerId.Second] = 10,
                [PlayerId.Third] = 15
            },
            GameScores = PlayerOrder.All.ToDictionary(player => player, _ => 0)
        };

        state = reducer.Apply(state, new SettleHandAction(PlayerId.First));

        Assert.Equal(-100, state.GameScores[PlayerId.First]);
        Assert.Equal(10, state.GameScores[PlayerId.Second]);
        Assert.Equal(15, state.GameScores[PlayerId.Third]);
    }

    [Fact]
    public void Reducer_SettleHand_WhenAnyPlayerReachesFiveHundredOne_EndsGame()
    {
        var reducer = new GameReducer();
        var state = GameState.StartNewHand(1) with
        {
            Phase = GamePhase.ScoringHand,
            Bidder = PlayerId.First,
            WinningBid = 20,
            HandScores = new Dictionary<PlayerId, int>
            {
                [PlayerId.First] = 31,
                [PlayerId.Second] = 10,
                [PlayerId.Third] = 15
            },
            GameScores = new Dictionary<PlayerId, int>
            {
                [PlayerId.First] = 480,
                [PlayerId.Second] = 0,
                [PlayerId.Third] = 0
            }
        };

        state = reducer.Apply(state, new SettleHandAction(PlayerId.First));

        Assert.Equal(GamePhase.GameOver, state.Phase);
        Assert.Equal(511, state.GameScores[PlayerId.First]);
    }

    [Fact]
    public void Reducer_RejectsOutOfTurnActions()
    {
        var reducer = new GameReducer();
        var state = GameState.StartNewHand(12);

        Assert.Throws<InvalidOperationException>(() => reducer.Apply(state, new BidAction(PlayerId.Second, 100)));
    }

    [Fact]
    public void Reducer_WhenThirdOpeningPlayerTriesToPass_Throws()
    {
        var reducer = new GameReducer();
        var state = GameState.StartNewHand(12);

        state = reducer.Apply(state, new BidAction(PlayerId.First, null));
        state = reducer.Apply(state, new BidAction(PlayerId.Third, null));

        Assert.Throws<InvalidOperationException>(() => reducer.Apply(state, new BidAction(PlayerId.Second, null)));
    }

    [Fact]
    public void Reducer_AnnounceMarriage_AddsTwentyOrFortyPointsOnce()
    {
        var reducer = new GameReducer();
        var state = GameState.StartNewHand(1) with
        {
            Phase = GamePhase.PlayingTricks,
            CurrentPlayer = PlayerId.First,
            Trump = Suit.Hearts,
            Hands = new Dictionary<PlayerId, IReadOnlyList<Card>>
            {
                [PlayerId.First] = new[]
                {
                    new Card(Suit.Hearts, Rank.King),
                    new Card(Suit.Hearts, Rank.Queen),
                    new Card(Suit.Clubs, Rank.King),
                    new Card(Suit.Clubs, Rank.Queen)
                },
                [PlayerId.Second] = Array.Empty<Card>(),
                [PlayerId.Third] = Array.Empty<Card>()
            }
        };

        state = reducer.Apply(state, new AnnounceMarriageAction(PlayerId.First, Suit.Hearts));
        state = reducer.Apply(state, new AnnounceMarriageAction(PlayerId.First, Suit.Clubs));

        Assert.Equal(60, state.HandScores[PlayerId.First]);
        Assert.Contains(state.MarriageAnnouncements, announcement => announcement.Suit == Suit.Hearts && announcement.Points == 40);
        Assert.Contains(state.MarriageAnnouncements, announcement => announcement.Suit == Suit.Clubs && announcement.Points == 20);
        Assert.Throws<InvalidOperationException>(() => reducer.Apply(state, new AnnounceMarriageAction(PlayerId.First, Suit.Hearts)));
    }

    [Fact]
    public void LegalActions_AllowMarriageOnlyWhenLeadingTrick()
    {
        var state = GameState.StartNewHand(1) with
        {
            Phase = GamePhase.PlayingTricks,
            CurrentPlayer = PlayerId.Third,
            Trump = Suit.Spades,
            Hands = new Dictionary<PlayerId, IReadOnlyList<Card>>
            {
                [PlayerId.First] = Array.Empty<Card>(),
                [PlayerId.Second] = Array.Empty<Card>(),
                [PlayerId.Third] = new[] { new Card(Suit.Spades, Rank.King), new Card(Suit.Spades, Rank.Queen) }
            },
            CurrentTrick = Array.Empty<PlayedCard>()
        };

        var legal = new LegalActionProvider().GetLegalActions(state, PlayerId.Third);
        Assert.Contains(legal, action => action is AnnounceMarriageAction);

        state = state with
        {
            CurrentTrick = new[] { new PlayedCard(PlayerId.Second, new Card(Suit.Hearts, Rank.Nine)) }
        };

        legal = new LegalActionProvider().GetLegalActions(state, PlayerId.Third);
        Assert.DoesNotContain(legal, action => action is AnnounceMarriageAction);
    }

    [Fact]
    public void LegalActions_SignalTrupa_WhenDefenderCanWinAndPartnerHasNotPlayed()
    {
        var reducer = new GameReducer();
        var state = GameState.StartNewHand(1) with
        {
            Phase = GamePhase.PlayingTricks,
            Bidder = PlayerId.First,
            CurrentPlayer = PlayerId.Second,
            Trump = Suit.Spades,
            CurrentTrick = new[] { new PlayedCard(PlayerId.First, new Card(Suit.Hearts, Rank.Ace)) },
            Hands = new Dictionary<PlayerId, IReadOnlyList<Card>>
            {
                [PlayerId.First] = Array.Empty<Card>(),
                [PlayerId.Second] = new[] { new Card(Suit.Spades, Rank.Nine), new Card(Suit.Clubs, Rank.Nine) },
                [PlayerId.Third] = new[] { new Card(Suit.Clubs, Rank.Ace) }
            }
        };

        var legal = new LegalActionProvider().GetLegalActions(state, PlayerId.Second);
        Assert.Contains(legal, action => action is SignalTrupaAction);

        state = reducer.Apply(state, new SignalTrupaAction(PlayerId.Second));

        Assert.Single(state.TrupaSignals);
        Assert.Equal(PlayerId.Second, state.TrupaSignals[0].Player);
        Assert.Equal(1, state.TrupaSignals[0].TrickNumber);
    }

    [Fact]
    public void LegalActions_SignalTrupa_HiddenWithOnlyOneCardRemaining()
    {
        var state = GameState.StartNewHand(1) with
        {
            Phase = GamePhase.PlayingTricks,
            Bidder = PlayerId.First,
            CurrentPlayer = PlayerId.Second,
            Trump = Suit.Spades,
            CurrentTrick = new[] { new PlayedCard(PlayerId.First, new Card(Suit.Hearts, Rank.Ace)) },
            Hands = new Dictionary<PlayerId, IReadOnlyList<Card>>
            {
                [PlayerId.First] = Array.Empty<Card>(),
                [PlayerId.Second] = new[] { new Card(Suit.Spades, Rank.Nine) },
                [PlayerId.Third] = new[] { new Card(Suit.Clubs, Rank.Ace) }
            }
        };

        var legal = new LegalActionProvider().GetLegalActions(state, PlayerId.Second);

        Assert.DoesNotContain(legal, action => action is SignalTrupaAction);
    }

    [Fact]
    public void LegalActions_SignalTrupa_HiddenWhenPartnerAlreadyPlayed()
    {
        var state = GameState.StartNewHand(1) with
        {
            Phase = GamePhase.PlayingTricks,
            Bidder = PlayerId.First,
            CurrentPlayer = PlayerId.Second,
            Trump = Suit.Spades,
            CurrentTrick = new[]
            {
                new PlayedCard(PlayerId.First, new Card(Suit.Hearts, Rank.Ace)),
                new PlayedCard(PlayerId.Third, new Card(Suit.Clubs, Rank.Ace))
            },
            Hands = new Dictionary<PlayerId, IReadOnlyList<Card>>
            {
                [PlayerId.First] = Array.Empty<Card>(),
                [PlayerId.Second] = new[] { new Card(Suit.Spades, Rank.Nine) },
                [PlayerId.Third] = Array.Empty<Card>()
            }
        };

        var legal = new LegalActionProvider().GetLegalActions(state, PlayerId.Second);
        Assert.DoesNotContain(legal, action => action is SignalTrupaAction);
    }

    [Fact]
    public void Reducer_ResignHand_GivesDefendersTwentyFiveAndSubtractsBid()
    {
        var reducer = new GameReducer();
        var state = StartPlayingTricks(reducer, Suit.Hearts);

        state = reducer.Apply(state, new ResignHandAction(PlayerId.First));
        Assert.Equal(GamePhase.ScoringHand, state.Phase);
        Assert.Equal(25, state.HandScores[PlayerId.Second]);
        Assert.Equal(25, state.HandScores[PlayerId.Third]);
        Assert.Equal(-100, state.Settlement.ScoreDeltas[PlayerId.First]);
        Assert.Equal(25, state.Settlement.ScoreDeltas[PlayerId.Second]);
        Assert.Equal(25, state.Settlement.ScoreDeltas[PlayerId.Third]);

        state = reducer.Apply(state, new SettleHandAction(PlayerId.First));
        Assert.Equal(-100, state.GameScores[PlayerId.First]);
        Assert.Equal(25, state.GameScores[PlayerId.Second]);
        Assert.Equal(25, state.GameScores[PlayerId.Third]);
    }

    [Fact]
    public void LegalActions_RequireTrumpWhenVoid()
    {
        var state = GameState.StartNewHand(1) with
        {
            Phase = GamePhase.PlayingTricks,
            CurrentPlayer = PlayerId.Second,
            Trump = Suit.Spades,
            CurrentTrick = new[] { new PlayedCard(PlayerId.First, new Card(Suit.Hearts, Rank.Ace)) },
            Hands = new Dictionary<PlayerId, IReadOnlyList<Card>>
            {
                [PlayerId.First] = Array.Empty<Card>(),
                [PlayerId.Second] = new[]
                {
                    new Card(Suit.Clubs, Rank.Nine),
                    new Card(Suit.Spades, Rank.Ace)
                },
                [PlayerId.Third] = Array.Empty<Card>()
            }
        };

        var plays = new LegalActionProvider().GetLegalActions(state, PlayerId.Second).OfType<PlayCardAction>().ToArray();

        Assert.Single(plays);
        Assert.Equal(new Card(Suit.Spades, Rank.Ace), plays[0].Card);
    }

    [Fact]
    public void LegalActions_RequireOvertrumpOnlyWhenPlayingTrump()
    {
        var state = GameState.StartNewHand(1) with
        {
            Phase = GamePhase.PlayingTricks,
            CurrentPlayer = PlayerId.Second,
            Trump = Suit.Spades,
            CurrentTrick = new[] { new PlayedCard(PlayerId.First, new Card(Suit.Spades, Rank.Ten)) },
            Hands = new Dictionary<PlayerId, IReadOnlyList<Card>>
            {
                [PlayerId.First] = Array.Empty<Card>(),
                [PlayerId.Second] = new[]
                {
                    new Card(Suit.Spades, Rank.Nine),
                    new Card(Suit.Spades, Rank.Ace),
                    new Card(Suit.Clubs, Rank.Ace)
                },
                [PlayerId.Third] = Array.Empty<Card>()
            }
        };

        var plays = new LegalActionProvider().GetLegalActions(state, PlayerId.Second).OfType<PlayCardAction>().ToArray();

        Assert.Single(plays);
        Assert.Equal(new Card(Suit.Spades, Rank.Ace), plays[0].Card);
    }

    [Fact]
    public void LegalActions_DoNotRequireOvertakingNonTrumpSuit()
    {
        var state = GameState.StartNewHand(1) with
        {
            Phase = GamePhase.PlayingTricks,
            CurrentPlayer = PlayerId.Second,
            Trump = Suit.Spades,
            CurrentTrick = new[] { new PlayedCard(PlayerId.First, new Card(Suit.Hearts, Rank.Ten)) },
            Hands = new Dictionary<PlayerId, IReadOnlyList<Card>>
            {
                [PlayerId.First] = Array.Empty<Card>(),
                [PlayerId.Second] = new[]
                {
                    new Card(Suit.Hearts, Rank.Nine),
                    new Card(Suit.Hearts, Rank.Ace)
                },
                [PlayerId.Third] = Array.Empty<Card>()
            }
        };

        var plays = new LegalActionProvider().GetLegalActions(state, PlayerId.Second).OfType<PlayCardAction>().ToArray();

        Assert.Equal(2, plays.Length);
        Assert.Contains(plays, action => action.Card == new Card(Suit.Hearts, Rank.Nine));
        Assert.Contains(plays, action => action.Card == new Card(Suit.Hearts, Rank.Ace));
    }

    [Fact]
    public void LegalActions_NoTrumpAllowsAnyCardWhenVoid()
    {
        var state = GameState.StartNewHand(1) with
        {
            Phase = GamePhase.PlayingTricks,
            CurrentPlayer = PlayerId.Second,
            Trump = null,
            CurrentTrick = new[] { new PlayedCard(PlayerId.First, new Card(Suit.Hearts, Rank.Ten)) },
            Hands = new Dictionary<PlayerId, IReadOnlyList<Card>>
            {
                [PlayerId.First] = Array.Empty<Card>(),
                [PlayerId.Second] = new[]
                {
                    new Card(Suit.Clubs, Rank.Nine),
                    new Card(Suit.Spades, Rank.Ace)
                },
                [PlayerId.Third] = Array.Empty<Card>()
            }
        };

        var plays = new LegalActionProvider().GetLegalActions(state, PlayerId.Second).OfType<PlayCardAction>().ToArray();

        Assert.Equal(2, plays.Length);
    }

    [Fact]
    public void SavedGame_RestoresStateFromInitialSeedAndActionLog()
    {
        var reducer = new GameReducer();
        var state = GameState.StartNewHand(12);
        state = reducer.Apply(state, new BidAction(PlayerId.First, 100));
        state = reducer.Apply(state, new BidAction(PlayerId.Third, null));
        state = reducer.Apply(state, new BidAction(PlayerId.Second, null));
        state = reducer.Apply(state, new RevealTalonAction(PlayerId.First));
        state = reducer.Apply(state, new ChooseTrumpAction(PlayerId.First, Suit.Hearts));

        var restored = SavedGame.FromState(state).Restore();

        Assert.Equal(state.Phase, restored.Phase);
        Assert.Equal(state.InitialSeed, restored.InitialSeed);
        Assert.Equal(state.Seed, restored.Seed);
        Assert.Equal(state.ActionLog, restored.ActionLog);
        Assert.Equal(state.GetHand(PlayerId.First), restored.GetHand(PlayerId.First));
        Assert.Equal(state.Talon, restored.Talon);
        Assert.Equal(state.Trump, restored.Trump);
    }

    private static GameState WinOpeningBid(GameReducer reducer)
    {
        var state = GameState.StartNewHand(12);
        state = reducer.Apply(state, new BidAction(PlayerId.First, 100));
        state = reducer.Apply(state, new BidAction(PlayerId.Third, null));
        return reducer.Apply(state, new BidAction(PlayerId.Second, null));
    }

    private static GameState WithHand(GameState state, PlayerId player, IReadOnlyList<Card> hand)
    {
        var hands = state.Hands.ToDictionary(pair => pair.Key, pair => pair.Value);
        hands[player] = hand;
        return state with { Hands = hands };
    }

    private static GameState StartPlayingTricks(GameReducer reducer, Suit trump)
    {
        var state = WinOpeningBid(reducer);
        state = reducer.Apply(state, new RevealTalonAction(PlayerId.First));
        state = reducer.Apply(state, new ChooseTrumpAction(PlayerId.First, trump));

        var firstPass = state.GetHand(PlayerId.First)[0];
        state = reducer.Apply(state, new PassCardAction(PlayerId.First, PlayerId.Second, firstPass));
        var secondPass = state.GetHand(PlayerId.First)[0];
        return reducer.Apply(state, new PassCardAction(PlayerId.First, PlayerId.Third, secondPass));
    }
}

using RuskaTrupa.AI;
using RuskaTrupa.Core.Cards;
using RuskaTrupa.Core.Game;

namespace RuskaTrupa.AI.Tests;

public sealed class AiBenchmarkTests
{
    [Fact]
    public void HeuristicAgent_OutscoresSimpleBaselineAcrossRotatedSeats()
    {
        var totalHeroDelta = 0;
        var totalOpponentDelta = 0;
        var settledHands = 0;

        for (var seed = 700; seed < 790; seed++)
        {
            var hero = PlayerOrder.All[seed % PlayerOrder.All.Count];
            var deltas = PlaySettledHand(seed, hero);
            totalHeroDelta += deltas[hero];
            totalOpponentDelta += deltas.Where(pair => pair.Key != hero).Sum(pair => pair.Value);
            settledHands++;
        }

        var heroAverage = totalHeroDelta / (double)settledHands;
        var opponentAverage = totalOpponentDelta / (double)(settledHands * 2);
        Assert.True(heroAverage > opponentAverage + 7.5, $"hero={heroAverage:0.0}, opponents={opponentAverage:0.0}");
    }

    [Fact]
    public void HeuristicAgents_CanAutoplayManyHandsWithoutIllegalActions()
    {
        for (var seed = 900; seed < 1_050; seed++)
        {
            PlayAutopilotHand(seed);
        }
    }

    private static IReadOnlyDictionary<PlayerId, int> PlaySettledHand(int seed, PlayerId heuristicPlayer)
    {
        var reducer = new GameReducer();
        var legal = new LegalActionProvider();
        var heuristic = new HeuristicPlayerAgent();
        var state = GameState.StartNewHand(seed);
        var random = new Random(seed * 17 + heuristicPlayer.Value);
        var pendingActions = new Queue<GameAction>();

        for (var guard = 0; guard < 2_000; guard++)
        {
            var action = pendingActions.Count > 0
                ? pendingActions.Dequeue()
                : ChooseAction(state, heuristicPlayer, heuristic, legal, random);
            action = ExpandMarriageLead(state, legal, action, pendingActions);
            var previousScores = state.GameScores;
            var next = reducer.Apply(state, action);
            if (action is SettleHandAction)
            {
                return PlayerOrder.All.ToDictionary(player => player, player => next.GameScores[player] - previousScores[player]);
            }

            state = next;
        }

        throw new InvalidOperationException($"Benchmark hand did not settle for seed {seed} and hero {heuristicPlayer}.");
    }

    private static void PlayAutopilotHand(int seed)
    {
        var reducer = new GameReducer();
        var legal = new LegalActionProvider();
        var agents = PlayerOrder.All.ToDictionary(
            player => player,
            player => new HeuristicPlayerAgent(
                BotSkillLevel.Advanced,
                player.Value switch
                {
                    2 => BotPlayStyle.Aggressive,
                    3 => BotPlayStyle.Cautious,
                    _ => BotPlayStyle.Balanced
                }));
        var state = GameState.StartNewHand(seed);

        for (var guard = 0; guard < 2_000; guard++)
        {
            var player = state.Phase == GamePhase.ScoringHand ? PlayerId.First : state.CurrentPlayer;
            var actions = legal.GetLegalActions(state, player);
            var action = ChooseHeuristicAction(state, agents[player], actions);
            action = ExpandMarriageLead(state, legal, action, new Queue<GameAction>());
            state = reducer.Apply(state, action);
            if (action is SettleHandAction)
            {
                return;
            }
        }

        throw new InvalidOperationException($"Autopilot hand did not settle for seed {seed}.");
    }

    private static GameAction ExpandMarriageLead(
        GameState state,
        LegalActionProvider legal,
        GameAction action,
        Queue<GameAction> pendingActions)
    {
        if (action is not PlayCardAction play
            || play.Card.Rank is not (Rank.King or Rank.Queen))
        {
            return action;
        }

        var marriage = legal.GetLegalActions(state, play.Player)
            .OfType<AnnounceMarriageAction>()
            .FirstOrDefault(announcement => announcement.Suit == play.Card.Suit);
        if (marriage is null)
        {
            return action;
        }

        pendingActions.Enqueue(action);
        return marriage;
    }

    private static GameAction ChooseAction(
        GameState state,
        PlayerId heuristicPlayer,
        HeuristicPlayerAgent heuristic,
        LegalActionProvider legal,
        Random random)
    {
        var player = state.Phase == GamePhase.ScoringHand ? heuristicPlayer : state.CurrentPlayer;
        var actions = legal.GetLegalActions(state, player);
        if (player == heuristicPlayer)
        {
            return ChooseHeuristicAction(state, heuristic, actions);
        }

        return ChooseBaselineAction(state, actions, random);
    }

    private static GameAction ChooseHeuristicAction(GameState state, HeuristicPlayerAgent heuristic, IReadOnlyList<GameAction> actions)
    {
        var player = state.Phase == GamePhase.ScoringHand ? actions[0].Player : state.CurrentPlayer;
        var observation = state.CreateObservation(player);
        return state.Phase switch
        {
            GamePhase.Bidding => ChooseBid(actions.OfType<BidAction>().ToArray(), heuristic.DecideOpeningBid(observation, actions.OfType<BidAction>().Select(action => action.Amount ?? 0).ToArray()).Action),
            GamePhase.RevealTalon => actions.OfType<RevealTalonAction>().First(),
            GamePhase.ChooseTrump => ChooseTrump(actions.OfType<ChooseTrumpAction>().ToArray(), heuristic.DecideTrump(observation, actions.OfType<ChooseTrumpAction>().Select(action => action.Trump).ToArray()).Action),
            GamePhase.PassCards => ChoosePass(actions.OfType<PassCardAction>().ToArray(), heuristic.DecideCardToPass(observation, actions.OfType<PassCardAction>().Select(action => action.Card).ToArray()).Action),
            GamePhase.PlayingTricks => ChoosePlay(state, heuristic, actions),
            GamePhase.ScoringHand => actions.OfType<SettleHandAction>().First(),
            _ => actions[0]
        };
    }

    private static GameAction ChoosePlay(GameState state, HeuristicPlayerAgent heuristic, IReadOnlyList<GameAction> actions)
    {
        if (ShouldSignalTrupa(state, actions) is { } trupa)
        {
            return trupa;
        }

        var player = state.CurrentPlayer;
        var playActions = actions.OfType<PlayCardAction>().ToArray();
        var decision = heuristic.DecideCard(state.CreateObservation(player), playActions.Select(action => action.Card).ToArray());
        return playActions.First(action => action.Card == decision.Action);
    }

    private static GameAction ChooseBaselineAction(GameState state, IReadOnlyList<GameAction> actions, Random random)
    {
        return state.Phase switch
        {
            GamePhase.Bidding => ChooseBaselineBid(actions.OfType<BidAction>().ToArray(), random),
            GamePhase.RevealTalon => actions.OfType<RevealTalonAction>().First(),
            GamePhase.ChooseTrump => actions.OfType<ChooseTrumpAction>().OrderBy(_ => random.Next()).First(),
            GamePhase.PassCards => actions.OfType<PassCardAction>().OrderBy(action => action.Card.PointValue).ThenBy(action => action.Card.Strength).First(),
            GamePhase.PlayingTricks => ChooseBaselinePlay(actions),
            GamePhase.ScoringHand => actions.OfType<SettleHandAction>().First(),
            _ => actions[0]
        };
    }

    private static BidAction ChooseBaselineBid(IReadOnlyList<BidAction> actions, Random random)
    {
        var pass = actions.FirstOrDefault(action => action.Amount is null);
        var bids = actions.Where(action => action.Amount is not null).OrderBy(action => action.Amount).ToArray();
        if (pass is not null && (bids.Length == 0 || random.NextDouble() < 0.72))
        {
            return pass;
        }

        return bids[0];
    }

    private static PlayCardAction ChooseBaselinePlay(IReadOnlyList<GameAction> actions)
    {
        return actions.OfType<PlayCardAction>()
            .OrderBy(action => action.Card.PointValue)
            .ThenBy(action => action.Card.Strength)
            .First();
    }

    private static BidAction ChooseBid(IReadOnlyList<BidAction> actions, int amount)
    {
        return actions.FirstOrDefault(action => action.Amount == amount)
            ?? actions.FirstOrDefault(action => action.Amount is null)
            ?? actions.OrderBy(action => action.Amount).First();
    }

    private static ChooseTrumpAction ChooseTrump(IReadOnlyList<ChooseTrumpAction> actions, Suit? trump)
    {
        return actions.First(action => action.Trump == trump);
    }

    private static PassCardAction ChoosePass(IReadOnlyList<PassCardAction> actions, Card card)
    {
        return actions.First(action => action.Card == card);
    }

    private static SignalTrupaAction? ShouldSignalTrupa(GameState state, IReadOnlyList<GameAction> actions)
    {
        if (state.Bidder is null || state.Bidder == state.CurrentPlayer)
        {
            return null;
        }

        var signal = actions.OfType<SignalTrupaAction>().FirstOrDefault();
        if (signal is null)
        {
            return null;
        }

        var partner = PlayerOrder.All.Single(player => player != state.CurrentPlayer && player != state.Bidder.Value);
        var partnerHasPoints = state.GetHand(partner).Any(card => card.PointValue >= 10);
        return state.CurrentTrick.Sum(played => played.Card.PointValue) >= 10 || partnerHasPoints ? signal : null;
    }
}

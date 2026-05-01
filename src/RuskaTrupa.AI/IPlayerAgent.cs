using RuskaTrupa.Core.Cards;
using RuskaTrupa.Core.Game;

namespace RuskaTrupa.AI;

public interface IPlayerAgent
{
    AgentDecision<int> DecideOpeningBid(GameObservation observation, IReadOnlyList<int> legalBids);
    AgentDecision<Suit?> DecideTrump(GameObservation observation, IReadOnlyList<Suit?> legalTrumpSuits);
    AgentDecision<Card> DecideCardToPass(GameObservation observation, IReadOnlyList<Card> legalCards);
    AgentDecision<Card> DecideCard(GameObservation observation, IReadOnlyList<Card> legalCards);
}

public sealed record AgentDecision<T>(T Action, double Confidence, string ReasonCode);

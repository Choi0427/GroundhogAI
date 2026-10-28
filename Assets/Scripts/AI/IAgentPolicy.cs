namespace GroundhogAI
{
    // A learned policy can replace this interface without changing simulation rules.
    public interface IAgentPolicy
    {
        AgentAction Decide(Observation observation);
    }
}

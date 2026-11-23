internal sealed class Agent
{
    // Creation time prevents an old UI action from targeting a reused process ID.
    internal int Id;
    internal long Created;
}

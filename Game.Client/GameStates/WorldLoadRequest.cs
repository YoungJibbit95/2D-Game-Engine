namespace Game.Client.GameStates;

public sealed record WorldLoadRequest(
    string Mode,
    int Seed,
    string WorldName,
    int LocalPlayerCount,
    string? RemoteEndpoint = null)
{
    public static WorldLoadRequest Singleplayer(int seed)
    {
        return new WorldLoadRequest("SINGLEPLAYER", seed, "New World", LocalPlayerCount: 1);
    }

    public static WorldLoadRequest SplitScreen(int seed, int localPlayerCount)
    {
        return new WorldLoadRequest("COOP SPLITSCREEN", seed, "Shared Local World", Math.Max(2, localPlayerCount));
    }

    public static WorldLoadRequest MultiplayerClient(string endpoint)
    {
        return new WorldLoadRequest("MULTIPLAYER", 0, "Remote World", LocalPlayerCount: 1, endpoint);
    }
}

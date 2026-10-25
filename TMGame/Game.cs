namespace TMGame;

public enum Phase { Lobby, Night, Day, Finished }
public enum Role { Villager, Mafia }
public sealed record PlayerView(int Id, string Name, bool Alive, bool Connected);
public sealed record View(Phase Phase, int Revision, int? Host, string? Winner,
    PlayerView[] Players, Role? YourRole, bool CanAct);

// Pure state machine. The transport serializes all access and supplies connection identity.
public sealed class Game
{
    private sealed class Player(int id, string name)
    {
        public int Id = id;
        public string Name = name;
        public bool Alive = true, Connected = true;
        public Role Role;
    }
    private readonly List<Player> players = [];
    private readonly Dictionary<int, int> votes = [];
    private readonly Random random;
    private int nextId;
    public int Capacity { get; }
    public Phase Phase { get; private set; }
    public int Revision { get; private set; }
    public string? Winner { get; private set; }
    public int? Host => players.FirstOrDefault(p => p.Connected)?.Id;
    public Game(int capacity, Random? random = null)
    {
        if (capacity is < 3 or > 16) throw new ArgumentException("Players must be 3–16.");
        Capacity = capacity;
        this.random = random ?? Random.Shared;
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    public int Join(string? name)
    {
        Require(Phase == Phase.Lobby, "Game is in progress; join after restart.");
        Require(players.Count < Capacity, "Lobby is full.");
        Require(name is { Length: >= 1 and <= 24 } && name.All(char.IsAsciiLetterOrDigit), "Name must be 1–24 ASCII letters or digits.");
        Require(!players.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)), "Name is already in use.");
        var player = new Player(++nextId, name!);
        players.Add(player);
        return player.Id;
    }
    public View ViewFor(int id)
    {
        var self = players.Single(p => p.Id == id);
        return new(Phase, Revision, Host, Winner,
            players.Select(p => new PlayerView(p.Id, p.Name, p.Alive, p.Connected)).ToArray(),
            Phase == Phase.Lobby ? null : self.Role,
            self.Connected && self.Alive && !votes.ContainsKey(id) &&
            (Phase == Phase.Day || Phase == Phase.Night && self.Role == Role.Mafia));
    }
    public void Act(int id, string? command, int? target, int revision)
    {
        var actor = players.SingleOrDefault(p => p.Id == id && p.Connected);
        Require(actor != null, "Not joined.");
        Require(revision == Revision, "Stale phase; use the latest state.");
        if (command == "start")
        {
            Require(id == Host && Phase == Phase.Lobby, "Only the host can start from the lobby.");
            Require(players.Count == Capacity, "Wait for the configured number of players.");
            foreach (var p in players) p.Role = Role.Villager;
            players[random.Next(players.Count)].Role = Role.Mafia;
            Advance(Phase.Night);
            return;
        }
        if (command == "restart")
        {
            Require(id == Host && Phase == Phase.Finished, "Only the host can restart a finished game.");
            Reset();
            return;
        }
        Require(actor!.Alive, "Eliminated players cannot act.");
        Require(command == "kill" && Phase == Phase.Night && actor.Role == Role.Mafia ||
                command == "vote" && Phase == Phase.Day, "Invalid or out-of-turn action.");
        Require(!votes.ContainsKey(id), "Already acted this phase.");
        Require(players.Any(p => p.Id == target && p.Alive && p.Connected && p.Id != id), "Choose another living player.");
        votes.Add(id, target!.Value);
        Resolve();
    }
    public void Disconnect(int id)
    {
        var player = players.SingleOrDefault(p => p.Id == id);
        if (player == null || !player.Connected) return;
        player.Connected = false;
        player.Alive = false;
        if (Phase == Phase.Lobby) players.Remove(player);
        if (!players.Any(p => p.Connected)) { Reset(); return; }
        if (Phase is Phase.Night or Phase.Day)
        {
            // A departed voter/target cannot stall resolution; affected voters can vote again.
            foreach (var voter in votes.Where(v => v.Key == id || v.Value == id).Select(v => v.Key).ToArray()) votes.Remove(voter);
            if (!CheckWin()) Resolve();
        }
    }
    private void Resolve()
    {
        var required = players.Where(p => p.Alive && (Phase == Phase.Day || p.Role == Role.Mafia)).ToArray();
        if (!required.All(p => votes.ContainsKey(p.Id))) return;
        var tally = votes.Values.GroupBy(id => id).OrderByDescending(g => g.Count()).ToArray();
        if (tally.Length > 0 && (tally.Length == 1 || tally[0].Count() > tally[1].Count()))
            players.Single(p => p.Id == tally[0].Key).Alive = false;
        if (!CheckWin()) Advance(Phase == Phase.Night ? Phase.Day : Phase.Night);
    }
    private bool CheckWin()
    {
        int mafia = players.Count(p => p.Alive && p.Role == Role.Mafia);
        int town = players.Count(p => p.Alive && p.Role == Role.Villager);
        Winner = mafia == 0 ? "Villagers" : mafia >= town ? "Mafia" : null;
        if (Winner == null) return false;
        Advance(Phase.Finished);
        return true;
    }
    private void Advance(Phase phase) { Phase = phase; Revision++; votes.Clear(); }
    private void Reset()
    {
        players.RemoveAll(p => !p.Connected);
        foreach (var p in players) { p.Alive = true; p.Role = Role.Villager; }
        Winner = null;
        Advance(Phase.Lobby);
    }
}

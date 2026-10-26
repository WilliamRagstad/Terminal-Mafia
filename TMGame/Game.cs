namespace TMGame;

public enum Phase { Lobby, Night, Day, Finished }
public enum Role { Villager, Mafia, Police, Doctor, Detective }
public sealed record PlayerView(int Id, string Name, bool Alive, bool Connected);
public sealed record View(Phase Phase, int Revision, int? Host, string? Winner,
    PlayerView[] Players, Role? YourRole, bool CanAct, GameSettings Settings,
    string? Action, int[] MafiaPartners, string[] PrivateReports);

// Pure state machine. The transport serializes all access and supplies connection identity.
public sealed class Game
{
    private sealed class Player(int id, string name)
    {
        public int Id = id;
        public string Name = name;
        public bool Alive = true, Connected = true;
        public Role Role;
        public List<string> Reports = [];
    }
    private readonly List<Player> players = [];
    private readonly Dictionary<int, int> votes = [];
    private readonly Random random;
    private int nextId;
    public GameSettings Settings { get; }
    public int Capacity => Settings.Players;
    public Phase Phase { get; private set; }
    public int Revision { get; private set; }
    public string? Winner { get; private set; }
    public int? Host => players.FirstOrDefault(p => p.Connected)?.Id;
    public Game(int capacity, Random? random = null) : this(GameSettings.Default(capacity), random) { }
    public Game(GameSettings settings, Random? random = null)
    {
        Settings = settings.Validate();
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
            (Phase == Phase.Day || Phase == Phase.Night && self.Role != Role.Villager),
            Settings, Phase == Phase.Day ? "vote" : Phase == Phase.Night ? NightCommand(self.Role) : null,
            Phase != Phase.Lobby && self.Role == Role.Mafia
                ? players.Where(p => p.Role == Role.Mafia && p.Id != id).Select(p => p.Id).ToArray() : [],
            self.Reports.ToArray());
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
            var roles = Enumerable.Repeat(Role.Mafia, Settings.Mafia)
                .Concat(Enumerable.Repeat(Role.Police, Settings.Police))
                .Concat(Enumerable.Repeat(Role.Doctor, Settings.Doctors))
                .Concat(Enumerable.Repeat(Role.Detective, Settings.Detectives))
                .Concat(Enumerable.Repeat(Role.Villager, Settings.Villagers)).ToArray();
            random.Shuffle(roles);
            for (int i = 0; i < players.Count; i++) players[i].Role = roles[i];
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
        Require(command != null && command == NightCommand(actor.Role) && Phase == Phase.Night ||
                command == "vote" && Phase == Phase.Day, "Invalid or out-of-turn action.");
        Require(!votes.ContainsKey(id), "Already acted this phase.");
        var victim = players.SingleOrDefault(p => p.Id == target && p.Alive && p.Connected);
        Require(victim != null && (victim.Id != id || Phase == Phase.Night && actor.Role == Role.Doctor), "Choose another living player (Doctors may protect themselves).");
        Require(!(Phase == Phase.Night && actor.Role == Role.Mafia && victim!.Role == Role.Mafia), "Mafia cannot target their teammates.");
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
    private static string? NightCommand(Role role) => role switch
    {
        Role.Mafia => "kill", Role.Police => "block", Role.Doctor => "protect",
        Role.Detective => "investigate", _ => null
    };
    private static int? Plurality(IEnumerable<int> choices)
    {
        var tally = choices.GroupBy(id => id).OrderByDescending(g => g.Count()).ToArray();
        return tally.Length > 0 && (tally.Length == 1 || tally[0].Count() > tally[1].Count()) ? tally[0].Key : null;
    }
    private void Resolve()
    {
        var required = players.Where(p => p.Alive && (Phase == Phase.Day || p.Role != Role.Villager)).ToArray();
        if (!required.All(p => votes.ContainsKey(p.Id))) return;
        int? victim;
        if (Phase == Phase.Day) victim = Plurality(votes.Values);
        else
        {
            // Police orders resolve simultaneously, then other unblocked actions.
            var blocked = required.Where(p => p.Role == Role.Police).Select(p => votes[p.Id]).ToHashSet();
            var active = required.Where(p => !blocked.Contains(p.Id)).ToArray();
            var protectedIds = active.Where(p => p.Role == Role.Doctor).Select(p => votes[p.Id]).ToHashSet();
            victim = Plurality(active.Where(p => p.Role == Role.Mafia).Select(p => votes[p.Id]));
            if (victim.HasValue && protectedIds.Contains(victim.Value)) victim = null;
            foreach (var detective in required.Where(p => p.Role == Role.Detective))
            {
                var target = players.Single(p => p.Id == votes[detective.Id]);
                detective.Reports.Add(blocked.Contains(detective.Id)
                    ? $"Night revision {Revision}: investigation blocked."
                    : $"Night revision {Revision}: #{target.Id} {target.Name} is {(target.Role == Role.Mafia ? "Mafia" : "not Mafia")}.");
                // Keep snapshots bounded even in a long game with no eliminations.
                if (detective.Reports.Count > 16) detective.Reports.RemoveAt(0);
            }
        }
        if (victim.HasValue) players.Single(p => p.Id == victim.Value).Alive = false;
        if (!CheckWin()) Advance(Phase == Phase.Night ? Phase.Day : Phase.Night);
    }
    private bool CheckWin()
    {
        int mafia = players.Count(p => p.Alive && p.Role == Role.Mafia);
        int town = players.Count(p => p.Alive && p.Role != Role.Mafia);
        Winner = mafia == 0 ? "Villagers" : mafia >= town ? "Mafia" : null;
        if (Winner == null) return false;
        Advance(Phase.Finished);
        return true;
    }
    private void Advance(Phase phase) { Phase = phase; Revision++; votes.Clear(); }
    private void Reset()
    {
        players.RemoveAll(p => !p.Connected);
        foreach (var p in players) { p.Alive = true; p.Role = Role.Villager; p.Reports.Clear(); }
        Winner = null;
        Advance(Phase.Lobby);
    }
}

using TMGame;

static void Check(bool value) { if (!value) throw new Exception("Assertion failed"); }
static void Reject(Action action) { try { action(); } catch (InvalidOperationException) { return; } throw new Exception("Expected rejection"); }
static (Game g, int[] ids, int mafia) Started(int count = 5)
{
    var g = new Game(count, new Random(42));
    var ids = Enumerable.Range(0, count).Select(i => g.Join("P" + i)).ToArray();
    Reject(() => g.Act(ids[1], "start", null, g.Revision));
    g.Act(ids[0], "start", null, g.Revision);
    return (g, ids, ids.Single(i => g.ViewFor(i).YourRole == Role.Mafia));
}
foreach (bool townWins in new[] { true, false })
{
    var (g, ids, mafia) = Started();
    int victim = ids.First(i => i != mafia);
    Reject(() => g.Act(victim, "kill", mafia, g.Revision));
    Reject(() => g.Act(mafia, "vote", victim, g.Revision));
    Reject(() => g.Act(mafia, "kill", mafia, g.Revision));
    Reject(() => g.Act(mafia, "kill", 999, g.Revision));
    Reject(() => g.Act(mafia, "kill", victim, -1));
    g.Act(mafia, "kill", victim, g.Revision);
    Reject(() => g.Act(victim, "vote", mafia, g.Revision));
    var living = g.ViewFor(mafia).Players.Where(p => p.Alive).Select(p => p.Id).ToArray();
    int target = townWins ? mafia : living.First(i => i != mafia);
    int rev = g.Revision;
    foreach (int id in living) g.Act(id, "vote", id == target ? living.First(i => i != target) : target, rev);
    if (!townWins) g.Act(mafia, "kill", g.ViewFor(mafia).Players.First(p => p.Alive && p.Id != mafia).Id, g.Revision);
    Check(g.Winner == (townWins ? "Villagers" : "Mafia"));
    Reject(() => g.Act(mafia, "kill", victim, g.Revision));
    g.Act(ids[0], "restart", null, g.Revision);
    Check(g.Phase == Phase.Lobby && g.ViewFor(ids[0]).YourRole == null && g.ViewFor(ids[0]).Players.All(p => p.Alive));
    g.Act(ids[0], "start", null, g.Revision);
    Check(g.Phase == Phase.Night);
}
{
    var (g, ids, mafia) = Started();
    g.Disconnect(mafia);
    Check(g.Winner == "Villagers");
    g.Act(g.Host!.Value, "restart", null, g.Revision);
    Check(g.ViewFor(g.Host.Value).Players.Length == 4);
    g.Join("Replacement");
    g.Act(g.Host.Value, "start", null, g.Revision);
    foreach (int id in ids) g.Disconnect(id);
    g.Disconnect(g.Host!.Value);
    Check(g.Phase == Phase.Lobby);
}
{
    var g = new Game(3);
    int host = g.Join("Host");
    Reject(() => g.Join("host")); Reject(() => g.Join("")); Reject(() => g.Join(null));
    Reject(() => g.Act(host, "start", null, g.Revision));
    int next = g.Join("Next"); g.Disconnect(host); Check(g.Host == next);
}
{
    // A 2–2 day tie kills nobody, then stale night commands cannot cross rounds.
    var (g, ids, mafia) = Started();
    g.Act(mafia, "kill", ids.First(i => i != mafia), g.Revision);
    var alive = g.ViewFor(mafia).Players.Where(p => p.Alive).Select(p => p.Id).ToArray();
    int rev = g.Revision;
    g.Act(alive[0], "vote", alive[1], rev);
    Reject(() => g.Act(alive[0], "vote", alive[2], rev));
    g.Act(alive[1], "vote", alive[0], rev);
    g.Act(alive[2], "vote", alive[0], rev);
    g.Act(alive[3], "vote", alive[1], rev);
    Check(g.Phase == Phase.Night && g.ViewFor(mafia).Players.Count(p => p.Alive) == 4);
    Reject(() => g.Act(mafia, "kill", alive.First(i => i != mafia), rev - 1));
}
{
    // Disconnecting a vote target releases affected voters and re-evaluates quorum.
    var (g, ids, mafia) = Started(6);
    g.Act(mafia, "kill", ids.First(i => i != mafia), g.Revision);
    var town = g.ViewFor(mafia).Players.Where(p => p.Alive && p.Id != mafia).Select(p => p.Id).ToArray();
    g.Act(town[0], "vote", town[1], g.Revision);
    g.Disconnect(town[1]);
    Check(g.ViewFor(town[0]).CanAct);
    int rev = g.Revision;
    foreach (var p in g.ViewFor(mafia).Players.Where(p => p.Alive))
        g.Act(p.Id, "vote", p.Id == mafia ? town[0] : mafia, rev);
    Check(g.Winner == "Villagers");
}
{
    var (g, ids, mafia) = Started(3);
    g.Disconnect(ids.First(i => i != mafia));
    Check(g.Winner == "Mafia");
}
Console.WriteLine("State machine tests passed.");

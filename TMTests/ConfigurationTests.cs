using System.Net;
using TMGame;
using TMServer;

static class ConfigurationTests
{
    static void Check(bool ok) { if (!ok) throw new Exception("Configuration assertion failed."); }
    static void Invalid(Action action)
    {
        try { action(); } catch (Exception e) when (e is ArgumentException or FormatException or InvalidOperationException or EndOfStreamException) { return; }
        throw new Exception("Expected invalid configuration/action.");
    }
    public static void Run()
    {
        var settings = new GameSettings("Friday Mafia", 8, 2, 1, 1, 1, 3);
        settings.Validate();
        foreach (var bad in new[] { settings with { Mafia = -1 }, settings with { Villagers = 2 },
            settings with { Mafia = 4, Villagers = 1 }, settings with { Mafia = 0, Villagers = 5 },
            settings with { Name = "\n" }, settings with { Doctors = int.MaxValue } }) Invalid(() => bad.Validate());
        var args = new[] { "--name", "Friday Mafia", "--players", "8", "--mafia", "2", "--police", "1", "--doctors", "1", "--detectives", "1" };
        var options = HostOptions.Parse(args, TextReader.Null, TextWriter.Null);
        Check(options.Game == settings && options.Port == 8209 && options.Address.Equals(IPAddress.Loopback));
        Check(HostOptions.Parse(["5", "0", "0.0.0.0"], TextReader.Null, TextWriter.Null).Game == GameSettings.Default());
        foreach (var bad in new[] { new[] { "--players" }, new[] { "--wat", "5" }, new[] { "--port", "65536" },
            new[] { "--players", "5", "--players", "6" }, new[] { "--mafia", "3" }, new[] { "--villagers", "0" },
            new[] { "5", "8209", "127.0.0.1", "extra" } }) Invalid(() => HostOptions.Parse(bad, TextReader.Null, TextWriter.Null));
        Invalid(() => HostOptions.Parse([], TextReader.Null, TextWriter.Null));
        // Retry invalid numbers and role totals, then accept defaults for networking.
        var input = new StringReader("Friday Mafia\ninvalid\n8\n2\n1\n1\n1\n2\n2\n1\n1\n1\n3\n\n\n");
        var output = new StringWriter();
        Check(HostOptions.Parse([], input, output).Game == settings);
        Check(output.ToString().Contains("add up") && output.ToString().Contains("whole number"));

        foreach (string scenario in new[] { "protected", "blockedDoctor", "blockedDetective", "splitMafia" })
        {
            var g = new Game(settings, new Random(12));
            var ids = Enumerable.Range(0, 8).Select(i => g.Join("P" + i)).ToArray();
            g.Act(ids[0], "start", null, g.Revision);
            int[] With(Role role) => ids.Where(i => g.ViewFor(i).YourRole == role).ToArray();
            var mafia = With(Role.Mafia); var villagers = With(Role.Villager);
            int police = With(Role.Police).Single(), doctor = With(Role.Doctor).Single(), detective = With(Role.Detective).Single();
            Check(mafia.Length == 2 && villagers.Length == 3);
            Check(g.ViewFor(mafia[0]).MafiaPartners.SequenceEqual(new[] { mafia[1] }));
            Check(g.ViewFor(doctor).MafiaPartners.Length == 0);
            Invalid(() => g.Act(mafia[0], "kill", mafia[1], g.Revision));
            Invalid(() => g.Act(doctor, "kill", villagers[0], g.Revision));
            Invalid(() => g.Act(detective, "investigate", detective, g.Revision));
            int target = scenario == "protected" ? doctor : villagers[0];
            g.Act(mafia[0], "kill", target, g.Revision);
            g.Act(mafia[1], "kill", scenario == "splitMafia" ? villagers[1] : target, g.Revision);
            g.Act(doctor, "protect", target, g.Revision); // includes self-protection
            g.Act(detective, "investigate", mafia[0], g.Revision);
            Check(g.Phase == Phase.Night); // wait for every role
            g.Act(police, "block", scenario == "blockedDoctor" ? doctor : scenario == "blockedDetective" ? detective : villagers[2], g.Revision);
            Check(g.Phase == Phase.Day);
            if (scenario == "splitMafia") Check(g.ViewFor(target).Players.All(p => p.Alive));
            Check(g.ViewFor(target).Players.Single(p => p.Id == target).Alive == (scenario != "blockedDoctor"));
            string report = g.ViewFor(detective).PrivateReports.Single();
            Check(report.Contains(scenario == "blockedDetective" ? "blocked" : "is Mafia"));
            Check(ids.Where(i => i != detective).All(i => g.ViewFor(i).PrivateReports.Length == 0));
            // Losing one of multiple Mafia must not award town victory.
            g.Disconnect(mafia[0]); Check(g.Winner == null);
            g.Disconnect(mafia[1]); Check(g.Winner == "Villagers");
            g.Act(g.Host!.Value, "restart", null, g.Revision);
            Check(g.Settings == settings && g.ViewFor(detective).PrivateReports.Length == 0);
            Check(g.ViewFor(detective).MafiaPartners.Length == 0);
        }
        {
            var g = new Game(new GameSettings("Block", 5, 1, 1, 0, 0, 3), new Random(2));
            var ids = Enumerable.Range(0, 5).Select(i => g.Join("B" + i)).ToArray();
            g.Act(ids[0], "start", null, g.Revision);
            int mafia = ids.Single(i => g.ViewFor(i).YourRole == Role.Mafia);
            int police = ids.Single(i => g.ViewFor(i).YourRole == Role.Police);
            g.Act(police, "block", mafia, g.Revision);
            g.Act(mafia, "kill", police, g.Revision);
            Check(g.Phase == Phase.Day && g.ViewFor(police).Players.All(p => p.Alive));
        }
        {
            var g = new Game(new GameSettings("Parity", 5, 2, 0, 1, 0, 2), new Random(3));
            var ids = Enumerable.Range(0, 5).Select(i => g.Join("T" + i)).ToArray();
            g.Act(ids[0], "start", null, g.Revision);
            g.Disconnect(ids.First(i => g.ViewFor(i).YourRole == Role.Villager));
            Check(g.Winner == "Mafia");
        }
        {
            // A disconnected special actor no longer holds night open.
            var g = new Game(new GameSettings("Night", 5, 1, 0, 1, 0, 3), new Random(1));
            var ids = Enumerable.Range(0, 5).Select(i => g.Join("N" + i)).ToArray();
            g.Act(ids[0], "start", null, g.Revision);
            int mafia = ids.Single(i => g.ViewFor(i).YourRole == Role.Mafia);
            int doctor = ids.Single(i => g.ViewFor(i).YourRole == Role.Doctor);
            g.Act(mafia, "kill", ids.First(i => g.ViewFor(i).YourRole == Role.Villager), g.Revision);
            g.Disconnect(doctor);
            Check(g.Phase == Phase.Day);
        }
    }
}

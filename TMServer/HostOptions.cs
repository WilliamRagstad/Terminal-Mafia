using System.Net;
using TMGame;
using TMProtocol;

namespace TMServer;

public sealed record HostOptions(GameSettings Game, IPAddress Address, int Port)
{
    public const string Usage = """
        TMServer                         Interactive hosting setup
        TMServer --setup                 Interactive hosting setup
        TMServer [players] [port] [bind]  Quick host (one Mafia, remaining Villagers)
        TMServer --name NAME --players N --mafia N --police N --doctors N
                 --detectives N --villagers N --port PORT --bind ADDRESS
        Defaults: name=Terminal Mafia, players=5, mafia=1, special roles=0,
        villagers=remaining seats, port=8209, bind=127.0.0.1.
        """;

    public static HostOptions Parse(string[] args, TextReader input, TextWriter output)
    {
        if (args.Length == 0 || args.SequenceEqual(new[] { "--setup" })) return Prompt(input, output);
        if (!args[0].StartsWith("--", StringComparison.Ordinal))
        {
            if (args.Length > 3) throw new ArgumentException("Too many positional arguments.");
            return Checked(GameSettings.Default(int.Parse(args[0])),
                args.Length > 2 ? IPAddress.Parse(args[2]) : IPAddress.Loopback,
                args.Length > 1 ? int.Parse(args[1]) : Codec.StandardPort);
        }
        string[] allowed = ["--name", "--players", "--mafia", "--police", "--doctors", "--detectives", "--villagers", "--port", "--bind"];
        var values = new Dictionary<string, string>();
        for (int i = 0; i < args.Length; i += 2)
        {
            if (!allowed.Contains(args[i]) || i + 1 == args.Length || !values.TryAdd(args[i], args[i + 1]))
                throw new ArgumentException($"Unknown, repeated, or incomplete option: {args[i]}");
        }
        int Number(string key, int fallback) => values.TryGetValue(key, out var value) ? int.Parse(value) : fallback;
        int players = Number("--players", 5), mafia = Number("--mafia", 1), police = Number("--police", 0),
            doctors = Number("--doctors", 0), detectives = Number("--detectives", 0);
        int villagers = Number("--villagers", (int)Math.Clamp((long)players - mafia - police - doctors - detectives, int.MinValue, int.MaxValue));
        return Checked(new(values.GetValueOrDefault("--name", "Terminal Mafia"), players, mafia, police, doctors, detectives, villagers),
            IPAddress.Parse(values.GetValueOrDefault("--bind", "127.0.0.1")), Number("--port", Codec.StandardPort));
    }

    private static HostOptions Checked(GameSettings game, IPAddress address, int port)
    {
        game.Validate();
        if (port is < 0 or > 65535) throw new ArgumentException("Port must be 0–65535 (0 selects a free port).");
        return new(game, address, port);
    }

    private static HostOptions Prompt(TextReader input, TextWriter output)
    {
        string Read(string label, string fallback)
        {
            output.Write($"{label} [{fallback}]: ");
            string line = input.ReadLine() ?? throw new EndOfStreamException("Hosting setup cancelled: input closed.");
            return string.IsNullOrWhiteSpace(line) ? fallback : line.Trim();
        }
        int Number(string label, int fallback, int min, int max)
        {
            while (true)
            {
                if (int.TryParse(Read(label, fallback.ToString()), out int value) && value >= min && value <= max) return value;
                output.WriteLine($"Enter a whole number from {min} to {max}.");
            }
        }
        output.WriteLine("Terminal Mafia | Hosting setup");
        string name;
        while (true)
        {
            name = Read("Server name", "Terminal Mafia");
            try { (GameSettings.Default() with { Name = name }).Validate(); break; }
            catch (ArgumentException e) { output.WriteLine(e.Message); }
        }
        int players = Number("Players", 5, 3, 16);
        GameSettings game;
        while (true)
        {
            output.WriteLine($"Assign exactly {players} roles. Mafia must be fewer than half of the players.");
            int mafia = Number("Mafia", Math.Max(1, players / 4), 1, (players - 1) / 2);
            int police = Number("Police", 0, 0, players);
            int doctors = Number("Doctors", 0, 0, players);
            int detectives = Number("Detectives", 0, 0, players);
            int villagers = Number("Villagers", Math.Max(0, players - mafia - police - doctors - detectives), 0, players);
            game = new(name, players, mafia, police, doctors, detectives, villagers);
            try { game.Validate(); break; }
            catch (ArgumentException e) { output.WriteLine(e.Message + " Enter role counts again."); }
        }
        int port = Number("Port", Codec.StandardPort, 0, 65535);
        IPAddress? address;
        while (!IPAddress.TryParse(Read("Bind address (0.0.0.0 for LAN)", "127.0.0.1"), out address))
            output.WriteLine("Enter a valid IP address.");
        return Checked(game, address, port);
    }
}

using System.Net.Sockets;
using TMGame;
using TMProtocol;

if (args.Length is < 2 or > 3 || args.Contains("--help"))
{
    Console.WriteLine("Terminal Mafia <host> <name> [port=8209]");
    return;
}
try
{
    using var socket = new TcpClient();
    await socket.ConnectAsync(args[0], args.Length == 3 ? int.Parse(args[2]) : Codec.StandardPort);
    var stream = socket.GetStream();
    await Codec.WriteAsync(stream, new Request(Command: "join", Name: args[1]));
    View? state = null;
    int? id = null;
    Console.WriteLine("Commands: start, kill <id>, vote <id>, restart, quit. Discuss with friends before voting.");
    var receive = Task.Run(async () =>
    {
        while (true)
        {
            var response = await Codec.ReadAsync<Response>(stream);
            if (response.Type == "joined") { id = response.Id; Console.WriteLine($"Joined as #{id}."); }
            if (response.Message != null) Console.WriteLine(response.Message);
            if (response.State is not { } view) continue;
            Volatile.Write(ref state, view);
            Console.WriteLine($"\n{view.Phase} | Your role: {view.YourRole?.ToString() ?? "unassigned"} | Host: #{view.Host} | Winner: {view.Winner ?? "none"}");
            foreach (var p in view.Players) Console.WriteLine($"  #{p.Id} {p.Name}: {(p.Alive ? "alive" : "eliminated")}{(p.Connected ? "" : " (disconnected)")}");
            Console.WriteLine(view.CanAct ? "Your action: " + (view.Phase == Phase.Night ? "kill <id>" : "vote <id>") : "Waiting / spectating.");
            if (id == view.Host && view.Phase is Phase.Lobby or Phase.Finished)
                Console.WriteLine(view.Phase == Phase.Lobby ? "When full, enter start." : "Enter restart to reopen the lobby.");
        }
    });
    while (true)
    {
        // Network events remain visible while the player is at the input prompt.
        var input = Task.Run(Console.ReadLine);
        if (await Task.WhenAny(input, receive) == receive) { await receive; break; }
        string? line = await input;
        if (line == null || line.Trim() == "quit") break;
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) continue;
        int? target = parts.Length == 2 && int.TryParse(parts[1], out int parsed) ? parsed : null;
        if (parts.Length > 2 || parts[0] is not ("start" or "restart" or "kill" or "vote"))
        { Console.WriteLine("Use start, kill <id>, vote <id>, restart, or quit."); continue; }
        await Codec.WriteAsync(stream, new Request(Command: parts[0], Target: target, Revision: Volatile.Read(ref state)?.Revision ?? -1));
    }
}
catch (Exception e) when (e is IOException or SocketException or FormatException or OverflowException or ArgumentException)
{
    Console.Error.WriteLine("Connection closed or failed: " + e.Message);
    Environment.ExitCode = 1;
}

using System.Net;
using TMProtocol;
using TMServer;

if (args.Contains("--help"))
{
    Console.WriteLine("TMServer [players=5] [port=8209] [bind-address=127.0.0.1]");
    return;
}
try
{
    int players = args.Length > 0 ? int.Parse(args[0]) : 5;
    int port = args.Length > 1 ? int.Parse(args[1]) : Codec.StandardPort;
    var address = args.Length > 2 ? IPAddress.Parse(args[2]) : IPAddress.Loopback;
    using var stop = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
    await new Server(address, port, players).RunAsync(stop.Token);
}
catch (Exception e) when (e is ArgumentException or FormatException or OverflowException or System.Net.Sockets.SocketException)
{
    Console.Error.WriteLine(e.Message + "\nUsage: TMServer [players=5] [port=8209] [bind-address=127.0.0.1]");
    Environment.ExitCode = 1;
}

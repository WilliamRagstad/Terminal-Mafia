using TMServer;

if (args.Contains("--help")) { Console.WriteLine(HostOptions.Usage); return; }
try
{
    var options = HostOptions.Parse(args, Console.In, Console.Out);
    using var stop = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
    await new Server(options.Address, options.Port, options.Game).RunAsync(stop.Token);
}
catch (Exception e) when (e is ArgumentException or FormatException or OverflowException or IOException or System.Net.Sockets.SocketException)
{
    Console.Error.WriteLine(e.Message + "\n" + HostOptions.Usage);
    Environment.ExitCode = 1;
}

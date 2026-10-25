using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using TMGame;
using TMProtocol;

namespace TMServer;
public sealed class Server(IPAddress address, int port, int players)
{
    private sealed class Peer(TcpClient socket)
    {
        public TcpClient Socket = socket;
        public NetworkStream Stream = socket.GetStream();
        public int? Id;
        public Channel<Response> Outbox = Channel.CreateBounded<Response>(64);
        public void Send(Response response)
        {
            if (!Outbox.Writer.TryWrite(response)) Socket.Dispose();
        }
    }
    private readonly Game game = new(players);
    private readonly object gate = new();
    private readonly HashSet<Peer> peers = [];
    public async Task RunAsync(CancellationToken cancellation)
    {
        var listener = new TcpListener(address, port);
        listener.Start();
        Console.WriteLine($"Listening on {listener.LocalEndpoint}; {players} players, one Mafia.");
        var tasks = new HashSet<Task>();
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                var socket = await listener.AcceptTcpClientAsync(cancellation);
                tasks.RemoveWhere(t => t.IsCompleted);
                // Bound unauthenticated sockets as well as joined players.
                if (tasks.Count >= 64) { socket.Dispose(); continue; }
                tasks.Add(HandleAsync(socket, cancellation));
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        finally
        {
            listener.Stop();
            await Task.WhenAll(tasks);
        }
    }
    private void Broadcast()
    {
        foreach (var peer in peers)
            peer.Send(new Response("state", State: game.ViewFor(peer.Id!.Value)));
    }
    private async Task HandleAsync(TcpClient socket, CancellationToken cancellation)
    {
        using (socket)
        {
            var peer = new Peer(socket);
            var writer = WriteAsync(peer, cancellation);
            try
            {
                using var handshake = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                handshake.CancelAfter(TimeSpan.FromSeconds(10));
                var request = await Codec.ReadAsync<Request>(peer.Stream, handshake.Token);
                lock (gate)
                {
                    if (request.Version != 1 || request.Command != "join")
                        throw new InvalidOperationException("First command must be a version 1 join.");
                    peer.Id = game.Join(request.Name);
                    peers.Add(peer);
                    peer.Send(new Response("joined", peer.Id));
                    Broadcast();
                }
                while (!cancellation.IsCancellationRequested)
                {
                    request = await Codec.ReadAsync<Request>(peer.Stream, cancellation);
                    lock (gate)
                    {
                        try
                        {
                            if (request.Version != 1) throw new InvalidOperationException("Unsupported protocol version.");
                            game.Act(peer.Id.Value, request.Command, request.Target, request.Revision);
                            peer.Send(new Response("ok"));
                            Broadcast();
                        }
                        catch (InvalidOperationException e) { peer.Send(new Response("error", Message: e.Message)); }
                    }
                }
            }
            catch (InvalidOperationException e) { peer.Send(new Response("error", Message: e.Message)); }
            catch (Exception e) when (e is IOException or SocketException or OperationCanceledException or System.Text.Json.JsonException or ObjectDisposedException) { }
            finally
            {
                lock (gate)
                {
                    if (peers.Remove(peer)) { game.Disconnect(peer.Id!.Value); Broadcast(); }
                }
                peer.Outbox.Writer.TryComplete();
                await writer;
            }
        }
    }
    private static async Task WriteAsync(Peer peer, CancellationToken cancellation)
    {
        try
        {
            await foreach (var response in peer.Outbox.Reader.ReadAllAsync(cancellation))
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                deadline.CancelAfter(TimeSpan.FromSeconds(5));
                await Codec.WriteAsync(peer.Stream, response, deadline.Token);
            }
        }
        catch (Exception e) when (e is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
        { peer.Socket.Dispose(); }
    }
}

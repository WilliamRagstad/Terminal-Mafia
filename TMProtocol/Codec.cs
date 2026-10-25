using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;
using TMGame;

namespace TMProtocol;
public sealed record Request(int Version = 1, string? Command = null, string? Name = null, int? Target = null, int Revision = 0);
public sealed record Response(string Type, int? Id = null, string? Message = null, View? State = null, int Version = 1);

public static class Codec
{
    public const int StandardPort = 8209;
    public const int MaxFrame = 16384;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    public static async Task<T> ReadAsync<T>(Stream stream, CancellationToken cancellation = default)
    {
        byte[] header = new byte[4];
        await stream.ReadExactlyAsync(header, cancellation);
        int length = BinaryPrimitives.ReadInt32BigEndian(header);
        if (length is <= 0 or > MaxFrame) throw new InvalidDataException("Invalid frame size.");
        byte[] body = new byte[length];
        await stream.ReadExactlyAsync(body, cancellation);
        return JsonSerializer.Deserialize<T>(body, Options) ?? throw new InvalidDataException("Null packet.");
    }
    public static async Task WriteAsync<T>(Stream stream, T packet, CancellationToken cancellation = default)
    {
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(packet, Options);
        if (body.Length > MaxFrame) throw new InvalidDataException("Frame too large.");
        byte[] header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, body.Length);
        await stream.WriteAsync(header, cancellation);
        await stream.WriteAsync(body, cancellation);
    }
}

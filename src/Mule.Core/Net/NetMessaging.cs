using System;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace Mule.Core.Net;

/// <summary>
/// Length-prefixed message framing over a TCP stream: a 4-byte little-endian length
/// followed by that many UTF-8 bytes. Keeps whole JSON messages intact across the
/// byte stream.
/// </summary>
public static class NetMessaging
{
    public static void WriteMessage(Stream stream, string message)
    {
        var payload = Encoding.UTF8.GetBytes(message);
        var header = BitConverter.GetBytes(payload.Length);
        stream.Write(header, 0, 4);
        stream.Write(payload, 0, payload.Length);
        stream.Flush();
    }

    /// <summary>Reads one message, or returns null if the stream closed cleanly.</summary>
    public static string? ReadMessage(Stream stream)
    {
        var header = ReadExactly(stream, 4);
        if (header == null) return null;
        int length = BitConverter.ToInt32(header, 0);
        if (length < 0 || length > 64 * 1024 * 1024) throw new IOException($"Bad message length {length}.");
        var payload = ReadExactly(stream, length);
        if (payload == null) return null;
        return Encoding.UTF8.GetString(payload);
    }

    private static byte[]? ReadExactly(Stream stream, int count)
    {
        var buffer = new byte[count];
        int offset = 0;
        while (offset < count)
        {
            int read = stream.Read(buffer, offset, count - offset);
            if (read <= 0) return null; // peer closed
            offset += read;
        }
        return buffer;
    }
}

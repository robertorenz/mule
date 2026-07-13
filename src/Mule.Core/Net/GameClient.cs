using System;
using System.Net.Sockets;
using System.Threading;

namespace Mule.Core.Net;

/// <summary>
/// A joining player's endpoint. Connects to the host, sends local input, and
/// receives state snapshots. Transport only — the client game loop interprets the
/// messages.
/// </summary>
public sealed class GameClient : IDisposable
{
    private TcpClient? _client;
    private volatile bool _running;

    /// <summary>Raised on a background thread when the host sends a message.</summary>
    public event Action<string>? MessageReceived;
    public event Action? Disconnected;

    public bool Connected => _client?.Connected ?? false;

    public void Connect(string host, int port)
    {
        _client = new TcpClient();
        _client.Connect(host, port);
        _running = true;
        new Thread(ReceiveLoop) { IsBackground = true, Name = "mule-client-recv" }.Start();
    }

    private void ReceiveLoop()
    {
        try
        {
            var stream = _client!.GetStream();
            while (_running)
            {
                var message = NetMessaging.ReadMessage(stream);
                if (message == null) break;
                MessageReceived?.Invoke(message);
            }
        }
        catch { /* dropped */ }
        finally
        {
            Disconnected?.Invoke();
        }
    }

    public void Send(string message)
    {
        if (_client != null && _client.Connected)
            NetMessaging.WriteMessage(_client.GetStream(), message);
    }

    public void Dispose()
    {
        _running = false;
        try { _client?.Dispose(); } catch { }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Mule.Core.Net;

/// <summary>
/// The authoritative host's network endpoint. Listens for client connections and
/// exchanges length-prefixed text messages (JSON snapshots one way, input the
/// other). Transport only — it holds no game rules; the host loop drives it.
/// </summary>
public sealed class GameServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly ConcurrentDictionary<int, TcpClient> _clients = new();
    private int _nextClientId;
    private volatile bool _running;

    /// <summary>Raised on a background thread when a client sends a message (clientId, message).</summary>
    public event Action<int, string>? MessageReceived;

    /// <summary>Raised when a client connects or disconnects.</summary>
    public event Action<int>? ClientConnected;
    public event Action<int>? ClientDisconnected;

    public int Port { get; private set; }
    public int ClientCount => _clients.Count;

    public GameServer(int port = 0)
    {
        _listener = new TcpListener(IPAddress.Loopback, port);
    }

    public void Start()
    {
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _running = true;
        new Thread(AcceptLoop) { IsBackground = true, Name = "mule-accept" }.Start();
    }

    private void AcceptLoop()
    {
        while (_running)
        {
            TcpClient client;
            try { client = _listener.AcceptTcpClient(); }
            catch { break; } // listener stopped

            int id = Interlocked.Increment(ref _nextClientId);
            _clients[id] = client;
            ClientConnected?.Invoke(id);
            new Thread(() => ReceiveLoop(id, client)) { IsBackground = true, Name = $"mule-client-{id}" }.Start();
        }
    }

    private void ReceiveLoop(int id, TcpClient client)
    {
        try
        {
            var stream = client.GetStream();
            while (_running)
            {
                var message = NetMessaging.ReadMessage(stream);
                if (message == null) break;
                MessageReceived?.Invoke(id, message);
            }
        }
        catch { /* connection dropped */ }
        finally
        {
            _clients.TryRemove(id, out _);
            client.Dispose();
            ClientDisconnected?.Invoke(id);
        }
    }

    public void Send(int clientId, string message)
    {
        if (_clients.TryGetValue(clientId, out var client))
            NetMessaging.WriteMessage(client.GetStream(), message);
    }

    public void Broadcast(string message)
    {
        foreach (var kv in _clients)
        {
            try { NetMessaging.WriteMessage(kv.Value.GetStream(), message); }
            catch { /* one bad client shouldn't stop the rest */ }
        }
    }

    public IReadOnlyCollection<int> ClientIds => (IReadOnlyCollection<int>)_clients.Keys;

    public void Dispose()
    {
        _running = false;
        try { _listener.Stop(); } catch { }
        foreach (var kv in _clients) kv.Value.Dispose();
        _clients.Clear();
    }
}

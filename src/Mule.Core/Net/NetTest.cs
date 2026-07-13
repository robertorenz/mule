using System;
using System.Text;
using System.Threading;

namespace Mule.Core.Net;

/// <summary>
/// A self-contained loopback proof for the networking foundation: build a non-trivial
/// game, capture a snapshot, send it host → client over real TCP on localhost, restore
/// it on the client, and verify the result is byte-identical. Runs headless so it can
/// be checked from the console (MULE_NETTEST=1).
/// </summary>
public static class NetTest
{
    public static string RunLoopback()
    {
        var log = new StringBuilder();
        var state = BuildSampleState();
        string sentJson = GameSnapshot.Capture(state).ToJson();
        log.AppendLine($"Captured snapshot: {sentJson.Length} bytes, {state.Players.Count} players.");

        string? receivedJson = null;
        using var gotMessage = new ManualResetEventSlim(false);

        using var server = new GameServer(port: 0);
        server.ClientConnected += id => server.Send(id, sentJson);
        server.Start();
        log.AppendLine($"Host listening on 127.0.0.1:{server.Port}.");

        using var client = new GameClient();
        client.MessageReceived += msg => { receivedJson = msg; gotMessage.Set(); };
        client.Connect("127.0.0.1", server.Port);
        log.AppendLine("Client connected.");

        if (!gotMessage.Wait(TimeSpan.FromSeconds(5)))
            return log.AppendLine("FAIL: timed out waiting for the snapshot.").ToString();

        log.AppendLine($"Client received: {receivedJson!.Length} bytes.");

        bool transportOk = receivedJson == sentJson;
        log.AppendLine($"Transport integrity (bytes match on the wire): {(transportOk ? "PASS" : "FAIL")}");

        var restored = GameSnapshot.Restore(GameSnapshot.FromJson(receivedJson));
        string reJson = GameSnapshot.Capture(restored).ToJson();
        bool restoreOk = reJson == sentJson;
        log.AppendLine($"Restore fidelity (rebuilt state re-serializes identically): {(restoreOk ? "PASS" : "FAIL")}");

        // A couple of human-readable spot checks.
        log.AppendLine("--- spot checks (original -> restored) ---");
        for (int i = 0; i < state.Players.Count; i++)
        {
            var a = state.Players[i];
            var b = restored.Players[i];
            log.AppendLine($"  {a.Name}: ${a.Money}->${b.Money}, score {a.Score(state)}->{b.Score(restored)}, " +
                           $"carrying {a.CarriedMule}->{b.CarriedMule}");
        }
        log.AppendLine($"  Month {state.Month}->{restored.Month}, phase {state.Phase}->{restored.Phase}, " +
                       $"store MULEs {state.Store.MulesAvailable}->{restored.Store.MulesAvailable}");

        log.AppendLine(transportOk && restoreOk
            ? "RESULT: PASS - snapshot survived a real TCP round-trip intact."
            : "RESULT: FAIL");
        return log.ToString();
    }

    private static GameState BuildSampleState()
    {
        var state = GameFactory.NewGame(seed: 1983, humanPlayers: 1, totalPlayers: 4, totalMonths: 12);
        state.Month = 4;
        state.Phase = GamePhase.Auction;
        state.ActivePlayerIndex = 2;

        state.Players[0].Money = 733;
        state.Players[0].SetStore(Resource.Food, 5);
        state.Players[0].SetStore(Resource.Smithore, 2);
        state.Players[0].CarriedMule = MuleOutfit.Smithore;
        state.Players[0].TimeFactor = 0.6f;
        state.Players[1].Money = 1288;
        state.Players[3].SetStore(Resource.Crystite, 4);

        var map = state.Map;
        map.At(2, 1).OwnerId = 0; map.At(2, 1).Mule = MuleOutfit.Food;
        map.At(3, 1).OwnerId = 1; map.At(3, 1).Mule = MuleOutfit.Energy;
        map.At(6, 3).OwnerId = 2; map.At(6, 3).Mule = MuleOutfit.Crystite;

        state.Prices.SetSpotPrice(Resource.Crystite, 137);
        state.Prices.MulePrice = 140;
        state.Store.MulesAvailable = 9;
        state.Store.SmithoreBuffer = 1;
        state.Store.AddStock(Resource.Smithore, 3);

        return state;
    }
}

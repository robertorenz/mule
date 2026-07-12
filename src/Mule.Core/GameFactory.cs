using System.Collections.Generic;

namespace Mule.Core;

/// <summary>
/// Builds a fresh <see cref="GameState"/>. Player slots not filled by a human are
/// handed to the AI, matching the "single-player vs computer" default while
/// leaving room to seat additional humans (local or, later, networked).
/// </summary>
public static class GameFactory
{
    /// <summary>Packed 0xRRGGBBAA colors for up to four players. Professional, no purple.</summary>
    public static readonly uint[] PlayerColors =
    {
        0x2F80EDFF, // blue
        0x27AE60FF, // green
        0xE2B93BFF, // amber
        0xEB5757FF, // coral red
    };

    private static readonly string[] DefaultNames = { "Player 1", "Blorb", "Zorp", "Nib" };

    public static GameState NewGame(int seed, int humanPlayers = 1, int totalPlayers = 4, int totalMonths = 12)
    {
        if (totalPlayers < 1) totalPlayers = 1;
        if (totalPlayers > 4) totalPlayers = 4;
        if (humanPlayers > totalPlayers) humanPlayers = totalPlayers;

        var players = new List<Player>(totalPlayers);
        for (int i = 0; i < totalPlayers; i++)
        {
            bool isAI = i >= humanPlayers;
            var player = new Player(i, DefaultNames[i], PlayerColors[i], isAI)
            {
                Money = 1000,
            };
            player.SetStore(Resource.Food, 4);
            player.SetStore(Resource.Energy, 2);
            players.Add(player);
        }

        var map = PlanetMap.Generate(seed);
        return new GameState(seed, map, players, totalMonths)
        {
            Phase = GamePhase.LandGrant,
        };
    }
}

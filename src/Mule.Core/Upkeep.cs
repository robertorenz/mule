using System;

namespace Mule.Core;

/// <summary>Result of a single colonist's monthly food upkeep, for reporting to the UI.</summary>
public readonly record struct FoodUpkeep(int PlayerId, int Needed, int Eaten, float TimeFactor);

/// <summary>
/// Monthly colony upkeep. Right now that means eating: each colonist must consume
/// Food to earn a full Development turn next month, and the requirement grows as the
/// colony ages. Underfed colonists get a proportionally shorter turn — the pressure
/// that gives the Food auction real stakes.
/// </summary>
public static class Upkeep
{
    /// <summary>Food a colonist must eat this month for a full turn. Ramps up over time.</summary>
    public static int FoodNeeded(int month) => 3 + month / 3;

    /// <summary>Turns are never cut below this fraction, so a starving player still plays.</summary>
    public const float MinTimeFactor = 0.35f;

    public static FoodUpkeep[] ConsumeFood(GameState state)
    {
        int need = FoodNeeded(state.Month);
        var report = new FoodUpkeep[state.Players.Count];

        for (int i = 0; i < state.Players.Count; i++)
        {
            var p = state.Players[i];
            int eaten = Math.Min(p.Store(Resource.Food), need);
            p.AddStore(Resource.Food, -eaten);

            float factor = need > 0 ? (float)eaten / need : 1f;
            p.TimeFactor = Math.Clamp(factor, MinTimeFactor, 1f);
            report[i] = new FoodUpkeep(p.Id, need, eaten, p.TimeFactor);
        }

        return report;
    }
}

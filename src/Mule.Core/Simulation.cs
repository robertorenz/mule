using System.Collections.Generic;
using System.Text;

namespace Mule.Core;

/// <summary>
/// Headless, rendering-free driver of the game loop. Every colonist is played by
/// <see cref="AiPlanner"/>. Used to sanity-check that the AI, the action rules, and
/// production actually move the game forward without opening a window.
/// </summary>
public static class Simulation
{
    public static string RunHeadless(int seed, int totalMonths = 12)
    {
        var state = GameFactory.NewGame(seed, humanPlayers: 0, totalPlayers: 4, totalMonths: totalMonths);
        var log = new StringBuilder();

        for (int month = 1; month <= totalMonths; month++)
        {
            state.Month = month;
            state.Phase = GamePhase.Development;
            Upkeep.ConsumeFood(state);
            var evt = ColonyEvents.Resolve(state);

            foreach (var player in state.Players)
            {
                state.ActivePlayerIndex = player.Id;
                var plan = AiPlanner.PlanTurn(state, player);
                foreach (var action in plan)
                {
                    switch (action.Type)
                    {
                        case AiActionType.Claim:
                            ColonyActions.ClaimPlot(state, player, state.Map.At(action.X, action.Y));
                            break;
                        case AiActionType.Buy:
                            ColonyActions.BuyMule(state, player, action.Outfit);
                            break;
                        case AiActionType.Install:
                            ColonyActions.InstallMule(state, player, state.Map.At(action.X, action.Y));
                            break;
                    }
                }
            }

            state.Phase = GamePhase.Production;
            var produced = Production.Resolve(state);
            int unpowered = produced.UnpoweredMules;

            // Auction each resource to completion (all AI, no human intent).
            state.Phase = GamePhase.Auction;
            int trades = 0, mulesBefore = state.Store.MulesAvailable;
            foreach (Resource resource in System.Enum.GetValues<Resource>())
            {
                var auction = Auction.Create(state, resource);
                int guard = 0;
                while (!auction.IsComplete && guard++ < 10000)
                    auction.Update(state, 1f / 30f);
                trades += auction.TradeCount;
            }
            int mulesMade = state.Store.MulesAvailable - mulesBefore;

            log.Append($"Month {month:D2}: ");
            for (int i = 0; i < state.Players.Count; i++)
            {
                var p = state.Players[i];
                log.Append($"{p.Name} ${p.Money}/sc {p.Score(state)}");
                if (i < state.Players.Count - 1) log.Append("  |  ");
            }
            log.Append($"   (+{produced.Yields.Count} yields, {unpowered} idle, {trades} trades, +{mulesMade} MULEs)");
            if (evt is { } e) log.Append($"   NEWS: {e.Headline} - {e.Detail}");
            log.Append('\n');
        }

        int owned = 0, mules = 0;
        foreach (var plot in state.Map.AllPlots())
        {
            if (plot.IsOwned) owned++;
            if (plot.HasMule) mules++;
        }
        log.Append($"Final: {owned} plots claimed, {mules} MULEs installed, " +
                   $"{state.Store.MulesAvailable} MULEs left in store.\n");

        return log.ToString();
    }
}

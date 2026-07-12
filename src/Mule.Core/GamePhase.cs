namespace Mule.Core;

/// <summary>
/// The phases that make up one turn (one "month") of colony life. The turn loop
/// cycles Development → Production → Auction, bracketed by land grants at the
/// start of a turn and event/wrap-up at the end.
/// </summary>
public enum GamePhase
{
    /// <summary>Pre-game: choosing players, species, colors, difficulty.</summary>
    Setup,

    /// <summary>Start of turn: a plot of land is granted or auctioned.</summary>
    LandGrant,

    /// <summary>Real-time: each player moves, visits the store, installs MULEs.</summary>
    Development,

    /// <summary>Plots with MULEs generate resources based on terrain and skill.</summary>
    Production,

    /// <summary>Real-time double auction: players buy and sell the four resources.</summary>
    Auction,

    /// <summary>Random colony events resolve; scores update; turn advances.</summary>
    Resolution,

    /// <summary>Final month reached: colony is scored and a winner declared.</summary>
    GameOver
}

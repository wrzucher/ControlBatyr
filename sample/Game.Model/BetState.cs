namespace Game.Model;

/// <summary>The lifecycle of a player's bet.</summary>
public enum BetState
{
    /// <summary>The bet remains open.</summary>
    Open,
    /// <summary>The player has cancelled the bet.</summary>
    Cancelled
}

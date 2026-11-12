namespace Game.Model;

/// <summary>The lifecycle of a player's stake.</summary>
public enum BetState
{
    /// <summary>The stake remains in play.</summary>
    Open,
    /// <summary>The player has withdrawn the stake.</summary>
    Cancelled
}

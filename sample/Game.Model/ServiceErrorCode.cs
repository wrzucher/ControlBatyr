namespace Game.Model;

/// <summary>Reasons a requested game operation cannot proceed.</summary>
public enum ServiceErrorCode
{
    /// <summary>The selected game does not exist.</summary>
    GameNotFound = 10,
    /// <summary>The cancellation deadline has passed.</summary>
    GameStarted = 11,
    /// <summary>The selected bet does not exist.</summary>
    BetNotFound = 20,
    /// <summary>The stake has already been withdrawn.</summary>
    BetAlreadyCancelled = 21
}

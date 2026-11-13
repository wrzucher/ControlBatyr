# Game reference

[Business API](index.md) · [Overview](game.md)

## Operations

<a id="type-game-core-gamemanager"></a>

### GameManager

[Source](../../Game.Core/GameManager.cs)

Lets players place bets and cancel them until their game starts.

<a id="type-game-core-gamemanager-creategameasync"></a>

#### CreateGame

Sets when the game starts and bets can no longer be cancelled.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| name | string | The title players use to recognise the game. |
| startsAt | DateTimeOffset | When cancellation closes for every bet on this game. |

**Returns:** [Game](#type-game-model-game)

The recorded game, including the identifier used to place bets.

<a id="type-game-core-gamemanager-placebetasync"></a>

#### PlaceBet

Places a player's bet on an existing game.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| gameId | Guid | The game the player chose. |
| playerId | Guid | The player who owns the bet. |
| amount | decimal | The amount the player bets. |

**Returns:** [Bet](#type-game-model-bet)

The recorded bet in its initial open state.

**Raises:** [BetPlaced](#type-game-core-gameeventpublisher-betplaced)

**Requirements**

- Placed bet preserves the player game and amount — [Source](../../Game.Tests/GameRequirements.cs)
- Placed bet is published with its recorded data — [Source](../../Game.Tests/GameRequirements.cs)

<a id="type-game-core-gamemanager-cancelbetproblemasync"></a>

#### CancelBetProblem

Explains why the player cannot cancel the bet.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| betId | Guid | The bet whose cancellation the player is considering. |

**Returns:** IReadOnlyList&lt;[ServiceErrorCode](#type-game-model-serviceerrorcode)&gt;

All current reasons preventing cancellation; empty when cancellation is allowed.

**Remarks**

This preview does not change the bet. CancelBet checks again before cancelling it.

**Requirements**

- Cancellation preview explains all reasons the bet cannot be cancelled — [Source](../../Game.Tests/GameRequirements.cs)

<a id="type-game-core-gamemanager-cancelbetasync"></a>

#### CancelBet

Lets the player cancel an open bet before the game starts.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| betId | Guid | The bet the player wants to cancel. |

**Remarks**

Listeners are notified after the cancellation has been recorded.

**Raises:** [BetCancelled](#type-game-core-gameeventpublisher-betcancelled)

**Requirements**

- Cancelling a bet refunds its bet amount to its players account — [Source](../../Game.Tests/AccountRequirements.cs)
- Open bet can be cancelled before the game starts — [Source](../../Game.Tests/GameRequirements.cs)
- Bet cannot be cancelled once the game starts — [Source](../../Game.Tests/GameRequirements.cs)
- Cancelled bet cannot be cancelled again — [Source](../../Game.Tests/GameRequirements.cs)
- Cancelled bet is recorded and its identifier is published — [Source](../../Game.Tests/GameRequirements.cs)

<a id="type-game-core-gamemanager-betasync"></a>

#### Bet

Lets the player see the bet's current state.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| betId | Guid | The bet to look up. |

**Returns:** [Bet](#type-game-model-bet)?

The current bet, or null when betId is unknown.

## Events

<a id="type-game-core-gameeventpublisher"></a>

### GameEventPublisher

[Source](../../Game.Core/GameEventPublisher.cs)

Announces changes to player bets after they have been recorded.

<a id="type-game-core-gameeventpublisher-betplaced"></a>

#### BetPlaced

The player's bet has been placed.

**Payload:** [BetPlacedEventArgs](#type-game-model-betplacedeventargs)

**Raised by:** [PlaceBet](#type-game-core-gamemanager-placebetasync)

<a id="type-game-core-gameeventpublisher-betcancelled"></a>

#### BetCancelled

The player's bet has been cancelled.

**Payload:** [BetCancelledEventArgs](#type-game-model-betcancelledeventargs)

**Raised by:** [CancelBet](#type-game-core-gamemanager-cancelbetasync)

## Models

<a id="type-game-model-bet"></a>

### Bet

[Source](../../Game.Model/Bet.cs)

A player's bet on a game, including its current cancellation state.

#### Data

| Name | Type | Description |
| --- | --- | --- |
| Id | Guid | Identifies the bet for later lookup or cancellation. |
| GameId | Guid | The game the player chose. |
| PlayerId | Guid | The player who placed the bet. |
| Amount | decimal | The amount the player bet. |
| State | [BetState](#type-game-model-betstate) | Whether the bet is still open or has been cancelled. |
| GameStartsAt | DateTimeOffset | The deadline for cancelling the bet. |

#### Computed properties

| Name | Type | Description |
| --- | --- | --- |
| IsOpen | bool | Whether the bet is still open. |

<a id="type-game-model-betcancelledeventargs"></a>

### BetCancelledEventArgs

[Source](../../Game.Model/BetCancelledEventArgs.cs)

Identifies the bet whose cancellation has been recorded.

#### Data

| Name | Type | Description |
| --- | --- | --- |
| BetId | Guid | The cancelled bet, for lookup or related updates. |
| PlayerId | Guid | The player who receives the refunded bet amount. |
| Amount | decimal | The bet amount to return. |

<a id="type-game-model-betplacedeventargs"></a>

### BetPlacedEventArgs

[Source](../../Game.Model/BetPlacedEventArgs.cs)

The recorded bet announced to listeners.

#### Data

| Name | Type | Description |
| --- | --- | --- |
| Bet | [Bet](#type-game-model-bet) | The bet as recorded when it was placed. |

<a id="type-game-model-betstate"></a>

### BetState

The lifecycle of a player's bet.

| Value | Description |
| --- | --- |
| Open = 0 | The bet remains open. |
| Cancelled = 1 | The player has cancelled the bet. |

<a id="type-game-model-game"></a>

### Game

[Source](../../Game.Model/Game.cs)

A scheduled game that players can bet on.

#### Data

| Name | Type | Description |
| --- | --- | --- |
| Id | Guid | Identifies the game when placing a bet. |
| Name | string | The title shown to players. |
| StartsAt | DateTimeOffset | When play begins and cancellation closes. |

<a id="type-game-model-serviceerrorcode"></a>

### ServiceErrorCode

Reasons a requested game operation cannot proceed.

| Value | Description |
| --- | --- |
| GameNotFound = 10 | The selected game does not exist. |
| GameStarted = 11 | The cancellation deadline has passed. |
| BetNotFound = 20 | The selected bet does not exist. |
| BetAlreadyCancelled = 21 | The bet has already been cancelled. |

## Connections

- [Game.BetCancelled](#type-game-core-gameeventpublisher-betcancelled) → [Account](account.md): RefundBet — Return the bet amount to the player's account after cancellation.

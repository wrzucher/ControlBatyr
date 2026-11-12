# Business API

## Managers

<a id="type-game-core-gamemanager"></a>

### GameManager

Lets players place bets and withdraw them until their game starts.

#### Methods

| Method | Description |
| --- | --- |
| CreateGame(name: string, startsAt: DateTimeOffset) → [Game](#type-game-model-game) | Sets when the game starts and bets can no longer be cancelled. |
| PlaceBet(gameId: Guid, playerId: Guid, amount: decimal) → [Bet](#type-game-model-bet) | Places a player's bet on an existing game. |
| CancelBetProblem(betId: Guid) → IReadOnlyList&lt;ServiceErrorCode&gt; | Explains why the player cannot cancel the bet. |
| CancelBet(betId: Guid) → void | Lets the player cancel an open bet before the game starts. |
| Bet(betId: Guid) → [Bet](#type-game-model-bet)? | Lets the player see the bet's current state. |

#### ServiceErrorCode

Reasons a requested game operation cannot proceed.

| Value | Description |
| --- | --- |
| GameNotFound = 10 | The selected game does not exist. |
| GameStarted = 11 | The cancellation deadline has passed. |
| BetNotFound = 20 | The selected bet does not exist. |
| BetAlreadyCancelled = 21 | The bet has already been cancelled. |

## Object models

<a id="type-game-model-bet"></a>

### Bet

A player's bet on a game, including its current cancellation state.

#### Data

| Name | Type | Description |
| --- | --- | --- |
| Id | Guid | Identifies the bet for later lookup or cancellation. |
| GameId | Guid | The game the player chose. |
| PlayerId | Guid | The player who placed the bet. |
| Amount | decimal | The amount the player bet. |
| State | BetState | Whether the bet is still open or has been cancelled. |
| GameStartsAt | DateTimeOffset | The deadline for cancelling the bet. |

#### Computed properties

| Name | Type | Description |
| --- | --- | --- |
| IsOpen | bool | Whether the bet is still open. |

#### BetState

The lifecycle of a player's bet.

| Value | Description |
| --- | --- |
| Open = 0 | The bet remains open. |
| Cancelled = 1 | The player has cancelled the bet. |

<a id="type-game-model-game"></a>

### Game

A scheduled game that players can bet on.

#### Data

| Name | Type | Description |
| --- | --- | --- |
| Id | Guid | Identifies the game when placing a bet. |
| Name | string | The title shown to players. |
| StartsAt | DateTimeOffset | When play begins and cancellation closes. |

## Events

<a id="type-game-core-gameeventpublisher"></a>

### GameEventPublisher

Announces changes to player bets after they have been recorded.

| Event | Payload | Description |
| --- | --- | --- |
| BetPlaced | [Bet](#type-game-model-bet) | The player's bet has been placed. |
| BetCancelled | Guid | The player's bet has been cancelled. |

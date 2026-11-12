# Business API

## Managers

<a id="type-game-core-gamemanager"></a>

### GameManager

Lets players place bets and withdraw them until their game starts.

#### Methods

| Method | Description |
| --- | --- |
| Bet(betId: Guid) → [Bet](#type-game-model-bet)? | Lets the player see the bet's current state. |
| CancelBet(betId: Guid) → void | Lets the player cancel an open bet before the game starts. |
| CancelBetProblem(betId: Guid) → IReadOnlyList&lt;ServiceErrorCode&gt; | Explains why the player cannot cancel the bet. |
| CreateGame(name: string, startsAt: DateTimeOffset) → [Game](#type-game-model-game) | Sets when the game starts and bets can no longer be cancelled. |
| PlaceBet(gameId: Guid, playerId: Guid, amount: decimal) → [Bet](#type-game-model-bet) | Places a player's bet on an existing game. |

#### ServiceErrorCode

Reasons a requested game operation cannot proceed.

| Value | Description |
| --- | --- |
| BetAlreadyCancelled = 21 | The stake has already been withdrawn. |
| BetNotFound = 20 | The selected bet does not exist. |
| GameNotFound = 10 | The selected game does not exist. |
| GameStarted = 11 | The cancellation deadline has passed. |

## Object models

<a id="type-game-model-bet"></a>

### Bet

A player's stake on a game, including its current cancellation state.

#### Data

| Name | Type | Description |
| --- | --- | --- |
| Amount | decimal | The amount the player committed. |
| GameId | Guid | The game the player chose. |
| GameStartsAt | DateTimeOffset | The deadline for withdrawing the stake. |
| Id | Guid | Identifies the bet for later lookup or cancellation. |
| PlayerId | Guid | The player who owns the stake. |
| State | BetState | Whether the stake is still in play or has been withdrawn. |

#### Computed properties

| Name | Type | Description |
| --- | --- | --- |
| IsOpen | bool | Whether cancellation is still possible before the game starts. |

#### BetState

The lifecycle of a player's stake.

| Value | Description |
| --- | --- |
| Cancelled = 1 | The player has withdrawn the stake. |
| Open = 0 | The stake remains in play. |

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
| BetCancelled | Guid | The player's bet has been cancelled. |
| BetPlaced | [Bet](#type-game-model-bet) | The player's bet has been placed. |

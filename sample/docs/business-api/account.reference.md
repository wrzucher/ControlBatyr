# Account reference

[Business API](index.md) · [Overview](account.md)

## Operations

<a id="type-game-core-accountmanager"></a>

### AccountManager

[Source](../../Game.Core/AccountManager.cs)

Holds players' funds and returns cancelled bet amounts.

<a id="type-game-core-accountmanager-openaccountasync"></a>

#### OpenAccount

Opens an account with the player's initial funds.

**Parameters**

| Name | Type |
| --- | --- |
| playerId | Guid |
| initialBalance | decimal |

**Returns:** [Account](#type-game-model-account)

<a id="type-game-core-accountmanager-accountasync"></a>

#### Account

Shows the player's available funds.

**Parameters**

| Name | Type |
| --- | --- |
| playerId | Guid |

**Returns:** [Account](#type-game-model-account)

<a id="type-game-core-accountmanager-refundbetasync"></a>

#### RefundBet

Returns the bet amount to the player after cancellation.

**Parameters**

| Name | Type |
| --- | --- |
| playerId | Guid |
| betId | Guid |
| amount | decimal |

**Raises:** [BetRefunded](#type-game-core-betrefunded)

**Requirements**

- Cancelling a bet refunds its bet amount to its players account — [Source](../../Game.Tests/AccountRequirements.cs)

## Events

<a id="type-game-core-betrefunded"></a>

#### BetRefunded

A cancelled bet amount was returned to the player.

**Payload:** [BetRefunded](#type-game-core-betrefunded-payload)

**Raised by:** [RefundBet](#type-game-core-accountmanager-refundbetasync)

## Models

<a id="type-game-core-betrefunded-payload"></a>

### BetRefunded

[Source](../../Game.Core/BetRefunded.cs)

#### Data

| Name | Type |
| --- | --- |
| PlayerId | Guid |
| BetId | Guid |
| Amount | decimal |

<a id="type-game-model-account"></a>

### Account

[Source](../../Game.Model/Account.cs)

The funds available to a player.

#### Data

| Name | Type |
| --- | --- |
| PlayerId | Guid |
| Balance | decimal |

## Connections

<a id="type-game-core-gameaccountconnections-refundbetasync"></a>

- [Game.BetCancelled](game.reference.md#type-game-core-gameeventpublisher-betcancelled) → [Account](account.md): RefundBet — Return the bet amount to the player's account after cancellation.

[Source](../../Game.Core/GameAccountConnections.cs)

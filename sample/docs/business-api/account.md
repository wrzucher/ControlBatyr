# Account

[Business API](index.md)

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

**Requirements**

No requirements

<a id="type-game-core-accountmanager-accountasync"></a>

#### Account

Shows the player's available funds.

**Parameters**

| Name | Type |
| --- | --- |
| playerId | Guid |

**Returns:** [Account](#type-game-model-account)

**Requirements**

No requirements

<a id="type-game-core-accountmanager-refundbetasync"></a>

#### RefundBet

Returns the bet amount to the player after cancellation.

**Parameters**

| Name | Type |
| --- | --- |
| playerId | Guid |
| betId | Guid |
| amount | decimal |

**Requirements**

- Cancelling a bet refunds its bet amount to its players account — [Source](../../Game.Tests/AccountRequirements.cs)

## Models

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

- [Game.BetCancelled](game.md#type-game-core-gameeventpublisher-betcancelled) → [Account](account.md): RefundBet — Return the bet amount to the player's account after cancellation.

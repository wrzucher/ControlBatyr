# Account

[Business API](index.md)

## Operations

<a id="type-game-core-accountmanager"></a>

### AccountManager

[Source](../../Game.Core/AccountManager.cs)

Holds players' funds and returns the amounts of cancelled bets.

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

Returns the cancelled bet's amount to the player who placed it.

**Parameters**

| Name | Type |
| --- | --- |
| playerId | Guid |
| betId | Guid |
| amount | decimal |

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

- [Game.BetCancelled](game.md#type-game-core-gameeventpublisher-betcancelled) → [Account.RefundBetAsync](#type-game-core-accountmanager-refundbetasync) — Return the cancelled bet's stake to its player's account.

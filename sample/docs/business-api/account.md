# Account

Holds players' funds and returns cancelled bet amounts.

[Business API](index.md) · [Reference](account.reference.md)

## Operations

- [OpenAccount](account.reference.md#type-game-core-accountmanager-openaccountasync) — Opens an account with the player's initial funds.

- [Account](account.reference.md#type-game-core-accountmanager-accountasync) — Shows the player's available funds.

- [RefundBet](account.reference.md#type-game-core-accountmanager-refundbetasync) — Returns the bet amount to the player after cancellation.

## Events

- [BetRefunded](account.reference.md#type-game-core-betrefunded) — A cancelled bet amount was returned to the player.

## Connections

- [RefundBet](account.reference.md#type-game-core-gameaccountconnections-refundbetasync) — Return the bet amount to the player's account after cancellation.

## Models

- [Account](account.reference.md#type-game-model-account) — The funds available to a player.

## Diagram

```mermaid
classDiagram
%% @arch/1 diagram profile=class-v1
%% @arch/1 type id="A:Game.Core|T:Game.Core.AccountManager"
class C0["AccountManager"] {
    +OpenAccount()
    +Account()
    +RefundBet()
}
%% @arch/1 type id="A:Game.Model|T:Game.Model.Account"
class C1["Account"] {
    <<record>>
}
%% @arch/1 type id="A:Game.Core|T:Game.Core.BetRefunded"
class C2["BetRefunded"] {
    <<event>>
}
%% @arch/1 type id="A:Game.Core|E:Game.Core.GameEventPublisher.BetCancelled"
class C3["BetCancelled"] {
    <<event>>
}
C0 ..> C1
C0 ..> C2 : raises RefundBet
C3 ..> C0 : on
```

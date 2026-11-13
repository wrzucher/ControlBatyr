# Game

Lets players place bets and cancel them until their game starts.

[Business API](index.md) · [Reference](game.reference.md)

## Operations

- [CreateGame](game.reference.md#type-game-core-gamemanager-creategameasync) — Sets when the game starts and bets can no longer be cancelled.

- [PlaceBet](game.reference.md#type-game-core-gamemanager-placebetasync) — Places a player's bet on an existing game.

- [CancelBetProblem](game.reference.md#type-game-core-gamemanager-cancelbetproblemasync) — Explains why the player cannot cancel the bet.

- [CancelBet](game.reference.md#type-game-core-gamemanager-cancelbetasync) — Lets the player cancel an open bet before the game starts.

- [Bet](game.reference.md#type-game-core-gamemanager-betasync) — Lets the player see the bet's current state.

## Events

- [BetPlaced](game.reference.md#type-game-core-gameeventpublisher-betplaced) — The player's bet has been placed.

- [BetCancelled](game.reference.md#type-game-core-gameeventpublisher-betcancelled) — The player's bet has been cancelled.

## Connections

- [RefundBet](account.reference.md#type-game-core-gameaccountconnections-refundbetasync) — Return the bet amount to the player's account after cancellation.

## Models

- [Bet](game.reference.md#type-game-model-bet) — A player's bet on a game, including its current cancellation state.

- [BetCancelledEventArgs](game.reference.md#type-game-model-betcancelledeventargs) — Identifies the bet whose cancellation has been recorded.

- [BetPlacedEventArgs](game.reference.md#type-game-model-betplacedeventargs) — The recorded bet announced to listeners.

- [BetState](game.reference.md#type-game-model-betstate) — The lifecycle of a player's bet.

- [Game](game.reference.md#type-game-model-game) — A scheduled game that players can bet on.

- [ServiceErrorCode](game.reference.md#type-game-model-serviceerrorcode) — Reasons a requested game operation cannot proceed.

## Diagram

```mermaid
classDiagram
%% @arch/1 diagram profile=class-v1
%% @arch/1 type id="A:Game.Core|T:Game.Core.GameManager"
class C0["GameManager"] {
    +CreateGame()
    +PlaceBet()
    +CancelBetProblem()
    +CancelBet()
    +Bet()
}
%% @arch/1 type id="A:Game.Model|T:Game.Model.Bet"
class C1["Bet"] {
    <<record>>
    +Guid Id
    +Guid GameId
    +Guid PlayerId
    +decimal Amount
    +BetState State
    +DateTimeOffset GameStartsAt
    +bool IsOpen
}
%% @arch/1 type id="A:Game.Model|T:Game.Model.BetCancelledEventArgs"
class C2["BetCancelledEventArgs"] {
    <<record>>
    +Guid BetId
    +Guid PlayerId
    +decimal Amount
}
%% @arch/1 type id="A:Game.Model|T:Game.Model.BetPlacedEventArgs"
class C3["BetPlacedEventArgs"] {
    <<record>>
    +Bet Bet
}
%% @arch/1 type id="A:Game.Model|T:Game.Model.BetState"
class C4["BetState"] {
    <<enumeration>>
    +Open
    +Cancelled
}
%% @arch/1 type id="A:Game.Model|T:Game.Model.Game"
class C5["Game"] {
    <<record>>
    +Guid Id
    +string Name
    +DateTimeOffset StartsAt
}
%% @arch/1 type id="A:Game.Model|T:Game.Model.ServiceErrorCode"
class C6["ServiceErrorCode"] {
    <<enumeration>>
    +GameNotFound
    +GameStarted
    +BetNotFound
    +BetAlreadyCancelled
}
%% @arch/1 type id="A:Game.Core|E:Game.Core.GameEventPublisher.BetPlaced"
class C7["BetPlaced"] {
    <<event>>
    +Bet Bet
}
%% @arch/1 type id="A:Game.Core|E:Game.Core.GameEventPublisher.BetCancelled"
class C8["BetCancelled"] {
    <<event>>
    +Guid BetId
    +Guid PlayerId
    +decimal Amount
}
%% @arch/1 type id="A:Game.Core|T:Game.Core.AccountManager"
class C9["Account"] {
}
C0 ..> C5
C0 ..> C1
C0 ..> C6
C0 ..> C7 : raises PlaceBet
C0 ..> C8 : raises CancelBet
C8 ..> C9 : on
```

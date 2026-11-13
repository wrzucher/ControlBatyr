# Business API

## Domains

- [Account](account.md) — Holds players' funds and returns cancelled bet amounts.

- [Game](game.md) — Lets players place bets and cancel them until their game starts.

## Connections

- [Game.BetCancelled](game.reference.md#type-game-core-gameeventpublisher-betcancelled) → [Account](account.md): RefundBet — Return the bet amount to the player's account after cancellation.

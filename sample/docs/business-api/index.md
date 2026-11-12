# Business API

## Domains

- [Account](account.md) — Holds players' funds and returns cancelled bet amounts.

  1 of 3 operations have requirements.

- [Game](game.md) — Lets players place bets and cancel them until their game starts.

  3 of 5 operations have requirements.

## Connections

- [Game.BetCancelled](game.md#type-game-core-gameeventpublisher-betcancelled) → [Account](account.md): RefundBet — Return the bet amount to the player's account after cancellation.

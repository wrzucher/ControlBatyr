# Business API

## Domains

- [Account](account.md) — Holds players' funds and returns the amounts of cancelled bets.

- [Game](game.md) — Lets players place bets and cancel them until their game starts.

## Connections

- [Game.BetCancelled](game.md#type-game-core-gameeventpublisher-betcancelled) → [Account.RefundBetAsync](account.md#type-game-core-accountmanager-refundbetasync) — Return the cancelled bet's stake to its player's account.

# Deck Building Backend Findings

Unity is now wired to the current backend behavior without editing the server, but these contract notes are worth aligning later.

## Deck creation

`PUT /api/v1/decks` requires both `deckId` and `displayName`. Unity now generates a client-side `deck_{guid}` for new decks before calling PUT.

If the backend should own deck IDs, change `DecksController.Upsert` to allow an empty `DeckId` and return the created deck.

## Deck deletion

I did not find a deck-level delete endpoint. Current backend only has:

```text
DELETE /api/v1/decks/{playerId}/{deckId}/cards/{entryId}
```

If deck deletion is expected in the UI, add something like:

```text
DELETE /api/v1/decks/{playerId}/{deckId}
```

## Incremental add-card editing

`POST /api/v1/decks/{playerId}/{deckId}/cards` validates the whole resulting deck. That can reject incremental building while the deck has fewer than the minimum card count.

Unity therefore edits a local working copy and saves the full deck with `PUT /api/v1/decks` when it is valid.

## Ownership validation

Unity limits deck editing to cards the player owns, using `GET /api/v1/players/{userId}/cards/summary`.

Server-side deck validation should also enforce ownership and available copy counts, because the client is only a convenience layer.

Current client validation mirrors server deck size:

```text
MinDeckSize = 20
MaxDeckSize = 30
MaxCopiesPerCard = 3
```

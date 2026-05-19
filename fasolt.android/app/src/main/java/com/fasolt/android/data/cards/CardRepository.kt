package com.fasolt.android.data.cards

import com.fasolt.android.data.api.FasoltApi
import com.fasolt.android.data.api.models.CardDto
import com.fasolt.android.data.api.models.CreateCardRequest
import com.fasolt.android.data.api.models.Paginated
import com.fasolt.android.data.api.models.SetSuspendedRequest
import com.fasolt.android.data.api.models.UpdateCardRequest

class CardRepository(private val api: FasoltApi) {
    suspend fun list(
        deckId: String? = null,
        sourceFile: String? = null,
        cursor: String? = null,
        limit: Int? = null,
    ): Paginated<CardDto> = api.listCards(deckId, sourceFile, cursor, limit)

    suspend fun get(id: String): CardDto = api.getCard(id)

    suspend fun create(
        front: String,
        back: String,
        sourceFile: String? = null,
        deckId: String? = null,
    ): CardDto = api.createCard(CreateCardRequest(front, back, sourceFile, deckId))

    suspend fun update(
        id: String,
        front: String,
        back: String,
        sourceFile: String? = null,
        deckIds: List<String>? = null,
    ): CardDto = api.updateCard(id, UpdateCardRequest(front, back, sourceFile, deckIds))

    suspend fun delete(id: String) = api.deleteCard(id)

    suspend fun setSuspended(id: String, isSuspended: Boolean): CardDto =
        api.setCardSuspended(id, SetSuspendedRequest(isSuspended))
}

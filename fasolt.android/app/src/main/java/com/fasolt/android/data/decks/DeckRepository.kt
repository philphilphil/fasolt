package com.fasolt.android.data.decks

import com.fasolt.android.data.api.FasoltApi
import com.fasolt.android.data.api.models.CreateDeckRequest
import com.fasolt.android.data.api.models.DeckDetailDto
import com.fasolt.android.data.api.models.DeckDto
import com.fasolt.android.data.api.models.SetSuspendedRequest
import com.fasolt.android.data.api.models.UpdateDeckRequest

class DeckRepository(private val api: FasoltApi) {
    suspend fun list(): List<DeckDto> = api.listDecks()
    suspend fun get(id: String): DeckDetailDto = api.getDeck(id)
    suspend fun create(name: String, description: String?): DeckDto =
        api.createDeck(CreateDeckRequest(name, description))
    suspend fun update(id: String, name: String, description: String?): DeckDto =
        api.updateDeck(id, UpdateDeckRequest(name, description))
    suspend fun delete(id: String, deleteCards: Boolean = false) =
        api.deleteDeck(id, deleteCards)
    suspend fun setSuspended(id: String, isSuspended: Boolean): DeckDto =
        api.setDeckSuspended(id, SetSuspendedRequest(isSuspended))
}

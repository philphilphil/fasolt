package com.fasolt.android.data.decks

import com.fasolt.android.data.api.FasoltApi
import com.fasolt.android.data.api.models.DeckDto

class DeckRepository(private val api: FasoltApi) {
    suspend fun fetchDecks(): List<DeckDto> = api.getDecks()
}

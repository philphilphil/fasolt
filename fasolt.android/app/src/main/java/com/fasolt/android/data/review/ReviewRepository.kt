package com.fasolt.android.data.review

import com.fasolt.android.data.api.FasoltApi
import com.fasolt.android.data.api.models.DueCardDto
import com.fasolt.android.data.api.models.Overview
import com.fasolt.android.data.api.models.ProgressDto
import com.fasolt.android.data.api.models.RateCardRequest
import com.fasolt.android.data.api.models.RateCardResponse
import com.fasolt.android.data.api.models.ReviewStats
import com.fasolt.android.data.api.models.StudyStats

class ReviewRepository(private val api: FasoltApi) {
    suspend fun due(deckId: String? = null, limit: Int = 50): List<DueCardDto> =
        api.dueCards(deckId, limit)

    suspend fun rate(cardId: String, rating: String): RateCardResponse =
        api.rateCard(RateCardRequest(cardId, rating))

    suspend fun stats(): ReviewStats = api.reviewStats()
    suspend fun overview(): Overview = api.overview()
    suspend fun studyStats(): StudyStats = api.studyStats()
    suspend fun progress(): ProgressDto = api.progress()
}

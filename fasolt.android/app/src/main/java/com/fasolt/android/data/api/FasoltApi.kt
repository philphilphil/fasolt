package com.fasolt.android.data.api

import com.fasolt.android.data.api.models.CardDto
import com.fasolt.android.data.api.models.CreateCardRequest
import com.fasolt.android.data.api.models.CreateDeckRequest
import com.fasolt.android.data.api.models.DeckDetailDto
import com.fasolt.android.data.api.models.DeckDto
import com.fasolt.android.data.api.models.DeviceTokenRequest
import com.fasolt.android.data.api.models.DueCardDto
import com.fasolt.android.data.api.models.NotificationSettings
import com.fasolt.android.data.api.models.Overview
import com.fasolt.android.data.api.models.Paginated
import com.fasolt.android.data.api.models.ProgressDto
import com.fasolt.android.data.api.models.RateCardRequest
import com.fasolt.android.data.api.models.RateCardResponse
import com.fasolt.android.data.api.models.ReviewStats
import com.fasolt.android.data.api.models.SchedulingSettings
import com.fasolt.android.data.api.models.CreateSnapshotResponse
import com.fasolt.android.data.api.models.SetSuspendedRequest
import com.fasolt.android.data.api.models.SnapshotDto
import com.fasolt.android.data.api.models.SourceDto
import com.fasolt.android.data.api.models.StudyStats
import com.fasolt.android.data.api.models.TokenResponse
import com.fasolt.android.data.api.models.UpdateCardRequest
import com.fasolt.android.data.api.models.UpdateDeckRequest
import com.fasolt.android.data.api.models.UpdateNotificationSettingsRequest
import com.fasolt.android.data.api.models.UpdateSchedulingSettingsRequest
import com.fasolt.android.data.api.models.UserInfo
import retrofit2.http.Body
import retrofit2.http.DELETE
import retrofit2.http.Field
import retrofit2.http.FormUrlEncoded
import retrofit2.http.GET
import retrofit2.http.POST
import retrofit2.http.PUT
import retrofit2.http.Path
import retrofit2.http.Query

interface AuthApi {
    @FormUrlEncoded
    @POST("oauth/token")
    suspend fun token(
        @Field("grant_type") grantType: String,
        @Field("client_id") clientId: String,
        @Field("code") code: String? = null,
        @Field("redirect_uri") redirectUri: String? = null,
        @Field("code_verifier") codeVerifier: String? = null,
        @Field("refresh_token") refreshToken: String? = null,
    ): TokenResponse
}

interface FasoltApi {
    // Account
    @GET("api/account/me")
    suspend fun me(): UserInfo

    @DELETE("api/account")
    suspend fun deleteAccount()

    // Decks
    @GET("api/decks")
    suspend fun listDecks(): List<DeckDto>

    @GET("api/decks/{id}")
    suspend fun getDeck(@Path("id") id: String): DeckDetailDto

    @POST("api/decks")
    suspend fun createDeck(@Body request: CreateDeckRequest): DeckDto

    @PUT("api/decks/{id}")
    suspend fun updateDeck(@Path("id") id: String, @Body request: UpdateDeckRequest): DeckDto

    @DELETE("api/decks/{id}")
    suspend fun deleteDeck(
        @Path("id") id: String,
        @Query("deleteCards") deleteCards: Boolean = false,
    )

    @PUT("api/decks/{id}/suspended")
    suspend fun setDeckSuspended(@Path("id") id: String, @Body request: SetSuspendedRequest): DeckDto

    // Cards
    @GET("api/cards")
    suspend fun listCards(
        @Query("deckId") deckId: String? = null,
        @Query("sourceFile") sourceFile: String? = null,
        @Query("cursor") cursor: String? = null,
        @Query("limit") limit: Int? = null,
    ): Paginated<CardDto>

    @GET("api/cards/{id}")
    suspend fun getCard(@Path("id") id: String): CardDto

    @POST("api/cards")
    suspend fun createCard(@Body request: CreateCardRequest): CardDto

    @PUT("api/cards/{id}")
    suspend fun updateCard(@Path("id") id: String, @Body request: UpdateCardRequest): CardDto

    @DELETE("api/cards/{id}")
    suspend fun deleteCard(@Path("id") id: String)

    @PUT("api/cards/{id}/suspended")
    suspend fun setCardSuspended(@Path("id") id: String, @Body request: SetSuspendedRequest): CardDto

    // Review
    @GET("api/review/due")
    suspend fun dueCards(
        @Query("deckId") deckId: String? = null,
        @Query("limit") limit: Int = 50,
    ): List<DueCardDto>

    @POST("api/review/rate")
    suspend fun rateCard(@Body request: RateCardRequest): RateCardResponse

    @GET("api/review/stats")
    suspend fun reviewStats(): ReviewStats

    @GET("api/review/overview")
    suspend fun overview(): Overview

    @GET("api/review/study-stats")
    suspend fun studyStats(): StudyStats

    @GET("api/review/progress")
    suspend fun progress(): ProgressDto

    // Sources
    @GET("api/sources")
    suspend fun listSources(): List<SourceDto>

    // Notifications
    @PUT("api/notifications/device-token")
    suspend fun upsertDeviceToken(@Body request: DeviceTokenRequest)

    @DELETE("api/notifications/device-token")
    suspend fun deleteDeviceToken()

    @GET("api/notifications/settings")
    suspend fun notificationSettings(): NotificationSettings

    @PUT("api/notifications/settings")
    suspend fun updateNotificationSettings(@Body request: UpdateNotificationSettingsRequest): NotificationSettings

    // Snapshots
    @POST("api/snapshots")
    suspend fun createSnapshots(): CreateSnapshotResponse

    @GET("api/snapshots/recent")
    suspend fun recentSnapshots(): List<SnapshotDto>

    // Scheduling (FSRS)
    @GET("api/settings/scheduling")
    suspend fun schedulingSettings(): SchedulingSettings

    @PUT("api/settings/scheduling")
    suspend fun updateSchedulingSettings(@Body request: UpdateSchedulingSettingsRequest): SchedulingSettings
}

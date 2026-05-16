package com.fasolt.android.data.api

import com.fasolt.android.data.api.models.DeckDto
import com.fasolt.android.data.api.models.TokenResponse
import retrofit2.http.Field
import retrofit2.http.FormUrlEncoded
import retrofit2.http.GET
import retrofit2.http.POST

interface FasoltApi {
    @GET("api/decks")
    suspend fun getDecks(): List<DeckDto>
}

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

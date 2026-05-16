package com.fasolt.android.data.api.models

import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable

@Serializable
data class TokenResponse(
    @SerialName("access_token") val accessToken: String,
    @SerialName("refresh_token") val refreshToken: String? = null,
    @SerialName("expires_in") val expiresIn: Int,
    @SerialName("token_type") val tokenType: String,
)

@Serializable
data class DeckDto(
    val id: String,
    val name: String,
    val description: String? = null,
    val cardCount: Int,
    val dueCount: Int,
    val createdAt: String,
    val isSuspended: Boolean,
)

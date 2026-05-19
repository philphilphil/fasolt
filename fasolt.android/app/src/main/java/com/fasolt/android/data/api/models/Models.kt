package com.fasolt.android.data.api.models

import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable

// MARK: - Auth

@Serializable
data class TokenResponse(
    @SerialName("access_token") val accessToken: String,
    @SerialName("refresh_token") val refreshToken: String? = null,
    @SerialName("expires_in") val expiresIn: Int,
    @SerialName("token_type") val tokenType: String,
)

// MARK: - User / Account

@Serializable
data class UserInfo(
    val email: String,
    val isAdmin: Boolean,
    val externalProvider: String? = null,
    val displayName: String? = null,
)

// MARK: - Decks

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

@Serializable
data class DeckDetailDto(
    val id: String,
    val name: String,
    val description: String? = null,
    val cardCount: Int,
    val dueCount: Int,
    val isSuspended: Boolean,
    val cards: List<DeckCardDto>,
)

@Serializable
data class DeckCardDto(
    val id: String,
    val front: String,
    val back: String,
    val sourceFile: String? = null,
    val state: String,
    val dueAt: String? = null,
    val isSuspended: Boolean,
    val stability: Double? = null,
    val difficulty: Double? = null,
    val step: Int? = null,
    val lastReviewedAt: String? = null,
    val frontSvg: String? = null,
    val backSvg: String? = null,
)

@Serializable
data class CreateDeckRequest(val name: String, val description: String? = null)

@Serializable
data class UpdateDeckRequest(val name: String, val description: String? = null)

@Serializable
data class SetSuspendedRequest(val isSuspended: Boolean)

// MARK: - Cards

@Serializable
data class CardDto(
    val id: String,
    val front: String,
    val back: String,
    val sourceFile: String? = null,
    val state: String,
    val dueAt: String? = null,
    val stability: Double? = null,
    val difficulty: Double? = null,
    val step: Int? = null,
    val lastReviewedAt: String? = null,
    val createdAt: String,
    val decks: List<CardDeckInfo> = emptyList(),
    val isSuspended: Boolean,
    val frontSvg: String? = null,
    val backSvg: String? = null,
)

@Serializable
data class CardDeckInfo(val id: String, val name: String, val isSuspended: Boolean)

@Serializable
data class CreateCardRequest(
    val front: String,
    val back: String,
    val sourceFile: String? = null,
    val deckId: String? = null,
)

@Serializable
data class UpdateCardRequest(
    val front: String,
    val back: String,
    val sourceFile: String? = null,
    val deckIds: List<String>? = null,
)

// MARK: - Review

@Serializable
data class DueCardDto(
    val id: String,
    val front: String,
    val back: String,
    val sourceFile: String? = null,
    val state: String,
    val frontSvg: String? = null,
    val backSvg: String? = null,
)

@Serializable
data class RateCardRequest(val cardId: String, val rating: String)

@Serializable
data class RateCardResponse(
    val cardId: String,
    val stability: Double? = null,
    val difficulty: Double? = null,
    val dueAt: String? = null,
    val state: String,
)

@Serializable
data class ReviewStats(val dueCount: Int, val totalCards: Int, val studiedToday: Int)

@Serializable
data class StudyStats(
    val currentStreak: Int,
    val bestStreak: Int,
    val totalAnswered: Int,
    val answeredToday: Int,
)

// MARK: - Progress

@Serializable
data class DailyActivity(val date: String, val count: Int, val hadDue: Boolean)

@Serializable
data class ProgressDto(
    val currentStreak: Int,
    val bestStreak: Int,
    val totalAnswered: Int,
    val answeredToday: Int,
    val answeredThisWeek: Int,
    val answeredThisMonth: Int,
    val dailyActivity: List<DailyActivity>,
)

// MARK: - Overview / Sources

@Serializable
data class Overview(
    val totalCards: Int,
    val dueCards: Int,
    val cardsByState: Map<String, Int>,
    val totalDecks: Int,
    val totalSources: Int,
)

@Serializable
data class SourceDto(
    val sourceFile: String,
    val cardCount: Int,
    val dueCount: Int,
)

// MARK: - Pagination

@Serializable
data class Paginated<T>(
    val items: List<T>,
    val hasMore: Boolean,
    val nextCursor: String? = null,
)

// MARK: - Notifications

@Serializable
data class DeviceTokenRequest(val token: String)

@Serializable
data class NotificationSettings(val intervalHours: Int, val hasDeviceToken: Boolean)

@Serializable
data class UpdateNotificationSettingsRequest(val intervalHours: Int)

// MARK: - Scheduling Settings (FSRS)

@Serializable
data class SchedulingSettings(
    val desiredRetention: Double,
    val maximumInterval: Int,
    val dayStartHour: Int,
    val timeZone: String? = null,
)

@Serializable
data class UpdateSchedulingSettingsRequest(
    val desiredRetention: Double,
    val maximumInterval: Int,
    val dayStartHour: Int,
    val timeZone: String,
)

// MARK: - Snapshots

@Serializable
data class SnapshotDto(
    val id: String,
    val deckName: String? = null,
    val cardCount: Int,
    val createdAt: String,
    val contentChanges: Int? = null,
)

@Serializable
data class CreateSnapshotResponse(val created: Int, val skipped: Int)

// MARK: - Account

@Serializable
data class ChangeEmailRequest(val newEmail: String, val password: String)

@Serializable
data class ChangePasswordRequest(val currentPassword: String, val newPassword: String)

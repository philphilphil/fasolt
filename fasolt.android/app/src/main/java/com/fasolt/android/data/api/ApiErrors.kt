package com.fasolt.android.data.api

import kotlinx.serialization.Serializable
import kotlinx.serialization.json.Json
import retrofit2.HttpException

@Serializable
private data class ApiErrorBody(val error: String? = null, val message: String? = null)

private val errorJson = Json {
    ignoreUnknownKeys = true
    isLenient = true
}

/**
 * The server reports structured failures as `{ error, message }` (see
 * ErrorResponseMiddleware and LinkedContentFilter) — e.g. a 403 for content reached
 * through a linked deck comes back with a human-readable explanation. HttpException's
 * own [Throwable.message] is just "HTTP 403 Forbidden", so pull the real message out
 * of the body when present and fall back to [default] otherwise.
 */
fun Throwable.apiMessage(default: String): String {
    if (this !is HttpException) return message ?: default
    val body = response()?.errorBody()?.string()?.takeIf { it.isNotBlank() } ?: return message ?: default
    return runCatching { errorJson.decodeFromString<ApiErrorBody>(body) }
        .getOrNull()?.message ?: (message ?: default)
}

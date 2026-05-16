package com.fasolt.android.data.api

import com.fasolt.android.data.auth.SecureStorage
import com.jakewharton.retrofit2.converter.kotlinx.serialization.asConverterFactory
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.logging.HttpLoggingInterceptor
import retrofit2.Retrofit
import java.util.concurrent.TimeUnit

/**
 * Builds Retrofit clients for the Fasolt backend.
 *
 * Two clients are produced:
 *  - [fasoltApi] — authenticated, injects the bearer token and triggers refresh on 401
 *  - [authApi]   — unauthenticated, used for /oauth/token (login + refresh)
 *
 * @param refreshToken called when an authenticated request returns 401. Returns true on
 *                    successful refresh so the original request can be retried.
 */
class FasoltApiFactory(
    private val secureStorage: SecureStorage,
    private val refreshToken: suspend () -> Boolean,
) {
    private val json = Json {
        ignoreUnknownKeys = true
        isLenient = true
    }

    private val baseUrl: String
        get() = secureStorage.serverUrl ?: DEFAULT_SERVER_URL

    private val loggingInterceptor = HttpLoggingInterceptor().apply {
        level = HttpLoggingInterceptor.Level.BASIC
    }

    val authApi: AuthApi by lazy { buildRetrofit(authenticated = false).create(AuthApi::class.java) }
    val fasoltApi: FasoltApi by lazy { buildRetrofit(authenticated = true).create(FasoltApi::class.java) }

    private fun buildRetrofit(authenticated: Boolean): Retrofit {
        val client = OkHttpClient.Builder()
            .connectTimeout(15, TimeUnit.SECONDS)
            .readTimeout(30, TimeUnit.SECONDS)
            .addInterceptor(loggingInterceptor)
            .apply {
                if (authenticated) {
                    addInterceptor(AuthInterceptor(secureStorage))
                    authenticator(TokenAuthenticator(secureStorage, refreshToken))
                }
            }
            .build()

        return Retrofit.Builder()
            .baseUrl(baseUrl.trimEnd('/') + "/")
            .client(client)
            .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
            .build()
    }

    companion object {
        const val DEFAULT_SERVER_URL = "https://fasolt.app"
    }
}

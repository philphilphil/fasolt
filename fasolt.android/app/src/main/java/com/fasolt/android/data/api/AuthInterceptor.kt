package com.fasolt.android.data.api

import com.fasolt.android.data.auth.SecureStorage
import kotlinx.coroutines.runBlocking
import okhttp3.Authenticator
import okhttp3.Interceptor
import okhttp3.Request
import okhttp3.Response
import okhttp3.Route

internal class AuthInterceptor(
    private val secureStorage: SecureStorage,
) : Interceptor {
    override fun intercept(chain: Interceptor.Chain): Response {
        val token = secureStorage.accessToken
        val request = chain.request().withBearer(token)
        return chain.proceed(request)
    }
}

internal class TokenAuthenticator(
    private val secureStorage: SecureStorage,
    private val refreshToken: suspend () -> Boolean,
) : Authenticator {
    override fun authenticate(route: Route?, response: Response): Request? {
        // Avoid infinite refresh loops — only retry once per request chain.
        if (response.priorResponse != null) return null

        val refreshed = runBlocking { refreshToken() }
        if (!refreshed) return null

        val newToken = secureStorage.accessToken ?: return null
        return response.request.withBearer(newToken)
    }
}

private fun Request.withBearer(token: String?): Request {
    if (token.isNullOrBlank()) return this
    return newBuilder().header("Authorization", "Bearer $token").build()
}

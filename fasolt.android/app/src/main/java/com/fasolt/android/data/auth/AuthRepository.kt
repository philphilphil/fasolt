package com.fasolt.android.data.auth

import android.content.Context
import android.content.Intent
import android.net.Uri
import com.fasolt.android.data.api.AuthApi
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import net.openid.appauth.AuthorizationException
import net.openid.appauth.AuthorizationRequest
import net.openid.appauth.AuthorizationResponse
import net.openid.appauth.AuthorizationService
import net.openid.appauth.AuthorizationServiceConfiguration
import net.openid.appauth.CodeVerifierUtil
import net.openid.appauth.ResponseTypeValues

/**
 * Mirrors the iOS [AuthService] PKCE flow:
 *  - Server returns OAuth endpoints under /oauth/authorize and /oauth/token
 *  - Client ID "fasolt-android" mirrors iOS "fasolt-ios"
 *  - Redirect scheme "fasolt://oauth/callback" is shared with iOS — backend already accepts it
 */
class AuthRepository(
    context: Context,
    private val secureStorage: SecureStorage,
    private val authApi: AuthApi,
) {
    private val appContext = context.applicationContext
    private val authService = AuthorizationService(appContext)
    private val refreshMutex = Mutex()

    private val _isAuthenticated = MutableStateFlow(hasValidSession())
    val isAuthenticated: StateFlow<Boolean> = _isAuthenticated.asStateFlow()

    private var pendingVerifier: String? = null

    /**
     * Builds the Intent that drives the Chrome Custom Tab login flow. The caller
     * launches it via [androidx.activity.result.ActivityResultLauncher] and feeds
     * the result back to [completeAuthorization].
     */
    fun buildAuthorizationIntent(serverUrl: String): Intent {
        val normalized = serverUrl.trimEnd('/')
        secureStorage.serverUrl = normalized

        val config = AuthorizationServiceConfiguration(
            Uri.parse("$normalized/oauth/authorize"),
            Uri.parse("$normalized/oauth/token"),
        )

        val verifier = CodeVerifierUtil.generateRandomCodeVerifier()
        pendingVerifier = verifier

        val request = AuthorizationRequest.Builder(
            config,
            CLIENT_ID,
            ResponseTypeValues.CODE,
            Uri.parse(REDIRECT_URI),
        )
            .setCodeVerifier(
                verifier,
                CodeVerifierUtil.deriveCodeVerifierChallenge(verifier),
                CodeVerifierUtil.getCodeVerifierChallengeMethod(),
            )
            .setScope("offline_access")
            .build()

        return authService.getAuthorizationRequestIntent(request)
    }

    /**
     * Handles the result of the Custom Tab. Exchanges the authorization code for
     * an access + refresh token pair and stores them.
     */
    suspend fun completeAuthorization(data: Intent?): Result<Unit> {
        if (data == null) return Result.failure(IllegalStateException("No auth response data"))

        val response = AuthorizationResponse.fromIntent(data)
        val exception = AuthorizationException.fromIntent(data)
        if (response == null) {
            return Result.failure(exception ?: IllegalStateException("No authorization response"))
        }
        val code = response.authorizationCode
            ?: return Result.failure(IllegalStateException("No authorization code in response"))
        val verifier = pendingVerifier
            ?: return Result.failure(IllegalStateException("PKCE verifier missing — relaunch login"))

        return runCatching {
            val tokens = authApi.token(
                grantType = "authorization_code",
                clientId = CLIENT_ID,
                code = code,
                redirectUri = REDIRECT_URI,
                codeVerifier = verifier,
            )
            storeTokens(tokens.accessToken, tokens.refreshToken, tokens.expiresIn)
            secureStorage.clientId = CLIENT_ID
            pendingVerifier = null
            _isAuthenticated.value = true
        }
    }

    /**
     * Coalesced refresh — multiple concurrent 401s share one refresh call.
     * Returns true on success so callers can retry their original request.
     */
    suspend fun refreshAccessToken(): Boolean = refreshMutex.withLock {
        val refresh = secureStorage.refreshToken ?: return@withLock false
        val client = secureStorage.clientId ?: CLIENT_ID
        runCatching {
            val tokens = authApi.token(
                grantType = "refresh_token",
                clientId = client,
                refreshToken = refresh,
            )
            storeTokens(tokens.accessToken, tokens.refreshToken ?: refresh, tokens.expiresIn)
            true
        }.getOrElse { error ->
            // Server-rejected refresh tokens are unrecoverable; clear and force re-login.
            // Network failures bubble up as exceptions other than HttpException, so we err
            // on the safe side and only clear on a 4xx-shaped error.
            if (error is retrofit2.HttpException && error.code() in 400..499) {
                signOut()
            }
            false
        }
    }

    fun signOut() {
        secureStorage.clear()
        _isAuthenticated.value = false
    }

    private fun storeTokens(accessToken: String, refreshToken: String?, expiresIn: Int) {
        secureStorage.accessToken = accessToken
        if (refreshToken != null) secureStorage.refreshToken = refreshToken
        secureStorage.tokenExpiryEpochMs = System.currentTimeMillis() + (expiresIn * 1000L)
    }

    private fun hasValidSession(): Boolean {
        if (secureStorage.accessToken == null) return false
        val expiry = secureStorage.tokenExpiryEpochMs
        if (expiry <= System.currentTimeMillis()) {
            return secureStorage.refreshToken != null
        }
        return true
    }

    companion object {
        const val CLIENT_ID = "fasolt-android"
        const val REDIRECT_URI = "fasolt://oauth/callback"
    }
}

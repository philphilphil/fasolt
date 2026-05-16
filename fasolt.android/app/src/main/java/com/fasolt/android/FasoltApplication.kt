package com.fasolt.android

import android.app.Application
import com.fasolt.android.data.api.FasoltApiFactory
import com.fasolt.android.data.auth.AuthRepository
import com.fasolt.android.data.auth.SecureStorage
import com.fasolt.android.data.decks.DeckRepository

class FasoltApplication : Application() {

    lateinit var secureStorage: SecureStorage
        private set
    lateinit var authRepository: AuthRepository
        private set
    lateinit var deckRepository: DeckRepository
        private set

    override fun onCreate() {
        super.onCreate()
        secureStorage = SecureStorage(this)
        val apiFactory = FasoltApiFactory(secureStorage) { authRepository.refreshAccessToken() }
        authRepository = AuthRepository(this, secureStorage, apiFactory.authApi)
        deckRepository = DeckRepository(apiFactory.fasoltApi)
    }
}

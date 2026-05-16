package com.fasolt.android

import android.app.Application
import com.fasolt.android.data.account.AccountRepository
import com.fasolt.android.data.api.FasoltApiFactory
import com.fasolt.android.data.auth.AuthRepository
import com.fasolt.android.data.auth.SecureStorage
import com.fasolt.android.data.cards.CardRepository
import com.fasolt.android.data.decks.DeckRepository
import com.fasolt.android.data.notifications.NotificationRepository
import com.fasolt.android.data.review.ReviewRepository
import com.fasolt.android.data.settings.SchedulingRepository
import com.fasolt.android.data.sources.SourceRepository

class FasoltApplication : Application() {

    lateinit var secureStorage: SecureStorage
        private set
    lateinit var authRepository: AuthRepository
        private set
    lateinit var accountRepository: AccountRepository
        private set
    lateinit var deckRepository: DeckRepository
        private set
    lateinit var cardRepository: CardRepository
        private set
    lateinit var reviewRepository: ReviewRepository
        private set
    lateinit var sourceRepository: SourceRepository
        private set
    lateinit var notificationRepository: NotificationRepository
        private set
    lateinit var schedulingRepository: SchedulingRepository
        private set

    override fun onCreate() {
        super.onCreate()
        secureStorage = SecureStorage(this)
        val apiFactory = FasoltApiFactory(secureStorage) { authRepository.refreshAccessToken() }
        authRepository = AuthRepository(this, secureStorage, apiFactory.authApi)
        accountRepository = AccountRepository(apiFactory.fasoltApi)
        deckRepository = DeckRepository(apiFactory.fasoltApi)
        cardRepository = CardRepository(apiFactory.fasoltApi)
        reviewRepository = ReviewRepository(apiFactory.fasoltApi)
        sourceRepository = SourceRepository(apiFactory.fasoltApi)
        notificationRepository = NotificationRepository(apiFactory.fasoltApi)
        schedulingRepository = SchedulingRepository(apiFactory.fasoltApi)
    }
}

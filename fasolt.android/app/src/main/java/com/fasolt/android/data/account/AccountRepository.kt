package com.fasolt.android.data.account

import com.fasolt.android.data.api.FasoltApi
import com.fasolt.android.data.api.models.UserInfo

class AccountRepository(private val api: FasoltApi) {
    suspend fun me(): UserInfo = api.me()
    suspend fun deleteAccount() = api.deleteAccount()
}

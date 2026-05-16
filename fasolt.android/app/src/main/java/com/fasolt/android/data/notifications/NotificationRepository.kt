package com.fasolt.android.data.notifications

import com.fasolt.android.data.api.FasoltApi
import com.fasolt.android.data.api.models.DeviceTokenRequest
import com.fasolt.android.data.api.models.NotificationSettings
import com.fasolt.android.data.api.models.UpdateNotificationSettingsRequest

class NotificationRepository(private val api: FasoltApi) {
    suspend fun upsertDeviceToken(token: String) = api.upsertDeviceToken(DeviceTokenRequest(token))
    suspend fun deleteDeviceToken() = api.deleteDeviceToken()
    suspend fun settings(): NotificationSettings = api.notificationSettings()
    suspend fun updateSettings(intervalHours: Int): NotificationSettings =
        api.updateNotificationSettings(UpdateNotificationSettingsRequest(intervalHours))
}

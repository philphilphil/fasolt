package com.fasolt.android.data.settings

import com.fasolt.android.data.api.FasoltApi
import com.fasolt.android.data.api.models.SchedulingSettings
import com.fasolt.android.data.api.models.UpdateSchedulingSettingsRequest

class SchedulingRepository(private val api: FasoltApi) {
    suspend fun get(): SchedulingSettings = api.schedulingSettings()
    suspend fun update(
        desiredRetention: Double,
        maximumInterval: Int,
        dayStartHour: Int,
        timeZone: String,
    ): SchedulingSettings = api.updateSchedulingSettings(
        UpdateSchedulingSettingsRequest(desiredRetention, maximumInterval, dayStartHour, timeZone)
    )
}

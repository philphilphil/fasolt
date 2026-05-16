package com.fasolt.android.data.snapshots

import com.fasolt.android.data.api.FasoltApi
import com.fasolt.android.data.api.models.CreateSnapshotResponse
import com.fasolt.android.data.api.models.SnapshotDto

class SnapshotRepository(private val api: FasoltApi) {
    suspend fun create(): CreateSnapshotResponse = api.createSnapshots()
    suspend fun recent(): List<SnapshotDto> = api.recentSnapshots()
}

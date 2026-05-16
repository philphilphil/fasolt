package com.fasolt.android.data.sources

import com.fasolt.android.data.api.FasoltApi
import com.fasolt.android.data.api.models.SourceDto

class SourceRepository(private val api: FasoltApi) {
    suspend fun list(): List<SourceDto> = api.listSources()
}

package com.fasolt.android.ui.settings

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Slider
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.lifecycle.viewmodel.compose.viewModel
import kotlin.math.roundToInt

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SchedulingSettingsScreen(
    onBack: () -> Unit,
    viewModel: SchedulingSettingsViewModel = viewModel(),
) {
    val state by viewModel.uiState.collectAsState()

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Scheduling (FSRS)") },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back")
                    }
                },
            )
        },
    ) { padding ->
        if (state.isLoading) {
            Box(Modifier.fillMaxSize().padding(padding), contentAlignment = Alignment.Center) {
                CircularProgressIndicator()
            }
            return@Scaffold
        }

        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .verticalScroll(rememberScrollState())
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(16.dp),
        ) {
            SettingSliderCard(
                title = "Desired retention",
                value = "${(state.desiredRetention * 100).roundToInt()}%",
                description = "How likely you want to remember a card when it's reviewed. " +
                    "Higher means more frequent reviews; lower means fewer reviews and more forgetting. " +
                    "Applies to future reviews only.",
            ) {
                Slider(
                    value = state.desiredRetention,
                    onValueChange = viewModel::setDesiredRetention,
                    valueRange = 0.80f..0.99f,
                    steps = 18,
                    enabled = !state.isSaving,
                )
            }

            SettingSliderCard(
                title = "Maximum interval",
                value = "${state.maximumInterval} days",
                description = "The longest gap between reviews. 365 means every card is seen at least once a year.",
            ) {
                Slider(
                    value = state.maximumInterval.toFloat(),
                    onValueChange = { viewModel.setMaximumInterval(it.roundToInt()) },
                    valueRange = 1f..365f,
                    enabled = !state.isSaving,
                )
            }

            SettingSliderCard(
                title = "Day starts at",
                value = "%02d:00".format(state.dayStartHour),
                description = "Hour at which a new study day begins, in your device's time zone. " +
                    "Cards scheduled a day or more in advance become due all at once at this time.",
            ) {
                Slider(
                    value = state.dayStartHour.toFloat(),
                    onValueChange = { viewModel.setDayStartHour(it.roundToInt()) },
                    valueRange = 0f..23f,
                    steps = 22,
                    enabled = !state.isSaving,
                )
            }

            state.successMessage?.let { msg ->
                Text(
                    msg,
                    color = MaterialTheme.colorScheme.primary,
                    style = MaterialTheme.typography.bodySmall,
                )
            }
            state.errorMessage?.let { error ->
                Text(
                    error,
                    color = MaterialTheme.colorScheme.error,
                    style = MaterialTheme.typography.bodySmall,
                )
            }

            Button(
                onClick = viewModel::save,
                enabled = !state.isSaving,
                modifier = Modifier.fillMaxWidth(),
            ) {
                if (state.isSaving) {
                    CircularProgressIndicator(
                        strokeWidth = 2.dp,
                        modifier = Modifier.size(18.dp),
                        color = MaterialTheme.colorScheme.onPrimary,
                    )
                    Spacer(Modifier.padding(start = 8.dp))
                    Text("Saving…")
                } else {
                    Text("Save")
                }
            }
        }
    }
}

@Composable
private fun SettingSliderCard(
    title: String,
    value: String,
    description: String,
    slider: @Composable () -> Unit,
) {
    Card(modifier = Modifier.fillMaxWidth()) {
        Column(Modifier.padding(16.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(
                    title,
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.SemiBold,
                    modifier = Modifier.weight(1f),
                )
                Text(
                    value,
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            Spacer(Modifier.height(4.dp))
            slider()
            Spacer(Modifier.height(4.dp))
            Text(
                description,
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}

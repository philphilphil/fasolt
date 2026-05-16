package com.fasolt.android.ui.decks.components

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.rememberModalBottomSheetState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.launch

/**
 * Modal bottom sheet that hosts a "create deck" or "edit deck" form.
 *
 * @param title shown at the top of the sheet (e.g. "New Deck" / "Edit Deck").
 * @param initialName pre-fills the name field; defaults to empty.
 * @param initialDescription pre-fills the description field; defaults to empty.
 * @param errorMessage if non-null, shown below the form (e.g. a server error).
 * @param onSubmit called with the trimmed name and (nullable) description when the user taps Save.
 * @param onDismiss invoked when the sheet is closed via swipe, scrim, or Cancel.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DeckFormSheet(
    title: String,
    initialName: String = "",
    initialDescription: String = "",
    errorMessage: String? = null,
    onSubmit: (name: String, description: String?) -> Unit,
    onDismiss: () -> Unit,
) {
    val sheetState = rememberModalBottomSheetState(skipPartiallyExpanded = true)
    val scope = rememberCoroutineScope()
    var name by rememberSaveable { mutableStateOf(initialName) }
    var description by rememberSaveable { mutableStateOf(initialDescription) }

    fun close(then: () -> Unit = {}) {
        scope.launch {
            sheetState.hide()
            then()
            onDismiss()
        }
    }

    ModalBottomSheet(
        onDismissRequest = onDismiss,
        sheetState = sheetState,
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 24.dp)
                .padding(bottom = 24.dp)
                .imePadding()
                .navigationBarsPadding(),
            verticalArrangement = Arrangement.spacedBy(16.dp),
        ) {
            Text(title, style = MaterialTheme.typography.titleLarge)

            OutlinedTextField(
                value = name,
                onValueChange = { name = it },
                label = { Text("Name") },
                singleLine = true,
                modifier = Modifier.fillMaxWidth(),
            )

            OutlinedTextField(
                value = description,
                onValueChange = { description = it },
                label = { Text("Description (optional)") },
                modifier = Modifier
                    .fillMaxWidth()
                    .heightIn(min = 80.dp),
            )

            if (errorMessage != null) {
                Text(
                    errorMessage,
                    color = MaterialTheme.colorScheme.error,
                    style = MaterialTheme.typography.bodySmall,
                )
            }

            androidx.compose.foundation.layout.Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(12.dp, alignment = androidx.compose.ui.Alignment.End),
            ) {
                TextButton(onClick = { close() }) { Text("Cancel") }
                Button(
                    onClick = {
                        val trimmedName = name.trim()
                        val trimmedDescription = description.trim().ifEmpty { null }
                        if (trimmedName.isNotEmpty()) {
                            onSubmit(trimmedName, trimmedDescription)
                        }
                    },
                    enabled = name.trim().isNotEmpty(),
                ) {
                    Text("Save")
                }
            }
        }
    }
}

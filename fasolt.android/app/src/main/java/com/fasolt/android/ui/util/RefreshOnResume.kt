package com.fasolt.android.ui.util

import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.rememberUpdatedState
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.lifecycle.compose.LocalLifecycleOwner

/**
 * Invokes [onResume] every time the host lifecycle goes RESUMED, except for
 * the very first composition (so a screen's own `init { load() }` isn't
 * shadowed by an immediate re-load).
 *
 * Use this when a screen needs to refresh server state after returning from
 * another destination — e.g. Dashboard after a study session ends, or
 * DeckDetail after the user studies that deck.
 */
@Composable
fun RefreshOnResume(onResume: () -> Unit) {
    val owner = LocalLifecycleOwner.current
    val callback = rememberUpdatedState(onResume)
    DisposableEffect(owner) {
        var first = true
        val observer = LifecycleEventObserver { _, event ->
            if (event == Lifecycle.Event.ON_RESUME) {
                if (first) {
                    first = false
                } else {
                    callback.value()
                }
            }
        }
        owner.lifecycle.addObserver(observer)
        onDispose { owner.lifecycle.removeObserver(observer) }
    }
}

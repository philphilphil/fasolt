package com.fasolt.android.ui.navigation

import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.platform.LocalContext
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.rememberNavController
import com.fasolt.android.FasoltApplication
import com.fasolt.android.ui.auth.LoginScreen
import com.fasolt.android.ui.decks.DecksScreen

private const val ROUTE_LOGIN = "login"
private const val ROUTE_DECKS = "decks"

@Composable
fun AppNavigation() {
    val navController = rememberNavController()
    val app = LocalContext.current.applicationContext as FasoltApplication
    val authed by app.authRepository.isAuthenticated.collectAsState()

    val startDestination = if (authed) ROUTE_DECKS else ROUTE_LOGIN

    NavHost(navController = navController, startDestination = startDestination) {
        composable(ROUTE_LOGIN) {
            LoginScreen(
                onAuthenticated = {
                    navController.navigate(ROUTE_DECKS) {
                        popUpTo(ROUTE_LOGIN) { inclusive = true }
                    }
                },
            )
        }
        composable(ROUTE_DECKS) {
            DecksScreen()
        }
    }
}

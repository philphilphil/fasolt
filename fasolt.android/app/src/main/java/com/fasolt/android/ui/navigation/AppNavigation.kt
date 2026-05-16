package com.fasolt.android.ui.navigation

import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Dashboard
import androidx.compose.material.icons.filled.PlayArrow
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material.icons.filled.Style
import androidx.compose.material3.Icon
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.navigation.NavGraphBuilder
import androidx.navigation.NavHostController
import androidx.navigation.NavType
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.currentBackStackEntryAsState
import androidx.navigation.compose.rememberNavController
import androidx.navigation.navArgument
import com.fasolt.android.FasoltApplication
import com.fasolt.android.ui.auth.LoginScreen
import com.fasolt.android.ui.cards.CardFormScreen
import com.fasolt.android.ui.cards.CardListScreen
import com.fasolt.android.ui.dashboard.DashboardScreen
import com.fasolt.android.ui.decks.DeckDetailScreen
import com.fasolt.android.ui.decks.DecksScreen
import com.fasolt.android.ui.progress.ProgressScreen
import com.fasolt.android.ui.settings.DeleteAccountScreen
import com.fasolt.android.ui.settings.McpSetupScreen
import com.fasolt.android.ui.settings.NotificationSettingsScreen
import com.fasolt.android.ui.settings.SchedulingSettingsScreen
import com.fasolt.android.ui.settings.SettingsScreen
import com.fasolt.android.ui.study.StudyScreen

private const val ROUTE_MAIN = "main"

@Composable
fun AppNavigation() {
    val rootNav = rememberNavController()
    val app = LocalContext.current.applicationContext as FasoltApplication
    val authed by app.authRepository.isAuthenticated.collectAsState()

    val start = if (authed) ROUTE_MAIN else Routes.LOGIN

    NavHost(navController = rootNav, startDestination = start) {
        composable(Routes.LOGIN) {
            LoginScreen(onAuthenticated = {
                rootNav.navigate(ROUTE_MAIN) {
                    popUpTo(Routes.LOGIN) { inclusive = true }
                }
            })
        }
        composable(ROUTE_MAIN) {
            MainScaffold(onSignedOut = {
                rootNav.navigate(Routes.LOGIN) {
                    popUpTo(ROUTE_MAIN) { inclusive = true }
                }
            })
        }
    }
}

private data class TabSpec(val route: String, val label: String, val icon: ImageVector)

private val TABS = listOf(
    TabSpec(Routes.DASHBOARD, "Home", Icons.Default.Dashboard),
    TabSpec(Routes.DECKS, "Decks", Icons.Default.Style),
    TabSpec("study", "Study", Icons.Default.PlayArrow),
    TabSpec(Routes.SETTINGS, "Settings", Icons.Default.Settings),
)

@Composable
private fun MainScaffold(onSignedOut: () -> Unit) {
    val tabNav = rememberNavController()
    val app = LocalContext.current.applicationContext as FasoltApplication
    val authed by app.authRepository.isAuthenticated.collectAsState()

    // Forced sign out (token refresh failure, account deletion, manual sign out) pops to login.
    LaunchedEffect(authed) {
        if (!authed) onSignedOut()
    }

    Scaffold(
        bottomBar = { FasoltBottomBar(tabNav) },
    ) { padding ->
        NavHost(
            navController = tabNav,
            startDestination = Routes.DASHBOARD,
            modifier = Modifier.padding(padding),
        ) {
            dashboardGraph(tabNav)
            decksGraph(tabNav)
            cardsGraph(tabNav)
            studyGraph(tabNav)
            settingsGraph(tabNav, onAccountDeleted = onSignedOut)
        }
    }
}

@Composable
private fun FasoltBottomBar(navController: NavHostController) {
    val backStack by navController.currentBackStackEntryAsState()
    val currentRoute = backStack?.destination?.route

    NavigationBar {
        TABS.forEach { tab ->
            // Tab is selected if the current destination's route starts with the tab's root.
            val selected = currentRoute?.substringBefore('?')?.substringBefore('/') == tab.route
            NavigationBarItem(
                selected = selected,
                onClick = {
                    navController.navigate(tab.route) {
                        popUpTo(navController.graph.startDestinationId) { saveState = true }
                        launchSingleTop = true
                        restoreState = true
                    }
                },
                icon = { Icon(tab.icon, contentDescription = tab.label) },
                label = { Text(tab.label) },
            )
        }
    }
}

// MARK: - Sub-graphs

private fun NavGraphBuilder.dashboardGraph(nav: NavHostController) {
    composable(Routes.DASHBOARD) {
        DashboardScreen(
            onStartStudy = { nav.navigate("study") },
            onOpenProgress = { nav.navigate(Routes.PROGRESS) },
        )
    }
    composable(Routes.PROGRESS) {
        ProgressScreen()
    }
}

private fun NavGraphBuilder.decksGraph(nav: NavHostController) {
    composable(Routes.DECKS) {
        DecksScreen(onDeckClick = { nav.navigate(Routes.deckDetail(it)) })
    }
    composable(
        route = Routes.DECK_DETAIL,
        arguments = listOf(navArgument("deckId") { type = NavType.StringType }),
    ) { backStackEntry ->
        val deckId = backStackEntry.arguments?.getString("deckId") ?: return@composable
        DeckDetailScreen(
            deckId = deckId,
            onNavigateBack = { nav.popBackStack() },
            onCardClick = { nav.navigate(Routes.cardEdit(it)) },
        )
    }
}

private fun NavGraphBuilder.cardsGraph(nav: NavHostController) {
    composable(Routes.CARDS) {
        CardListScreen(
            onCardClick = { nav.navigate(Routes.cardEdit(it)) },
            onCreateCard = { nav.navigate(Routes.CARD_CREATE) },
        )
    }
    composable(Routes.CARD_CREATE) {
        CardFormScreen(cardId = null, onNavigateBack = { nav.popBackStack() })
    }
    composable(
        route = Routes.CARD_EDIT,
        arguments = listOf(navArgument("cardId") { type = NavType.StringType }),
    ) { backStackEntry ->
        val cardId = backStackEntry.arguments?.getString("cardId") ?: return@composable
        CardFormScreen(cardId = cardId, onNavigateBack = { nav.popBackStack() })
    }
}

private fun NavGraphBuilder.studyGraph(nav: NavHostController) {
    composable(
        route = "study?deckId={deckId}",
        arguments = listOf(navArgument("deckId") {
            type = NavType.StringType
            nullable = true
            defaultValue = null
        }),
    ) { backStackEntry ->
        StudyScreen(
            deckId = backStackEntry.arguments?.getString("deckId"),
            onExit = { nav.popBackStack() },
        )
    }
}

private fun NavGraphBuilder.settingsGraph(nav: NavHostController, onAccountDeleted: () -> Unit) {
    composable(Routes.SETTINGS) {
        SettingsScreen(
            onOpenNotifications = { nav.navigate(Routes.SETTINGS_NOTIFICATIONS) },
            onOpenScheduling = { nav.navigate(Routes.SETTINGS_SCHEDULING) },
            onOpenMcpSetup = { nav.navigate(Routes.SETTINGS_MCP) },
            onOpenDeleteAccount = { nav.navigate(Routes.SETTINGS_DELETE_ACCOUNT) },
        )
    }
    composable(Routes.SETTINGS_NOTIFICATIONS) {
        NotificationSettingsScreen(onBack = { nav.popBackStack() })
    }
    composable(Routes.SETTINGS_SCHEDULING) {
        SchedulingSettingsScreen(onBack = { nav.popBackStack() })
    }
    composable(Routes.SETTINGS_MCP) {
        McpSetupScreen(onBack = { nav.popBackStack() })
    }
    composable(Routes.SETTINGS_DELETE_ACCOUNT) {
        DeleteAccountScreen(
            onBack = { nav.popBackStack() },
            onAccountDeleted = onAccountDeleted,
        )
    }
}

package com.fasolt.android.ui.navigation

/** Central registry of nav routes — keeps screens from string-typing routes inline. */
object Routes {
    const val LOGIN = "login"

    // Study tab (Dashboard)
    const val DASHBOARD = "dashboard"

    // Library tab — segmented Decks/Cards
    const val LIBRARY = "library"
    const val DECK_DETAIL = "decks/{deckId}"
    fun deckDetail(deckId: String) = "decks/$deckId"

    const val CARD_EDIT = "cards/{cardId}/edit"
    fun cardEdit(cardId: String) = "cards/$cardId/edit"
    const val CARD_CREATE = "cards/new"

    // Study (spaced-repetition flow) — presented fullscreen, hides bottom bar.
    const val STUDY = "study?deckId={deckId}"
    fun study(deckId: String? = null) = if (deckId == null) "study" else "study?deckId=$deckId"

    // Progress tab
    const val PROGRESS = "progress"

    // Settings tab
    const val SETTINGS = "settings"
    const val SETTINGS_NOTIFICATIONS = "settings/notifications"
    const val SETTINGS_SCHEDULING = "settings/scheduling"
    const val SETTINGS_MCP = "settings/mcp"
    const val SETTINGS_DELETE_ACCOUNT = "settings/delete-account"
    const val SETTINGS_HELP = "settings/help"

    /** Tab root routes, in bottom-bar order. */
    val TABS: List<String> = listOf(DASHBOARD, LIBRARY, PROGRESS, SETTINGS)
}

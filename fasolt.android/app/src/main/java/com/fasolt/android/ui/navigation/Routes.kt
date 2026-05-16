package com.fasolt.android.ui.navigation

/** Central registry of nav routes — keeps screens from string-typing routes inline. */
object Routes {
    const val LOGIN = "login"
    const val DASHBOARD = "dashboard"
    const val DECKS = "decks"
    const val DECK_DETAIL = "decks/{deckId}"
    fun deckDetail(deckId: String) = "decks/$deckId"

    const val CARDS = "cards"
    const val CARD_EDIT = "cards/{cardId}/edit"
    fun cardEdit(cardId: String) = "cards/$cardId/edit"
    const val CARD_CREATE = "cards/new"

    const val STUDY = "study?deckId={deckId}"
    fun study(deckId: String? = null) = if (deckId == null) "study" else "study?deckId=$deckId"

    const val PROGRESS = "progress"

    const val SETTINGS = "settings"
    const val SETTINGS_NOTIFICATIONS = "settings/notifications"
    const val SETTINGS_SCHEDULING = "settings/scheduling"
    const val SETTINGS_MCP = "settings/mcp"
    const val SETTINGS_DELETE_ACCOUNT = "settings/delete-account"

    /** Tabs visible in the bottom bar. */
    val TABS: List<String> = listOf(DASHBOARD, DECKS, STUDY.substringBefore('?'), SETTINGS)
}

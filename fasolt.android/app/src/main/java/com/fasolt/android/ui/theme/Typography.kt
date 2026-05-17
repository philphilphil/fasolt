package com.fasolt.android.ui.theme

import androidx.compose.material3.Typography
import androidx.compose.ui.text.TextStyle

// Material 3 default Typography with `tnum` enabled on the large numeric-friendly styles so
// stat values line up monospaced even though we keep the proportional font everywhere else.
private val Base = Typography()

val FasoltTypography = Typography(
    displayLarge = Base.displayLarge.withTabularNumerals(),
    displayMedium = Base.displayMedium.withTabularNumerals(),
    displaySmall = Base.displaySmall.withTabularNumerals(),
    headlineLarge = Base.headlineLarge.withTabularNumerals(),
    headlineMedium = Base.headlineMedium.withTabularNumerals(),
    headlineSmall = Base.headlineSmall.withTabularNumerals(),
    titleLarge = Base.titleLarge.withTabularNumerals(),
    titleMedium = Base.titleMedium,
    titleSmall = Base.titleSmall,
    bodyLarge = Base.bodyLarge,
    bodyMedium = Base.bodyMedium,
    bodySmall = Base.bodySmall,
    labelLarge = Base.labelLarge,
    labelMedium = Base.labelMedium,
    labelSmall = Base.labelSmall,
)

private fun TextStyle.withTabularNumerals(): TextStyle =
    copy(fontFeatureSettings = "tnum")

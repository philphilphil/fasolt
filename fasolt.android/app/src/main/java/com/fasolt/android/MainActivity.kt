package com.fasolt.android

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import com.fasolt.android.ui.navigation.AppNavigation
import com.fasolt.android.ui.theme.FasoltTheme

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            FasoltTheme {
                AppNavigation()
            }
        }
    }
}

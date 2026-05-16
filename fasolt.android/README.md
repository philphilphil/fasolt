# Fasolt Android

Native Android client for Fasolt — Kotlin + Jetpack Compose. Sibling to `fasolt.ios/`.

This is the **spike** scaffold for [issue #126](https://github.com/philphilphil/fasolt/issues/126), item 1: project scaffold + login + decks list.

## Requirements

- Android Studio Ladybug or newer (bundles JDK 17 + Android SDK)
- Android SDK 35 (compileSdk) — Studio installs on first sync
- An Android emulator (API 26+) or a physical device with USB debugging

## First-time setup

1. Open the `fasolt.android/` directory in Android Studio
2. Studio will prompt to **generate the Gradle wrapper** — accept (this repo does not check in `gradlew` / `gradle-wrapper.jar`)
3. Let Studio sync — first sync downloads ~500MB of Gradle + Android SDK packages
4. Run `app` configuration against an emulator or device

If Studio doesn't auto-generate the wrapper, run from the CLI once Gradle is on `PATH`:

```
gradle wrapper --gradle-version 8.11.1
```

## Running against local backend

The default server URL on the login screen is `https://fasolt.app`. To point at a local backend running on `localhost:8080`:

- **Emulator:** change the field to `http://10.0.2.2:8080` (the emulator's loopback to the host machine)
- **Physical device:** use your machine's LAN IP, e.g. `http://192.168.1.42:8080`. Note: cleartext HTTP only works in dev — `usesCleartextTraffic` is not enabled, so for non-localhost dev you'd need to add a `network-security-config` exemption.

The login flow uses OAuth 2.0 PKCE via Chrome Custom Tabs. The OAuth client ID is `fasolt-android` (mirrors iOS's `fasolt-ios`); the backend needs that ID registered in OpenIddict.

## Architecture

Folder layout under `app/src/main/java/com/fasolt/android/`:

```
data/api/        — Retrofit interfaces, OkHttp interceptors, DTOs
data/auth/       — SecureStorage (EncryptedSharedPreferences), AuthRepository (AppAuth PKCE)
data/decks/      — DeckRepository
ui/theme/        — Material 3 theme
ui/auth/         — Login screen + ViewModel
ui/decks/        — Decks list screen + ViewModel
ui/navigation/   — Compose Navigation graph
```

DI is intentionally minimal — `FasoltApplication` owns singleton instances; ViewModels reach them via `AndroidViewModel`. Hilt will be introduced once the scope justifies it.

## What's intentionally missing for v1 follow-ups

- Offline cache (Room) and pending-review queue → issue #126 item 4
- Card editing, study flow, dashboard, progress, settings → items 5–8
- FCM push notifications → item 9
- Network-state banner, polish → item 10

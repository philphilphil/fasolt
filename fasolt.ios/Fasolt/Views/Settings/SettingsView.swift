import SwiftUI

enum SettingsSegment: String, CaseIterable {
    case general = "General"
    case fsrs = "FSRS"
    case about = "About"
}

struct SettingsView: View {
    @Environment(AuthService.self) private var authService
    @State private var viewModel: SettingsViewModel
    @State private var notificationViewModel: NotificationSettingsViewModel
    @State private var schedulingViewModel: SchedulingSettingsViewModel
    @State private var selectedSegment: SettingsSegment = .general
    @State private var showSignOutConfirmation = false
    @State private var showDeleteAccount = false
    @AppStorage("hasSeenWelcomeFlow") private var hasSeenWelcomeFlow = false

    init(viewModel: SettingsViewModel, notificationViewModel: NotificationSettingsViewModel, schedulingViewModel: SchedulingSettingsViewModel) {
        _viewModel = State(initialValue: viewModel)
        _notificationViewModel = State(initialValue: notificationViewModel)
        _schedulingViewModel = State(initialValue: schedulingViewModel)
    }

    var body: some View {
        NavigationStack {
            VStack(spacing: 0) {
                Picker("View", selection: $selectedSegment) {
                    ForEach(SettingsSegment.allCases, id: \.self) { segment in
                        Text(segment.rawValue).tag(segment)
                    }
                }
                .pickerStyle(.segmented)
                .tint(FasoltTheme.accent)
                .padding(.horizontal, FasoltTheme.pagePadding)
                .padding(.top, 6)
                .padding(.bottom, 8)
                .background(FasoltTheme.paper0)

                switch selectedSegment {
                case .general:
                    generalContent
                case .fsrs:
                    fsrsContent
                case .about:
                    aboutContent
                }
            }
            .background(FasoltTheme.paper0.ignoresSafeArea())
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    NavigationLink {
                        HelpView()
                    } label: {
                        Label("Help", systemImage: "questionmark.circle")
                    }
                }
            }
            .offlineBanner()
            .alert("Sign Out?", isPresented: $showSignOutConfirmation) {
                Button("Cancel", role: .cancel) {}
                Button("Sign Out", role: .destructive) {
                    Task { await authService.signOut() }
                }
            } message: {
                Text("You'll need to sign in again to use Fasolt.")
            }
            .task {
                await viewModel.loadUserInfo()
                await notificationViewModel.load()
                await schedulingViewModel.load()
            }
        }
    }

    // MARK: - General

    private var generalContent: some View {
        List {
            McpSetupSection(serverURL: authService.serverURL)

            Section {
                if notificationViewModel.isLoading {
                    HStack {
                        Text("Loading...")
                            .foregroundStyle(FasoltTheme.ink2)
                        Spacer()
                        ProgressView()
                    }
                } else {
                    LabeledContent {
                        Text(notificationViewModel.permissionLabel)
                    } label: {
                        Label {
                            Text("Permission")
                        } icon: {
                            ColorIconBadge(systemName: "bell.fill", tint: FasoltTheme.accent)
                        }
                    }
                    .onTapGesture {
                        if notificationViewModel.isPermissionDenied {
                            if let url = URL(string: UIApplication.openSettingsURLString) {
                                UIApplication.shared.open(url)
                            }
                        }
                    }

                    Picker("Check interval", selection: Binding(
                            get: { notificationViewModel.intervalHours },
                            set: { newValue in
                                Task { await notificationViewModel.updateInterval(newValue) }
                            }
                        )) {
                            ForEach(NotificationSettingsViewModel.allowedIntervals, id: \.self) { hours in
                                Text("Every \(hours)h").tag(hours)
                            }
                        }

                    Label("How often to check for due cards and send a notification.", systemImage: "info.circle")
                        .font(.caption)
                        .foregroundStyle(FasoltTheme.ink2)

                    if let error = notificationViewModel.errorMessage {
                        Text(error)
                            .foregroundStyle(FasoltTheme.again)
                            .font(.caption)
                    }
                }
            } header: {
                Text("Notifications").sectionLabel()
            }

            Section {
                if viewModel.isLoading {
                    HStack {
                        Text("Loading...")
                            .foregroundStyle(FasoltTheme.ink2)
                        Spacer()
                        ProgressView()
                    }
                } else if let error = viewModel.errorMessage {
                    HStack {
                        Label(error, systemImage: "exclamationmark.triangle")
                            .foregroundStyle(FasoltTheme.ink2)
                        Spacer()
                        Button("Retry") {
                            Task { await viewModel.loadUserInfo() }
                        }
                        .font(.subheadline)
                        .tint(FasoltTheme.accentText)
                    }
                } else {
                    if let displayName = viewModel.displayName {
                        LabeledContent {
                            Text(displayName)
                        } label: {
                            Label {
                                Text("Signed in as")
                            } icon: {
                                ColorIconBadge(systemName: "person.fill", tint: FasoltTheme.accent)
                            }
                        }
                    } else if let email = viewModel.email {
                        LabeledContent {
                            Text(email)
                        } label: {
                            Label {
                                Text("Email")
                            } icon: {
                                ColorIconBadge(systemName: "envelope.fill", tint: FasoltTheme.accent)
                            }
                        }
                    }
                    LabeledContent("Account type", value: viewModel.externalProvider ?? "Email & password")
                    if let serverURL = viewModel.serverURL {
                        LabeledContent("Server", value: serverURL)
                            .lineLimit(1)
                    }
                }
            } header: {
                Text("Account").sectionLabel()
            }

            Section {
                Button("Sign Out", role: .destructive) {
                    showSignOutConfirmation = true
                }
            }

            Section {
                Button("Delete Account", role: .destructive) {
                    showDeleteAccount = true
                }
            } footer: {
                Text("Permanently deletes your account and all associated data.")
                    .foregroundStyle(FasoltTheme.ink2)
            }
        }
        .scrollContentBackground(.hidden)
        .sheet(isPresented: $showDeleteAccount) {
            DeleteAccountView(viewModel: viewModel)
                .environment(authService)
        }
    }

    // MARK: - FSRS

    private var fsrsContent: some View {
        List {
            Section {
                if schedulingViewModel.isLoading {
                    HStack {
                        Text("Loading...")
                            .foregroundStyle(FasoltTheme.ink2)
                        Spacer()
                        ProgressView()
                    }
                } else {
                    if let success = schedulingViewModel.successMessage {
                        Text(success)
                            .foregroundStyle(FasoltTheme.good)
                            .font(.caption)
                    }
                    if let error = schedulingViewModel.errorMessage {
                        Text(error)
                            .foregroundStyle(FasoltTheme.again)
                            .font(.caption)
                    }

                    VStack(alignment: .leading, spacing: 4) {
                        HStack {
                            Text("Desired retention")
                                .foregroundStyle(FasoltTheme.ink0)
                            Spacer()
                            Text(String(format: "%.0f%%", schedulingViewModel.desiredRetention * 100))
                                .foregroundStyle(FasoltTheme.ink2)
                        }
                        Slider(
                            value: $schedulingViewModel.desiredRetention,
                            in: 0.70...0.97,
                            step: 0.01
                        )
                        .tint(FasoltTheme.accent)
                        Text("How likely you want to remember a card when it comes up for review. Higher values mean more frequent reviews but stronger recall. Lower values mean fewer reviews but more forgetting. Changes apply to future reviews only.")
                            .font(.caption)
                            .foregroundStyle(FasoltTheme.ink2)
                    }

                    VStack(alignment: .leading, spacing: 4) {
                        HStack {
                            Text("Maximum interval")
                                .foregroundStyle(FasoltTheme.ink0)
                            Spacer()
                            Text("\(schedulingViewModel.maximumInterval) days")
                                .foregroundStyle(FasoltTheme.ink2)
                        }
                        TextField("Days", value: $schedulingViewModel.maximumInterval, format: .number)
                            .keyboardType(.numberPad)
                            .textFieldStyle(.roundedBorder)
                        Text("The longest gap between reviews, in days. 365 means every card is seen at least once a year. Default is 36500 (≈ 100 years).")
                            .font(.caption)
                            .foregroundStyle(FasoltTheme.ink2)
                    }

                    VStack(alignment: .leading, spacing: 4) {
                        Picker("Day starts at", selection: $schedulingViewModel.dayStartHour) {
                            ForEach(0..<24) { hour in
                                Text(String(format: "%02d:00", hour)).tag(hour)
                            }
                        }
                        Text("Hour at which a new study day begins, in your device's time zone. Cards scheduled a day or more in advance become due all at once at this time. Sub-day learning steps still fire at their exact times.")
                            .font(.caption)
                            .foregroundStyle(FasoltTheme.ink2)
                    }

                    Button("Save") {
                        Task { await schedulingViewModel.save() }
                    }
                    .tint(FasoltTheme.accentText)
                }
            } header: {
                Text("Scheduling").sectionLabel()
            }
        }
        .scrollContentBackground(.hidden)
    }

    // MARK: - About

    private var aboutContent: some View {
        List {
            Section {
                LabeledContent {
                    Text(viewModel.appVersion)
                        .foregroundStyle(FasoltTheme.ink2)
                } label: {
                    Label {
                        Text("Version")
                    } icon: {
                        ColorIconBadge(systemName: "info.circle.fill", tint: FasoltTheme.accent)
                    }
                }
                Button {
                    hasSeenWelcomeFlow = false
                } label: {
                    Label {
                        Text("Show welcome again")
                    } icon: {
                        ColorIconBadge(systemName: "sparkles", tint: FasoltTheme.accent)
                    }
                }
                .tint(FasoltTheme.accentText)
            }
        }
        .scrollContentBackground(.hidden)
    }
}

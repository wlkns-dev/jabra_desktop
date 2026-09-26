# Jabra Desktop 0.2.0 Release Features Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement a consent gate for the Jabra SDK, dynamic German/English localization, persisted tray language/theme settings, and a visible app version, then build and publish the Arch package as GitHub release `v0.2.0`.

**Architecture:** Store optional language choice, theme choice, and versioned SDK consent in a small atomic JSON file under XDG config. A localization service supplies a single German/English string catalog to XAML and ViewModels and refreshes it live; the tray menu is rebuilt from the current locale and settings. The consent gate runs after single-instance acquisition and before Jabra backend construction.

**Tech Stack:** C#/.NET 10, Avalonia 11.3.10, CommunityToolkit.Mvvm, xUnit, Arch `makepkg`, GitHub Releases.

**Spec:** `docs/superpowers/specs/2026-09-26-release-consent-localization-design.md`

## Global Constraints

- Consent must be current and successfully stored before any Jabra SDK component is created.
- Declining consent exits without creating `JabraBackend` or `DeviceSession`.
- Language defaults to German for German system UI cultures and English otherwise; a manual selection is persistent and changes the running UI immediately.
- Theme choices are System, Light, and Dark; System maps to `ThemeVariant.Default` and the selection is persistent.
- Jabra SDK limitations and third-party warranty/liability exclusions are shown in both languages, limited to GN Audio and third-party providers, and qualified by applicable law.
- All app-authored visible UI text is available in English and German; SDK-supplied messages remain unchanged.
- `Directory.Build.props`, `packaging/PKGBUILD`, Git tag, and release use version `0.2.0`.
- Keep .NET dependencies unchanged; use the existing xUnit test project and wrapper scripts.

## Review Focus

- A user declines consent: no consent is written and Jabra initialization does not run.
- Consent storage fails: the SDK remains uninitialized and the user sees a clear startup error.
- A stored consent belongs to an older terms version: prompt again before SDK initialization.
- A user has an unsupported system locale such as French: choose English unless a saved language choice exists.
- A user settings file is malformed or contains unknown enum values: use safe defaults without losing the fail-closed consent rule.

---

### Task 1: User preferences and SDK consent gate

**Files:**
- Create: `src/JabraDesktop.App/AppPreferences.cs`
- Create: `src/JabraDesktop.App/AppPreferencesStore.cs`
- Create: `src/JabraDesktop.App/TermsConsentGate.cs`
- Test: `tests/JabraDesktop.Tests/AppPreferencesTests.cs`
- Test: `tests/JabraDesktop.Tests/TermsConsentGateTests.cs`

**Interfaces:**
- `AppPreferences` is an immutable record with nullable `UiLanguage? Language`, `ThemePreference Theme`, nullable `string? AcceptedTermsVersion`, and nullable `DateTimeOffset? AcceptedTermsAt`.
- `AppPreferencesStore(string? filePath = null)` exposes `AppPreferences Load()` and `void Save(AppPreferences value)`. A null path uses `${XDG_CONFIG_HOME:-$HOME/.config}/jabra-desktop/settings.json`.
- `TermsConsentGate(AppPreferencesStore store, string currentTermsVersion)` exposes `Task<bool> EnsureAcceptedAsync(Func<CancellationToken, Task<bool>> requestAcceptance, CancellationToken cancellationToken = default)`.

- [ ] **Step 1: Write a failing test for preference defaults and JSON round-trip**

```csharp
[Fact]
public void SettingsRoundTripPreservesLanguageThemeAndConsent()
{
    var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "settings.json");
    var store = new AppPreferencesStore(path);
    var saved = new AppPreferences(UiLanguage.German, ThemePreference.Dark, "jabra-sdk-terms-1", DateTimeOffset.UnixEpoch);

    store.Save(saved);

    Assert.Equal(saved, store.Load());
}
```

- [ ] **Step 2: Run the focused test and verify it fails because the preferences types do not exist**

Run: `./scripts/dotnet.sh test tests/JabraDesktop.Tests --filter FullyQualifiedName~AppPreferencesTests -m:1`
Expected: FAIL at compile time for missing `AppPreferences`/`AppPreferencesStore`.

- [ ] **Step 3: Implement validated settings and atomic persistence**

Add the two enums `UiLanguage { German, English }`, `ThemePreference { System, Light, Dark }`. Load malformed JSON or invalid enum data as safe defaults. Save to a sibling temporary file and atomically replace the settings file; do not silently convert a failed write into success.

- [ ] **Step 4: Run the preference test and verify it passes**

Run: `./scripts/dotnet.sh test tests/JabraDesktop.Tests --filter FullyQualifiedName~AppPreferencesTests -m:1`
Expected: PASS, including the missing-file default and malformed-file fallback cases.

- [ ] **Step 5: Write failing consent-gate tests**

```csharp
[Fact]
public async Task DecliningDoesNotPersistConsent()
{
    var store = new AppPreferencesStore(TestPath());
    var gate = new TermsConsentGate(store, "jabra-sdk-terms-1");

    var accepted = await gate.EnsureAcceptedAsync((_) => Task.FromResult(false));

    Assert.False(accepted);
    Assert.Null(store.Load().AcceptedTermsVersion);
}
```

Add tests that current consent skips the prompt, stale consent prompts again, accepted consent is persisted with a timestamp, and a write failure is propagated.

- [ ] **Step 6: Run the focused consent test and verify it fails because `TermsConsentGate` does not exist**

Run: `./scripts/dotnet.sh test tests/JabraDesktop.Tests --filter FullyQualifiedName~TermsConsentGateTests -m:1`
Expected: FAIL at compile time for missing `TermsConsentGate`.

- [ ] **Step 7: Implement `TermsConsentGate` with fail-closed persistence**

Return true immediately only if `AcceptedTermsVersion` equals `currentTermsVersion`. Otherwise await the supplied prompt. On refusal, return false without writing. On acceptance, save the exact current version and `DateTimeOffset.UtcNow` before returning true. Let persistence exceptions escape to the startup error handler.

- [ ] **Step 8: Run the focused tests and verify the gate behavior**

Run: `./scripts/dotnet.sh test tests/JabraDesktop.Tests --filter FullyQualifiedName~TermsConsentGateTests -m:1`
Expected: PASS for current, stale, declined, accepted, and storage-failure cases.

- [ ] **Step 9: Commit preferences and consent gate**

```bash
git add src/JabraDesktop.App/AppPreferences.cs src/JabraDesktop.App/AppPreferencesStore.cs src/JabraDesktop.App/TermsConsentGate.cs tests/JabraDesktop.Tests/AppPreferencesTests.cs tests/JabraDesktop.Tests/TermsConsentGateTests.cs
git commit -m "feat: add user settings and Jabra consent gate"
```

### Task 2: Dynamic German and English text catalog

**Files:**
- Create: `src/JabraDesktop.App/Localization/LocalizationService.cs`
- Create: `src/JabraDesktop.App/Localization/UiText.cs`
- Create: `src/JabraDesktop.App/Localization/UiTextCatalog.cs`
- Test: `tests/JabraDesktop.Tests/LocalizationServiceTests.cs`
- Modify: `src/JabraDesktop.App/Views/MainWindow.axaml`
- Modify: `src/JabraDesktop.App/ViewModels/MainViewModel.cs` (`PeerRow` localized action and status text)
- Modify: `src/JabraDesktop.App/Views/MainWindow.axaml.cs` (unpair confirmation text)

**Interfaces:**
- `UiText` is an enum containing one key per app-authored visible string.
- `UiTextCatalog.Get(UiLanguage language, UiText key)` returns a non-empty translated string for every key.
- `LocalizationService(UiLanguage initialLanguage)` exposes `UiLanguage Language`, `string this[UiText key]`, `void SetLanguage(UiLanguage language)`, and `event EventHandler? LanguageChanged`.
- `MainViewModel` and each `PeerRow` consume the same `LocalizationService`; after `SetLanguage`, localized computed properties raise change notifications.

- [ ] **Step 1: Write failing tests for catalog completeness, system locale selection, and live language change**

```csharp
[Fact]
public void UnsupportedSystemCultureDefaultsToEnglish()
{
    Assert.Equal(UiLanguage.English, LocalizationService.DetectLanguage(CultureInfo.GetCultureInfo("fr-FR")));
}

[Fact]
public void SetLanguageChangesTheTextImmediately()
{
    var texts = new LocalizationService(UiLanguage.German);
    Assert.Equal("Öffnen", texts[UiText.Open]);

    texts.SetLanguage(UiLanguage.English);

    Assert.Equal("Open", texts[UiText.Open]);
}
```

Also iterate every `UiText` value in both languages and assert the returned text is non-empty.

- [ ] **Step 2: Run the localization test and verify it fails because the service and catalog do not exist**

Run: `./scripts/dotnet.sh test tests/JabraDesktop.Tests --filter FullyQualifiedName~LocalizationServiceTests -m:1`
Expected: FAIL at compile time for the missing localization types.

- [ ] **Step 3: Implement the complete catalog and language event**

Map German system cultures to `UiLanguage.German`, other system cultures to `UiLanguage.English`; store all English and German translations in the central catalog. `SetLanguage` changes the active language only when it differs, then raises `LanguageChanged`.

- [ ] **Step 4: Run localization tests and verify they pass**

Run: `./scripts/dotnet.sh test tests/JabraDesktop.Tests --filter FullyQualifiedName~LocalizationServiceTests -m:1`
Expected: PASS for system fallback, switching, and all keys in both locales.

- [ ] **Step 5: Localize ViewModel-generated labels and verify a failing regression test first**

Add tests proving that `DeviceTitle`, `StatusText`, `SearchSummary`, `PeerRow.ActionLabel`, and `PeerRow.Status` change from German to English on the same ViewModel without rebuilding it. Then add `LocalizationService` to the ViewModel constructors, subscribe/unsubscribe on disposal, and raise the relevant property notifications on `LanguageChanged`.

- [ ] **Step 6: Bind fixed XAML text and confirmation dialogs to live localized text**

Use the existing `MainViewModel` data context and the catalog indexer for fixed text and tooltip/accessibility labels. Inject the same localization service into `MainWindow` confirmation UI so an open dialog uses the selected language. Remove the sidebar theme button. Preserve names and statuses supplied by attached devices as reported.

- [ ] **Step 7: Run all localization and existing ViewModel tests**

Run: `./scripts/dotnet.sh test tests/JabraDesktop.Tests --filter FullyQualifiedName~LocalizationServiceTests -m:1`
Expected: PASS, including dynamic MainViewModel and peer-row text changes.

- [ ] **Step 8: Commit dynamic localization**

```bash
git add src/JabraDesktop.App/Localization src/JabraDesktop.App/Views/MainWindow.axaml src/JabraDesktop.App/Views/MainWindow.axaml.cs src/JabraDesktop.App/ViewModels/MainViewModel.cs tests/JabraDesktop.Tests/LocalizationServiceTests.cs
git commit -m "feat: localize the desktop app in German and English"
```

### Task 3: Consent UI, tray preferences, startup order, and version display

**Files:**
- Create: `src/JabraDesktop.App/Views/ConsentWindow.axaml`
- Create: `src/JabraDesktop.App/Views/ConsentWindow.axaml.cs`
- Create: `src/JabraDesktop.App/ConsentTerms.cs`
- Create: `src/JabraDesktop.App/ConsentStartupCoordinator.cs`
- Create: `src/JabraDesktop.App/Resources/Terms.de.md`
- Create: `src/JabraDesktop.App/Resources/Terms.en.md`
- Modify: `src/JabraDesktop.App/App.axaml`
- Modify: `src/JabraDesktop.App/App.axaml.cs`
- Modify: `src/JabraDesktop.App/Program.cs` or a focused new `AppVersion.cs`
- Test: `tests/JabraDesktop.Tests/ConsentTermsTests.cs`
- Test: `tests/JabraDesktop.Tests/AppVersionTests.cs`
- Test: `tests/JabraDesktop.Tests/ConsentStartupCoordinatorTests.cs`

**Interfaces:**
- `ConsentTerms.CurrentVersion` is a stable identifier independent of product version (initially `jabra-sdk-terms-1`).
- `ConsentTerms.GetText(UiLanguage language)` reads the matching embedded `Resources/Terms.de.md` or `Terms.en.md` and returns the full localized text displayed in the consent window.
- `ConsentStartupCoordinator(TermsConsentGate gate)` exposes `Task<IDeviceBackend?> CreateBackendIfAcceptedAsync(Func<CancellationToken, Task<bool>> requestAcceptance, Func<IDeviceBackend> createBackend, CancellationToken cancellationToken = default)`; it invokes the factory only after a current accepted consent.
- `AppVersion.Display` reads `AssemblyInformationalVersion`, falling back to the assembly's three-part version.
- `App` owns the loaded `AppPreferences`, `AppPreferencesStore`, `TermsConsentGate`, and shared `LocalizationService` for the process lifetime.

- [ ] **Step 1: Write failing tests for localized required consent clauses and version source**

Assert the German and English terms each include the GN-product use restriction, warranty disclaimer, liability exclusion, applicable-law qualifier, and official Jabra license URL. Assert `AppVersion.Display` matches the app assembly version set by MSBuild.

- [ ] **Step 2: Run the focused tests and verify they fail because terms and version sources do not exist**

Run: `./scripts/dotnet.sh test tests/JabraDesktop.Tests --filter FullyQualifiedName~ConsentTermsTests -m:1`
Expected: FAIL at compile time for missing types.

- [ ] **Step 3: Add matching German and English consent text and version source**

Use Jabra's license requirement for Developer Application users: restrict the SDK to GN Audio products; disclaim GN Audio and third-party provider warranties including merchantability, fitness, and non-infringement; exclude GN Audio and providers from direct, indirect, consequential, special, and punitive damages to the fullest extent allowed by law. Do not disclaim the app author's own responsibility or claim to remove mandatory user rights. Add the two localized Markdown files as embedded resources. Ensure the UI gets product version from the built assembly; the version itself is bumped in Task 4.

- [ ] **Step 4: Run the terms and version tests**

Run: `./scripts/dotnet.sh test tests/JabraDesktop.Tests --filter FullyQualifiedName~ConsentTermsTests -m:1`
Expected: PASS for both languages and version consistency.

- [ ] **Step 5: Integrate consent before constructing the Jabra backend**

First write `ConsentStartupCoordinatorTests`: a declined request and a failing settings save leave a counting backend factory at zero calls; accepted/current consent invokes it once. Run the focused test and verify it fails because the coordinator type does not exist. Implement the coordinator around `TermsConsentGate`. In `StartDesktopAsync`, invoke it after single-instance acquisition and before `DeviceSession` creation; the prompt callback shows `ConsentWindow`. A null result disposes `SingleInstanceCoordinator`, shuts down Avalonia, and returns. Exceptions show a localized startup error without creating the backend.

- [ ] **Step 6: Build and verify the consent window in both languages**

Create a scrollable window with app name, terms text, an explicit “Agree and continue” / “Zustimmen und fortfahren” action, and a “Decline and exit” / “Ablehnen und beenden” action. Default selection is no consent; window close is equivalent to decline. Persist only after the affirmative button.

- [ ] **Step 7: Integrate persisted language and theme in application startup**

Load settings before creating the main window. Choose saved language or `DetectLanguage(CultureInfo.CurrentUICulture)`. Choose saved theme or `ThemePreference.System`. Map System to `ThemeVariant.Default`, Light to `ThemeVariant.Light`, Dark to `ThemeVariant.Dark`. Remove the fixed Dark XAML application variant so System can inherit the OS setting.

- [ ] **Step 8: Build the dynamic tray menu with language, theme, and version entries**

Rebuild menu labels when locale changes. Add English/German submenu items with a checked active choice, System/Light/Dark theme submenu items, existing Open/Autostart/Quit actions, and a disabled `Jabra Desktop {AppVersion.Display}` footer. Each setting change updates the in-memory choice, saves JSON, and applies to the running app immediately. On a save failure, restore the prior setting and show a localized error.

- [ ] **Step 9: Run app and ViewModel tests**

Run: `./scripts/dotnet.sh test tests/JabraDesktop.Tests -m:1`
Expected: PASS, including backend factory untouched after decline, stale-term reprompt, language and theme selection persistence, and all prior tests.

- [ ] **Step 10: Commit consent UI, startup gate, tray preferences, and version display**

```bash
git add src/JabraDesktop.App/App.axaml src/JabraDesktop.App/App.axaml.cs src/JabraDesktop.App/Views/ConsentWindow.axaml src/JabraDesktop.App/Views/ConsentWindow.axaml.cs src/JabraDesktop.App/ConsentTerms.cs src/JabraDesktop.App/ConsentStartupCoordinator.cs src/JabraDesktop.App/Resources/Terms.de.md src/JabraDesktop.App/Resources/Terms.en.md src/JabraDesktop.App/AppVersion.cs tests/JabraDesktop.Tests/ConsentTermsTests.cs tests/JabraDesktop.Tests/AppVersionTests.cs tests/JabraDesktop.Tests/ConsentStartupCoordinatorTests.cs tests/JabraDesktop.Tests/TermsConsentGateTests.cs
git commit -m "feat: add consent and tray preferences"
```

### Task 4: Arch package, documentation, and GitHub release `v0.2.0`

**Files:**
- Modify: `packaging/PKGBUILD`
- Modify: `README.md`
- Modify: `THIRD-PARTY-NOTICES.md`
- Modify: `Directory.Build.props`

**Interfaces:**
- Package license directory contains the app's license/notices and both user-terms languages.
- Release asset is exactly `jabra-desktop-0.2.0-1-x86_64.pkg.tar.zst`; its SHA-256 is included in release notes.

- [ ] **Step 1: Add failing package-content verification**

Run package-content assertions that the package contains `Terms.de.md` and `Terms.en.md` under `/usr/share/licenses/jabra-desktop/` and that `pacman -Qp` reports version `0.2.0-1`.

- [ ] **Step 2: Update package metadata and user documentation**

Bump `pkgver` and central MSBuild version to `0.2.0`. Update README first-run instructions, consent behavior, language/theme controls, and release installation command. Update third-party notice to point to packaged localized terms and the official Jabra License Agreement. In `PKGBUILD`, install `src/JabraDesktop.App/Resources/Terms.de.md` and `Terms.en.md` beside the other license files.

- [ ] **Step 3: Build, test, and package**

Run:

```bash
./scripts/dotnet.sh test -m:1
./scripts/publish.sh
cd packaging && makepkg -f
```

Expected: all tests pass, self-contained Linux publish completes, and `makepkg` produces a package named `jabra-desktop-0.2.0-1-x86_64.pkg.tar.zst`.

- [ ] **Step 4: Verify package contents, version, and checksum**

Run `tar --zstd -tf` on the package to confirm app, license files, both terms, launcher, desktop entry, and udev rule are present. Run `pacman -Qp` to verify the package metadata and `sha256sum` to capture its download checksum.

- [ ] **Step 5: Commit release preparation**

```bash
git add Directory.Build.props packaging/PKGBUILD README.md THIRD-PARTY-NOTICES.md packaging
git commit -m "release: prepare Jabra Desktop 0.2.0"
```

- [ ] **Step 6: Publish source and package**

Push the implementation branch to `origin`, merge it to `main` without force-pushing, and push annotated tag `v0.2.0` on the release commit. Create the GitHub release with the package asset and its SHA-256. Keep `v0.1.1` unchanged. If GitHub's authenticated release-write path is unavailable, report the exact limitation and provide the built package and release notes for manual upload.

## Self-review

- Spec coverage: consent UI, fail-closed startup gate, stale-version re-prompt, system locale fallback, live language changes, all app-authored text, dynamic tray settings, persistence, version display, package contents, documentation, tests, and release asset all map to a task.
- Placeholder scan: no TODO/TBD implementation steps remain; each task names exact files, interfaces, tests, commands, and expected results.
- Type consistency: Task 1 owns enums/preferences/store/gate; Task 2 consumes `UiLanguage` and creates `LocalizationService`; Task 3 consumes those exact types and adds consent/version; Task 4 uses the same `0.2.0` version.
- Review focus: each failure condition is directly tested in Task 1 or Task 3; package verification is in Task 4.

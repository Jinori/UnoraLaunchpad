# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

`UnoraLaunchpad` — WPF launcher for the **Unora: Elemental Harmony** DarkAges private server. Targets **.NET Framework 4.8**, WinExe output, single-file deployment via **Costura.Fody** (all NuGet dependencies bundled into one exe). Current `AssemblyVersion` lives in `Properties/AssemblyInfo.cs` and is read by the self-update flow.

## Build & run

Solution file: `UnoraLaunchpad.sln`. The csproj is **legacy non-SDK format** — new source files must be added by hand as `<Compile Include="..." />` entries (the csproj does not auto-discover files).

```pwsh
# Restore packages
msbuild UnoraLaunchpad.sln /t:Restore

# Debug build — note: x86 PlatformTarget (required for parity with the 32-bit DarkAges client when testing patching)
msbuild UnoraLaunchpad.sln /p:Configuration=Debug

# Release build — AnyCPU
msbuild UnoraLaunchpad.sln /p:Configuration=Release
```

There is **no test project and no test runner**. There is no lint/format step beyond what the IDE provides. Validate behavior by running the launcher; runtime patching requires an actual DarkAges client on disk.

`<LangVersion>latest</LangVersion>` is set, so file-scoped namespaces, collection expressions (`= []`), and other modern C# syntax are in use even though the target is Framework 4.8.

## Debug vs Release endpoint switching

`Definitions/CONSTANTS.cs` uses `#if DEBUG` to switch the API base URL:
- Debug → `http://localhost:5001/api/files/`
- Release → `http://unora.freeddns.org:5001/api/files/`

`UnoraApiRoutes` (static class) composes asset endpoints under `/Unora/` (`/Unora/details`, `/Unora/get/{path}`, `/Unora/gameUpdates`) plus shared launcher endpoints (`/launcherversion`, `/getlauncher`). When debugging against a local **asset** server, the Debug build is required — there is no runtime override of the asset endpoint. The **lobby** server (host/port the game client connects to) is independently controlled by `Settings.UseLocalhost` via `MainWindow.GetLobbyEndpoint`: `127.0.0.1:4200` on localhost, otherwise `chaotic-minds.dynu.net:6900`.

## Application bootstrap (App.xaml.cs `Main`)

The startup order is load-bearing:
1. Acquire single-instance `Mutex` named `UnoraLaunchpadSingleInstance`. If already held, show a message box and exit — do not work around this for "second copy" scenarios.
2. Construct `App` *before* applying the theme — `pack://application:,,,/...` URIs require `Application.Current` to exist.
3. Read `LauncherSettings/settings.json` directly (not via `MainWindow`) to determine the theme, then merge `Resources/{Theme}Theme.xaml` into `App.Resources.MergedDictionaries`.
4. Default theme is `Dark`. 16 themes ship: Amber, Aquamarine, Amethyst, Citrine, Dark, Emerald, Garnet, Light, Obsidian, Pearl, Peridot, Ruby, Sapphire, Teal, Topaz, Violet.

`App.ChangeTheme(Uri)` swaps the theme dictionary at runtime by removing any merged dictionary whose `Source` ends in `Theme.xaml`.

## Settings & persistence

`Settings.cs` is a flat POCO; persisted to `LauncherSettings/settings.json` via `FileService.LoadSettings`/`SaveSettings` (Newtonsoft.Json, indented). Notable fields:
- `SavedCharacters` — list of `Character` with **DPAPI-encrypted** passwords (`PasswordHelper` + `EncryptionHelper`, `DataProtectionScope.CurrentUser`). Plaintext passwords must never be written to settings.
- `Combos` — dictionary of hotkey-name → macro string consumed by `ComboParser`.
- `IsComboSystemEnabled`, `SkipIntro`, `UseDawndWindower`, `UseLocalhost` — feature toggles. ChaosClient is now the only launch path, so `SkipIntro` and `UseDawndWindower` are effectively dead (their checkboxes are shown disabled in Settings, stored values preserved); they would only matter to the dormant `LegacyDarkAgesLauncher`.

## Game launching

`MainWindow.Launch` and `MainWindow.LaunchAndLogin` build a `LaunchContext` (install root, lobby host/port, skip-intro, dawnd-windower flag) and always launch via **`ChaosClientLauncher`**. The `IGameLauncher` abstraction and the `LegacyDarkAgesLauncher` (plus `SuspendedProcess`, `ProcessMemoryStream`, `RuntimePatcher`, and the DLL-injection code) remain in the tree but are **dormant** — nothing instantiates the legacy launcher anymore. Both implementations are documented below for reference:

- **`LegacyDarkAgesLauncher`** (dormant) — the original DarkAges 7.41 flow:
  1. `SuspendedProcess.Start` → Win32 `CreateProcess` with `CREATE_SUSPENDED`.
  2. `ProcessMemoryStream` exposes a `Stream` over the suspended process's address space using `VmRead`/`VmWrite` rights.
  3. `RuntimePatcher` writes binary patches **at hardcoded addresses for DarkAges 7.41** (server hostname ≈`0x4333C2`, server port ≈`0x4333E4`, skip-intro, allow-multiple-instances, fix-darkness, hide-walls). **These addresses are version-specific — do not change them without confirming the target client version.**
  4. If `UseDawndWindower` is on, **`dawnd.dll` is DLL-injected** into the running process via `OpenProcess` → `VirtualAllocEx` → `WriteProcessMemory` → `CreateRemoteThread` pointing at `LoadLibraryA`. The DLL is expected to already be present in the install folder (delivered via the file-update flow); the launcher does not drop it. Injection runs while the main thread is still suspended; `CreateRemoteThread` spawns a concurrent thread that completes `LoadLibrary` before the main thread resumes.
  5. The suspended thread is resumed and the process runs with patches + injected DLL in effect.

- **`ChaosClientLauncher`** — custom C# client, no patching:
  1. `Process.Start(ProcessStartInfo)` with `UseShellExecute = false` (required for env vars), `FileName = {InstallRoot}/ChaosClient/ChaosClient.exe`, `WorkingDirectory = {InstallRoot}/ChaosClient/`.
  2. Environment block adds `DA_PATH=..\` (so the client can find the parent `Unora/` asset folder), `DA_LOBBY_HOST`, and `DA_LOBBY_PORT` (from `GetLobbyEndpoint`).
  3. Pre-launch check: if `ChaosClient.exe` is missing in the install folder, `MainWindow.VerifyChaosClientPresent` shows a friendly `MessageBox` and aborts before constructing the `LaunchContext`.

After spawn, both clients go through identical post-launch orchestration in `MainWindow`: `RenameGameWindowAsync` (sets the game window title to `"Unora"`, then to the character's username after auto-login), `PerformAutomatedLogin` (InputSimulator-driven keyboard/mouse input), `WaitForClientReady`. ChaosClient mimics the legacy login UI exactly, so the same input-simulation script works for both.

**`Resources/dawnd.dll`** is the only file actively used: bundled as Content for distribution, present in the install folder via the file-update flow, and DLL-injected into the legacy client when `UseDawndWindower` is on. **`Resources/ddraw.dll`** is bundled as a `.resx` resource but has **zero runtime references** — dead weight kept for now.

## Auto-login & combo system

After `LaunchAndLogin`, the launcher polls until the game window handle is available, then uses **InputSimulator** to type the username and password. `Saved Characters` dropdown in `MainWindow` triggers this flow.

`ComboParser` implements a simple macro DSL:
- `{Wait <ms>}`, `{KeyPress <VirtualKeyCode>}`, `{KeyDown <VK>}`, `{KeyUp <VK>}`, plus literal characters.
- Global hotkeys are registered via Win32 `RegisterHotKey` and dispatched from `WM_HOTKEY` in a `WndProc` hook.
- **Critical guard**: hotkeys only fire when the foreground window title matches `Darkages`, `Unora`, or a saved character's username — this prevents macros from firing into unrelated apps. Preserve this check when modifying hotkey handling.

## File update system

`FileService` (combined with the API and `FileDetail` manifest) handles game-file updates:
- Server returns a list of `FileDetail` entries containing relative paths and MD5 hashes.
- Client computes local MD5s and downloads only files that are missing or mismatched.
- Downloads run in parallel with per-file progress and transfer-speed metrics surfaced in `MainWindow`.

`UnoraClient` wraps `HttpClient` with a **Polly** resilience policy: 5 exponential-backoff retries on `HttpRequestException` and `TaskCanceledException`, 30-minute timeout (sized for large game-asset downloads — do not shorten without thinking through resumability).

## Launcher self-update

`CheckAndUpdateLauncherAsync` compares the server launcher version against `AssemblyVersion`. On mismatch it spawns **`UnoraBootstrapper.exe`** (an external project not in this repo) with the current launcher path and PID, then exits. The bootstrapper waits for the launcher process to exit, replaces the exe atomically, and relaunches. Anything depending on launcher version bumps must update `AssemblyInfo.cs` *and* be coordinated with the bootstrapper.

## Notable gotchas

- **Debug builds are x86, Release is AnyCPU** — switching this breaks parity with the 32-bit client when testing in Debug.
- The csproj does not auto-include files. New `.cs` and XAML files must be added explicitly. New themes additionally need a case in the `themeFile` switch in `App.xaml.cs`.
- `MainWindow.xaml.cs` owns the long-running flows (download, patch, launch, hotkeys). Keep it on the UI thread for control updates and dispatch heavy work to background tasks.
- Costura embeds dependencies at build time — runtime probing for satellite assemblies will not work; everything must be referenced statically.

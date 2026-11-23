# Development and verification

The supported build entry is `build.cmd`, which embeds its Windows PowerShell logic and uses the Windows .NET Framework C# compiler. It produces one `bin/CommanderCompanion.exe`; the EXE carries every application support file and the licence notices as embedded resources.

The bootstrap prepares a content-addressed, per-user cache, verifies cached files against the embedded bytes, resolves the two managed WebView2 assemblies there and configures the native loader before the UI entry point is JIT-compiled. No source-tree-relative paths are used by the distributed executable. Concurrent launches synchronise cache preparation.

The normal build runs pure data tests for remote-process matching, console Unicode/colour boundaries, RTF escaping, exact Free/Pro account parsing, account-message origin/request validation and numeric stable-release comparison. They do not open windows or start/stop agents.

## Package integration test

Copy only `CommanderCompanion.exe` into an otherwise empty temporary directory. Run it with `--check <absolute-report-path>` using a hidden process and check exit code 0. The report confirms embedded script/artwork presence and existing-agent recognition. Check that no support files appeared beside the EXE.

The optional `tests/Remote_Desktop_Commander_Launcher.PackageSmoke.cs` harness accepts three arguments:

1. Absolute path to the standalone EXE.
2. A temporary screenshot-output directory.
3. A new, empty temporary account-profile directory, never the normal account profile.

Compile it using the Windows C# compiler with `System.Windows.Forms.dll` and `System.Drawing.dll` references. Apply the application's manifest and icon if producing screenshots. It loads the packaged resources, checks a fresh signed-out session against the official HTTPS website, renders Free/Unlimited Pro/signed-out/unavailable/About examples off-screen and verifies embedded licence notices. The screenshots use fixtures, not personal account data. It never connects/disconnects the agent, sends console input, shows windows or changes focus. Internet and WebView2 Runtime are required for the sign-in check. Remove its temporary account profile after its browser processes exit.

An explicit `--check-usage <absolute-report-path>` diagnostic checks the normal saved account session without showing a window. It writes only usage state/count/limit/reset and a non-sensitive status diagnostic. Such reports are private and must not be committed. Normal startup does not write them.

`--minimised` opens quietly without auto-connecting. `--startup` is reserved for the enabled Windows startup entry. Console helper arguments (`--terminal-view`, `--disconnect`, `--inspect-console`) are internal and validate the target process and creation time before acting.

## State and updates

The internal Windows Run value `RemoteDesktopCommander` and private WebView2 profile `ZeroFlagLabs/RemoteDesktopCommanderLauncher/AccountBrowser` retain their development names for compatibility. Update preferences use `HKCU/Software/ZeroFlagLabs/CommanderCompanion`.

The update checker reads `updates/latest.json` from this repository through raw.githubusercontent.com, avoiding the anonymous GitHub REST API allowance shared by apps behind one internet connection. The manifest uses `tag_name`, `draft` and `prerelease`; invalid entries and non-stable versions are rejected. Update this file only after the corresponding stable GitHub release is public, so an unpublished build is never offered as an update. The release-page button remains available independently of the version check. A failed request is never reported as an up-to-date result. No automatic software download or installation is implemented.

The native licence viewer uses readable headings and plain overview text, while retaining the supplied legal licence texts verbatim. Package integration checks also verify that no Markdown styling leaks into that viewer and that short/wrapped update statuses have equal vertical gaps above and below the action buttons.

Before publishing: run the data tests and source hash verification, check the standalone copy and account states, inspect screenshots for clipping, audit source and binary for personal data, confirm licence notices are embedded, and record the exact EXE SHA-256 in the release notes. No browser profile, logs, tokens, previous builds or personal diagnostics belong in the repository.

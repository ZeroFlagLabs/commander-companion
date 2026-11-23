# Commander Companion v1.0.1

Fixes the update-check failure caused by GitHub's anonymous REST API allowance being shared across apps on the same internet connection.

- Update checks now read a small public version file instead of the GitHub REST API. No GitHub login or token is required.
- **Open GitHub release** remains available even if a version check fails or the installed version is current.
- The About window keeps equal vertical spacing above and below the update buttons, including when the status message wraps.
- The licence viewer has readable headings and a plain overview, while preserving the supplied legal texts verbatim. Markdown styling no longer leaks into the display.

The live monthly usage panel, connection controls and single-EXE packaging are unchanged. Download `CommanderCompanion.exe`, close the existing launcher/terminal windows and replace the EXE; the independent agent can remain connected. Your saved sign-in and Windows startup preference are retained when the path stays the same.

Verified with the data tests, off-screen short/wrapped status layout checks, the native licence print path and an isolated account-profile integration check. The EXE is unsigned and uses standard Windows 11 runtime components.

## SHA-256 — CommanderCompanion.exe

```text
b561beb6048df565802002dc0a6efd6a87bc7d86c076f2a7b58930ab8b964a5a
```

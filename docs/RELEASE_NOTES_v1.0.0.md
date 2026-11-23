# Commander Companion v1.0.0

The first public release of ZeroFlagLabs' Windows companion for Desktop Commander Remote.

**Live monthly tool-call usage is front and centre:** see exact calls used, the Free allowance and reset date, or **Unlimited** for Pro, in the same compact window as your connection controls.

## Download

Download **CommanderCompanion.exe**, save it in a permanent folder and run it. The EXE is the only application file you need to download; it bundles its libraries, artwork, launch command and licence notices. No ZIP, installer or sidecar DLLs are required.

Windows 11 x64 was tested. The app uses the .NET Framework and Evergreen WebView2 components normally included with Windows 11. Internet access and your own Desktop Commander account are needed. First Connect invokes the vendor's agent setup, including its Node.js installation offer if required.

## Highlights

- Live Free and Unlimited Pro usage displays, last-updated time and manual refresh.
- Dedicated official account sign-in, with signed-out and unavailable states.
- Connect/disconnect controls and detection of separately started remote agents.
- A colour-preserving terminal viewer; closing it leaves the agent running.
- Optional Start with Windows: auto-connect and start minimised without taking focus.
- Compact About, embedded licence notices and optional GitHub update checks.

## Verification

Checked as an EXE alone in an empty folder; fresh signed-out and remembered live account sessions tested; Free/Pro/error UI fixtures reviewed. Data tests cover process matching, Unicode/colour rendering, account parsing and origin/request checks, and stable update comparisons. Publishable source and binary were audited for personal data and credential patterns, and a Defender custom file scan completed.

The release is unsigned. Internal support files and account data use per-user app-data folders, rather than files beside the EXE. The usage integration follows the vendor's dashboard endpoint; changes to that website may temporarily affect the usage panel independently of the connection controls.

## SHA-256 — CommanderCompanion.exe

```text
adccf87a3b160ab5461c07afebad45244b98b3a3dab1d122b232694b994a7c47
```

Independent, unofficial software by ZeroFlagLabs; not affiliated with or endorsed by Desktop Commander. Original application code is MIT licensed. Third-party components retain their own licences.

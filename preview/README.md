# Launcher usage preview

Run `bin\Remote_Desktop_Commander_Launcher_Usage_Preview.exe` to try the proposed usage panel in a real Windows program.

The selector at the bottom switches between Free, Pro, signed out, nearly at the limit, allowance reached and temporarily unavailable examples. All usage figures and account states are sample data. Pro shows **Unlimited**, with no quota progress bar or invented limit. It is an example only: it does not change your subscription.

The Connect/Disconnect buttons and Start with Windows checkbox are simulated and change no real connection or registry setting. Minimise works normally. Open terminal does not open your real terminal. Closing this preview has no effect on Commander or the installed launcher.

The Sign in / View usage link opens `https://mcp.desktopcommander.app/usage` in your default browser when clicked. The preview does not read browser cookies, store credentials or synchronise a website sign-in back into the program. The production launcher includes live account sign-in; this separate preview intentionally remains a simulation.

`build.cmd` rebuilds this preview only, then refreshes the parent project's SHA256SUMS.txt. Keep this `preview` folder directly inside the main launcher project when rebuilding: the build reuses the parent project's icon, PNG, manifest and status control. The compiled preview EXE embeds its artwork and can run on its own on this PC.

The production executable is unchanged. The preview opens without taking focus from another application.

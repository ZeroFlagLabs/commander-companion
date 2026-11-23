# Third-party components

Microsoft.Web.WebView2 SDK **1.0.4370.26**, from the official Microsoft NuGet package:

https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.4370.26

The managed Core and WinForms libraries and native WebView2Loader files are included under `lib/WebView2`. Their licence and notices are retained there as `LICENSE.txt` and `NOTICE.txt`. The single-file build embeds both managed libraries and the x64, x86 and ARM64 native loaders. At runtime, the appropriate files are prepared in a per-user app-data cache. Their full licence and notices are also embedded and available through About → Licences.

The Microsoft Edge WebView2 Runtime is a separate prerequisite, normally already installed on Windows 11. The SDK files bundled here do not include the browser runtime itself. Runtime information: https://developer.microsoft.com/microsoft-edge/webview2/

Desktop Commander authentication and account usage remain supplied by the vendor's HTTPS websites. This utility is an unofficial companion; it neither implements the hosted service nor includes its credentials or source code.

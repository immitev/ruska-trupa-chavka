# Microsoft Store PWA submission

Public PWA URL: https://ruska-trupa.logi4no.com

## Readiness checklist

- Manifest is available at `/manifest.webmanifest`.
- Start URL launches the game at `/play/index.html`.
- Scope is `/`, so landing and game pages remain in-app.
- The landing page redirects to `/play/index.html` when opened in standalone/PWA display mode.
- Service worker is registered from both entry pages.
- Published service worker is network-first with cached same-origin static assets and an offline navigation fallback.
- App icons include 192x192, 512x512, and a 512x512 maskable icon.
- Manifest includes description, language, categories, orientation, screenshots, shortcuts, theme color, and background color.
- Store screenshot source files are in `src/RuskaTrupa.Web/wwwroot/screenshots/`.

## Local validation

```powershell
dotnet test RuskaTrupa.slnx
dotnet publish src/RuskaTrupa.Web/RuskaTrupa.Web.csproj -c Release -o artifacts/pwa-publish
```

Serve `artifacts/pwa-publish/wwwroot`, then verify:

- `/play/index.html` starts without console errors.
- `navigator.serviceWorker.getRegistration()` returns `/service-worker.js`.
- `/manifest.webmanifest` returns HTTP 200 and contains the Store-ready metadata.
- PWABuilder reports the site as package-ready.

## Partner Center and PWABuilder

1. In Microsoft Partner Center, create a new `MSIX or PWA app` reservation for the final Store name.
2. Copy the `Package ID`, `Publisher ID`, and `Publisher display name` from Product Identity.
3. Open https://www.pwabuilder.com and scan `https://ruska-trupa.logi4no.com`.
4. Package for Windows and enter the Partner Center identity values.
5. Download the generated Windows package archive.
6. In Partner Center, start the submission and upload the generated `.msixbundle` and `.classic.appxbundle`.
7. Use the generated screenshots as Store listing screenshots, or capture higher-resolution marketing screenshots if desired.

When the web app code changes, publishing the website is normally enough. When `manifest.webmanifest` changes, regenerate and resubmit the Store package because Windows package metadata is copied from the manifest.

# Endless Tweaks website

Single-product static website at https://endlesstweaks.github.io/endless-site/.
The GitHub Pages workflow publishes this repository after pushes to main.

## Downloads

Only Endless Tweaks is offered. Its direct standalone Windows x64 EXE link resolves to the latest published GitHub release asset, EndlessTweaks-Consumer-win-x64.exe. The ZIP and signed manifest assets used by the existing app updater are unaffected by the website cleanup.

Free and Lifetime Pro are access levels within the same application. Pro access is arranged through the existing Discord ticket and code-redemption flow.

## Website 10.0

Buildless HTML/CSS/JavaScript with a searchable 225-entry read-only catalog, real app preview, expandable Free/Pro comparison, GitHub release history and SHA-256 details, searchable help, and copyable support information. Website 10.0 is separate from the app version (currently 9.6.1).

Serve this folder over HTTP for local preview; opening index.html as a file will prevent catalog fetching in some browsers. No package installation or build step is required. index.html loads site.css, site.js, experience.js, tweaks.json, app-preview.png, and the existing logo. Publish these together; 404.html uses the /endless-site/ GitHub Pages base path.

The catalog retains the app's original IDs, names, descriptions, risks and Free/Pro classifications. It contains 25 Free and 200 Pro registrations, including three Pro controller USB settings. Update tweaks.json from the app catalog when registrations change. The screenshot is a clearly labeled v9.6.1 UI-test fixture, not a visitor's PC or live performance data.

Release details use the public GitHub API, with a session cache and retry controls. Direct EXE links remain available if the API times out or is rate limited. History includes stable Endless Tweaks releases with the expected EXE asset. Hashes come from GitHub asset metadata; the website does not claim to verify downloaded bytes. Existing signed updater packages and backend files are unchanged.

No website checkout or account dashboard is active. Purchases use the existing Discord flow and license redemption occurs in the Windows app. A future web account or checkout requires an authenticated backend and payment integration; this website does not collect credentials or license codes.

## Validation

Checked JavaScript syntax; catalog totals/unique IDs; local assets/anchor targets; release success, cached results, service failure and recovery; official URL validation; filters and empty results; mobile navigation; plan and screenshot dialogs; help search; clipboard actions; release history and digest. Backend file hashes were checked unchanged.

Browser overflow checks passed at 320, 390, 768, 1366, 1920 and 2560 CSS pixels. The browser capped the requested 3840 width at 2560, so native 4K/ultrawide verification remains outstanding. Reduced-motion behavior was observed. No console errors appeared during tested interactions.

Not measured: Lighthouse scores, real-device performance, all browser engines, actual purchases, or new account flows. Decorative hero effects do not read a visitor's hardware. No Windows configuration changes are performed by this site.

# Endless website

Static site source for GitHub Pages. Open `index.html` locally to preview it; no
build step or external JavaScript libraries are required.

## Publish

Use a new, empty public GitHub repository for this folder so the unrelated desktop
app and its development files are not published. Keep the default branch named
`main`, push the contents of this folder to the repository root, then open
**Settings → Pages** and set the publishing source to **GitHub Actions**. The
included workflow deploys this static site after each push to `main`.

## Product download links

For a local `file://` preview, the two download buttons point to ZIP files in
this workspace's `artifacts/` directory. When hosted, the page switches them to
the latest GitHub Release assets in `Bonesv1/endless-site`. Publish the two
consumer ZIPs there with these exact filenames before sharing the website:

- `EndlessTweaks-Consumer-win-x64.zip`
- `EndlessZero-Consumer-win-x64.zip`

The hero panel's animated CPU and memory figures are decorative sample values,
not readings from a visitor's computer.

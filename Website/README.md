# YjsE Product Site

The website is a statically deployable Svelte 5 and Vite multi-page app. It has two product surfaces:

- `index.html`: concise game, engine, validation and download information.
- `docs.html`: searchable, filterable engine development wiki.

The browser receives compiled Svelte bundles only. There is no CDN runtime, remote font, analytics SDK or server dependency.

## Structure

```text
Website/
|- src/
|  |- HomeApp.svelte
|  |- DocsApp.svelte
|  |- lib/components/
|  `- styles/app.css
|- static/
|  |- assets/       # byte-exact selected Game.Data copies
|  |- data/         # status, releases, wiki and provenance
|  `- downloads/    # versioned artifacts only
|- tools/
|- index.html
|- docs.html
`- vite.config.js
```

`vite.config.js` emits both HTML entry points and uses a relative base so the result can be hosted from a repository subpath. Everything under `static/` is copied to `dist/` unchanged.

## Local development

```powershell
cd Website
npm ci
npm run dev
```

Vite prints the local URL. Product and docs changes hot-reload without rebuilding the C# projects.

## Production gate

```powershell
cd Website
npm run validate
```

The gate runs `svelte-check`, creates the optimized multi-page build, and validates:

- status, download, wiki and capability schemas;
- Svelte accessibility and interaction contracts;
- portable local bundle references;
- exact static-data copying into `dist/`;
- PNG headers and dimensions;
- byte-exact provenance against `Game.Data`;
- Wave 05 manifest coverage.

Preview the production output with:

```powershell
npm run preview
```

## Milestone update contract

After every validated engine or game slice:

1. Update `static/data/status.json` with current checkout evidence.
2. Add a concise development note to `static/data/releases.json`.
3. Update affected articles in `static/data/docs.json`.
4. Copy displayed game assets into `static/assets/` and record their exact `Game.Data` source in `static/data/assets.json`.
5. Run `./Website/tools/update-site-data.ps1 -Check` from the repository root.
6. Inspect product and docs pages at desktop and mobile widths.

Do not add a public download URL before a real, versioned artifact exists. Unpublished entries keep `published: false` and `href: null`.

To stamp a milestone and rebuild:

```powershell
./Website/tools/update-site-data.ps1 `
  -Summary "Current locally verified milestone summary" `
  -ValidationLabel "N/N tests, Debug + Release"
```

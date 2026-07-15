# YjsE Website

Static, dependency-free product site for the YjsE engine and its Terraria-like reference game.

## Surfaces

- `index.html`: concise game/engine information, honest download states, real in-project artwork and development notes.
- `docs.html`: searchable development wiki with category filters, deep links and capability-status badges.
- `data/status.json`: current product and validation summary.
- `data/releases.json`: downloads and development notes. Unpublished artifacts must use `published: false` and `href: null`.
- `data/docs.json`: wiki navigation and article content.
- `data/assets.json`: provenance map for website copies of real `Game.Data` PNG assets.

The site has no CDN or package-manager runtime dependency. System fonts and local assets keep offline previews deterministic.

## Local preview

From the repository root:

```powershell
python -m http.server 4173 --directory Website
```

Open `http://localhost:4173/`. A server is required because the pages load JSON through `fetch`.

## Milestone update contract

After every validated engine/game slice:

1. Update `data/status.json` using only current checkout evidence.
2. Add one concise development note to `data/releases.json`.
3. Update affected API articles in `data/docs.json`; never call tile-aware 2D ray casting hardware raytracing.
4. If a displayed game asset changes, copy the real source into `assets/` and update `data/assets.json`.
5. Run the static gate, then inspect home and docs at desktop and mobile sizes.

Do not add a public download URL before a real versioned artifact exists.

```powershell
./Website/tools/update-site-data.ps1 -Check
node --check Website/app.js
```

`validate-site.mjs` verifies JSON contracts, status vocabulary, wiki links, HTML fragments, local references, UTF-8 health, local-only scripts/styles, PNG headers and byte-exact `Game.Data` asset provenance.

To stamp a validated milestone:

```powershell
./Website/tools/update-site-data.ps1 `
  -Summary "Current locally verified milestone summary" `
  -ValidationLabel "N/N tests, Debug + Release"
```

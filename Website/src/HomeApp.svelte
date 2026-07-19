<script>
  import { onMount } from "svelte";
  import {
    ArrowDown,
    ArrowRight,
    Blocks,
    Boxes,
    Check,
    Code2,
    Cpu,
    Database,
    Download,
    Gamepad2,
    Gauge,
    GitBranch,
    Globe2,
    Layers3,
    LockKeyhole,
    MonitorDown,
    ShieldCheck,
    Sparkles,
    Terminal
  } from "@lucide/svelte";
  import SceneStage from "./lib/components/SceneStage.svelte";
  import SiteHeader from "./lib/components/SiteHeader.svelte";
  import StatusBadge from "./lib/components/StatusBadge.svelte";
  import { loadHomeData } from "./lib/site-data.js";

  let status = $state(null);
  let releases = $state(null);
  let assets = $state(null);
  let loading = $state(true);
  let loadError = $state(false);

  const sceneAssets = $derived(
    (assets?.assets ?? [])
      .filter((asset) => asset.role === "scene")
      .sort((left, right) => (left.featuredOrder ?? 999) - (right.featuredOrder ?? 999))
  );
  const heroScene = $derived(sceneAssets.find((asset) => asset.id === "wave05-amber-grove") ?? sceneAssets[0] ?? null);
  const features = $derived(status?.features ?? []);
  const downloads = $derived(releases?.downloads ?? []);
  const notes = $derived((releases?.notes ?? []).slice(0, 4));

  onMount(async () => {
    try {
      const data = await loadHomeData();
      status = data.status;
      releases = data.releases;
      assets = data.assets;
    } catch {
      loadError = true;
    } finally {
      loading = false;
    }
  });
</script>

<svelte:head>
  <meta property="og:title" content="YjsE | 2D Engine & Sandbox Game" />
  <meta property="og:description" content="Renderer-neutrale C#-Engine. Lebendige Sideview-Sandbox. Ein messbarer 2D-Stack." />
</svelte:head>

<a class="skip-link" href="#main">Zum Inhalt</a>
<SiteHeader />

<main id="main">
  <section class="product-hero" aria-labelledby="hero-title">
    {#if heroScene}
      <div class="hero-art" style:background-image={`url('${heroScene.localPath}')`} role="img" aria-label={heroScene.alt}></div>
    {/if}
    <div class="hero-atmosphere"></div>
    <div class="hero-grid"></div>

    <div class="hero-content page-shell">
      <div class="hero-copy">
        <div class="live-pill"><i></i> Active development <span>v0.x</span></div>
        <h1 id="hero-title">YjsE</h1>
        <p class="hero-statement">Eine schnelle C#-Engine.<br />Eine lebendige 2D-Welt.</p>
        <p class="hero-description">
          Renderer-neutrale Simulation, unendliche Chunk-Welten und datengetriebene Systeme fuer Sandbox-,
          Farming- und Topdown-Spiele. Das Reference Game prueft den Stack unter echten Gameplay-Bedingungen.
        </p>
        <div class="hero-actions">
          <a class="button primary" href="#download"><MonitorDown size={18} /> Build ansehen</a>
          <a class="button secondary" href="./docs.html"><Code2 size={18} /> Engine erkunden <ArrowRight size={16} /></a>
        </div>
      </div>

      <aside class="runtime-card" aria-label="Runtime Snapshot">
        <div class="runtime-card-head">
          <span><i></i> Runtime snapshot</span>
          <strong>LOCAL</strong>
        </div>
        <div class="runtime-chart" aria-hidden="true">
          {#each [28, 43, 37, 58, 49, 71, 63, 78, 68, 84, 73, 91, 82, 94] as height}
            <i style:height={`${height}%`}></i>
          {/each}
        </div>
        <div class="runtime-primary"><strong>6.06</strong><span>ms / frame<br />1080p Forest</span></div>
        <div class="runtime-facts">
          <div><span>Simulation</span><strong>60 Hz</strong></div>
          <div><span>Presentation</span><strong>30-360</strong></div>
          <div><span>Core</span><strong>0 GPU refs</strong></div>
        </div>
      </aside>
    </div>

    <div class="hero-bottom page-shell">
      <p><span>AUTHORED SCENE</span>{heroScene?.title ?? "Living World"}</p>
      <a href="#status" aria-label="Zum Projektstatus"><ArrowDown size={18} /></a>
    </div>
  </section>

  <section class="signal-band" id="status" aria-labelledby="status-title">
    <div class="page-shell signal-layout">
      <div class="signal-title">
        <span class="section-label">Build telemetry</span>
        <h2 id="status-title">Aktueller Projektstand</h2>
      </div>
      {#if loading}
        <div class="metric-skeleton"></div><div class="metric-skeleton"></div><div class="metric-skeleton"></div><div class="metric-skeleton"></div>
      {:else}
        {#each status?.metrics ?? [] as metric (metric.label)}
          <div class="signal-metric"><strong>{metric.value}</strong><span>{metric.label}</span></div>
        {/each}
      {/if}
    </div>
  </section>

  <section class="product-section" id="engine" aria-labelledby="engine-title">
    <div class="page-shell">
      <div class="section-heading wide">
        <div><span class="section-label">Engine + game</span><h2 id="engine-title">Getrennte Produkte.<br />Ein belastbarer Vertrag.</h2></div>
        <p>YjsE besitzt die wiederverwendbaren Systeme. Das Sideview-Spiel liefert Content, Spielgefuehl und reale Belastungsfaelle, ohne eine zweite Simulation aufzubauen.</p>
      </div>

      <div class="product-workspace">
        <article class="product-pane engine-pane">
          <div class="pane-head"><span>01 / ENGINE</span><Cpu size={22} /></div>
          <h3>Das technische Fundament</h3>
          <p>Deterministische Runtime-Vertraege fuer Welten, Physik, AI, Combat, Animation, Saves und eigene Tools.</p>
          <div class="code-window" aria-label="Engine Projektgrenzen">
            <div class="code-title"><i></i><i></i><i></i><span>YjsE.sln</span></div>
            <code><span>Game.Core</span>      simulation + rules<br /><span>Game.Client</span>    MonoGame host<br /><span>Game.Data</span>      replaceable content<br /><span>Game.Tests</span>     contracts + budgets</code>
          </div>
          <a class="inline-link" href="./docs.html#architecture">Architektur oeffnen <ArrowRight size={15} /></a>
        </article>

        <article class="product-pane game-pane">
          <div class="pane-head"><span>02 / REFERENCE GAME</span><Gamepad2 size={22} /></div>
          <h3>Die spielbare Beweisflaeche</h3>
          <p>Mining, Building, Crafting, Mana, Combat, regionale Biome und lebende Entity-Populationen auf derselben Engine.</p>
          <div class="game-preview">
            <div class="game-preview-world"></div>
            <div class="character-rig" aria-hidden="true">
              <img src="./assets/wave04/player/body-actions.png" alt="" />
              <img src="./assets/wave04/player/clothes-actions.png" alt="" />
              <img src="./assets/wave04/player/hair-actions.png" alt="" />
              <img src="./assets/wave04/player/equipment-actions.png" alt="" />
            </div>
            <div class="preview-hud"><span><ShieldCheck size={14} /> 100</span><span><Sparkles size={14} /> 40</span></div>
          </div>
          <a class="inline-link" href="./docs.html#reference-game">Game-Systeme oeffnen <ArrowRight size={15} /></a>
        </article>
      </div>
    </div>
  </section>

  <section class="capabilities-section" aria-labelledby="capability-title">
    <div class="page-shell">
      <div class="section-heading compact">
        <div><span class="section-label">Verified capabilities</span><h2 id="capability-title">Systeme, die heute tragen.</h2></div>
        <a class="inline-link" href="./docs.html?status=all">Alle Engine-Vertraege <ArrowRight size={15} /></a>
      </div>
      <div class="capability-grid">
        {#if loading}
          {#each Array(6) as _}<div class="capability-card skeleton"></div>{/each}
        {:else}
          {#each features as feature, index (feature.title)}
            <article class="capability-card">
              <div class="capability-top"><span>{String(index + 1).padStart(2, "0")}</span><StatusBadge status={feature.status} compact /></div>
              <h3>{feature.title}</h3>
              <p>{feature.text}</p>
            </article>
          {/each}
        {/if}
      </div>
    </div>
  </section>

  <section class="world-section" aria-labelledby="world-title">
    <div class="page-shell world-layout">
      <div class="world-copy">
        <span class="section-label light">Living world</span>
        <h2 id="world-title">Biomes mit eigener Sprache.</h2>
        <p>Regionale Materialien, Wetter, Spawns, Licht und Parallax-Szenen bilden zusammenhaengende Spielraeume. Die Vorschau verwendet echte, manifestierte Game.Data-Assets.</p>
        <ul class="world-points">
          <li><Globe2 size={17} /><span><strong>Infinite X</strong>Negative und positive Chunk-Koordinaten</span></li>
          <li><Layers3 size={17} /><span><strong>Authored depth</strong>Far-, Mid- und Near-Presentation</span></li>
          <li><Sparkles size={17} /><span><strong>Living runtime</strong>Wetter, Events, AI und Ambient FX</span></li>
        </ul>
      </div>
      <SceneStage scenes={sceneAssets.slice(0, 7)} />
    </div>
  </section>

  <section class="stack-section" aria-labelledby="stack-title">
    <div class="page-shell">
      <div class="section-heading wide">
        <div><span class="section-label">Core stack</span><h2 id="stack-title">Fuer Spiele gebaut,<br />nicht fuer Demos.</h2></div>
        <p>Jedes System besitzt eine klare Ownership-Grenze, datengetriebene Erweiterungspunkte und messbare Gates fuer Korrektheit und Performance.</p>
      </div>
      <div class="stack-grid">
        <article><Boxes size={21} /><span>World</span><strong>Chunk streaming</strong><small>Infinite X, async apply budgets</small></article>
        <article><Gauge size={21} /><span>Runtime</span><strong>Fixed simulation</strong><small>Deterministic 60 Hz contracts</small></article>
        <article><Blocks size={21} /><span>Physics</span><strong>Tile + body</strong><small>Swept collision, material contacts</small></article>
        <article><Database size={21} /><span>Content</span><strong>Data driven</strong><small>JSON registries, mod overrides</small></article>
        <article><Layers3 size={21} /><span>Rendering</span><strong>Prepared passes</strong><small>Lighting, atlas, reflections</small></article>
        <article><Terminal size={21} /><span>Tooling</span><strong>Observable</strong><small>Console, telemetry, smokes</small></article>
      </div>
    </div>
  </section>

  <section class="download-section" id="download" aria-labelledby="download-title">
    <div class="page-shell download-layout">
      <div class="download-heading">
        <span class="section-label">Downloads</span>
        <h2 id="download-title">Bereit, wenn der Build es ist.</h2>
        <p>Kein Attrappen-Download: Ein Button wird erst aktiv, wenn ein reales und versioniertes Artefakt existiert.</p>
        <div class="download-trust"><ShieldCheck size={18} /><span><strong>Honest release state</strong> Datenstand {status?.updated ?? "lokal"}</span></div>
      </div>
      <div class="download-list">
        {#each downloads as download (download.platform)}
          <article class="download-card">
            <div class="download-icon">{#if download.published}<Download size={23} />{:else}<LockKeyhole size={22} />{/if}</div>
            <div class="download-card-copy">
              <span>{download.published ? "AVAILABLE" : "DEVELOPMENT"}</span>
              <h3>{download.platform}</h3>
              <p>{download.detail}</p>
              <ul>{#each download.requirements as requirement}<li><Check size={12} />{requirement}</li>{/each}</ul>
            </div>
            {#if download.published}
              <a class="button primary" href={download.href}><Download size={17} />{download.label}</a>
            {:else}
              <button class="button disabled" type="button" disabled title={download.status}><LockKeyhole size={16} />{download.label}</button>
            {/if}
          </article>
        {/each}
        <a class="source-row" href="https://github.com/YoungJibbit95/2D-Game-Engine"><GitBranch size={19} /><span><strong>Source repository</strong>Aktueller Entwicklungsbranch auf GitHub</span><ArrowRight size={17} /></a>
      </div>
    </div>
  </section>

  <section class="release-section" aria-labelledby="release-title">
    <div class="page-shell release-layout">
      <div class="release-heading"><span class="section-label">Development log</span><h2 id="release-title">Was sich veraendert.</h2><p>Kurze, nachpruefbare Notizen statt Roadmap-Marketing.</p></div>
      <div class="release-list">
        {#each notes as note, index (note.date + note.title)}
          <details open={index === 0}>
            <summary><time>{note.date}</time><strong>{note.title}</strong><span></span></summary>
            <ul>{#each note.items as item}<li>{item}</li>{/each}</ul>
          </details>
        {/each}
      </div>
    </div>
  </section>
</main>

<footer class="site-footer">
  <div class="page-shell footer-main">
    <div><strong>YjsE</strong><p>YoungJibbit's 2D Engine</p></div>
    <nav aria-label="Fussnavigation"><a href="./docs.html">Engine Docs</a><a href="./docs.html#tests">Validation</a><a href="#download">Download</a></nav>
    <p>Development snapshot.<br />Keine Release- oder Stabilitaetsgarantie.</p>
  </div>
  <div class="page-shell footer-bottom"><span>C# / .NET 8 / MonoGame host</span><span>Engine + Reference Game</span></div>
</footer>

{#if loadError}<div class="site-toast" role="status">Lokale Fallback-Daten aktiv.</div>{/if}

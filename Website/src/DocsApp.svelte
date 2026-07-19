<script>
  import { onMount, tick } from "svelte";
  import {
    ArrowRight,
    ArrowUp,
    BookOpen,
    Check,
    ChevronRight,
    Copy,
    FileCode2,
    Home,
    Menu,
    Search,
    SlidersHorizontal,
    X
  } from "@lucide/svelte";
  import SiteHeader from "./lib/components/SiteHeader.svelte";
  import StatusBadge from "./lib/components/StatusBadge.svelte";
  import { loadDocsData } from "./lib/site-data.js";

  let status = $state(null);
  let docs = $state(null);
  let query = $state("");
  let category = $state("Alle");
  let statusFilter = $state("all");
  let sidebarOpen = $state(false);
  let activeArticle = $state("");
  let copiedCode = $state("");
  let searchInput = $state();

  const articles = $derived(docs?.articles ?? []);
  const categories = $derived(["Alle", ...(docs?.categories ?? [])]);
  const normalizedQuery = $derived(query.trim().toLocaleLowerCase("de"));
  const filteredArticles = $derived(
    articles.filter((article) => {
      const categoryMatch = category === "Alle" || article.category === category;
      const statusMatch = statusFilter === "all" || article.status === statusFilter;
      const haystack = [
        article.title,
        article.shortTitle,
        article.summary,
        article.category,
        ...(article.tags ?? []),
        ...(article.paragraphs ?? []),
        ...(article.bullets ?? []),
        ...(article.api ?? []).flatMap((entry) => [entry.label, entry.code])
      ].join(" ").toLocaleLowerCase("de");
      return categoryMatch && statusMatch && (!normalizedQuery || haystack.includes(normalizedQuery));
    })
  );
  const categoryGroups = $derived(
    (docs?.categories ?? []).map((name) => ({
      name,
      articles: articles.filter((article) => article.category === name)
    })).filter((group) => group.articles.length > 0)
  );

  function focusSearch() {
    searchInput?.focus();
  }

  function resetFilters() {
    query = "";
    category = "Alle";
    statusFilter = "all";
    history.replaceState(null, "", "./docs.html");
  }

  function navigateTo(id) {
    activeArticle = id;
    sidebarOpen = false;
    history.replaceState(null, "", `${location.pathname}${location.search}#${id}`);
    document.getElementById(id)?.scrollIntoView({ behavior: "smooth", block: "start" });
  }

  async function copyCode(id, code) {
    await navigator.clipboard.writeText(code);
    copiedCode = id;
    window.setTimeout(() => {
      if (copiedCode === id) copiedCode = "";
    }, 1600);
  }

  function handleKeyboard(event) {
    if (event.key === "/" && document.activeElement?.tagName !== "INPUT") {
      event.preventDefault();
      focusSearch();
    }
    if (event.key === "Escape") {
      sidebarOpen = false;
      if (document.activeElement === searchInput && query) query = "";
    }
  }

  function updateActiveArticle() {
    const candidates = [...document.querySelectorAll("[data-doc-article]")];
    let current = candidates[0]?.id ?? "";
    for (const element of candidates) {
      if (element.getBoundingClientRect().top <= 150) current = element.id;
      else break;
    }
    if (current) activeArticle = current;
  }

  onMount(async () => {
    const data = await loadDocsData();
    status = data.status;
    docs = data.docs;

    const parameters = new URLSearchParams(location.search);
    statusFilter = parameters.get("status") ?? "all";
    if (!new Set(["all", "verified", "partial", "planned", "implemented-unverified"]).has(statusFilter)) statusFilter = "all";
    activeArticle = location.hash.slice(1) || articles[0]?.id || "";
    await tick();
    if (location.hash) document.getElementById(activeArticle)?.scrollIntoView({ block: "start" });
    updateActiveArticle();
  });
</script>

<svelte:window onkeydown={handleKeyboard} onscroll={updateActiveArticle} />

<a class="skip-link" href="#docs-main">Zum Inhalt</a>
<SiteHeader docs />

<div class="docs-mobile-tools">
  <button type="button" onclick={() => (sidebarOpen = true)}><Menu size={18} /> Kapitel</button>
  <span>{articles.find((article) => article.id === activeArticle)?.shortTitle ?? "Engine Docs"}</span>
  <button type="button" aria-label="Suche fokussieren" onclick={focusSearch}><Search size={18} /></button>
</div>

<div class="docs-shell">
  <aside class:open={sidebarOpen} class="docs-sidebar" aria-label="Dokumentationsnavigation">
    <div class="sidebar-mobile-head"><strong>Engine Docs</strong><button type="button" aria-label="Navigation schliessen" onclick={() => (sidebarOpen = false)}><X size={18} /></button></div>
    <div class="sidebar-state">
      <i></i>
      <span><strong>{status?.version ?? "Development snapshot"}</strong><small>Updated {status?.updated ?? "lokal"}</small></span>
    </div>
    <nav>
      {#each categoryGroups as group (group.name)}
        <div class="sidebar-group">
          <strong>{group.name}</strong>
          {#each group.articles as article (article.id)}
            <button class:active={article.id === activeArticle} type="button" onclick={() => navigateTo(article.id)}>
              <span>{article.shortTitle}</span><ChevronRight size={13} />
            </button>
          {/each}
        </div>
      {/each}
    </nav>
    <div class="sidebar-links"><a href="./index.html"><Home size={14} /> Product page</a><a href="./index.html#download"><ArrowRight size={14} /> Build status</a></div>
  </aside>

  {#if sidebarOpen}<button class="docs-scrim" type="button" aria-label="Navigation schliessen" onclick={() => (sidebarOpen = false)}></button>{/if}

  <main class="docs-main" id="docs-main">
    <section class="docs-hero">
      <div>
        <span class="section-label">Development wiki</span>
        <h1>Engine-Vertraege.<br />Ohne Marketing-Nebel.</h1>
        <p>Aktuelle APIs, Ownership-Grenzen, Capability-Status und praktische Einstiegspfade aus dem laufenden YjsE-Checkout.</p>
      </div>
      <div class="docs-hero-terminal" aria-label="YjsE Quickstart">
        <div><i></i><i></i><i></i><span>powershell</span></div>
        <code><span>$</span> dotnet restore YjsE.sln --locked-mode<br /><span>$</span> dotnet test YjsE.sln -c Debug<br /><span>$</span> dotnet run --project Game.Client</code>
      </div>
    </section>

    <section class="docs-toolbar" aria-label="Dokumentation durchsuchen und filtern">
      <div class="docs-search">
        <Search size={18} />
        <input bind:this={searchInput} bind:value={query} type="search" placeholder="APIs, Systeme, Begriffe ..." aria-label="Dokumentation durchsuchen" />
        {#if query}<button type="button" aria-label="Suche leeren" onclick={() => (query = "")}><X size={16} /></button>{:else}<kbd>/</kbd>{/if}
      </div>
      <div class="docs-filter-scroll">
        {#each categories as item (item)}
          <button class:active={category === item} type="button" aria-pressed={category === item} onclick={() => (category = item)}>{item}</button>
        {/each}
      </div>
      <div class="status-filter">
        <SlidersHorizontal size={16} />
        <select bind:value={statusFilter} aria-label="Capability-Status filtern">
          <option value="all">Alle Status</option>
          <option value="verified">Verified</option>
          <option value="partial">Partial</option>
          <option value="planned">Planned</option>
          <option value="implemented-unverified">Implemented</option>
        </select>
      </div>
    </section>

    <div class="docs-result-bar">
      <span><strong>{filteredArticles.length}</strong> von {articles.length} Kapiteln</span>
      {#if query || category !== "Alle" || statusFilter !== "all"}<button type="button" onclick={resetFilters}>Filter loeschen <X size={13} /></button>{/if}
    </div>

    {#if filteredArticles.length > 0}
      <div class="docs-content">
        {#each filteredArticles as article (article.id)}
          <article class="doc-article" id={article.id} data-doc-article>
            <header>
              <div><span>{article.category}</span><StatusBadge status={article.status} compact /></div>
              <h2>{article.title}</h2>
              <p>{article.summary}</p>
            </header>

            <div class="article-body">
              {#each article.paragraphs ?? [] as paragraph}<p>{paragraph}</p>{/each}

              {#if article.bullets?.length}
                <ul class="article-bullets">{#each article.bullets as bullet}<li><Check size={14} /><span>{bullet}</span></li>{/each}</ul>
              {/if}

              {#each article.api ?? [] as api, apiIndex}
                <div class="api-block">
                  <div><span><FileCode2 size={14} />{api.label}</span><button type="button" onclick={() => copyCode(`${article.id}-${apiIndex}`, api.code)}>{#if copiedCode === `${article.id}-${apiIndex}`}<Check size={14} /> Copied{:else}<Copy size={14} /> Copy{/if}</button></div>
                  <pre><code>{api.code}</code></pre>
                </div>
              {/each}

              {#each article.callouts ?? [] as callout}
                <aside class="doc-callout {callout.kind}"><strong>{callout.title}</strong><p>{callout.text}</p></aside>
              {/each}

              {#if article.tags?.length}
                <div class="article-tags">{#each article.tags as tag}<span>{tag}</span>{/each}</div>
              {/if}
            </div>

            {#if article.links?.length}
              <footer>{#each article.links as link}<button type="button" onclick={() => navigateTo(link.target)}>{link.label}<ArrowRight size={14} /></button>{/each}</footer>
            {/if}
          </article>
        {/each}
      </div>
    {:else}
      <div class="docs-empty"><Search size={28} /><strong>Keine passenden Kapitel.</strong><p>Versuche einen API-Namen oder setze die Filter zurueck.</p><button class="button primary" type="button" onclick={resetFilters}>Alles anzeigen</button></div>
    {/if}
  </main>

  <aside class="docs-rail" aria-label="Seiteninformationen">
    <div><span>Current page</span><strong>{articles.find((article) => article.id === activeArticle)?.shortTitle ?? "Overview"}</strong></div>
    <nav>
      {#each filteredArticles.slice(0, 10) as article (article.id)}
        <button class:active={article.id === activeArticle} type="button" onclick={() => navigateTo(article.id)}>{article.shortTitle}</button>
      {/each}
    </nav>
    <button type="button" onclick={() => window.scrollTo({ top: 0, behavior: "smooth" })}><ArrowUp size={14} /> Nach oben</button>
  </aside>
</div>

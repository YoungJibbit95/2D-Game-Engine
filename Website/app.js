"use strict";

const FALLBACK_STATUS = Object.freeze({
  version: "Development snapshot",
  updated: "lokal",
  summary: "YjsE befindet sich in aktiver Entwicklung. Ein oeffentlicher Installer ist noch nicht publiziert.",
  metrics: [
    { value: ".NET 8", label: "Runtime" },
    { value: "60 Hz", label: "Fixed simulation" }
  ],
  features: [
    { title: "Renderer-neutraler Core", text: "Simulation und Gameplay bleiben unabhaengig vom MonoGame-Host.", status: "verified" }
  ]
});
const FALLBACK_RELEASES = Object.freeze({ downloads: [], notes: [] });
const FALLBACK_DOCS = Object.freeze({ categories: [], articles: [] });
const FALLBACK_ASSETS = Object.freeze({ assets: [] });

const STATUS_LABELS = Object.freeze({
  "verified": "verified",
  "implemented-unverified": "implemented / unverified",
  "partial": "partial",
  "planned": "planned",
  "blocked": "blocked",
  "deprecated": "deprecated"
});

async function loadJson(path, fallback) {
  try {
    const response = await fetch(path, { cache: "no-store" });
    if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
    return await response.json();
  } catch (error) {
    console.info(`Fallback fuer ${path}: ${error.message}`);
    return fallback;
  }
}

function element(tag, className, text) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined) node.textContent = text;
  return node;
}

function setChildren(node, ...children) {
  node.replaceChildren(...children.filter(Boolean));
  return node;
}

function renderBadge(status) {
  const normalized = STATUS_LABELS[status] ? status : "partial";
  return element("span", `capability-badge status-${normalized}`, STATUS_LABELS[normalized]);
}

function setupSharedNavigation() {
  const header = document.querySelector("#site-header");
  const navToggle = document.querySelector("#nav-toggle");
  const mainNavigation = document.querySelector("#main-navigation");

  function syncHeader() {
    header?.classList.toggle("is-scrolled", window.scrollY > 18);
  }
  syncHeader();
  window.addEventListener("scroll", syncHeader, { passive: true });

  function closeNavigation() {
    document.body.classList.remove("nav-open");
    navToggle?.setAttribute("aria-expanded", "false");
  }
  navToggle?.addEventListener("click", () => {
    const open = !document.body.classList.contains("nav-open");
    document.body.classList.toggle("nav-open", open);
    navToggle.setAttribute("aria-expanded", String(open));
  });
  mainNavigation?.addEventListener("click", event => {
    if (event.target.closest("a")) closeNavigation();
  });
  document.addEventListener("keydown", event => {
    if (event.key === "Escape") closeNavigation();
  });
  window.addEventListener("resize", () => {
    if (window.innerWidth > 820) closeNavigation();
  });
}

function renderHome(status, releases, assets) {
  const summary = document.querySelector("#status-summary");
  const updated = document.querySelector("#status-updated");
  if (summary) summary.textContent = status.summary;
  if (updated) {
    updated.textContent = status.updated;
    if (/^\d{4}-\d{2}-\d{2}$/.test(status.updated)) updated.dateTime = status.updated;
  }

  const metrics = document.querySelector("#status-metrics");
  const metricNodes = (status.metrics || []).map(metric => {
    const item = element("div", "metric");
    return setChildren(item, element("strong", "", metric.value), element("span", "", metric.label));
  });
  metrics?.replaceChildren(...metricNodes);

  const features = document.querySelector("#feature-list");
  const featureNodes = (status.features || []).map((feature, index) => {
    const card = element("article", "feature-card");
    const top = element("div", "feature-topline");
    top.append(element("span", "feature-index", String(index + 1).padStart(2, "0")), renderBadge(feature.status));
    card.append(top, element("h3", "", feature.title), element("p", "", feature.text));
    return card;
  });
  features?.replaceChildren(...featureNodes);

  const scenes = document.querySelector("#asset-scenes");
  const sceneAssets = (assets.assets || [])
    .filter(asset => asset.role === "scene")
    .sort((left, right) => (left.featuredOrder ?? 999) - (right.featuredOrder ?? 999));
  const sceneNodes = sceneAssets.map(asset => {
    const card = element("figure", "scene-card");
    const image = element("img");
    image.src = asset.localPath;
    image.alt = asset.alt;
    image.loading = "lazy";
    image.decoding = "async";
    const caption = element("figcaption", "scene-card-copy");
    caption.append(element("strong", "", asset.title), element("span", "", asset.meta));
    card.append(image, caption);
    return card;
  });
  scenes?.replaceChildren(...sceneNodes);

  const downloads = document.querySelector("#download-options");
  const downloadNodes = (releases.downloads || []).map(download => {
    const row = element("article", "download-option");
    const copy = element("div");
    copy.append(element("h3", "", download.platform), element("p", "", download.detail));
    const tags = element("div", "download-tags");
    (download.requirements || []).forEach(value => tags.append(element("span", "download-tag", value)));
    copy.append(tags);

    const control = element("div", "download-control");
    if (download.published === true && download.href) {
      const link = element("a", "button button-dark", download.label || "Download");
      link.href = download.href;
      if (download.downloadName) link.download = download.downloadName;
      control.append(link);
    } else {
      const button = element("button", "button", download.label || "Nicht publiziert");
      button.type = "button";
      button.disabled = true;
      control.append(button, element("small", "", download.status || "Kein Artefakt hinterlegt"));
    }
    row.append(copy, control);
    return row;
  });
  if (downloadNodes.length === 0) downloadNodes.push(element("p", "empty-inline", "Kein oeffentlicher Build konfiguriert."));
  downloads?.replaceChildren(...downloadNodes);

  const releaseList = document.querySelector("#release-list");
  const noteNodes = (releases.notes || []).map(note => {
    const row = element("article", "release-item");
    const copy = element("div");
    const list = element("ul");
    (note.items || []).forEach(item => list.append(element("li", "", item)));
    copy.append(element("h3", "", note.title), list);
    row.append(element("div", "release-meta", note.date), copy);
    return row;
  });
  releaseList?.replaceChildren(...noteNodes);
}

function showToast(message) {
  const toast = document.querySelector("#site-toast");
  if (!toast) return;
  toast.textContent = message;
  toast.hidden = false;
  window.clearTimeout(showToast.timeoutId);
  showToast.timeoutId = window.setTimeout(() => { toast.hidden = true; }, 1800);
}

function copyText(text, successMessage) {
  if (navigator.clipboard?.writeText) {
    navigator.clipboard.writeText(text).then(() => showToast(successMessage)).catch(() => showToast("Kopieren nicht verfuegbar"));
    return;
  }
  const input = document.createElement("textarea");
  input.value = text;
  input.setAttribute("readonly", "");
  input.style.position = "fixed";
  input.style.opacity = "0";
  document.body.append(input);
  input.select();
  const copied = document.execCommand("copy");
  input.remove();
  showToast(copied ? successMessage : "Kopieren nicht verfuegbar");
}

function renderApiBlock(api) {
  const block = element("div", "api-block");
  const header = element("div", "api-block-header");
  const copy = element("button", "", "Copy");
  copy.type = "button";
  copy.title = `${api.label} kopieren`;
  copy.addEventListener("click", () => copyText(api.code, "Code kopiert"));
  header.append(element("span", "", api.label), copy);
  const pre = element("pre");
  pre.append(element("code", "", api.code));
  block.append(header, pre);
  return block;
}

function renderCallout(callout) {
  const node = element("aside", `doc-callout ${callout.kind || "note"}`);
  const mark = element("span", "callout-mark", callout.kind === "warning" ? "!" : "i");
  const copy = element("div");
  copy.append(element("strong", "", callout.title), element("p", "", callout.text));
  node.append(mark, copy);
  return node;
}

function renderDocs(data, status) {
  const content = document.querySelector("#docs-content");
  const navigation = document.querySelector("#docs-navigation");
  const toc = document.querySelector("#docs-toc");
  const filters = document.querySelector("#docs-filters");
  const search = document.querySelector("#docs-search");
  const note = document.querySelector("#search-result-note");
  const empty = document.querySelector("#docs-empty");
  const reset = document.querySelector("#reset-search");
  const emptyReset = document.querySelector("#empty-reset-search");
  const version = document.querySelector("#sidebar-version");
  const updated = document.querySelector("#sidebar-updated");
  if (!content || !navigation || !toc || !filters || !search || !note || !empty) return;

  if (version) version.textContent = status.version;
  if (updated) updated.textContent = `Datenstand ${status.updated}`;

  const url = new URL(window.location.href);
  const categories = ["Alle", ...(data.categories || [])];
  let activeCategory = categories.includes(url.searchParams.get("category")) ? url.searchParams.get("category") : "Alle";
  search.value = url.searchParams.get("q") || "";
  const articles = [];
  const navLinks = new Map();
  const tocLinks = new Map();
  const filterButtons = new Map();

  for (const label of categories) {
    const button = element("button", `filter-button${label === activeCategory ? " active" : ""}`, label);
    button.type = "button";
    button.dataset.category = label;
    button.setAttribute("aria-pressed", String(label === activeCategory));
    button.addEventListener("click", () => {
      activeCategory = label;
      for (const [category, item] of filterButtons) {
        const active = category === activeCategory;
        item.classList.toggle("active", active);
        item.setAttribute("aria-pressed", String(active));
      }
      applyFilter(true);
    });
    filterButtons.set(label, button);
    filters.append(button);
  }

  for (const category of data.categories || []) {
    const categoryArticles = (data.articles || []).filter(article => article.category === category);
    if (categoryArticles.length === 0) continue;
    const group = element("section", "nav-group");
    const title = element("div", "nav-group-title");
    title.append(element("strong", "", category), element("span", "", String(categoryArticles.length)));
    const links = element("div", "nav-group-links");
    for (const article of categoryArticles) {
      const link = element("a", "", article.title);
      link.href = `#${article.id}`;
      link.dataset.articleId = article.id;
      links.append(link);
      navLinks.set(article.id, link);
    }
    group.append(title, links);
    navigation.append(group);
  }

  for (const article of data.articles || []) {
    const tocLink = element("a", "", article.shortTitle || article.title);
    tocLink.href = `#${article.id}`;
    tocLink.dataset.articleId = article.id;
    toc.append(tocLink);
    tocLinks.set(article.id, tocLink);

    const section = element("article", "doc-article");
    section.id = article.id;
    section.dataset.category = article.category;
    section.dataset.search = [
      article.title,
      article.shortTitle,
      article.summary,
      ...(article.paragraphs || []),
      ...(article.bullets || []),
      ...(article.tags || []),
      ...(article.api || []).flatMap(api => [api.label, api.code])
    ].filter(Boolean).join(" ").toLocaleLowerCase("de");

    const heading = element("div", "article-heading-row");
    const actions = element("div", "article-heading-actions");
    const anchor = element("button", "anchor-button", "#");
    anchor.type = "button";
    anchor.title = "Direktlink kopieren";
    anchor.setAttribute("aria-label", `Direktlink zu ${article.title} kopieren`);
    anchor.addEventListener("click", () => copyText(`${window.location.origin}${window.location.pathname}#${article.id}`, "Direktlink kopiert"));
    actions.append(renderBadge(article.status), anchor);
    heading.append(element("h2", "", article.title), actions);
    section.append(element("p", "article-breadcrumb", article.category), heading, element("p", "article-summary", article.summary));
    (article.paragraphs || []).forEach(paragraph => section.append(element("p", "", paragraph)));
    if (article.bullets?.length) {
      const list = element("ul", "doc-list");
      article.bullets.forEach(item => list.append(element("li", "", item)));
      section.append(list);
    }
    (article.callouts || []).forEach(callout => section.append(renderCallout(callout)));
    (article.api || []).forEach(api => section.append(renderApiBlock(api)));
    if (article.code) {
      const pre = element("pre");
      pre.append(element("code", "", article.code));
      section.append(pre);
    }
    if (article.tags?.length) {
      const tags = element("div", "article-tags");
      article.tags.forEach(tag => tags.append(element("span", "article-tag", tag)));
      section.append(tags);
    }
    if (article.links?.length) {
      const links = element("div", "article-links");
      article.links.forEach(item => {
        const link = element("a", "text-link", `${item.label}  ->`);
        link.href = `#${item.target}`;
        links.append(link);
      });
      section.append(links);
    }
    content.append(section);
    articles.push(section);
  }

  function syncUrl() {
    const current = new URL(window.location.href);
    const query = search.value.trim();
    if (query) current.searchParams.set("q", query); else current.searchParams.delete("q");
    if (activeCategory !== "Alle") current.searchParams.set("category", activeCategory); else current.searchParams.delete("category");
    window.history.replaceState(null, "", `${current.pathname}${current.search}${current.hash}`);
  }

  function applyFilter(updateUrl) {
    const query = search.value.trim().toLocaleLowerCase("de");
    let visible = 0;
    for (const article of articles) {
      const matchesCategory = activeCategory === "Alle" || article.dataset.category === activeCategory;
      const matchesSearch = !query || article.dataset.search.includes(query);
      const show = matchesCategory && matchesSearch;
      article.hidden = !show;
      navLinks.get(article.id)?.toggleAttribute("hidden", !show);
      tocLinks.get(article.id)?.toggleAttribute("hidden", !show);
      if (show) visible++;
    }
    const filtering = Boolean(query) || activeCategory !== "Alle";
    note.textContent = filtering ? `${visible} von ${articles.length} Kapiteln sichtbar` : `${articles.length} Kapitel im aktuellen Development Wiki`;
    empty.hidden = visible !== 0;
    if (reset) reset.hidden = !filtering;
    if (updateUrl) syncUrl();
  }

  function clearFilters() {
    search.value = "";
    activeCategory = "Alle";
    for (const [category, button] of filterButtons) {
      const active = category === activeCategory;
      button.classList.toggle("active", active);
      button.setAttribute("aria-pressed", String(active));
    }
    applyFilter(true);
    search.focus();
  }

  search.addEventListener("input", () => applyFilter(true));
  reset?.addEventListener("click", clearFilters);
  emptyReset?.addEventListener("click", clearFilters);
  document.addEventListener("keydown", event => {
    const typing = /^(INPUT|TEXTAREA|SELECT)$/.test(document.activeElement?.tagName || "");
    if (event.key === "/" && !typing) {
      event.preventDefault();
      search.focus();
    }
  });

  const sidebar = document.querySelector("#docs-sidebar");
  const scrim = document.querySelector("#sidebar-scrim");
  const toggles = [document.querySelector("#sidebar-toggle"), document.querySelector("#mobile-section-button")].filter(Boolean);
  function closeSidebar() {
    document.body.classList.remove("sidebar-open");
    toggles.forEach(toggle => toggle.setAttribute("aria-expanded", "false"));
    if (scrim) scrim.hidden = true;
  }
  function toggleSidebar() {
    const open = !document.body.classList.contains("sidebar-open");
    document.body.classList.toggle("sidebar-open", open);
    toggles.forEach(toggle => toggle.setAttribute("aria-expanded", String(open)));
    if (scrim) scrim.hidden = !open;
  }
  toggles.forEach(toggle => toggle.addEventListener("click", toggleSidebar));
  scrim?.addEventListener("click", closeSidebar);
  sidebar?.addEventListener("click", event => { if (event.target.closest("a")) closeSidebar(); });
  document.addEventListener("keydown", event => { if (event.key === "Escape") closeSidebar(); });
  window.addEventListener("resize", () => { if (window.innerWidth > 820) closeSidebar(); });

  function activateArticle(id) {
    for (const [articleId, link] of navLinks) link.classList.toggle("active", articleId === id);
    for (const [articleId, link] of tocLinks) link.classList.toggle("active", articleId === id);
    const target = document.getElementById(id);
    const mobile = document.querySelector("#mobile-current-section");
    if (mobile) mobile.textContent = target?.querySelector("h2")?.textContent || "Engine Docs";
  }

  if ("IntersectionObserver" in window) {
    const visibleArticles = new Set();
    const observer = new IntersectionObserver(entries => {
      for (const entry of entries) {
        if (entry.isIntersecting) visibleArticles.add(entry.target);
        else visibleArticles.delete(entry.target);
      }
      const current = [...visibleArticles]
        .filter(article => !article.hidden)
        .sort((a, b) => Math.abs(a.getBoundingClientRect().top - 190) - Math.abs(b.getBoundingClientRect().top - 190))[0];
      if (!current) return;
      activateArticle(current.id);
    }, { rootMargin: "-18% 0px -68%", threshold: 0.01 });
    articles.forEach(article => observer.observe(article));
  }

  applyFilter(false);
  if (window.location.hash) {
    window.requestAnimationFrame(() => {
      const targetId = decodeURIComponent(window.location.hash.slice(1));
      const target = document.getElementById(targetId);
      if (!target) return;
      const previousBehavior = document.documentElement.style.scrollBehavior;
      document.documentElement.style.scrollBehavior = "auto";
      target.scrollIntoView({ block: "start" });
      document.documentElement.style.scrollBehavior = previousBehavior;
      activateArticle(targetId);
    });
  }
}

async function boot() {
  setupSharedNavigation();
  const page = document.body.dataset.page;
  const statusPromise = loadJson("data/status.json", FALLBACK_STATUS);
  if (page === "home") {
    const [status, releases, assets] = await Promise.all([
      statusPromise,
      loadJson("data/releases.json", FALLBACK_RELEASES),
      loadJson("data/assets.json", FALLBACK_ASSETS)
    ]);
    renderHome(status, releases, assets);
  } else if (page === "docs") {
    const [status, docs] = await Promise.all([statusPromise, loadJson("data/docs.json", FALLBACK_DOCS)]);
    renderDocs(docs, status);
  }
}

boot();

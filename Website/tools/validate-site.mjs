import { createHash } from "node:crypto";
import { readFile, readdir, stat } from "node:fs/promises";
import path from "node:path";
import process from "node:process";
import { fileURLToPath } from "node:url";

const toolsRoot = path.dirname(fileURLToPath(import.meta.url));
const websiteRoot = path.resolve(toolsRoot, "..");
const repositoryRoot = path.resolve(websiteRoot, "..");
const staticRoot = path.join(websiteRoot, "static");
const distRoot = path.join(websiteRoot, "dist");
const allowedStatuses = new Set(["verified", "implemented-unverified", "partial", "planned", "blocked", "deprecated"]);
const requiredArticles = [
  "getting-started", "reference-game", "architecture", "game-loop", "world",
  "structure-materialization", "living-world", "world-events", "distributed-spawning", "rng-streams",
  "gameplay", "building-transactions", "combat", "entities", "animation", "rendering", "ray-lighting",
  "texture-groups", "content", "ui", "ui-renderer", "saving", "tools", "phase-telemetry", "tests"
];
const errors = [];
let checkedReferences = 0;
let checkedPngs = 0;
let checkedProvenanceCopies = 0;
let checkedWave05Assets = 0;
let checkedWave05Frames = 0;

function fail(message) {
  errors.push(message);
}

function requireText(value, location) {
  if (typeof value !== "string" || value.trim().length === 0) fail(`${location}: expected non-empty text`);
}

function isInside(root, target) {
  const relative = path.relative(root, target);
  return relative === "" || (!relative.startsWith(`..${path.sep}`) && relative !== "..");
}

async function readText(root, relativePath) {
  const buffer = await readFile(path.join(root, relativePath));
  if (buffer.length >= 3 && buffer[0] === 0xef && buffer[1] === 0xbb && buffer[2] === 0xbf) {
    fail(`${relativePath}: UTF-8 BOM is not allowed`);
  }
  const source = buffer.toString("utf8");
  if (/\ufffd|\u00c3.|\u00c2.|\u00e2(?:\u20ac|\u2122|\u2020|\u2021|\u02c6|\u2030|\u0160|\u2039|\u0152|\u017d|\u201c|\u201d|\u2022|\u2013|\u2014|\u02dc)/u.test(source)) {
    fail(`${relativePath}: possible mojibake or replacement character`);
  }
  return source;
}

async function readJson(root, relativePath) {
  try {
    return JSON.parse(await readText(root, relativePath));
  } catch (error) {
    fail(`${relativePath}: invalid JSON (${error.message})`);
    return null;
  }
}

function validateStatus(status) {
  if (!status) return;
  requireText(status.version, "status version");
  requireText(status.updated, "status updated");
  requireText(status.summary, "status summary");
  if (!/^\d{4}-\d{2}-\d{2}$/.test(status.updated)) fail("status updated: expected YYYY-MM-DD");
  if (!Array.isArray(status.metrics) || status.metrics.length !== 4) fail("status metrics: expected exactly four entries");
  for (const [index, metric] of (status.metrics || []).entries()) {
    requireText(metric.value, `status metric ${index} value`);
    requireText(metric.label, `status metric ${index} label`);
  }
  if (!Array.isArray(status.features) || status.features.length < 4) fail("status features: expected at least four entries");
  for (const [index, feature] of (status.features || []).entries()) {
    requireText(feature.title, `status feature ${index} title`);
    requireText(feature.text, `status feature ${index} text`);
    if (!allowedStatuses.has(feature.status)) fail(`status feature ${index}: invalid status '${feature.status}'`);
  }
}

function validateDocs(docs) {
  const ids = new Set();
  if (!docs || !Array.isArray(docs.categories) || !Array.isArray(docs.articles)) {
    fail("docs: categories and articles arrays are required");
    return ids;
  }
  const categories = new Set();
  for (const [index, category] of docs.categories.entries()) {
    requireText(category, `docs category ${index}`);
    if (categories.has(category)) fail(`docs: duplicate category '${category}'`);
    categories.add(category);
  }
  for (const [index, article] of docs.articles.entries()) {
    requireText(article.id, `docs article ${index} id`);
    requireText(article.title, `docs article ${index} title`);
    requireText(article.shortTitle, `docs article ${index} shortTitle`);
    requireText(article.summary, `docs article ${index} summary`);
    if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(article.id || "")) fail(`article '${article.id}': id must be kebab-case`);
    if (ids.has(article.id)) fail(`docs: duplicate article id '${article.id}'`);
    ids.add(article.id);
    if (!categories.has(article.category)) fail(`article '${article.id}': unknown category '${article.category}'`);
    if (!allowedStatuses.has(article.status)) fail(`article '${article.id}': invalid status '${article.status}'`);
    if (!Array.isArray(article.tags) || article.tags.length === 0) fail(`article '${article.id}': at least one search tag is required`);
    for (const [apiIndex, api] of (article.api || []).entries()) {
      requireText(api.label, `article '${article.id}' api ${apiIndex} label`);
      requireText(api.code, `article '${article.id}' api ${apiIndex} code`);
    }
    for (const [calloutIndex, callout] of (article.callouts || []).entries()) {
      if (!new Set(["note", "warning"]).has(callout.kind)) fail(`article '${article.id}' callout ${calloutIndex}: invalid kind`);
      requireText(callout.title, `article '${article.id}' callout ${calloutIndex} title`);
      requireText(callout.text, `article '${article.id}' callout ${calloutIndex} text`);
    }
  }
  for (const id of requiredArticles) if (!ids.has(id)) fail(`docs: missing required article '${id}'`);
  for (const article of docs.articles) {
    for (const link of article.links || []) {
      requireText(link.label, `article '${article.id}' link label`);
      if (!ids.has(link.target)) fail(`article '${article.id}': missing link target '${link.target}'`);
    }
  }
  return ids;
}

function validateReleases(releases) {
  if (!releases || !Array.isArray(releases.downloads) || releases.downloads.length < 2) {
    fail("releases: engine and reference-game download states are required");
    return;
  }
  for (const [index, download] of releases.downloads.entries()) {
    requireText(download.platform, `download ${index} platform`);
    requireText(download.detail, `download ${index} detail`);
    requireText(download.label, `download ${index} label`);
    if (!Array.isArray(download.requirements) || download.requirements.length === 0) fail(`download ${index}: requirements are required`);
    if (typeof download.published !== "boolean") fail(`download ${index}: published must be boolean`);
    if (download.published) requireText(download.href, `download ${index} published href`);
    else {
      if (download.href !== null) fail(`download ${index}: unpublished builds must use href: null`);
      requireText(download.status, `download ${index} disabled status`);
    }
  }
  if (!Array.isArray(releases.notes) || releases.notes.length === 0) fail("releases notes: expected entries");
}

async function validateAssets(data) {
  const ids = new Set();
  const localPaths = new Set();
  if (!data || !Array.isArray(data.assets) || data.assets.length === 0) {
    fail("assets: assets array is required");
    return;
  }
  for (const [index, asset] of data.assets.entries()) {
    requireText(asset.id, `asset ${index} id`);
    requireText(asset.role, `asset ${index} role`);
    requireText(asset.title, `asset ${index} title`);
    requireText(asset.alt, `asset ${index} alt`);
    requireText(asset.localPath, `asset ${index} localPath`);
    requireText(asset.sourcePath, `asset ${index} sourcePath`);
    if (ids.has(asset.id)) fail(`assets: duplicate id '${asset.id}'`);
    if (localPaths.has(asset.localPath)) fail(`assets: duplicate localPath '${asset.localPath}'`);
    ids.add(asset.id);
    localPaths.add(asset.localPath);

    const local = path.resolve(staticRoot, asset.localPath || "");
    const source = path.resolve(repositoryRoot, asset.sourcePath || "");
    if (!isInside(path.join(staticRoot, "assets"), local)) {
      fail(`asset '${asset.id}': localPath must stay inside Website/static/assets`);
      continue;
    }
    if (!isInside(path.join(repositoryRoot, "Game.Data"), source)) {
      fail(`asset '${asset.id}': sourcePath must stay inside Game.Data`);
      continue;
    }
    try {
      const [localBytes, sourceBytes] = await Promise.all([readFile(local), readFile(source)]);
      if (createHash("sha256").update(localBytes).digest("hex") !== createHash("sha256").update(sourceBytes).digest("hex")) {
        fail(`asset '${asset.id}': website copy differs from '${asset.sourcePath}'`);
      } else checkedProvenanceCopies++;
    } catch (error) {
      fail(`asset '${asset.id}': missing local/source file (${error.message})`);
    }
  }
}

async function validateWave05SourceContract(data) {
  const manifestPath = path.join(repositoryRoot, "Game.Data", "assets", "wave05_living_world.sprites.json");
  try {
    const manifest = JSON.parse(await readFile(manifestPath, "utf8"));
    checkedWave05Assets = Array.isArray(manifest.sprites) ? manifest.sprites.length : 0;
    checkedWave05Frames = (manifest.sprites || []).reduce((total, sprite) => total + (sprite.frames?.length ?? 0), 0);
    if (checkedWave05Assets !== 24) fail(`Wave 05 manifest: expected 24 assets, found ${checkedWave05Assets}`);
    if (checkedWave05Frames !== 122) fail(`Wave 05 manifest: expected 122 frames, found ${checkedWave05Frames}`);
    const sourcePaths = new Set((manifest.sprites || []).map((sprite) => `Game.Data/${String(sprite.path || "").replaceAll("\\", "/")}`.toLowerCase()));
    const websiteAssets = (data?.assets || []).filter((asset) => asset.localPath?.startsWith("assets/wave05/"));
    for (const asset of websiteAssets) {
      if (!sourcePaths.has(String(asset.sourcePath).toLowerCase())) fail(`asset '${asset.id}': source not declared by Wave 05 manifest`);
    }
  } catch (error) {
    fail(`Wave 05 source manifest: invalid or missing (${error.message})`);
  }
}

async function walk(directory, visitor) {
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const absolute = path.join(directory, entry.name);
    if (entry.isDirectory()) await walk(absolute, visitor);
    else await visitor(absolute);
  }
}

async function validatePng(absolute) {
  const buffer = await readFile(absolute);
  const label = path.relative(websiteRoot, absolute).replaceAll(path.sep, "/");
  if (buffer.length < 24 || buffer.subarray(0, 8).toString("hex") !== "89504e470d0a1a0a") {
    fail(`${label}: invalid PNG signature or header`);
    return;
  }
  if (buffer.readUInt32BE(16) === 0 || buffer.readUInt32BE(20) === 0) fail(`${label}: invalid PNG dimensions`);
  checkedPngs++;
}

function collectReferences(source) {
  return [...source.matchAll(/\b(?:href|src)\s*=\s*["']([^"']+)["']/gi)].map((match) => match[1].trim());
}

async function validateBuiltReference(reference, sourceFile) {
  if (!reference || /^(?:https?:|mailto:|tel:|data:|#)/i.test(reference)) return;
  const withoutHash = reference.split("#", 1)[0].split("?", 1)[0];
  if (!withoutHash) return;
  const normalized = withoutHash.startsWith("/") ? withoutHash.slice(1) : withoutHash;
  const target = path.resolve(distRoot, path.dirname(sourceFile), normalized);
  if (!isInside(distRoot, target)) {
    fail(`${sourceFile}: reference escapes dist '${reference}'`);
    return;
  }
  try {
    if (!(await stat(target)).isFile()) fail(`${sourceFile}: reference is not a file '${reference}'`);
    else checkedReferences++;
  } catch {
    fail(`${sourceFile}: missing built reference '${reference}'`);
  }
}

async function validateSourceContracts() {
  const packageJson = await readJson(websiteRoot, "package.json");
  if (!packageJson?.scripts?.build?.includes("vite build")) fail("package.json: Vite build script is required");
  if (!packageJson?.scripts?.check?.includes("svelte-check")) fail("package.json: svelte-check script is required");
  for (const dependency of ["svelte", "vite", "@sveltejs/vite-plugin-svelte", "@lucide/svelte"]) {
    if (!(packageJson?.dependencies?.[dependency] || packageJson?.devDependencies?.[dependency])) fail(`package.json: missing '${dependency}'`);
  }

  const [homeSource, docsSource, styles, headerSource, viteConfig] = await Promise.all([
    readText(websiteRoot, "src/HomeApp.svelte"),
    readText(websiteRoot, "src/DocsApp.svelte"),
    readText(websiteRoot, "src/styles/app.css"),
    readText(websiteRoot, "src/lib/components/SiteHeader.svelte"),
    readText(websiteRoot, "vite.config.js")
  ]);
  for (const [label, source] of [["HomeApp", homeSource], ["DocsApp", docsSource]]) {
    for (const contract of ["skip-link", "<main", "aria-label", "SiteHeader"]) {
      if (!source.includes(contract)) fail(`${label}: missing '${contract}' accessibility/component contract`);
    }
  }
  for (const contract of ["aria-pressed", "bind:value", "copyCode", "navigateTo"]) {
    if (!docsSource.includes(contract)) fail(`DocsApp: missing '${contract}' interaction contract`);
  }
  for (const contract of ["@supports (backdrop-filter", "prefers-reduced-motion", ":focus-visible", ".status-badge", ".docs-scrim"]) {
    if (!styles.includes(contract)) fail(`app.css: missing '${contract}' resilient UI contract`);
  }
  if (!headerSource.includes("@lucide/svelte")) fail("SiteHeader: Lucide icon support is required");
  if (!viteConfig.includes('base: "./"')) fail("vite.config.js: relative base is required for portable static hosting");
}

async function validateBuild() {
  const htmlFiles = ["index.html", "docs.html"];
  for (const file of htmlFiles) {
    let source;
    try {
      source = await readText(distRoot, file);
    } catch (error) {
      fail(`dist/${file}: missing production build (${error.message})`);
      continue;
    }
    if (!/<html\s+lang=["']de["']/i.test(source)) fail(`dist/${file}: html lang='de' is required`);
    if (!/<meta\s+charset=["']UTF-8["']/i.test(source)) fail(`dist/${file}: UTF-8 meta is required`);
    if (!/id=["']app["']/.test(source)) fail(`dist/${file}: Svelte mount root is required`);
    if (!/<script[^>]+type=["']module["']/i.test(source)) fail(`dist/${file}: module bundle is required`);
    for (const reference of collectReferences(source)) await validateBuiltReference(reference, file);
  }

  for (const file of ["data/status.json", "data/releases.json", "data/docs.json", "data/assets.json"]) {
    try {
      const [source, built] = await Promise.all([readFile(path.join(staticRoot, file)), readFile(path.join(distRoot, file))]);
      if (!source.equals(built)) fail(`dist/${file}: differs from static source data`);
    } catch (error) {
      fail(`dist/${file}: missing copied static data (${error.message})`);
    }
  }

  const bundles = [];
  await walk(path.join(distRoot, "assets"), async (absolute) => {
    if (/\.(?:js|css)$/.test(absolute)) bundles.push(absolute);
  });
  if (!bundles.some((file) => file.endsWith(".js"))) fail("dist/assets: JavaScript bundle is missing");
  if (!bundles.some((file) => file.endsWith(".css"))) fail("dist/assets: CSS bundle is missing");
}

const status = await readJson(staticRoot, "data/status.json");
const docs = await readJson(staticRoot, "data/docs.json");
const releases = await readJson(staticRoot, "data/releases.json");
const assets = await readJson(staticRoot, "data/assets.json");
validateStatus(status);
const docsIds = validateDocs(docs);
validateReleases(releases);
await validateAssets(assets);
await validateWave05SourceContract(assets);
await validateSourceContracts();
await validateBuild();
await walk(path.join(staticRoot, "assets"), async (absolute) => {
  if (absolute.toLowerCase().endsWith(".png")) await validatePng(absolute);
});

for (const file of ["README.md", "tools/update-site-data.ps1", "tools/validate-site.mjs"]) await readText(websiteRoot, file);

if (errors.length > 0) {
  for (const error of errors) console.error(`ERROR ${error}`);
  process.exitCode = 1;
} else {
  console.log(
    `Svelte website validation passed: ${docsIds.size} docs, ${checkedReferences} built references, ${checkedPngs} PNG assets, ` +
    `${checkedProvenanceCopies} byte-exact Game.Data copies; Wave 05 ${checkedWave05Assets} assets / ${checkedWave05Frames} frames.`
  );
}

import { createHash } from "node:crypto";
import { readFile, readdir, stat } from "node:fs/promises";
import path from "node:path";
import process from "node:process";
import { fileURLToPath } from "node:url";

const toolsRoot = path.dirname(fileURLToPath(import.meta.url));
const websiteRoot = path.resolve(toolsRoot, "..");
const repositoryRoot = path.resolve(websiteRoot, "..");
const allowedStatuses = new Set(["verified", "implemented-unverified", "partial", "planned", "blocked", "deprecated"]);
const requiredArticles = [
  "getting-started", "reference-game", "architecture", "game-loop", "world",
  "structure-materialization", "living-world", "world-events", "distributed-spawning", "rng-streams",
  "gameplay", "building-transactions", "combat", "entities", "animation", "rendering", "ray-lighting",
  "texture-groups", "content", "ui", "ui-renderer", "saving", "tools",
  "phase-telemetry", "tests"
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

async function readText(relativePath) {
  const buffer = await readFile(path.join(websiteRoot, relativePath));
  if (buffer.length >= 3 && buffer[0] === 0xef && buffer[1] === 0xbb && buffer[2] === 0xbf) {
    fail(`${relativePath}: UTF-8 BOM is not allowed`);
  }
  const source = buffer.toString("utf8");
  if (/\ufffd|\u00c3.|\u00c2.|\u00e2(?:\u20ac|\u2122|\u2020|\u2021|\u02c6|\u2030|\u0160|\u2039|\u0152|\u017d|\u201c|\u201d|\u2022|\u2013|\u2014|\u02dc)/u.test(source)) {
    fail(`${relativePath}: possible mojibake or replacement character`);
  }
  return source;
}

async function readJson(relativePath) {
  try {
    return JSON.parse(await readText(relativePath));
  } catch (error) {
    fail(`${relativePath}: invalid JSON (${error.message})`);
    return null;
  }
}

function validateStatus(status) {
  if (!status) return;
  requireText(status.version, "data/status.json version");
  requireText(status.updated, "data/status.json updated");
  requireText(status.summary, "data/status.json summary");
  if (!/^\d{4}-\d{2}-\d{2}$/.test(status.updated)) fail("data/status.json updated: expected YYYY-MM-DD");
  if (!Array.isArray(status.metrics) || status.metrics.length !== 4) fail("data/status.json metrics: expected exactly four entries");
  for (const [index, metric] of (status.metrics || []).entries()) {
    requireText(metric.value, `status metric ${index} value`);
    requireText(metric.label, `status metric ${index} label`);
  }
  if (!Array.isArray(status.features) || status.features.length < 4) fail("data/status.json features: expected at least four entries");
  for (const [index, feature] of (status.features || []).entries()) {
    requireText(feature.title, `status feature ${index} title`);
    requireText(feature.text, `status feature ${index} text`);
    if (!allowedStatuses.has(feature.status)) fail(`status feature ${index}: invalid status '${feature.status}'`);
  }
}

function validateDocs(docs) {
  const ids = new Set();
  if (!docs || !Array.isArray(docs.categories) || !Array.isArray(docs.articles)) {
    fail("data/docs.json: categories and articles arrays are required");
    return ids;
  }
  const categories = new Set();
  for (const [index, category] of docs.categories.entries()) {
    requireText(category, `docs category ${index}`);
    if (categories.has(category)) fail(`data/docs.json: duplicate category '${category}'`);
    categories.add(category);
  }
  for (const [index, article] of docs.articles.entries()) {
    requireText(article.id, `docs article ${index} id`);
    requireText(article.title, `docs article ${index} title`);
    requireText(article.shortTitle, `docs article ${index} shortTitle`);
    requireText(article.summary, `docs article ${index} summary`);
    if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(article.id || "")) fail(`article '${article.id}': id must be kebab-case`);
    if (ids.has(article.id)) fail(`data/docs.json: duplicate article id '${article.id}'`);
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
  for (const id of requiredArticles) if (!ids.has(id)) fail(`data/docs.json: missing required article '${id}'`);
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
    fail("data/releases.json: engine and reference-game download states are required");
    return;
  }
  for (const [index, download] of releases.downloads.entries()) {
    requireText(download.platform, `download ${index} platform`);
    requireText(download.detail, `download ${index} detail`);
    requireText(download.label, `download ${index} label`);
    if (!Array.isArray(download.requirements) || download.requirements.length === 0) fail(`download ${index}: requirements are required`);
    if (typeof download.published !== "boolean") fail(`download ${index}: published must be boolean`);
    if (download.published) {
      requireText(download.href, `download ${index} published href`);
    } else {
      if (download.href !== null) fail(`download ${index}: unpublished builds must use href: null`);
      requireText(download.status, `download ${index} disabled status`);
    }
  }
  if (!Array.isArray(releases.notes) || releases.notes.length === 0) fail("data/releases.json notes: expected entries");
}

async function validateAssets(data) {
  const ids = new Set();
  const localPaths = new Set();
  if (!data || !Array.isArray(data.assets) || data.assets.length === 0) {
    fail("data/assets.json: assets array is required");
    return localPaths;
  }
  for (const [index, asset] of data.assets.entries()) {
    requireText(asset.id, `asset ${index} id`);
    requireText(asset.role, `asset ${index} role`);
    requireText(asset.title, `asset ${index} title`);
    requireText(asset.alt, `asset ${index} alt`);
    requireText(asset.localPath, `asset ${index} localPath`);
    requireText(asset.sourcePath, `asset ${index} sourcePath`);
    if (asset.featuredOrder !== undefined && (!Number.isInteger(asset.featuredOrder) || asset.featuredOrder < 0)) {
      fail(`asset '${asset.id}': featuredOrder must be a non-negative integer`);
    }
    if (ids.has(asset.id)) fail(`data/assets.json: duplicate id '${asset.id}'`);
    ids.add(asset.id);
    if (localPaths.has(asset.localPath)) fail(`data/assets.json: duplicate localPath '${asset.localPath}'`);
    localPaths.add(asset.localPath);

    const local = path.resolve(websiteRoot, asset.localPath || "");
    const source = path.resolve(repositoryRoot, asset.sourcePath || "");
    if (!isInside(path.join(websiteRoot, "assets"), local)) {
      fail(`asset '${asset.id}': localPath must stay inside Website/assets`);
      continue;
    }
    if (!isInside(path.join(repositoryRoot, "Game.Data"), source)) {
      fail(`asset '${asset.id}': sourcePath must stay inside Game.Data`);
      continue;
    }
    try {
      const [localBytes, sourceBytes] = await Promise.all([readFile(local), readFile(source)]);
      const localHash = createHash("sha256").update(localBytes).digest("hex");
      const sourceHash = createHash("sha256").update(sourceBytes).digest("hex");
      if (localHash !== sourceHash) fail(`asset '${asset.id}': website copy differs from '${asset.sourcePath}'`);
      else checkedProvenanceCopies++;
    } catch (error) {
      fail(`asset '${asset.id}': missing local/source file (${error.message})`);
    }
  }
  return localPaths;
}

async function validateWave05SourceContract(data) {
  const manifestPath = path.join(repositoryRoot, "Game.Data", "assets", "wave05_living_world.sprites.json");
  let manifest;
  try {
    manifest = JSON.parse(await readFile(manifestPath, "utf8"));
  } catch (error) {
    fail(`Wave 05 source manifest: invalid or missing (${error.message})`);
    return;
  }
  if (!Array.isArray(manifest.sprites)) {
    fail("Wave 05 source manifest: sprites array is required");
    return;
  }
  checkedWave05Assets = manifest.sprites.length;
  checkedWave05Frames = manifest.sprites.reduce((total, sprite) => total + (Array.isArray(sprite.frames) ? sprite.frames.length : 0), 0);
  if (checkedWave05Assets !== 24) fail(`Wave 05 source manifest: expected 24 assets, found ${checkedWave05Assets}`);
  if (checkedWave05Frames !== 122) fail(`Wave 05 source manifest: expected 122 explicit frames, found ${checkedWave05Frames}`);

  const sourcePaths = new Set(manifest.sprites.map(sprite => `Game.Data/${String(sprite.path || "").replaceAll("\\", "/")}`.toLowerCase()));
  const websiteWave05 = (data?.assets || []).filter(asset => asset.localPath?.startsWith("assets/wave05/"));
  const requiredWebsiteIds = [
    "wave05-amber-grove", "wave05-twilight-marsh", "wave05-mangrove-root",
    "wave05-marsh-frog", "wave05-canopy-owl", "wave05-amber-beetle", "wave05-prism-wisp"
  ];
  if (websiteWave05.length < 8) fail(`data/assets.json: expected a representative Wave 05 selection, found ${websiteWave05.length}`);
  for (const id of requiredWebsiteIds) {
    if (!websiteWave05.some(asset => asset.id === id)) fail(`data/assets.json: missing required Wave 05 website asset '${id}'`);
  }
  for (const asset of websiteWave05) {
    if (!sourcePaths.has(String(asset.sourcePath).toLowerCase())) {
      fail(`asset '${asset.id}': sourcePath is not declared by wave05_living_world.sprites.json`);
    }
  }
  const featuredScenes = websiteWave05.filter(asset => asset.role === "scene" && Number.isInteger(asset.featuredOrder));
  if (featuredScenes.length < 2) fail("data/assets.json: both Wave 05 biome backgrounds must be featured scenes");
}

function collectHtmlIds(source, relativePath) {
  const ids = new Set();
  for (const match of source.matchAll(/\bid\s*=\s*["']([^"']+)["']/gi)) {
    if (ids.has(match[1])) fail(`${relativePath}: duplicate id '${match[1]}'`);
    ids.add(match[1]);
  }
  return ids;
}

function collectReferences(source, expression) {
  return [...source.matchAll(expression)].map(match => match[1].trim());
}

function splitReference(reference) {
  const hash = reference.indexOf("#");
  const rawFile = hash < 0 ? reference : reference.slice(0, hash);
  const query = rawFile.indexOf("?");
  return {
    file: query < 0 ? rawFile : rawFile.slice(0, query),
    fragment: hash < 0 ? "" : reference.slice(hash + 1)
  };
}

async function validateLocalReference(reference, sourceFile, htmlIds, docsIds) {
  if (!reference || /^(?:https?:|mailto:|tel:|data:)/i.test(reference)) return;
  const { file, fragment } = splitReference(reference);
  const targetRelative = file || path.basename(sourceFile);
  const target = path.resolve(websiteRoot, path.dirname(sourceFile), targetRelative);
  if (!isInside(websiteRoot, target)) {
    fail(`${sourceFile}: reference escapes Website root '${reference}'`);
    return;
  }
  try {
    const targetStat = await stat(target);
    if (!targetStat.isFile()) fail(`${sourceFile}: reference is not a file '${reference}'`);
  } catch {
    fail(`${sourceFile}: missing local reference '${reference}'`);
    return;
  }
  checkedReferences++;
  if (!fragment || path.extname(target).toLowerCase() !== ".html") return;
  const targetKey = path.relative(websiteRoot, target).replaceAll(path.sep, "/");
  if (targetKey === "docs.html" && docsIds.has(fragment)) return;
  if (!(htmlIds.get(targetKey) || new Set()).has(fragment)) fail(`${sourceFile}: missing fragment '${reference}'`);
}

function validateHtmlContract(source, file) {
  if (!/<html\s+lang=["']de["']/i.test(source)) fail(`${file}: html lang='de' is required`);
  if (!/<meta\s+charset=["']utf-8["']/i.test(source)) fail(`${file}: UTF-8 meta declaration is required`);
  if (!/class=["'][^"']*skip-link/i.test(source)) fail(`${file}: skip link is required`);
  if ((source.match(/<main\b/gi) || []).length !== 1) fail(`${file}: exactly one main element is required`);
  for (const match of source.matchAll(/<img\b([^>]*)>/gi)) {
    if (!/\balt=["'][^"']*["']/i.test(match[1])) fail(`${file}: every image needs alt text (empty is allowed for decorative images)`);
  }
  for (const reference of collectReferences(source, /<(?:script|link)\b[^>]*(?:src|href)\s*=\s*["']([^"']+)["']/gi)) {
    if (/^https?:/i.test(reference)) fail(`${file}: CDN/external script or stylesheet dependency is not allowed ('${reference}')`);
  }
}

async function validatePng(relativePath) {
  const buffer = await readFile(path.join(websiteRoot, relativePath));
  if (buffer.length < 24 || buffer.subarray(0, 8).toString("hex") !== "89504e470d0a1a0a") {
    fail(`${relativePath}: invalid PNG signature or header`);
    return;
  }
  const width = buffer.readUInt32BE(16);
  const height = buffer.readUInt32BE(20);
  if (width === 0 || height === 0) fail(`${relativePath}: invalid PNG dimensions ${width}x${height}`);
  checkedPngs++;
}

const status = await readJson("data/status.json");
const docs = await readJson("data/docs.json");
const releases = await readJson("data/releases.json");
const assets = await readJson("data/assets.json");
validateStatus(status);
const docsIds = validateDocs(docs);
validateReleases(releases);
const manifestAssetPaths = await validateAssets(assets);
await validateWave05SourceContract(assets);

const htmlFiles = (await readdir(websiteRoot)).filter(file => file.endsWith(".html"));
if (!new Set(htmlFiles).has("index.html") || !new Set(htmlFiles).has("docs.html")) fail("Website requires index.html and docs.html");
const htmlSources = new Map();
const htmlIds = new Map();
for (const file of htmlFiles) {
  const source = await readText(file);
  htmlSources.set(file, source);
  htmlIds.set(file, collectHtmlIds(source, file));
  validateHtmlContract(source, file);
}
for (const [file, source] of htmlSources) {
  for (const reference of collectReferences(source, /\b(?:href|src)\s*=\s*["']([^"']+)["']/gi)) {
    await validateLocalReference(reference, file, htmlIds, docsIds);
    const { file: referenceFile } = splitReference(reference);
    if (/^assets\/wave0[45]\//.test(referenceFile) && !manifestAssetPaths.has(referenceFile)) {
      fail(`${file}: production asset '${referenceFile}' is missing from data/assets.json provenance`);
    }
  }
}

const css = await readText("styles.css");
for (const requiredCss of ["@supports (backdrop-filter", "prefers-reduced-motion", ":focus-visible", ".capability-badge", ".sidebar-scrim"]) {
  if (!css.includes(requiredCss)) fail(`styles.css: missing required resilient UI contract '${requiredCss}'`);
}
for (const reference of collectReferences(css, /url\(\s*["']?([^"')]+)["']?\s*\)/gi)) {
  await validateLocalReference(reference, "styles.css", htmlIds, docsIds);
  if (/^assets\/wave0[45]\//.test(reference) && !manifestAssetPaths.has(reference)) {
    fail(`styles.css: production asset '${reference}' is missing from data/assets.json provenance`);
  }
}

const appSource = await readText("app.js");
try {
  new Function(appSource);
} catch (error) {
  fail(`app.js: syntax error (${error.message})`);
}
for (const requiredScriptContract of ["aria-pressed", "IntersectionObserver", "replaceState"]) {
  if (!appSource.includes(requiredScriptContract)) fail(`app.js: missing interaction contract '${requiredScriptContract}'`);
}
for (const dataPath of ["data/status.json", "data/docs.json", "data/releases.json", "data/assets.json"]) {
  if (!appSource.includes(dataPath)) fail(`app.js: does not load '${dataPath}'`);
  await validateLocalReference(dataPath, "app.js", htmlIds, docsIds);
}

async function walkAssets(directory) {
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const absolute = path.join(directory, entry.name);
    if (entry.isDirectory()) await walkAssets(absolute);
    else if (entry.name.toLowerCase().endsWith(".png")) await validatePng(path.relative(websiteRoot, absolute).replaceAll(path.sep, "/"));
  }
}
await walkAssets(path.join(websiteRoot, "assets"));

for (const file of ["README.md", "tools/update-site-data.ps1", "tools/validate-site.mjs"]) {
  await readText(file);
}

if (errors.length > 0) {
  for (const error of errors) console.error(`ERROR ${error}`);
  process.exitCode = 1;
} else {
  console.log(`Website validation passed: ${htmlFiles.length} HTML, ${docsIds.size} docs, ${checkedReferences} local references, ${checkedPngs} PNG assets, ${checkedProvenanceCopies} byte-exact Game.Data copies; Wave 05 source ${checkedWave05Assets} assets / ${checkedWave05Frames} explicit frames.`);
}

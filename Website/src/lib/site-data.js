const FALLBACK_STATUS = Object.freeze({
  version: "Development snapshot",
  updated: "lokal",
  summary: "YjsE befindet sich in aktiver Entwicklung.",
  metrics: [
    { value: ".NET 8", label: "Runtime" },
    { value: "60 Hz", label: "Fixed simulation" }
  ],
  features: []
});

const FALLBACK_RELEASES = Object.freeze({ downloads: [], notes: [] });
const FALLBACK_DOCS = Object.freeze({ categories: [], articles: [] });
const FALLBACK_ASSETS = Object.freeze({ assets: [] });

async function loadJson(path, fallback) {
  try {
    const response = await fetch(path, { cache: "no-store" });
    if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
    return await response.json();
  } catch (error) {
    console.info(`Fallback for ${path}: ${error instanceof Error ? error.message : error}`);
    return fallback;
  }
}

export async function loadHomeData() {
  const [status, releases, assets] = await Promise.all([
    loadJson("./data/status.json", FALLBACK_STATUS),
    loadJson("./data/releases.json", FALLBACK_RELEASES),
    loadJson("./data/assets.json", FALLBACK_ASSETS)
  ]);
  return { status, releases, assets };
}

export async function loadDocsData() {
  const [status, docs] = await Promise.all([
    loadJson("./data/status.json", FALLBACK_STATUS),
    loadJson("./data/docs.json", FALLBACK_DOCS)
  ]);
  return { status, docs };
}

export const statusLabels = Object.freeze({
  verified: "Verified",
  "implemented-unverified": "Implemented",
  partial: "Partial",
  planned: "Planned",
  blocked: "Blocked",
  deprecated: "Deprecated"
});

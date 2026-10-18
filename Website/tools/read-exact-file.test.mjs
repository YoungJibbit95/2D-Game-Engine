import assert from "node:assert/strict";
import { mkdtemp, mkdir, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import path from "node:path";
import { test } from "node:test";
import { readExactFile } from "./read-exact-file.mjs";

test("website sources require exact directory and file case on every OS", async () => {
  const root = await mkdtemp(path.join(tmpdir(), "yjse-website-path-"));
  try {
    await mkdir(path.join(root, "assets", "Wave05"), { recursive: true });
    await writeFile(path.join(root, "assets", "Wave05", "sprite.png"), "source");
    assert.equal((await readExactFile(root, path.join(root, "assets", "Wave05", "sprite.png"))).toString(), "source");
    for (const relative of ["Assets/Wave05/sprite.png", "assets/wave05/sprite.png", "assets/Wave05/Sprite.png"]) {
      await assert.rejects(readExactFile(root, path.join(root, relative)), /Missing case-exact path/);
    }
    await assert.rejects(readExactFile(root, path.resolve(root, "..", "outside.png")), /escapes file root/);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

import { readdir, readFile } from "node:fs/promises";
import path from "node:path";

// stat/readFile use the host's case rules; published URLs and Linux sources do not.
export async function readExactFile(root, absolute) {
  const relative = path.relative(root, absolute);
  const segments = relative.split(path.sep);
  if (path.isAbsolute(relative) || segments.includes("..") || !relative) {
    throw new Error(`Path escapes file root: ${absolute}`);
  }
  let directory = root;
  for (const segment of segments) {
    if (!(await readdir(directory)).includes(segment)) {
      throw new Error(`Missing case-exact path: ${relative}`);
    }
    directory = path.join(directory, segment);
  }
  return readFile(directory);
}

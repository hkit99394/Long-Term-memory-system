import { mkdir, readFile, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { stripTypeScriptTypes } from "node:module";

const here = dirname(fileURLToPath(import.meta.url));

await build("src/review-dashboard.ts", "../../src/MemorySystem.Api/wwwroot/reviews/review-dashboard.js");
await build("src/admin-console.ts", "../../src/MemorySystem.Api/wwwroot/admin/admin-console.js");

async function build(sourceRelativePath, outputRelativePath) {
  const sourcePath = resolve(here, sourceRelativePath);
  const outputPath = resolve(here, outputRelativePath);
  const source = await readFile(sourcePath, "utf8");
  const stripped = stripTypeScriptTypes(source, { mode: "strip" })
    .split(/\r?\n/)
    .map(line => line.trimEnd())
    .join("\n");
  const output = `// Generated from tools/ui/${sourceRelativePath}. Run npm run build in tools/ui.\n${stripped}`;

  await mkdir(dirname(outputPath), { recursive: true });
  await writeFile(outputPath, output);
}

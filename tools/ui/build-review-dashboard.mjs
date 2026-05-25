import { mkdir, readFile, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { stripTypeScriptTypes } from "node:module";

const here = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(here, "src/review-dashboard.ts");
const outputPath = resolve(here, "../../src/MemorySystem.Api/wwwroot/reviews/review-dashboard.js");

const source = await readFile(sourcePath, "utf8");
const stripped = stripTypeScriptTypes(source, { mode: "strip" });
const output = `// Generated from tools/ui/src/review-dashboard.ts. Run npm run build in tools/ui.\n${stripped}`;

await mkdir(dirname(outputPath), { recursive: true });
await writeFile(outputPath, output);

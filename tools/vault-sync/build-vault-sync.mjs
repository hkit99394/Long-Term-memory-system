import { mkdir, readFile, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { stripTypeScriptTypes } from "node:module";

const here = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(here, "src/vault-sync.ts");
const outputPath = resolve(here, "dist/vault-sync.js");

const source = await readFile(sourcePath, "utf8");
const stripped = stripTypeScriptTypes(source, { mode: "strip" });
const output = `// Generated from tools/vault-sync/src/vault-sync.ts. Run npm run build in tools/vault-sync.\n${stripped}`;

await mkdir(dirname(outputPath), { recursive: true });
await writeFile(outputPath, output);

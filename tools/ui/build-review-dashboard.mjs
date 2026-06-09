import { mkdir, readFile, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const here = dirname(fileURLToPath(import.meta.url));

await build(
  ["src/review-dashboard.ts"],
  "../../src/MemorySystem.Api/wwwroot/reviews/review-dashboard.js");
await build(
  ["src/admin/access-panel.ts", "src/admin/registration-panel.ts", "src/admin/management-panel.ts", "src/admin-console.ts"],
  "../../src/MemorySystem.Api/wwwroot/admin/admin-console.js");

async function build(sourceRelativePaths, outputRelativePath) {
  const sourcePaths = sourceRelativePaths.map(sourceRelativePath => resolve(here, sourceRelativePath));
  const outputPath = resolve(here, outputRelativePath);
  const checkOptions = {
    target: ts.ScriptTarget.ES2022,
    module: ts.ModuleKind.Preserve,
    lib: ["lib.es2022.d.ts", "lib.dom.d.ts"],
    strict: true,
    skipLibCheck: true,
    noEmit: true
  };

  await mkdir(dirname(outputPath), { recursive: true });

  const program = ts.createProgram(sourcePaths, checkOptions);
  const preEmitDiagnostics = ts.getPreEmitDiagnostics(program);
  if (preEmitDiagnostics.length > 0) {
    throw new Error(formatDiagnostics(preEmitDiagnostics));
  }

  const emittedParts = await Promise.all(sourcePaths.map(async sourcePath => {
    const source = await readFile(sourcePath, "utf8");
    const transpiled = ts.transpileModule(source, {
      compilerOptions: {
        target: ts.ScriptTarget.ES2022,
        module: ts.ModuleKind.Preserve,
        newLine: ts.NewLineKind.LineFeed,
        removeComments: false
      },
      fileName: sourcePath
    });

    if (transpiled.diagnostics && transpiled.diagnostics.length > 0) {
      throw new Error(formatDiagnostics(transpiled.diagnostics));
    }

    return transpiled.outputText.trimEnd();
  }));
  const sourcesLabel = sourceRelativePaths.map(source => `tools/ui/${source}`).join(", ");
  const output = `// Generated from ${sourcesLabel}. Run npm run build in tools/ui.\n${emittedParts.join("\n\n")}\n`;

  await writeFile(outputPath, output);
}

function formatDiagnostics(diagnostics) {
  return ts.formatDiagnosticsWithColorAndContext(diagnostics, {
    getCanonicalFileName: fileName => fileName,
    getCurrentDirectory: () => here,
    getNewLine: () => "\n"
  });
}

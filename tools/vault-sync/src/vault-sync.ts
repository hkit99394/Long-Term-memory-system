import { mkdir, writeFile } from "node:fs/promises";
import { dirname, isAbsolute, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";

interface ObsidianExportResponse {
  documents: ObsidianExportDocument[];
  staleDocuments: ObsidianStaleExportDocument[];
}

interface ObsidianArchiveExportResponse {
  documents: ObsidianArchiveExportDocument[];
}

interface ObsidianExportDocument {
  path: string;
  title: string;
  memoryFactId: string;
  sourceEventId: string;
  content: string;
}

interface ObsidianStaleExportDocument {
  path: string;
  memoryFactId: string;
  sourceEventId: string;
  status: string;
  reason: string;
  content: string;
}

interface ObsidianArchiveExportDocument {
  path: string;
  title: string;
  memoryFactId: string;
  sourceEventId: string;
  status: string;
  content: string;
}

interface CliOptions {
  apiBase: string;
  apiKey: string;
  vaultRoot: string;
  limit: string;
  scopeType: string | null;
  scopeId: string | null;
  dryRun: boolean;
  includeArchive: boolean;
}

const options = parseOptions(process.argv.slice(2));

if (options === null) {
  process.exitCode = 0;
} else {
  await syncVault(options);
}

async function syncVault(options: CliOptions): Promise<void> {
  const exportUrl = new URL("/api/vault/exports/obsidian", options.apiBase);
  exportUrl.searchParams.set("limit", options.limit);

  if (options.scopeType && options.scopeId) {
    exportUrl.searchParams.set("scopeType", options.scopeType);
    exportUrl.searchParams.set("scopeId", options.scopeId);
  }

  const payload = await fetchJson<ObsidianExportResponse>(exportUrl, options.apiKey, "Obsidian export");
  const documents = payload.documents ?? [];
  const staleDocuments = payload.staleDocuments ?? [];
  const archiveDocuments = options.includeArchive
    ? ((await fetchJson<ObsidianArchiveExportResponse>(buildArchiveUrl(options), options.apiKey, "Obsidian archive export")).documents ?? [])
    : [];
  const vaultRoot = resolve(options.vaultRoot);

  for (const document of documents) {
    const outputPath = resolveVaultPath(vaultRoot, document.path);

    if (options.dryRun) {
      console.log(`Would write ${relative(vaultRoot, outputPath)} (${document.memoryFactId}, ${document.sourceEventId})`);
      continue;
    }

    await mkdir(dirname(outputPath), { recursive: true });
    await writeFile(outputPath, document.content, "utf8");
    console.log(`Wrote ${relative(vaultRoot, outputPath)}`);
  }

  for (const document of staleDocuments) {
    const outputPath = resolveVaultPath(vaultRoot, document.path);

    if (options.dryRun) {
      console.log(`Would mark stale ${relative(vaultRoot, outputPath)} (${document.memoryFactId}, ${document.reason})`);
      continue;
    }

    await mkdir(dirname(outputPath), { recursive: true });
    await writeFile(outputPath, document.content, "utf8");
    console.log(`Marked stale ${relative(vaultRoot, outputPath)} (${document.status})`);
  }

  for (const document of archiveDocuments) {
    const outputPath = resolveVaultPath(vaultRoot, document.path);

    if (options.dryRun) {
      console.log(`Would archive ${relative(vaultRoot, outputPath)} (${document.memoryFactId}, ${document.status})`);
      continue;
    }

    await mkdir(dirname(outputPath), { recursive: true });
    await writeFile(outputPath, document.content, "utf8");
    console.log(`Archived ${relative(vaultRoot, outputPath)} (${document.status})`);
  }

  console.log(`${options.dryRun ? "Checked" : "Exported"} ${documents.length} Obsidian document(s), ${staleDocuments.length} stale marker(s), ${archiveDocuments.length} archive document(s).`);
}

async function fetchJson<T>(url: URL, apiKey: string, label: string): Promise<T> {
  const response = await fetch(url, {
    headers: {
      "X-Api-Key": apiKey
    }
  });

  if (!response.ok) {
    throw new Error(`${label} failed with ${response.status}: ${await response.text()}`);
  }

  return await response.json() as T;
}

function buildArchiveUrl(options: CliOptions): URL {
  const exportUrl = new URL("/api/vault/exports/obsidian/archive", options.apiBase);
  exportUrl.searchParams.set("limit", options.limit);

  if (options.scopeType && options.scopeId) {
    exportUrl.searchParams.set("scopeType", options.scopeType);
    exportUrl.searchParams.set("scopeId", options.scopeId);
  }

  return exportUrl;
}

function parseOptions(args: string[]): CliOptions | null {
  const parsed = new Map<string, string>();
  let dryRun = false;
  let includeArchive = false;

  for (let index = 0; index < args.length; index += 1) {
    const arg = args[index];

    if (arg === "--help" || arg === "-h") {
      printHelp();
      return null;
    }

    if (arg === "--dry-run") {
      dryRun = true;
      continue;
    }

    if (arg === "--include-archive") {
      includeArchive = true;
      continue;
    }

    if (!arg.startsWith("--")) {
      throw new Error(`Unexpected argument '${arg}'.`);
    }

    const key = arg.slice(2);
    const value = args[index + 1];

    if (!value || value.startsWith("--")) {
      throw new Error(`Option '${arg}' requires a value.`);
    }

    parsed.set(key, value);
    index += 1;
  }

  const here = dirname(fileURLToPath(import.meta.url));
  const defaultVaultRoot = resolve(here, "../../../vault/AI Memory System");
  const apiKey = parsed.get("api-key") ?? process.env.MEMORYSYSTEM_API_KEY;

  if (!apiKey) {
    throw new Error("Provide --api-key or set MEMORYSYSTEM_API_KEY.");
  }

  const scopeType = parsed.get("scope-type") ?? null;
  const scopeId = parsed.get("scope-id") ?? null;

  if ((scopeType === null) !== (scopeId === null)) {
    throw new Error("--scope-type and --scope-id must be provided together.");
  }

  return {
    apiBase: parsed.get("api-base") ?? process.env.MEMORYSYSTEM_API_BASE ?? "http://127.0.0.1:5099",
    apiKey,
    vaultRoot: parsed.get("vault-root") ?? process.env.MEMORYSYSTEM_VAULT_ROOT ?? defaultVaultRoot,
    limit: parsed.get("limit") ?? "50",
    scopeType,
    scopeId,
    dryRun,
    includeArchive
  };
}

function resolveVaultPath(vaultRoot: string, documentPath: string): string {
  const normalized = documentPath.replaceAll("\\", "/");
  const parts = normalized.split("/");

  if (normalized.length === 0 || normalized.startsWith("/") || parts.some(part => part.length === 0 || part === "." || part === "..")) {
    throw new Error(`Unsafe vault export path '${documentPath}'.`);
  }

  const outputPath = resolve(vaultRoot, ...parts);
  const relativePath = relative(vaultRoot, outputPath);

  if (relativePath.startsWith("..") || isAbsolute(relativePath)) {
    throw new Error(`Vault export path escapes the vault root: '${documentPath}'.`);
  }

  return outputPath;
}

function printHelp(): void {
  console.log(`Usage: node dist/vault-sync.js --api-key <key> [options]

Options:
  --api-base <url>       API base URL. Defaults to MEMORYSYSTEM_API_BASE or http://127.0.0.1:5099.
  --vault-root <path>    Obsidian vault root. Defaults to vault/AI Memory System.
  --scope-type <type>    Optional export scope filter.
  --scope-id <id>        Optional export scope id filter.
  --limit <count>        Maximum documents to export. Defaults to 50.
  --include-archive      Also write readable archive documents for old inactive memory.
  --dry-run              Validate and print output paths without writing files.
  --help                 Show this help.`);
}

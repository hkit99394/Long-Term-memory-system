#!/usr/bin/env node
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const rootDir = path.resolve(scriptDir, "..");

const requiredAreas = [
  "api",
  "worker",
  "postgresql",
  "retrieval",
  "review",
  "vault_export",
  "backup",
  "governance"
];

const files = {
  apiMetrics: "observability/alert-inputs/api-metrics.txt",
  externalMetrics: "observability/alert-inputs/external-pilot-metrics.txt",
  alerts: "observability/prometheus/memorysystem-pilot-alerts.yml",
  dashboard: "observability/grafana/memorysystem-pilot-dashboard.json",
  tracing: "observability/tracing/memorysystem-pilot-trace-coverage.json"
};

function readText(relativePath) {
  const absolutePath = path.join(rootDir, relativePath);

  if (!fs.existsSync(absolutePath)) {
    throw new Error(`Missing observability artifact: ${relativePath}`);
  }

  return fs.readFileSync(absolutePath, "utf8");
}

function readMetricList(relativePath) {
  return readText(relativePath)
    .split(/\r?\n/u)
    .map((line) => line.trim())
    .filter((line) => line.length > 0 && !line.startsWith("#"));
}

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

const apiMetrics = readMetricList(files.apiMetrics);
const externalMetrics = readMetricList(files.externalMetrics);
const alertRules = readText(files.alerts);
const dashboard = JSON.parse(readText(files.dashboard));
const tracing = JSON.parse(readText(files.tracing));
const dashboardText = JSON.stringify(dashboard);

assert(apiMetrics.length >= 20, "Expected at least 20 API metric inputs.");
assert(externalMetrics.length >= 5, "Expected at least 5 external metric inputs.");

for (const metricName of apiMetrics) {
  assert(
    alertRules.includes(metricName) || dashboardText.includes(metricName),
    `API metric '${metricName}' is not referenced by alert rules or dashboard.`
  );
}

for (const metricName of externalMetrics) {
  assert(
    alertRules.includes(metricName) || dashboardText.includes(metricName),
    `External metric '${metricName}' is not referenced by alert rules or dashboard.`
  );
}

const alertNames = [...alertRules.matchAll(/^\s*-\s*alert:\s*([A-Za-z0-9_]+)/gmu)]
  .map((match) => match[1]);
assert(alertNames.length >= 16, "Expected at least 16 pilot alert rules.");

for (const area of requiredAreas) {
  const alertAreaPattern = new RegExp(`area:\\s*${area}\\b`, "u");
  assert(alertAreaPattern.test(alertRules), `Alert rules are missing area '${area}'.`);
}

assert(dashboard.uid === "memorysystem-pilot", "Dashboard uid must be memorysystem-pilot.");
assert(Array.isArray(dashboard.panels), "Dashboard must contain a panels array.");
assert(dashboard.panels.length >= requiredAreas.length, "Dashboard must contain panels for required areas.");

const dashboardDescriptions = dashboard.panels
  .map((panel) => String(panel.description ?? ""))
  .join("\n");

for (const area of requiredAreas) {
  assert(
    dashboardDescriptions.includes(`area:${area}`),
    `Dashboard is missing a panel description for area '${area}'.`
  );
}

for (const metricName of [...apiMetrics, ...externalMetrics]) {
  assert(
    dashboardText.includes(metricName),
    `Dashboard is missing metric '${metricName}'.`
  );
}

assert(Array.isArray(tracing.requiredAreas), "Trace coverage must define requiredAreas.");
assert(Array.isArray(tracing.spans), "Trace coverage must define spans.");
assert(Array.isArray(tracing.forbiddenAttributes), "Trace coverage must define forbiddenAttributes.");
assert(tracing.forbiddenAttributes.includes("memory.raw_query"), "Trace coverage must forbid raw query attributes.");
assert(tracing.forbiddenAttributes.includes("event.raw_payload"), "Trace coverage must forbid raw event payload attributes.");

for (const area of requiredAreas) {
  assert(
    tracing.requiredAreas.includes(area),
    `Trace coverage requiredAreas is missing '${area}'.`
  );
  assert(
    tracing.spans.some((span) => span.area === area),
    `Trace coverage spans are missing area '${area}'.`
  );
}

for (const span of tracing.spans) {
  assert(typeof span.spanName === "string" && span.spanName.length > 0, "Every trace span must name spanName.");
  assert(Array.isArray(span.requiredAttributes) && span.requiredAttributes.length > 0, `Trace span '${span.spanName}' must list requiredAttributes.`);
}

console.log("Observability artifacts smoke passed.");
console.log(`  API metric inputs: ${apiMetrics.length}`);
console.log(`  External metric inputs: ${externalMetrics.length}`);
console.log(`  Alert rules: ${alertNames.length}`);
console.log(`  Dashboard panels: ${dashboard.panels.length}`);
console.log(`  Trace spans: ${tracing.spans.length}`);

// Generated from tools/ui/src/admin-console.ts. Run npm run build in tools/ui.































































const state                    = {
  facts: [],
  selectedFactId: null,
  selectedSource: null,
  busy: false
};

const elements = {
  apiBase: byId                  ("api-base"),
  apiKey: byId                  ("api-key"),
  statusFilter: byId                   ("status-filter"),
  query: byId                  ("query"),
  refresh: byId                   ("refresh"),
  status: byId             ("status"),
  memoryList: byId             ("memory-list"),
  memoryDetail: byId             ("memory-detail"),
  sourceDetail: byId             ("source-detail")
};

elements.refresh.addEventListener("click", () => void loadFacts());
elements.query.addEventListener("keydown", event => {
  if (event.key === "Enter") {
    event.preventDefault();
    void loadFacts();
  }
});
elements.statusFilter.addEventListener("change", () => void loadFacts());

render();

function byId                       (id        )    {
  const element = document.getElementById(id);

  if (!element) {
    throw new Error(`Missing element '${id}'.`);
  }

  return element     ;
}

async function loadFacts()                {
  setBusy(true);
  setStatus("Loading");
  state.selectedSource = null;

  try {
    const params = new URLSearchParams();
    params.set("limit", "50");

    if (elements.statusFilter.value !== "all") {
      params.set("status", elements.statusFilter.value);
    }

    const query = elements.query.value.trim();
    if (query) {
      params.set("q", query);
    }

    const response = await apiFetch                          (`/api/admin/memory/facts?${params}`);
    state.facts = response.facts;
    state.selectedFactId = response.facts[0]?.id ?? null;
    setStatus(`${response.facts.length} facts`);
  } catch (error) {
    setStatus("Error");
    state.facts = [];
    state.selectedFactId = null;
    state.selectedSource = null;
    elements.sourceDetail.replaceChildren(emptyPanel(errorMessage(error)));
  } finally {
    setBusy(false);
    render();
  }
}

async function openSource(fact                 )                {
  setBusy(true);
  setStatus("Opening source");

  try {
    state.selectedSource = await apiFetch                     (fact.sourceLink);
    setStatus("Source opened");
  } catch (error) {
    state.selectedSource = null;
    elements.sourceDetail.replaceChildren(emptyPanel(errorMessage(error)));
  } finally {
    setBusy(false);
    renderSourceDetail();
  }
}

async function apiFetch   (path        , init              = {})             {
  const apiBase = elements.apiBase.value.trim().replace(/\/$/, "");
  const headers = new Headers(init.headers);
  const apiKey = elements.apiKey.value.trim();

  if (apiKey) {
    headers.set("X-Api-Key", apiKey);
  }

  const response = await fetch(`${apiBase}${path}`, {
    ...init,
    headers
  });
  const text = await response.text();
  const payload = text ? JSON.parse(text) : null;

  if (!response.ok) {
    const detail = payload?.detail ?? payload?.title ?? response.statusText;
    throw new Error(`${response.status} ${detail}`);
  }

  return payload     ;
}

function render()       {
  renderMemoryList();
  renderMemoryDetail();
  renderSourceDetail();
}

function renderMemoryList()       {
  elements.memoryList.replaceChildren();

  if (state.facts.length === 0) {
    elements.memoryList.append(emptyPanel("No memory facts"));
    return;
  }

  for (const fact of state.facts) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = fact.id === state.selectedFactId ? "memory-row selected" : "memory-row";
    button.addEventListener("click", () => {
      state.selectedFactId = fact.id;
      state.selectedSource = null;
      render();
    });

    button.append(
      line(displaySubject(fact), "memory-title"),
      pillRow([fact.status, fact.memoryType]),
      line(`${fact.scopeType}:${fact.scopeId}`, "memory-meta"),
      line(shortDate(fact.updatedAt), "memory-date"));
    elements.memoryList.append(button);
  }
}

function renderMemoryDetail()       {
  const fact = selectedFact();
  elements.memoryDetail.replaceChildren();

  if (!fact) {
    elements.memoryDetail.append(emptyPanel("Select a memory fact"));
    return;
  }

  const object = fact.policy.contentVisible
    ? fact.object ?? ""
    : contentHiddenText(fact.policy.contentVisibilityReason);

  const rows                     = [
    ["Memory", fact.id],
    ["Status", fact.status],
    ["Scope", `${fact.scopeType}:${fact.scopeId}`],
    ["Namespace", fact.namespace],
    ["Type", fact.memoryType],
    ["Visibility", fact.visibility],
    ["Predicate", fact.predicate ?? contentHiddenText(fact.policy.contentVisibilityReason)],
    ["Confidence", fact.confidence.toFixed(3)],
    ["Trust", fact.trustLevel],
    ["Source", fact.sourceLink],
    ["Source retention", fact.policy.sourceRetentionClass],
    ["Source sensitivity", fact.policy.sourceSensitivity],
    ["Source trust", fact.policy.sourceTrustLevel],
    ["Source redaction", fact.policy.sourceRedactionStatus],
    ["Proposed by", fact.proposedByPrincipalId ?? ""],
    ["Updated", shortDate(fact.updatedAt)]
  ];

  const openButton = document.createElement("button");
  openButton.type = "button";
  openButton.className = "primary-action";
  openButton.textContent = "Open source";
  openButton.disabled = state.busy;
  openButton.addEventListener("click", () => void openSource(fact));

  const objectClass = fact.policy.contentVisible ? "memory-object" : "memory-object hidden-content";
  elements.memoryDetail.append(
    heading(displaySubject(fact)),
    paragraph(object, objectClass),
    detailGrid(rows),
    openButton);
}

function renderSourceDetail()       {
  elements.sourceDetail.replaceChildren();

  if (!state.selectedSource) {
    elements.sourceDetail.append(emptyPanel("No source opened"));
    return;
  }

  const source = state.selectedSource;
  const rows                     = [
    ["Event", source.id],
    ["Type", source.eventType],
    ["Scope", `${source.scope.scopeType}:${source.scope.scopeId}`],
    ["Retention", source.retentionClass],
    ["Sensitivity", source.sensitivity],
    ["Trust", source.trustLevel],
    ["Hash", source.contentHash ?? ""],
    ["Created", shortDate(source.createdAt)]
  ];

  const json = document.createElement("pre");
  json.className = "source-json";
  json.textContent = JSON.stringify(source.content, null, 2);

  elements.sourceDetail.append(
    heading("Source event"),
    detailGrid(rows),
    json);
}

function selectedFact()                         {
  return state.facts.find(fact => fact.id === state.selectedFactId) ?? null;
}

function setBusy(busy         )       {
  state.busy = busy;
  elements.refresh.disabled = busy;
}

function setStatus(value        )       {
  elements.status.textContent = value;
}

function displaySubject(fact                 )         {
  return fact.policy.contentVisible
    ? fact.subject ?? "(untitled memory)"
    : contentHiddenText(fact.policy.contentVisibilityReason);
}

function contentHiddenText(reason               )         {
  return reason === "memory_content_hidden_by_source_policy"
    ? "[content hidden by source policy]"
    : "[content hidden by lifecycle]";
}

function errorMessage(error         )         {
  return error instanceof Error ? error.message : String(error);
}

function shortDate(value        )         {
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
    timeStyle: "short"
  }).format(new Date(value));
}

function line(value        , className        )              {
  const element = document.createElement("span");
  element.className = className;
  element.textContent = value;
  return element;
}

function heading(value        )              {
  const element = document.createElement("h2");
  element.textContent = value;
  return element;
}

function paragraph(value        , className        )              {
  const element = document.createElement("p");
  element.className = className;
  element.textContent = value;
  return element;
}

function pillRow(values          )              {
  const row = document.createElement("span");
  row.className = "memory-status";

  for (const value of values) {
    const pill = document.createElement("span");
    pill.className = `pill ${value}`;
    pill.textContent = value;
    row.append(pill);
  }

  return row;
}

function detailGrid(rows                    )              {
  const grid = document.createElement("dl");
  grid.className = "detail-grid";

  for (const [label, value] of rows) {
    const term = document.createElement("dt");
    const description = document.createElement("dd");
    term.textContent = label;
    description.textContent = value;
    grid.append(term, description);
  }

  return grid;
}

function emptyPanel(value        )              {
  const panel = document.createElement("div");
  panel.className = "empty";
  panel.textContent = value;
  return panel;
}

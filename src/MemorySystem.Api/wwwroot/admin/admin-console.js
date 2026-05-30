// Generated from tools/ui/src/admin-console.ts. Run npm run build in tools/ui.




















































































































const state                    = {
  mode: "memory",
  facts: [],
  events: [],
  selectedFactId: null,
  selectedEventId: null,
  selectedSource: null,
  busy: false
};

const elements = {
  apiBase: byId                  ("api-base"),
  apiKey: byId                  ("api-key"),
  modeFilter: byId                   ("mode-filter"),
  statusFilter: byId                   ("status-filter"),
  eventTypeFilter: byId                   ("event-type-filter"),
  retentionFilter: byId                   ("retention-filter"),
  sensitivityFilter: byId                   ("sensitivity-filter"),
  trustFilter: byId                   ("trust-filter"),
  redactionFilter: byId                   ("redaction-filter"),
  scopeTypeFilter: byId                   ("scope-type-filter"),
  scopeIdFilter: byId                  ("scope-id-filter"),
  createdFromFilter: byId                  ("created-from-filter"),
  createdToFilter: byId                  ("created-to-filter"),
  query: byId                  ("query"),
  refresh: byId                   ("refresh"),
  status: byId             ("status"),
  listTitle: byId             ("list-title"),
  resultList: byId             ("result-list"),
  detail: byId             ("detail"),
  sourceTitle: byId             ("source-title"),
  sourceDetail: byId             ("source-detail")
};

elements.apiBase.value = window.location.origin;
elements.refresh.addEventListener("click", () => void loadCurrentMode());
elements.query.addEventListener("keydown", event => {
  if (event.key === "Enter") {
    event.preventDefault();
    void loadCurrentMode();
  }
});
elements.statusFilter.addEventListener("change", () => void loadFacts());
elements.modeFilter.addEventListener("change", () => {
  state.mode = elements.modeFilter.value === "events" ? "events" : "memory";
  state.selectedSource = null;
  updateFilterVisibility();
  render();
  void loadCurrentMode();
});

for (const element of [
  elements.eventTypeFilter,
  elements.retentionFilter,
  elements.sensitivityFilter,
  elements.trustFilter,
  elements.redactionFilter,
  elements.scopeTypeFilter,
  elements.createdFromFilter,
  elements.createdToFilter
]) {
  element.addEventListener("change", () => void loadCurrentMode());
}

elements.scopeIdFilter.addEventListener("keydown", event => {
  if (event.key === "Enter") {
    event.preventDefault();
    void loadCurrentMode();
  }
});

updateFilterVisibility();
render();

function byId                       (id        )    {
  const element = document.getElementById(id);

  if (!element) {
    throw new Error(`Missing element '${id}'.`);
  }

  return element     ;
}

async function loadCurrentMode()                {
  if (state.mode === "events") {
    await loadEvents();
    return;
  }

  await loadFacts();
}

async function loadFacts()                {
  setBusy(true);
  setStatus("Loading");
  state.selectedSource = null;

  try {
    const params = commonParams();
    params.set("limit", "50");

    if (elements.statusFilter.value !== "all") {
      params.set("status", elements.statusFilter.value);
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

async function loadEvents()                {
  setBusy(true);
  setStatus("Loading");
  state.selectedSource = null;

  try {
    const params = commonParams();
    params.set("limit", "50");
    addSelectParam(params, "eventType", elements.eventTypeFilter);
    addSelectParam(params, "retentionClass", elements.retentionFilter);
    addSelectParam(params, "sensitivity", elements.sensitivityFilter);
    addSelectParam(params, "trustLevel", elements.trustFilter);
    addSelectParam(params, "redactionStatus", elements.redactionFilter);
    addDateParam(params, "createdFrom", elements.createdFromFilter.value);
    addDateParam(params, "createdTo", elements.createdToFilter.value);

    const response = await apiFetch                           (`/api/admin/source-events?${params}`);
    state.events = response.events;
    state.selectedEventId = response.events[0]?.id ?? null;
    setStatus(`${response.events.length} sources`);
  } catch (error) {
    setStatus("Error");
    state.events = [];
    state.selectedEventId = null;
    state.selectedSource = null;
    elements.sourceDetail.replaceChildren(emptyPanel(errorMessage(error)));
  } finally {
    setBusy(false);
    render();
  }
}

function commonParams()                  {
  const params = new URLSearchParams();

  const scopeType = elements.scopeTypeFilter.value.trim();
  const scopeId = elements.scopeIdFilter.value.trim();
  if (scopeType && scopeId) {
    params.set("scopeType", scopeType);
    params.set("scopeId", scopeId);
  }

  const query = elements.query.value.trim();
  if (query) {
    params.set("q", query);
  }

  return params;
}

function addSelectParam(params                 , key        , element                   )       {
  if (element.value !== "all") {
    params.set(key, element.value);
  }
}

function addDateParam(params                 , key        , value        )       {
  if (!value) {
    return;
  }

  params.set(key, new Date(value).toISOString());
}

async function openSource(path        )                {
  setBusy(true);
  setStatus("Opening source");

  try {
    state.selectedSource = await apiFetch                     (path);
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
  elements.listTitle.textContent = state.mode === "events" ? "Source Events" : "Memory Facts";
  elements.sourceTitle.textContent = state.mode === "events" ? "Payload" : "Source Evidence";
  renderResultList();
  renderDetail();
  renderSourceDetail();
}

function renderResultList()       {
  elements.resultList.replaceChildren();

  if (state.mode === "events") {
    renderEventList();
    return;
  }

  renderMemoryList();
}

function renderMemoryList()       {
  if (state.facts.length === 0) {
    elements.resultList.append(emptyPanel("No memory facts"));
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
    elements.resultList.append(button);
  }
}

function renderEventList()       {
  if (state.events.length === 0) {
    elements.resultList.append(emptyPanel("No source events"));
    return;
  }

  for (const sourceEvent of state.events) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = sourceEvent.id === state.selectedEventId ? "memory-row selected" : "memory-row";
    button.addEventListener("click", () => {
      state.selectedEventId = sourceEvent.id;
      state.selectedSource = null;
      render();
    });

    button.append(
      line(sourceEvent.eventType, "memory-title"),
      pillRow([sourceEvent.retentionClass, sourceEvent.sensitivity, sourceEvent.redactionStatus]),
      line(`${sourceEvent.scope.scopeType}:${sourceEvent.scope.scopeId}`, "memory-meta"),
      line(`${sourceEvent.references.length} refs · ${shortDate(sourceEvent.createdAt)}`, "memory-date"));
    elements.resultList.append(button);
  }
}

function renderDetail()       {
  elements.detail.replaceChildren();

  if (state.mode === "events") {
    renderEventDetail();
    return;
  }

  renderMemoryDetail();
}

function renderMemoryDetail()       {
  const fact = selectedFact();

  if (!fact) {
    elements.detail.append(emptyPanel("Select a memory fact"));
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
  openButton.addEventListener("click", () => void openSource(fact.sourceLink));

  const objectClass = fact.policy.contentVisible ? "memory-object" : "memory-object hidden-content";
  elements.detail.append(
    heading(displaySubject(fact)),
    paragraph(object, objectClass),
    detailGrid(rows),
    openButton);
}

function renderEventDetail()       {
  const sourceEvent = selectedEvent();

  if (!sourceEvent) {
    elements.detail.append(emptyPanel("Select a source event"));
    return;
  }

  const rows                     = [
    ["Event", sourceEvent.id],
    ["Type", sourceEvent.eventType],
    ["Scope", `${sourceEvent.scope.scopeType}:${sourceEvent.scope.scopeId}`],
    ["Retention", sourceEvent.retentionClass],
    ["Sensitivity", sourceEvent.sensitivity],
    ["Trust", sourceEvent.trustLevel],
    ["Redaction", sourceEvent.redactionStatus],
    ["Redacted", sourceEvent.redactedAt ? shortDate(sourceEvent.redactedAt) : ""],
    ["Redaction event", sourceEvent.redactionEventLink ?? ""],
    ["Hash", sourceEvent.contentHash ?? ""],
    ["External payload", sourceEvent.externalPayloadUri ?? ""],
    ["Principal", sourceEvent.principalId ?? ""],
    ["Agent", sourceEvent.agentPrincipalId ?? ""],
    ["Role", sourceEvent.roleId ?? sourceEvent.scope.roleId ?? ""],
    ["Created", shortDate(sourceEvent.createdAt)]
  ];

  elements.detail.append(
    heading(sourceEvent.eventType),
    paragraph(sourceEvent.policy.contentVisibilityReason, "memory-object hidden-content"),
    detailGrid(rows),
    referenceList(sourceEvent.references));

  if (canOpenSourceEvent(sourceEvent)) {
    const openButton = document.createElement("button");
    openButton.type = "button";
    openButton.className = "primary-action";
    openButton.textContent = "Open payload";
    openButton.disabled = state.busy;
    openButton.addEventListener("click", () => void openSource(sourceEvent.sourceLink));
    elements.detail.append(openButton);
  }
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

function referenceList(references                             )              {
  const section = document.createElement("section");
  section.className = "audit-section";
  section.append(heading("References"));

  if (references.length === 0) {
    section.append(emptyPanel("No linked records"));
    return section;
  }

  const list = document.createElement("div");
  list.className = "audit-list";

  for (const reference of references) {
    const row = document.createElement("div");
    row.className = "audit-row";
    row.append(
      line(reference.referenceType, "memory-title"),
      pillRow([reference.status]),
      line(reference.id, "memory-meta"),
      line(reference.label ?? reference.targetType ?? "", "memory-date"));
    list.append(row);
  }

  section.append(list);
  return section;
}

function selectedFact()                         {
  return state.facts.find(fact => fact.id === state.selectedFactId) ?? null;
}

function selectedEvent()                          {
  return state.events.find(sourceEvent => sourceEvent.id === state.selectedEventId) ?? null;
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

function canOpenSourceEvent(sourceEvent                  )          {
  return sourceEvent.retentionClass !== "erasure_requested"
    && sourceEvent.redactionStatus === "none";
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
    if (!value) {
      continue;
    }

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

function updateFilterVisibility()       {
  for (const element of document.querySelectorAll             ("[data-event-filter]")) {
    element.hidden = state.mode !== "events";
  }

  elements.statusFilter.closest("label") .hidden = state.mode !== "memory";
}

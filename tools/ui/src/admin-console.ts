interface AdminMemoryFactsResponse {
  facts: AdminMemoryFact[];
}

interface AdminMemoryFact {
  id: string;
  scopeType: string;
  scopeId: string;
  namespace: string;
  userPrincipalId: string | null;
  projectId: string | null;
  orgId: string | null;
  roleId: string | null;
  agentPrincipalId: string | null;
  memoryType: string;
  visibility: string;
  subject: string | null;
  predicate: string | null;
  object: string | null;
  confidence: number;
  trustLevel: string;
  status: string;
  sourceEventId: string;
  sourceLink: string;
  proposedByPrincipalId: string | null;
  createdAt: string;
  updatedAt: string;
  policy: AdminMemoryFactPolicy;
}

interface AdminMemoryFactPolicy {
  contentVisible: boolean;
  contentVisibilityReason: string | null;
  sourceRetentionClass: string;
  sourceSensitivity: string;
  sourceTrustLevel: string;
  sourceRedactionStatus: string;
  sourcePayloadIncluded: boolean;
}

interface AdminSourceEventsResponse {
  events: AdminSourceEvent[];
}

interface AdminSourceEvent {
  id: string;
  principalId: string | null;
  conversationId: string | null;
  agentPrincipalId: string | null;
  roleId: string | null;
  eventType: string;
  sourceLink: string;
  contentHash: string | null;
  externalPayloadUri: string | null;
  retentionClass: string;
  sensitivity: string;
  redactionStatus: string;
  redactedAt: string | null;
  redactionEventId: string | null;
  redactionEventLink: string | null;
  trustLevel: string;
  createdAt: string;
  scope: AdminSourceEventScope;
  policy: AdminSourceEventPolicy;
  references: AdminSourceEventReference[];
}

interface AdminSourceEventScope {
  scopeType: string;
  scopeId: string;
  orgId: string | null;
  projectId: string | null;
  principalId: string | null;
  roleId: string | null;
}

interface AdminSourceEventPolicy {
  sourcePayloadIncluded: boolean;
  contentVisibilityReason: string;
}

interface AdminSourceEventReference {
  referenceType: string;
  id: string;
  status: string;
  targetType: string | null;
  targetId: string | null;
  label: string | null;
}

interface SourceEventResponse {
  id: string;
  eventType: string;
  content: unknown;
  contentHash: string | null;
  externalPayloadUri: string | null;
  retentionClass: string;
  sensitivity: string;
  trustLevel: string;
  createdAt: string;
  scope: {
    scopeType: string;
    scopeId: string;
  };
}

interface AdminConsoleState {
  mode: "memory" | "events";
  facts: AdminMemoryFact[];
  events: AdminSourceEvent[];
  selectedFactId: string | null;
  selectedEventId: string | null;
  selectedSource: SourceEventResponse | null;
  busy: boolean;
}

const state: AdminConsoleState = {
  mode: "memory",
  facts: [],
  events: [],
  selectedFactId: null,
  selectedEventId: null,
  selectedSource: null,
  busy: false
};

const elements = {
  apiBase: byId<HTMLInputElement>("api-base"),
  apiKey: byId<HTMLInputElement>("api-key"),
  modeFilter: byId<HTMLSelectElement>("mode-filter"),
  statusFilter: byId<HTMLSelectElement>("status-filter"),
  eventTypeFilter: byId<HTMLSelectElement>("event-type-filter"),
  retentionFilter: byId<HTMLSelectElement>("retention-filter"),
  sensitivityFilter: byId<HTMLSelectElement>("sensitivity-filter"),
  trustFilter: byId<HTMLSelectElement>("trust-filter"),
  redactionFilter: byId<HTMLSelectElement>("redaction-filter"),
  scopeTypeFilter: byId<HTMLSelectElement>("scope-type-filter"),
  scopeIdFilter: byId<HTMLInputElement>("scope-id-filter"),
  createdFromFilter: byId<HTMLInputElement>("created-from-filter"),
  createdToFilter: byId<HTMLInputElement>("created-to-filter"),
  query: byId<HTMLInputElement>("query"),
  refresh: byId<HTMLButtonElement>("refresh"),
  status: byId<HTMLElement>("status"),
  listTitle: byId<HTMLElement>("list-title"),
  resultList: byId<HTMLElement>("result-list"),
  detail: byId<HTMLElement>("detail"),
  sourceTitle: byId<HTMLElement>("source-title"),
  sourceDetail: byId<HTMLElement>("source-detail")
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

function byId<T extends HTMLElement>(id: string): T {
  const element = document.getElementById(id);

  if (!element) {
    throw new Error(`Missing element '${id}'.`);
  }

  return element as T;
}

async function loadCurrentMode(): Promise<void> {
  if (state.mode === "events") {
    await loadEvents();
    return;
  }

  await loadFacts();
}

async function loadFacts(): Promise<void> {
  setBusy(true);
  setStatus("Loading");
  state.selectedSource = null;

  try {
    const params = commonParams();
    params.set("limit", "50");

    if (elements.statusFilter.value !== "all") {
      params.set("status", elements.statusFilter.value);
    }

    const response = await apiFetch<AdminMemoryFactsResponse>(`/api/admin/memory/facts?${params}`);
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

async function loadEvents(): Promise<void> {
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

    const response = await apiFetch<AdminSourceEventsResponse>(`/api/admin/source-events?${params}`);
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

function commonParams(): URLSearchParams {
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

function addSelectParam(params: URLSearchParams, key: string, element: HTMLSelectElement): void {
  if (element.value !== "all") {
    params.set(key, element.value);
  }
}

function addDateParam(params: URLSearchParams, key: string, value: string): void {
  if (!value) {
    return;
  }

  params.set(key, new Date(value).toISOString());
}

async function openSource(path: string): Promise<void> {
  setBusy(true);
  setStatus("Opening source");

  try {
    state.selectedSource = await apiFetch<SourceEventResponse>(path);
    setStatus("Source opened");
  } catch (error) {
    state.selectedSource = null;
    elements.sourceDetail.replaceChildren(emptyPanel(errorMessage(error)));
  } finally {
    setBusy(false);
    renderSourceDetail();
  }
}

async function apiFetch<T>(path: string, init: RequestInit = {}): Promise<T> {
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

  return payload as T;
}

function render(): void {
  elements.listTitle.textContent = state.mode === "events" ? "Source Events" : "Memory Facts";
  elements.sourceTitle.textContent = state.mode === "events" ? "Payload" : "Source Evidence";
  renderResultList();
  renderDetail();
  renderSourceDetail();
}

function renderResultList(): void {
  elements.resultList.replaceChildren();

  if (state.mode === "events") {
    renderEventList();
    return;
  }

  renderMemoryList();
}

function renderMemoryList(): void {
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

function renderEventList(): void {
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

function renderDetail(): void {
  elements.detail.replaceChildren();

  if (state.mode === "events") {
    renderEventDetail();
    return;
  }

  renderMemoryDetail();
}

function renderMemoryDetail(): void {
  const fact = selectedFact();

  if (!fact) {
    elements.detail.append(emptyPanel("Select a memory fact"));
    return;
  }

  const object = fact.policy.contentVisible
    ? fact.object ?? ""
    : contentHiddenText(fact.policy.contentVisibilityReason);

  const rows: [string, string][] = [
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

function renderEventDetail(): void {
  const sourceEvent = selectedEvent();

  if (!sourceEvent) {
    elements.detail.append(emptyPanel("Select a source event"));
    return;
  }

  const rows: [string, string][] = [
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

function renderSourceDetail(): void {
  elements.sourceDetail.replaceChildren();

  if (!state.selectedSource) {
    elements.sourceDetail.append(emptyPanel("No source opened"));
    return;
  }

  const source = state.selectedSource;
  const rows: [string, string][] = [
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

function referenceList(references: AdminSourceEventReference[]): HTMLElement {
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

function selectedFact(): AdminMemoryFact | null {
  return state.facts.find(fact => fact.id === state.selectedFactId) ?? null;
}

function selectedEvent(): AdminSourceEvent | null {
  return state.events.find(sourceEvent => sourceEvent.id === state.selectedEventId) ?? null;
}

function setBusy(busy: boolean): void {
  state.busy = busy;
  elements.refresh.disabled = busy;
}

function setStatus(value: string): void {
  elements.status.textContent = value;
}

function displaySubject(fact: AdminMemoryFact): string {
  return fact.policy.contentVisible
    ? fact.subject ?? "(untitled memory)"
    : contentHiddenText(fact.policy.contentVisibilityReason);
}

function contentHiddenText(reason: string | null): string {
  return reason === "memory_content_hidden_by_source_policy"
    ? "[content hidden by source policy]"
    : "[content hidden by lifecycle]";
}

function canOpenSourceEvent(sourceEvent: AdminSourceEvent): boolean {
  return sourceEvent.retentionClass !== "erasure_requested"
    && sourceEvent.redactionStatus === "none";
}

function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}

function shortDate(value: string): string {
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
    timeStyle: "short"
  }).format(new Date(value));
}

function line(value: string, className: string): HTMLElement {
  const element = document.createElement("span");
  element.className = className;
  element.textContent = value;
  return element;
}

function heading(value: string): HTMLElement {
  const element = document.createElement("h2");
  element.textContent = value;
  return element;
}

function paragraph(value: string, className: string): HTMLElement {
  const element = document.createElement("p");
  element.className = className;
  element.textContent = value;
  return element;
}

function pillRow(values: string[]): HTMLElement {
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

function detailGrid(rows: [string, string][]): HTMLElement {
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

function emptyPanel(value: string): HTMLElement {
  const panel = document.createElement("div");
  panel.className = "empty";
  panel.textContent = value;
  return panel;
}

function updateFilterVisibility(): void {
  for (const element of document.querySelectorAll<HTMLElement>("[data-event-filter]")) {
    element.hidden = state.mode !== "events";
  }

  elements.statusFilter.closest("label")!.hidden = state.mode !== "memory";
}

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

interface AdminComplianceStatusResponse {
  generatedAt: string;
  payloadSafe: boolean;
  rawSourcePayloadsIncluded: boolean;
  items: AdminComplianceStatusItem[];
}

interface AdminComplianceStatusItem {
  id: string;
  title: string;
  status: string;
  evidenceKind: string;
  summary: string;
  count: number | null;
  payloadSafe: boolean;
  rawSourcePayloadsIncluded: boolean;
  links: AdminComplianceStatusLink[];
  metrics: AdminComplianceStatusMetric[];
}

interface AdminComplianceStatusLink {
  label: string;
  href: string;
  kind: string;
  method: string;
}

interface AdminComplianceStatusMetric {
  name: string;
  description: string;
}

interface AdminPilotReadinessStatusResponse {
  schemaVersion: number;
  statusId: string;
  generatedAt: string;
  scope: string;
  environment: Record<string, string>;
  decision: AdminPilotReadinessDecision;
  payloadSafe: boolean;
  rawSourcePayloadsIncluded: boolean;
  canonicalDocuments: Record<string, string>;
  gates: AdminPilotReadinessGate[];
  requiredToFlipToGo: string[];
  nextRecommendedWork: AdminPilotReadinessNextWork[];
}

interface AdminPilotReadinessDecision {
  status: string;
  externalInviteApproved: boolean;
  record: string;
  reason: string;
}

interface AdminPilotReadinessGate {
  id: string;
  priority: string;
  status: string;
  title: string;
  summary: string;
  evidence: string[];
  blocksExternalInvite: boolean;
  missingInputs: string[];
}

interface AdminPilotReadinessNextWork {
  id: string;
  title: string;
  uses: string;
}

interface AdminOperationsSummary {
  generatedAt: string;
  status: string;
  api: {
    status: string;
  };
  worker: {
    workerType: string;
    observed: boolean;
    status: string;
    workerId: string | null;
    lastSeenAt: string | null;
    lastSeenAgeSeconds: number | null;
    lastSuccessAt: string | null;
    lastError: string | null;
    stale: boolean;
  };
  outbox: {
    readyPending: number;
    delayedPending: number;
    processing: number;
    deadLetter: number;
    failed: number;
    retryingFailed: number;
    expiredProcessing: number;
    oldestReadyPendingSeconds: number;
  };
  reviews: {
    pending: number;
  };
  vaultExports: {
    stale: number;
  };
  retrievalFeedback: {
    windowHours: number;
    total: number;
    byType: AdminOperationsFeedbackType[];
  };
  memoryQuality: {
    windowHours: number;
    durableMemoryItems: number;
    activeMemoryItems: number;
    sourceLinkedActiveMemoryItems: number;
    sourceLinkCoverage: number;
    staleMemoryItems: number;
    staleMemoryRate: number;
    usefulFeedbackTotal: number;
    usefulFeedbackRate: number;
    missingMemoryReports: number;
    missingMemoryReportsPerHour: number;
    roleBoundaryMisses: number;
    roleBoundaryMissDisclosedItems: number;
    duplicateCandidateGroups: number;
    duplicateCandidateItems: number;
    duplicateRatio: number;
  };
  contextProduct: {
    runtime: {
      packetCount: number;
      itemCount: number;
      explainedItemCount: number;
      feedbackActionCount: number;
      explanationCoverage: number;
    };
    feedbackActions: {
      total: number;
      byAction: AdminOperationsFeedbackType[];
    };
    benchmark: {
      observed: boolean;
      source: string;
      generatedAt: string | null;
      readError: string | null;
    };
  };
  embeddingFailures: {
    retryingFailed: number;
    deadLetter: number;
    failed: number;
    expiredProcessing: number;
  };
}

interface AdminOperationsFeedbackType {
  feedbackType: string;
  count: number;
  share: number;
  perHour: number;
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

interface AdminAccessResult {
  [key: string]: unknown;
}

interface TextFetchResult {
  text: string;
  headers: Headers;
}

const credentialStorageKey = "memorySystem.consoleCredential";
const credentialKindStorageKey = "memorySystem.consoleCredentialKind";
const consoleReturnUrl = "/admin/";

interface AdminConsoleState {
  mode: "memory" | "events" | "operations" | "access" | "registration" | "compliance" | "pilot";
  facts: AdminMemoryFact[];
  events: AdminSourceEvent[];
  operationsSummary: AdminOperationsSummary | null;
  selectedOperationId: string | null;
  complianceStatus: AdminComplianceStatusResponse | null;
  pilotReadiness: AdminPilotReadinessStatusResponse | null;
  selectedFactId: string | null;
  selectedEventId: string | null;
  selectedComplianceId: string | null;
  selectedPilotGateId: string | null;
  selectedRegistrationStepId: RegistrationStepId;
  selectedSource: SourceEventResponse | null;
  accessResult: AdminAccessResult | null;
  registrationResult: Record<string, unknown> | null;
  busy: boolean;
}

const state: AdminConsoleState = {
  mode: "memory",
  facts: [],
  events: [],
  operationsSummary: null,
  selectedOperationId: null,
  complianceStatus: null,
  pilotReadiness: null,
  selectedFactId: null,
  selectedEventId: null,
  selectedComplianceId: null,
  selectedPilotGateId: null,
  selectedRegistrationStepId: "scope",
  selectedSource: null,
  accessResult: null,
  registrationResult: null,
  busy: false
};

const elements = {
  apiBase: byId<HTMLInputElement>("api-base"),
  apiKey: byId<HTMLInputElement>("api-key"),
  credentialState: byId<HTMLElement>("credential-state"),
  modeFilter: byId<HTMLSelectElement>("mode-filter"),
  statusFilter: byId<HTMLSelectElement>("status-filter"),
  memoryTypeFilter: byId<HTMLSelectElement>("memory-type-filter"),
  roleFilter: byId<HTMLSelectElement>("role-filter"),
  namespacePrefixFilter: byId<HTMLInputElement>("namespace-prefix-filter"),
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
  logout: byId<HTMLElement>("logout"),
  status: byId<HTMLElement>("status"),
  listTitle: byId<HTMLElement>("list-title"),
  resultList: byId<HTMLElement>("result-list"),
  detail: byId<HTMLElement>("detail"),
  sourceTitle: byId<HTMLElement>("source-title"),
  sourceDetail: byId<HTMLElement>("source-detail")
};

elements.apiBase.value = window.location.origin;
elements.apiKey.value = sessionStorage.getItem(credentialStorageKey) ?? "";
updateCredentialState();
redirectToLoginIfMissingCredential(consoleReturnUrl);
elements.refresh.addEventListener("click", () => void loadCurrentMode());
elements.logout.addEventListener("click", event => {
  event.preventDefault();
  void logout();
});
elements.apiKey.addEventListener("change", () => {
  persistCredential();
  updateCredentialState();
});
elements.query.addEventListener("keydown", event => {
  if (event.key === "Enter") {
    event.preventDefault();
    void loadCurrentMode();
  }
});
elements.statusFilter.addEventListener("change", () => void loadFacts());
elements.modeFilter.addEventListener("change", () => {
  state.mode = readMode();
  state.selectedSource = null;
  updateFilterVisibility();
  render();
  void loadCurrentMode();
});

for (const element of [
  elements.memoryTypeFilter,
  elements.roleFilter,
  elements.namespacePrefixFilter,
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
elements.namespacePrefixFilter.addEventListener("keydown", event => {
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

function readMode(): AdminConsoleState["mode"] {
  if (elements.modeFilter.value === "events") {
    return "events";
  }

  if (elements.modeFilter.value === "access") {
    return "access";
  }

  if (elements.modeFilter.value === "registration") {
    return "registration";
  }

  if (elements.modeFilter.value === "operations") {
    return "operations";
  }

  if (elements.modeFilter.value === "compliance") {
    return "compliance";
  }

  if (elements.modeFilter.value === "pilot") {
    return "pilot";
  }

  return "memory";
}

async function loadCurrentMode(): Promise<void> {
  if (state.mode === "events") {
    await loadEvents();
    return;
  }

  if (state.mode === "compliance") {
    await loadCompliance();
    return;
  }

  if (state.mode === "operations") {
    await loadOperations();
    return;
  }

  if (state.mode === "pilot") {
    await loadPilotReadiness();
    return;
  }

  if (state.mode === "access") {
    setStatus("Access");
    render();
    return;
  }

  if (state.mode === "registration") {
    setStatus("Registration");
    render();
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
    addSelectParam(params, "memoryType", elements.memoryTypeFilter);

    const namespacePrefix = elements.namespacePrefixFilter.value.trim();
    if (namespacePrefix) {
      params.set("namespacePrefix", namespacePrefix);
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

async function loadOperations(): Promise<void> {
  setBusy(true);
  setStatus("Loading");
  state.selectedSource = null;

  try {
    const response = await apiFetch<AdminOperationsSummary>("/api/operations/summary");
    state.operationsSummary = response;
    state.selectedOperationId = state.selectedOperationId ?? "readiness";
    setStatus(`Ops ${response.status}`);
  } catch (error) {
    setStatus("Error");
    state.operationsSummary = null;
    state.selectedOperationId = null;
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

async function loadCompliance(): Promise<void> {
  setBusy(true);
  setStatus("Loading");
  state.selectedSource = null;

  try {
    const response = await apiFetch<AdminComplianceStatusResponse>("/api/admin/compliance/status");
    state.complianceStatus = response;
    state.selectedComplianceId = response.items[0]?.id ?? null;
    setStatus(`${response.items.length} checks`);
  } catch (error) {
    setStatus("Error");
    state.complianceStatus = null;
    state.selectedComplianceId = null;
    state.selectedSource = null;
    elements.sourceDetail.replaceChildren(emptyPanel(errorMessage(error)));
  } finally {
    setBusy(false);
    render();
  }
}

async function loadPilotReadiness(): Promise<void> {
  setBusy(true);
  setStatus("Loading");
  state.selectedSource = null;

  try {
    const response = await apiFetch<AdminPilotReadinessStatusResponse>("/api/admin/pilot/readiness");
    state.pilotReadiness = response;
    state.selectedPilotGateId = response.gates[0]?.id ?? null;
    setStatus(response.decision.externalInviteApproved ? "Pilot GO" : "Pilot NO-GO");
  } catch (error) {
    setStatus("Error");
    state.pilotReadiness = null;
    state.selectedPilotGateId = null;
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

  addSelectParam(params, "roleId", elements.roleFilter);

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
  applyAuth(headers);

  const response = await fetch(`${apiBase}${path}`, {
    ...init,
    credentials: "same-origin",
    headers
  });
  const text = await response.text();
  const payload = text ? JSON.parse(text) : null;

  if (!response.ok) {
    const detail = payload?.detail ?? payload?.title ?? response.statusText;
    if (isAuthenticationFailure(response)) {
      redirectToLoginAfterAuthenticationFailure();
    }

    throw new Error(`${response.status} ${detail}`);
  }

  return payload as T;
}

async function logout(): Promise<void> {
  setBusy(true);
  clearStoredCredential();
  elements.apiKey.value = "";
  window.location.assign(logoutUrl(consoleReturnUrl));
  setBusy(false);
}

function redirectToLoginIfMissingCredential(returnUrl: string): void {
  if (readCredential()) {
    return;
  }

  window.location.replace(loginUrl(returnUrl));
}

function redirectToLoginAfterAuthenticationFailure(): void {
  clearStoredCredential();
  elements.apiKey.value = "";
  updateCredentialState();
  window.location.replace(loginUrl(consoleReturnUrl));
}

function clearStoredCredential(): void {
  sessionStorage.removeItem(credentialStorageKey);
  sessionStorage.removeItem(credentialKindStorageKey);
}

function isAuthenticationFailure(response: Response): boolean {
  return response.status === 401 || response.status === 403;
}

function loginUrl(returnUrl: string): string {
  return `/auth/login?returnUrl=${encodeURIComponent(returnUrl)}`;
}

function logoutUrl(returnUrl: string): string {
  return `/auth/logout?returnUrl=${encodeURIComponent(returnUrl)}`;
}

function persistCredential(): void {
  const credential = elements.apiKey.value.trim();
  if (credential) {
    sessionStorage.setItem(credentialStorageKey, credential);
    sessionStorage.setItem(credentialKindStorageKey, looksLikeJwt(credential) ? "jwt" : "api_key");
  } else {
    clearStoredCredential();
  }
}

function applyAuth(headers: Headers): void {
  const credential = readCredential();
  if (!credential) {
    return;
  }

  if (!readStoredCredential()) {
    persistCredential();
  }

  if (usesBearerCredential(credential)) {
    headers.set("Authorization", `Bearer ${credential}`);
    headers.delete("X-Api-Key");
    return;
  }

  headers.set("X-Api-Key", credential);
  headers.delete("Authorization");
}

function readCredential(): string {
  const storedCredential = readStoredCredential();
  if (storedCredential) {
    if (elements.apiKey.value.trim() !== storedCredential) {
      elements.apiKey.value = storedCredential;
      updateCredentialState();
    }

    return storedCredential;
  }

  return elements.apiKey.value.trim();
}

function readStoredCredential(): string {
  return sessionStorage.getItem(credentialStorageKey)?.trim() ?? "";
}

function usesBearerCredential(credential: string): boolean {
  const credentialKind = sessionStorage.getItem(credentialKindStorageKey);

  return credentialKind === "oidc_jwt"
    || credentialKind === "jwt"
    || (!credentialKind && looksLikeJwt(credential));
}

function looksLikeJwt(value: string): boolean {
  return value.split(".").length === 3;
}

function updateCredentialState(): void {
  const credential = elements.apiKey.value.trim();

  if (!credential) {
    elements.credentialState.textContent = "Missing";
    return;
  }

  elements.credentialState.textContent = looksLikeJwt(credential) ? "JWT" : "API key";
}

function render(): void {
  elements.listTitle.textContent = state.mode === "events"
    ? "Source Events"
    : state.mode === "pilot"
      ? "Pilot Gates"
    : state.mode === "operations"
      ? "Operations"
    : state.mode === "compliance"
      ? "Compliance"
    : state.mode === "registration"
      ? "Registration Steps"
    : state.mode === "access"
      ? "Access Actions"
      : "Memory Facts";
  elements.sourceTitle.textContent = state.mode === "events"
    ? "Payload"
    : state.mode === "pilot"
      ? "Pilot Work"
    : state.mode === "operations"
      ? "Operator Links"
    : state.mode === "compliance"
      ? "Evidence Links"
    : state.mode === "registration"
      ? "Registration Evidence"
    : state.mode === "access"
      ? "Result"
      : "Source Evidence";
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

  if (state.mode === "compliance") {
    renderComplianceList();
    return;
  }

  if (state.mode === "operations") {
    renderOperationsList();
    return;
  }

  if (state.mode === "pilot") {
    renderPilotList();
    return;
  }

  if (state.mode === "access") {
    renderAccessList();
    return;
  }

  if (state.mode === "registration") {
    renderRegistrationList();
    return;
  }

  renderMemoryList();
}

function renderAccessList(): void {
  for (const action of [
    "Organization membership",
    "Project membership",
    "Project role definition",
    "Role assignment",
    "Namespace grant",
    "Break-glass admin",
    "Effective preview",
    "Audit export"
  ]) {
    const row = document.createElement("div");
    row.className = "memory-row";
    row.append(line(action, "memory-title"));
    elements.resultList.append(row);
  }
}

function renderPilotList(): void {
  const status = state.pilotReadiness;
  const gates = status?.gates ?? [];

  if (!status || gates.length === 0) {
    elements.resultList.append(emptyPanel("No pilot readiness status"));
    return;
  }

  for (const gate of gates) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = gate.id === state.selectedPilotGateId ? "memory-row selected" : "memory-row";
    button.addEventListener("click", () => {
      state.selectedPilotGateId = gate.id;
      state.selectedSource = null;
      render();
    });

    button.append(
      line(`${gate.id} ${gate.title}`, "memory-title"),
      pillRow([gate.priority, gate.status, gate.blocksExternalInvite ? "blocks_invite" : "not_blocking"]),
      line(gate.summary, "memory-meta"),
      line(`${gate.evidence.length} evidence link${gate.evidence.length === 1 ? "" : "s"}`, "memory-date"));
    elements.resultList.append(button);
  }
}

function renderComplianceList(): void {
  const items = state.complianceStatus?.items ?? [];

  if (items.length === 0) {
    elements.resultList.append(emptyPanel("No compliance status"));
    return;
  }

  for (const item of items) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = item.id === state.selectedComplianceId ? "memory-row selected" : "memory-row";
    button.addEventListener("click", () => {
      state.selectedComplianceId = item.id;
      state.selectedSource = null;
      render();
    });

    button.append(
      line(item.title, "memory-title"),
      pillRow([item.status, item.evidenceKind]),
      line(item.summary, "memory-meta"),
      line(item.count === null ? "Linked evidence" : `${item.count} visible`, "memory-date"));
    elements.resultList.append(button);
  }
}

function renderOperationsList(): void {
  const summary = state.operationsSummary;

  if (!summary) {
    elements.resultList.append(emptyPanel("No operations summary"));
    return;
  }

  for (const item of operationListItems(summary)) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = item.id === state.selectedOperationId ? "memory-row selected" : "memory-row";
    button.addEventListener("click", () => {
      state.selectedOperationId = item.id;
      state.selectedSource = null;
      render();
    });

    button.append(
      line(item.title, "memory-title"),
      pillRow(item.pills),
      line(item.summary, "memory-meta"),
      line(item.detail, "memory-date"));
    elements.resultList.append(button);
  }
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

  if (state.mode === "compliance") {
    renderComplianceDetail();
    return;
  }

  if (state.mode === "operations") {
    renderOperationsDetail();
    return;
  }

  if (state.mode === "pilot") {
    renderPilotDetail();
    return;
  }

  if (state.mode === "access") {
    renderAccessDetail();
    return;
  }

  if (state.mode === "registration") {
    renderRegistrationDetail();
    return;
  }

  renderMemoryDetail();
}

function renderPilotDetail(): void {
  const status = state.pilotReadiness;
  const gate = selectedPilotGate();

  if (!status || !gate) {
    elements.detail.append(emptyPanel("Select a pilot gate"));
    return;
  }

  const rows: [string, string][] = [
    ["Decision", status.decision.status],
    ["External invite", status.decision.externalInviteApproved ? "approved" : "blocked"],
    ["Gate", gate.id],
    ["Priority", gate.priority],
    ["Status", gate.status],
    ["Blocks invite", gate.blocksExternalInvite ? "yes" : "no"],
    ["Payload safe", status.payloadSafe ? "yes" : "no"],
    ["Raw source payloads", status.rawSourcePayloadsIncluded ? "included" : "not included"],
    ["Status id", status.statusId],
    ["Generated", status.generatedAt]
  ];

  elements.detail.append(
    heading(`${gate.id} ${gate.title}`),
    paragraph(status.decision.reason, status.decision.externalInviteApproved ? "memory-object" : "memory-object hidden-content"),
    paragraph(gate.summary, "memory-object"),
    detailGrid(rows),
    evidenceList(gate.evidence, "Evidence"),
    missingInputList(gate.missingInputs));
}

function renderComplianceDetail(): void {
  const status = state.complianceStatus;
  const item = selectedComplianceItem();

  if (!status || !item) {
    elements.detail.append(emptyPanel("Select a compliance check"));
    return;
  }

  const rows: [string, string][] = [
    ["Check", item.id],
    ["Status", item.status],
    ["Evidence kind", item.evidenceKind],
    ["Count", item.count === null ? "" : item.count.toString()],
    ["Payload safe", item.payloadSafe ? "yes" : "no"],
    ["Raw source payloads", item.rawSourcePayloadsIncluded ? "included" : "not included"],
    ["Generated", shortDate(status.generatedAt)]
  ];

  elements.detail.append(
    heading(item.title),
    paragraph(item.summary, "memory-object"),
    detailGrid(rows),
    metricList(item.metrics));
}

function renderOperationsDetail(): void {
  const summary = state.operationsSummary;

  if (!summary) {
    elements.detail.append(emptyPanel("Select an operations summary"));
    return;
  }

  const selectedId = state.selectedOperationId ?? "readiness";

  if (selectedId === "worker") {
    elements.detail.append(
      heading("Worker"),
      detailGrid([
        ["Type", summary.worker.workerType],
        ["Observed", summary.worker.observed ? "yes" : "no"],
        ["Status", summary.worker.status],
        ["Worker", summary.worker.workerId ?? ""],
        ["Stale", summary.worker.stale ? "yes" : "no"],
        ["Last seen", summary.worker.lastSeenAt ? shortDate(summary.worker.lastSeenAt) : ""],
        ["Last seen age", secondsText(summary.worker.lastSeenAgeSeconds)],
        ["Last success", summary.worker.lastSuccessAt ? shortDate(summary.worker.lastSuccessAt) : ""],
        ["Last error", summary.worker.lastError ?? ""]
      ]));
    return;
  }

  if (selectedId === "outbox") {
    elements.detail.append(
      heading("Outbox"),
      detailGrid([
        ["Ready pending", summary.outbox.readyPending.toString()],
        ["Delayed pending", summary.outbox.delayedPending.toString()],
        ["Processing", summary.outbox.processing.toString()],
        ["Dead letter", summary.outbox.deadLetter.toString()],
        ["Failed", summary.outbox.failed.toString()],
        ["Retrying failed", summary.outbox.retryingFailed.toString()],
        ["Expired processing", summary.outbox.expiredProcessing.toString()],
        ["Oldest ready pending", secondsText(summary.outbox.oldestReadyPendingSeconds)]
      ]));
    return;
  }

  if (selectedId === "retrieval") {
    elements.detail.append(
      heading("Retrieval feedback"),
      detailGrid([
        ["Window", `${summary.retrievalFeedback.windowHours.toFixed(1)} hours`],
        ["Total", summary.retrievalFeedback.total.toString()]
      ]),
      feedbackList(summary.retrievalFeedback.byType));
    return;
  }

  if (selectedId === "quality") {
    elements.detail.append(
      heading("Memory quality"),
      detailGrid([
        ["Window", `${summary.memoryQuality.windowHours.toFixed(1)} hours`],
        ["Durable memory items", summary.memoryQuality.durableMemoryItems.toString()],
        ["Active memory items", summary.memoryQuality.activeMemoryItems.toString()],
        ["Source-linked active items", summary.memoryQuality.sourceLinkedActiveMemoryItems.toString()],
        ["Source-link coverage", percentText(summary.memoryQuality.sourceLinkCoverage)],
        ["Stale memory items", summary.memoryQuality.staleMemoryItems.toString()],
        ["Stale memory rate", percentText(summary.memoryQuality.staleMemoryRate)],
        ["Useful feedback", summary.memoryQuality.usefulFeedbackTotal.toString()],
        ["Useful feedback rate", percentText(summary.memoryQuality.usefulFeedbackRate)],
        ["Missing reports", summary.memoryQuality.missingMemoryReports.toString()],
        ["Missing reports per hour", summary.memoryQuality.missingMemoryReportsPerHour.toFixed(3)],
        ["Role-boundary misses", summary.memoryQuality.roleBoundaryMisses.toString()],
        ["Role-boundary disclosed items", summary.memoryQuality.roleBoundaryMissDisclosedItems.toString()],
        ["Duplicate groups", summary.memoryQuality.duplicateCandidateGroups.toString()],
        ["Duplicate items", summary.memoryQuality.duplicateCandidateItems.toString()],
        ["Duplicate ratio", percentText(summary.memoryQuality.duplicateRatio)]
      ]));
    return;
  }

  if (selectedId === "reviews") {
    elements.detail.append(
      heading("Reviews and exports"),
      detailGrid([
        ["Pending reviews", summary.reviews.pending.toString()],
        ["Stale vault exports", summary.vaultExports.stale.toString()]
      ]),
      actionRow([
        linkAction("Open reviews", "/reviews/"),
        linkAction("Open memory", "/admin/")
      ]));
    return;
  }

  if (selectedId === "context") {
    elements.detail.append(
      heading("Context product"),
      detailGrid([
        ["Packets", summary.contextProduct.runtime.packetCount.toString()],
        ["Items", summary.contextProduct.runtime.itemCount.toString()],
        ["Explained items", summary.contextProduct.runtime.explainedItemCount.toString()],
        ["Feedback actions", summary.contextProduct.runtime.feedbackActionCount.toString()],
        ["Explanation coverage", percentText(summary.contextProduct.runtime.explanationCoverage)],
        ["Benchmark observed", summary.contextProduct.benchmark.observed ? "yes" : "no"],
        ["Benchmark source", summary.contextProduct.benchmark.source],
        ["Benchmark error", summary.contextProduct.benchmark.readError ?? ""]
      ]),
      feedbackList(summary.contextProduct.feedbackActions.byAction));
    return;
  }

  if (selectedId === "embeddings") {
    elements.detail.append(
      heading("Embedding index failures"),
      detailGrid([
        ["Retrying failed", summary.embeddingFailures.retryingFailed.toString()],
        ["Dead letter", summary.embeddingFailures.deadLetter.toString()],
        ["Failed", summary.embeddingFailures.failed.toString()],
        ["Expired processing", summary.embeddingFailures.expiredProcessing.toString()]
      ]));
    return;
  }

  elements.detail.append(
    heading("Readiness"),
    detailGrid([
      ["Status", summary.status],
      ["API", summary.api.status],
      ["Generated", shortDate(summary.generatedAt)]
    ]));
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
    actionRow([
      openButton,
      linkAction("Open reviews", "/reviews/")
    ]));
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
    elements.detail.append(actionRow([
      openButton,
      linkAction("Open reviews", "/reviews/")
    ]));
  } else {
    elements.detail.append(actionRow([
      linkAction("Open reviews", "/reviews/")
    ]));
  }
}

function renderSourceDetail(): void {
  elements.sourceDetail.replaceChildren();

  if (state.mode === "compliance") {
    const item = selectedComplianceItem();
    if (!item) {
      elements.sourceDetail.append(emptyPanel("No compliance check selected"));
      return;
    }

    elements.sourceDetail.append(linkList(item.links));
    return;
  }

  if (state.mode === "pilot") {
    const status = state.pilotReadiness;
    if (!status) {
      elements.sourceDetail.append(emptyPanel("No pilot readiness status"));
      return;
    }

    elements.sourceDetail.append(
      requiredGoInputList(status.requiredToFlipToGo),
      evidenceList(Object.values(status.canonicalDocuments), "Canonical Docs"),
      nextWorkList(status.nextRecommendedWork));
    return;
  }

  if (state.mode === "operations") {
    elements.sourceDetail.append(linkList([
      {
        label: "Operations summary",
        href: "/api/operations/summary",
        kind: "api",
        method: "GET"
      },
      {
        label: "Operations metrics",
        href: "/api/operations/metrics",
        kind: "api",
        method: "GET"
      },
      {
        label: "Readiness health",
        href: "/health/ready",
        kind: "api",
        method: "GET"
      },
      {
        label: "Review queue",
        href: "/reviews/",
        kind: "ui",
        method: "GET"
      }
    ]));
    return;
  }

  if (state.mode === "access") {
    if (!state.accessResult) {
      elements.sourceDetail.append(emptyPanel("No access result"));
      return;
    }

    const json = document.createElement("pre");
    json.className = "source-json";
    json.textContent = JSON.stringify(state.accessResult, null, 2);
    elements.sourceDetail.append(json);
    return;
  }

  if (state.mode === "registration") {
    renderRegistrationSourceDetail();
    return;
  }

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

function evidenceList(paths: string[], title: string): HTMLElement {
  const section = document.createElement("section");
  section.className = "audit-section";
  section.append(heading(title));

  if (paths.length === 0) {
    section.append(emptyPanel("No evidence links"));
    return section;
  }

  const list = document.createElement("div");
  list.className = "audit-list";

  for (const path of paths) {
    const row = document.createElement("div");
    row.className = "audit-row";
    row.append(
      line(path, "memory-title"),
      pillRow([path.endsWith(".md") ? "doc" : path.endsWith(".json") ? "json" : "code"]));
    list.append(row);
  }

  section.append(list);
  return section;
}

function missingInputList(inputs: string[]): HTMLElement {
  const section = document.createElement("section");
  section.className = "audit-section";
  section.append(heading("Missing Inputs"));

  if (inputs.length === 0) {
    section.append(emptyPanel("No missing inputs"));
    return section;
  }

  const list = document.createElement("div");
  list.className = "audit-list";

  for (const input of inputs) {
    const row = document.createElement("div");
    row.className = "audit-row";
    row.append(
      line(input, "memory-title"),
      pillRow(["required"]));
    list.append(row);
  }

  section.append(list);
  return section;
}

function requiredGoInputList(inputs: string[]): HTMLElement {
  const section = document.createElement("section");
  section.className = "audit-section";
  section.append(heading("Required To GO"));

  if (inputs.length === 0) {
    section.append(emptyPanel("No required GO inputs"));
    return section;
  }

  const list = document.createElement("div");
  list.className = "audit-list";

  for (const input of inputs) {
    const row = document.createElement("div");
    row.className = "audit-row";
    row.append(
      line(input, "memory-title"),
      pillRow(["go_input"]));
    list.append(row);
  }

  section.append(list);
  return section;
}

function nextWorkList(items: AdminPilotReadinessNextWork[]): HTMLElement {
  const section = document.createElement("section");
  section.className = "audit-section";
  section.append(heading("Next Work"));

  if (items.length === 0) {
    section.append(emptyPanel("No next work recorded"));
    return section;
  }

  const list = document.createElement("div");
  list.className = "audit-list";

  for (const item of items) {
    const row = document.createElement("div");
    row.className = "audit-row";
    row.append(
      line(`${item.id} ${item.title}`, "memory-title"),
      line(item.uses, "memory-meta"));
    list.append(row);
  }

  section.append(list);
  return section;
}

function accessForm(
  title: string,
  fields: HTMLElement[],
  buttonText: string,
  onSubmit: (form: HTMLFormElement) => Promise<void>): HTMLElement {
  const form = document.createElement("form");
  form.className = "access-form";
  form.append(heading(title));

  const grid = document.createElement("div");
  grid.className = "access-grid";
  grid.append(...fields);

  const button = document.createElement("button");
  button.type = "submit";
  button.className = "primary-action";
  button.textContent = buttonText;
  button.disabled = state.busy;

  form.append(grid, button);
  form.addEventListener("submit", event => {
    event.preventDefault();
    void onSubmit(form);
  });

  return form;
}

function textField(name: string, labelText: string): HTMLElement {
  const label = document.createElement("label");
  const span = document.createElement("span");
  const input = document.createElement("input");
  span.textContent = labelText;
  input.name = name;
  input.autocomplete = "off";
  label.append(span, input);
  return label;
}

function dateTimeField(name: string, labelText: string): HTMLElement {
  const label = document.createElement("label");
  const span = document.createElement("span");
  const input = document.createElement("input");
  span.textContent = labelText;
  input.name = name;
  input.type = "datetime-local";
  label.append(span, input);
  return label;
}

function dateField(name: string, labelText: string): HTMLElement {
  const label = document.createElement("label");
  const span = document.createElement("span");
  const input = document.createElement("input");
  span.textContent = labelText;
  input.name = name;
  input.type = "date";
  label.append(span, input);
  return label;
}

function numberField(name: string, labelText: string, value: string): HTMLElement {
  const label = document.createElement("label");
  const span = document.createElement("span");
  const input = document.createElement("input");
  span.textContent = labelText;
  input.name = name;
  input.type = "number";
  input.min = "1";
  input.max = "5000";
  input.value = value;
  label.append(span, input);
  return label;
}

function selectField(name: string, labelText: string, values: string[]): HTMLElement {
  const label = document.createElement("label");
  const span = document.createElement("span");
  const select = document.createElement("select");
  span.textContent = labelText;
  select.name = name;

  for (const value of values) {
    const option = document.createElement("option");
    option.value = value;
    option.textContent = value;
    select.append(option);
  }

  label.append(span, select);
  return label;
}

function formValue(form: HTMLFormElement, name: string): string {
  const value = new FormData(form).get(name);
  return typeof value === "string" ? value.trim() : "";
}

async function apiFetchText(path: string, init: RequestInit = {}): Promise<TextFetchResult> {
  const apiBase = elements.apiBase.value.trim().replace(/\/$/, "");
  const headers = new Headers(init.headers);
  applyAuth(headers);

  const response = await fetch(`${apiBase}${path}`, {
    ...init,
    credentials: "same-origin",
    headers
  });
  const text = await response.text();

  if (!response.ok) {
    let detail = response.statusText;

    try {
      const payload = text ? JSON.parse(text) : null;
      detail = payload?.detail ?? payload?.title ?? detail;
    } catch {
      detail = text || detail;
    }

    throw new Error(`${response.status} ${detail}`);
  }

  return {
    text,
    headers: response.headers
  };
}

async function postAccess(path: string, body: Record<string, unknown>): Promise<void> {
  setBusy(true);
  setStatus("Saving");

  try {
    state.accessResult = await apiFetch<AdminAccessResult>(path, {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify(body)
    });
    setStatus("Saved");
  } catch (error) {
    state.accessResult = { error: errorMessage(error) };
    setStatus("Error");
  } finally {
    setBusy(false);
    render();
  }
}

async function exportAudit(form: HTMLFormElement): Promise<void> {
  setBusy(true);
  setStatus("Exporting");

  const actionType = formValue(form, "actionType");
  const outcome = formValue(form, "outcome");
  const body: Record<string, unknown> = {
    occurredFrom: dateTimeValue(form, "occurredFrom"),
    occurredTo: dateTimeValue(form, "occurredTo"),
    scopeType: formValue(form, "scopeType"),
    scopeId: formValue(form, "scopeId"),
    limit: Number(formValue(form, "limit") || "1000")
  };

  if (actionType !== "all") {
    body.actionTypes = [actionType];
  }

  if (outcome !== "all") {
    body.outcomes = [outcome];
  }

  try {
    const response = await apiFetchText("/api/admin/audit-exports", {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify(body)
    });
    const firstLine = response.text.split("\n", 1)[0] || "{}";
    const manifest = JSON.parse(firstLine);
    const fileName = auditExportFileName(response.headers, manifest.exportId);
    downloadText(fileName, response.text, "application/x-ndjson");
    state.accessResult = {
      manifest,
      fileName
    };
    setStatus("Exported");
  } catch (error) {
    state.accessResult = { error: errorMessage(error) };
    setStatus("Error");
  } finally {
    setBusy(false);
    render();
  }
}

function dateTimeValue(form: HTMLFormElement, name: string): string | null {
  const value = formValue(form, name);
  return value ? new Date(value).toISOString() : null;
}

function auditExportFileName(headers: Headers, exportId: unknown): string {
  const disposition = headers.get("content-disposition") ?? "";
  const match = /filename="([^"]+)"/i.exec(disposition);

  if (match) {
    return match[1];
  }

  return typeof exportId === "string" && exportId
    ? `access-audit-${exportId.replaceAll("-", "")}.ndjson`
    : "access-audit.ndjson";
}

function downloadText(fileName: string, text: string, type: string): void {
  const blob = new Blob([text], { type });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = fileName;
  document.body.append(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
}

function metricList(metrics: AdminComplianceStatusMetric[]): HTMLElement {
  const section = document.createElement("section");
  section.className = "audit-section";
  section.append(heading("Metrics"));

  if (metrics.length === 0) {
    section.append(emptyPanel("No dedicated metric names"));
    return section;
  }

  const list = document.createElement("div");
  list.className = "audit-list";

  for (const metric of metrics) {
    const row = document.createElement("div");
    row.className = "audit-row";
    row.append(
      line(metric.name, "memory-title"),
      line(metric.description, "memory-meta"));
    list.append(row);
  }

  section.append(list);
  return section;
}

function linkList(links: AdminComplianceStatusLink[]): HTMLElement {
  const section = document.createElement("section");
  section.className = "audit-section";
  section.append(heading("Links"));

  if (links.length === 0) {
    section.append(emptyPanel("No evidence links"));
    return section;
  }

  const list = document.createElement("div");
  list.className = "audit-list";

  for (const link of links) {
    const row = document.createElement("div");
    row.className = "audit-row";

    const target = document.createElement(link.method === "GET" ? "a" : "span");
    target.className = "memory-meta";
    target.textContent = link.href;

    if (target instanceof HTMLAnchorElement) {
      target.href = apiUrl(link.href);
      target.target = "_blank";
      target.rel = "noreferrer";
    }

    row.append(
      line(link.label, "memory-title"),
      pillRow([link.method, link.kind]),
      target);
    list.append(row);
  }

  section.append(list);
  return section;
}

function apiUrl(path: string): string {
  return `${elements.apiBase.value.trim().replace(/\/$/, "")}${path}`;
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

function operationListItems(summary: AdminOperationsSummary): Array<{
  id: string;
  title: string;
  pills: string[];
  summary: string;
  detail: string;
}> {
  const outboxRisk = summary.outbox.deadLetter + summary.outbox.failed + summary.outbox.retryingFailed + summary.outbox.expiredProcessing;
  const embeddingRisk = summary.embeddingFailures.deadLetter
    + summary.embeddingFailures.failed
    + summary.embeddingFailures.retryingFailed
    + summary.embeddingFailures.expiredProcessing;

  return [
    {
      id: "readiness",
      title: "Readiness",
      pills: [summary.status, summary.api.status],
      summary: `Generated ${shortDate(summary.generatedAt)}`,
      detail: "/api/operations/summary"
    },
    {
      id: "worker",
      title: "Worker",
      pills: [summary.worker.status, summary.worker.stale ? "stale" : "fresh"],
      summary: summary.worker.observed ? summary.worker.workerType : "No heartbeat observed",
      detail: summary.worker.lastSeenAgeSeconds === null ? "" : secondsText(summary.worker.lastSeenAgeSeconds)
    },
    {
      id: "outbox",
      title: "Outbox",
      pills: [outboxRisk > 0 ? "needs_review" : "clear"],
      summary: `${summary.outbox.readyPending} ready, ${summary.outbox.processing} processing`,
      detail: `${outboxRisk} failure signals`
    },
    {
      id: "retrieval",
      title: "Retrieval feedback",
      pills: [summary.retrievalFeedback.total > 0 ? "observed" : "quiet"],
      summary: `${summary.retrievalFeedback.total} actions in ${summary.retrievalFeedback.windowHours.toFixed(0)}h`,
      detail: topFeedbackLabel(summary.retrievalFeedback.byType)
    },
    {
      id: "quality",
      title: "Memory quality",
      pills: [summary.memoryQuality.missingMemoryReports > 0 || summary.memoryQuality.duplicateCandidateGroups > 0 ? "needs_review" : "clear"],
      summary: `${percentText(summary.memoryQuality.sourceLinkCoverage)} source coverage`,
      detail: `${summary.memoryQuality.missingMemoryReports} missing reports`
    },
    {
      id: "reviews",
      title: "Reviews and exports",
      pills: [summary.reviews.pending > 0 ? "needs_review" : "clear"],
      summary: `${summary.reviews.pending} pending reviews`,
      detail: `${summary.vaultExports.stale} stale vault exports`
    },
    {
      id: "context",
      title: "Context product",
      pills: [summary.contextProduct.benchmark.observed ? "benchmark" : "no_benchmark"],
      summary: `${summary.contextProduct.runtime.packetCount} packets`,
      detail: `${percentText(summary.contextProduct.runtime.explanationCoverage)} explanation coverage`
    },
    {
      id: "embeddings",
      title: "Embeddings",
      pills: [embeddingRisk > 0 ? "needs_review" : "clear"],
      summary: `${summary.embeddingFailures.retryingFailed} retrying, ${summary.embeddingFailures.deadLetter} dead-letter`,
      detail: `${embeddingRisk} failure signals`
    }
  ];
}

function feedbackList(items: AdminOperationsFeedbackType[]): HTMLElement {
  const section = document.createElement("section");
  section.className = "audit-section";
  section.append(heading("Feedback Types"));

  if (items.length === 0) {
    section.append(emptyPanel("No feedback rows"));
    return section;
  }

  const list = document.createElement("div");
  list.className = "audit-list";

  for (const item of items) {
    const row = document.createElement("div");
    row.className = "audit-row";
    row.append(
      line(item.feedbackType, "memory-title"),
      pillRow([item.count > 0 ? "observed" : "quiet"]),
      line(`${item.count} total`, "memory-meta"),
      line(`${percentText(item.share)} share · ${item.perHour.toFixed(2)}/hour`, "memory-date"));
    list.append(row);
  }

  section.append(list);
  return section;
}

function actionRow(actions: HTMLElement[]): HTMLElement {
  const row = document.createElement("div");
  row.className = "action-row";
  row.append(...actions);
  return row;
}

function linkAction(label: string, href: string): HTMLAnchorElement {
  const link = document.createElement("a");
  link.className = "secondary-action";
  link.href = href;
  link.textContent = label;
  return link;
}

function topFeedbackLabel(items: AdminOperationsFeedbackType[]): string {
  const top = [...items].sort((left, right) => right.count - left.count)[0];
  return top && top.count > 0 ? `${top.feedbackType} leads` : "No feedback yet";
}

function secondsText(value: number | null): string {
  if (value === null) {
    return "";
  }

  if (value < 60) {
    return `${value.toFixed(0)}s`;
  }

  if (value < 3600) {
    return `${(value / 60).toFixed(1)}m`;
  }

  return `${(value / 3600).toFixed(1)}h`;
}

function percentText(value: number): string {
  return `${(value * 100).toFixed(1)}%`;
}

function selectedFact(): AdminMemoryFact | null {
  return state.facts.find(fact => fact.id === state.selectedFactId) ?? null;
}

function selectedEvent(): AdminSourceEvent | null {
  return state.events.find(sourceEvent => sourceEvent.id === state.selectedEventId) ?? null;
}

function selectedComplianceItem(): AdminComplianceStatusItem | null {
  return state.complianceStatus?.items.find(item => item.id === state.selectedComplianceId) ?? null;
}

function selectedPilotGate(): AdminPilotReadinessGate | null {
  return state.pilotReadiness?.gates.find(gate => gate.id === state.selectedPilotGateId) ?? null;
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

  for (const element of document.querySelectorAll<HTMLElement>("[data-memory-filter]")) {
    element.hidden = state.mode !== "memory";
  }

  for (const element of document.querySelectorAll<HTMLElement>("[data-role-filter]")) {
    element.hidden = state.mode !== "memory" && state.mode !== "events";
  }

  elements.statusFilter.closest("label")!.hidden = state.mode !== "memory";
  elements.scopeTypeFilter.closest("label")!.hidden = state.mode === "access" || state.mode === "registration" || state.mode === "compliance" || state.mode === "pilot" || state.mode === "operations";
  elements.scopeIdFilter.closest("label")!.hidden = state.mode === "access" || state.mode === "registration" || state.mode === "compliance" || state.mode === "pilot" || state.mode === "operations";
  elements.query.closest("label")!.hidden = state.mode === "access" || state.mode === "registration" || state.mode === "compliance" || state.mode === "pilot" || state.mode === "operations";
}

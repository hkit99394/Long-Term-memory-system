type ReviewAction = "approve" | "reject" | "edit" | "expire" | "delete" | "supersede";

interface PendingReviewsResponse {
  reviews: PendingReview[];
}

interface PendingReview {
  id: string;
  reviewStatus: string;
  reviewerId: string | null;
  notes: string | null;
  sourceEventId: string;
  sourceLink: string;
  createdAt: string;
  updatedAt: string;
  memory: PendingMemory;
}

interface PendingMemory {
  id: string;
  scopeType: string;
  scopeId: string;
  namespace: string;
  memoryType: string;
  visibility: string;
  subject: string;
  predicate: string;
  object: string;
  confidence: number;
  trustLevel: string;
  status: string;
  sourceEventId: string;
  sourceLink: string;
  proposedByPrincipalId: string | null;
}

interface ReviewActionResponse {
  action: ReviewAction;
  review: PendingReview;
  replacementMemoryFactId: string | null;
}

interface DashboardState {
  reviews: PendingReview[];
  selectedReviewId: string | null;
  selectedAction: ReviewAction;
  busy: boolean;
  actionIdempotencyKeys: Record<string, string>;
}

const credentialStorageKey = "memorySystem.consoleCredential";
const credentialKindStorageKey = "memorySystem.consoleCredentialKind";
const consoleReturnUrl = "/reviews/";

const actions: ReviewAction[] = ["approve", "reject", "edit", "expire", "delete", "supersede"];
const actionLabels: Record<ReviewAction, string> = {
  approve: "Approve",
  reject: "Reject",
  edit: "Edit",
  expire: "Expire",
  delete: "Delete",
  supersede: "Supersede"
};

const state: DashboardState = {
  reviews: [],
  selectedReviewId: null,
  selectedAction: "approve",
  busy: false,
  actionIdempotencyKeys: {}
};

const elements = {
  apiBase: byId<HTMLInputElement>("api-base"),
  apiKey: byId<HTMLInputElement>("api-key"),
  refresh: byId<HTMLButtonElement>("refresh"),
  logout: byId<HTMLElement>("logout"),
  status: byId<HTMLElement>("status"),
  reviewList: byId<HTMLElement>("review-list"),
  detail: byId<HTMLElement>("review-detail"),
  actionTabs: byId<HTMLElement>("action-tabs"),
  actionForm: byId<HTMLFormElement>("action-form"),
  sourceEventId: byId<HTMLInputElement>("action-source-event-id"),
  notes: byId<HTMLTextAreaElement>("action-notes"),
  subject: byId<HTMLInputElement>("action-subject"),
  predicate: byId<HTMLInputElement>("action-predicate"),
  object: byId<HTMLTextAreaElement>("action-object"),
  contentFields: byId<HTMLElement>("content-fields"),
  submit: byId<HTMLButtonElement>("action-submit"),
  activity: byId<HTMLElement>("activity")
};

elements.apiBase.value = window.location.origin;
elements.apiKey.value = sessionStorage.getItem(credentialStorageKey) ?? "";
redirectToLoginIfMissingCredential(consoleReturnUrl);
elements.refresh.addEventListener("click", () => void loadReviews());
elements.logout.addEventListener("click", event => {
  event.preventDefault();
  void logout();
});
elements.apiKey.addEventListener("change", persistCredential);
elements.actionForm.addEventListener("submit", event => {
  event.preventDefault();
  void submitAction();
});

for (const action of actions) {
  const button = document.createElement("button");
  button.type = "button";
  button.dataset.action = action;
  button.className = action === "reject" || action === "delete" ? "tool danger" : "tool";
  button.textContent = actionLabels[action];
  button.addEventListener("click", () => selectAction(action));
  elements.actionTabs.append(button);
}

render();

function byId<T extends HTMLElement>(id: string): T {
  const element = document.getElementById(id);

  if (!element) {
    throw new Error(`Missing element '${id}'.`);
  }

  return element as T;
}

async function loadReviews(): Promise<void> {
  setBusy(true);
  setStatus("Loading");

  try {
    const response = await apiFetch<PendingReviewsResponse>("/api/reviews/pending?limit=50");
    state.reviews = response.reviews;
    state.selectedReviewId = response.reviews[0]?.id ?? null;
    resetActionDefaults(selectedReview());
    setStatus(`${response.reviews.length} pending`);
    writeActivity("Pending queue refreshed.");
  } catch (error) {
    setStatus("Error");
    writeActivity(errorMessage(error));
  } finally {
    setBusy(false);
    render();
  }
}

async function submitAction(): Promise<void> {
  if (state.busy) {
    return;
  }

  const review = selectedReview();

  if (!review) {
    return;
  }

  setBusy(true);

  try {
    const body: Record<string, string | null> = {
      sourceEventId: elements.sourceEventId.value.trim(),
      notes: emptyToNull(elements.notes.value)
    };

    if (requiresContent(state.selectedAction)) {
      body.subject = elements.subject.value.trim();
      body.predicate = elements.predicate.value.trim();
      body.object = elements.object.value.trim();
    }

    const bodyJson = JSON.stringify(body);
    const attemptKey = actionAttemptKey(review.id, state.selectedAction, bodyJson);
    const idempotencyKey = state.actionIdempotencyKeys[attemptKey]
      ?? createIdempotencyKey(review.id, state.selectedAction);
    state.actionIdempotencyKeys[attemptKey] = idempotencyKey;

    const response = await apiFetch<ReviewActionResponse>(
      `/api/reviews/${review.id}/${state.selectedAction}`,
      {
        method: "POST",
        headers: {
          "Idempotency-Key": idempotencyKey
        },
        body: bodyJson
      });

    delete state.actionIdempotencyKeys[attemptKey];
    writeActivity(`${actionLabels[response.action]} completed.`);
    await loadReviews();
  } catch (error) {
    writeActivity(errorMessage(error));
  } finally {
    setBusy(false);
    render();
  }
}

async function apiFetch<T>(path: string, init: RequestInit = {}): Promise<T> {
  const apiBase = elements.apiBase.value.trim().replace(/\/$/, "");
  const headers = new Headers(init.headers);
  applyAuth(headers);

  if (init.body && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

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

function render(): void {
  renderReviewList();
  renderDetail();
  renderAction();
}

function renderReviewList(): void {
  elements.reviewList.replaceChildren();

  if (state.reviews.length === 0) {
    const empty = document.createElement("div");
    empty.className = "empty";
    empty.textContent = "No pending reviews";
    elements.reviewList.append(empty);
    return;
  }

  for (const review of state.reviews) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = review.id === state.selectedReviewId ? "review selected" : "review";
    button.addEventListener("click", () => {
      state.selectedReviewId = review.id;
      resetActionDefaults(review);
      render();
    });

    button.append(
      line(review.memory.subject, "review-title"),
      line(`${review.memory.scopeType} / ${review.memory.memoryType} / ${review.memory.status}`, "review-meta"),
      line(shortDate(review.createdAt), "review-date"));
    elements.reviewList.append(button);
  }
}

function renderDetail(): void {
  const review = selectedReview();
  elements.detail.replaceChildren();

  if (!review) {
    elements.detail.append(emptyPanel("Select a review"));
    return;
  }

  const rows: [string, string][] = [
    ["Review", review.id],
    ["Review source", review.sourceLink],
    ["Memory", review.memory.id],
    ["Memory source", review.memory.sourceLink],
    ["Scope", `${review.memory.scopeType}:${review.memory.scopeId}`],
    ["Namespace", review.memory.namespace],
    ["Predicate", review.memory.predicate],
    ["Confidence", review.memory.confidence.toFixed(3)],
    ["Trust", review.memory.trustLevel],
    ["Proposed by", review.memory.proposedByPrincipalId ?? ""]
  ];

  elements.detail.append(
    heading(review.memory.subject),
    paragraph(review.memory.object, "memory-object"),
    detailGrid(rows),
    paragraph(review.notes ?? "", "notes"));
}

function renderAction(): void {
  const review = selectedReview();

  for (const button of elements.actionTabs.querySelectorAll<HTMLButtonElement>("button")) {
    button.classList.toggle("active", button.dataset.action === state.selectedAction);
  }

  elements.actionForm.hidden = review === null;
  elements.contentFields.hidden = !requiresContent(state.selectedAction);
  elements.submit.textContent = actionLabels[state.selectedAction];
  elements.submit.disabled = state.busy || review === null;

}

function resetActionDefaults(review: PendingReview | null): void {
  elements.sourceEventId.value = review?.sourceEventId ?? "";
  elements.notes.value = "";
  elements.subject.value = review?.memory.subject ?? "";
  elements.predicate.value = review?.memory.predicate ?? "";
  elements.object.value = review?.memory.object ?? "";
}

function selectAction(action: ReviewAction): void {
  state.selectedAction = action;
  resetActionDefaults(selectedReview());
  render();
}

function selectedReview(): PendingReview | null {
  return state.reviews.find(review => review.id === state.selectedReviewId) ?? null;
}

function requiresContent(action: ReviewAction): boolean {
  return action === "edit" || action === "supersede";
}

function createIdempotencyKey(reviewId: string, action: ReviewAction): string {
  const randomValue = globalThis.crypto?.randomUUID?.()
    ?? `${Date.now().toString(36)}-${Math.random().toString(16).slice(2)}`;

  return `review:${reviewId}:${action}:${randomValue}`;
}

function actionAttemptKey(reviewId: string, action: ReviewAction, bodyJson: string): string {
  return `${reviewId}:${action}:${bodyJson}`;
}

function setBusy(busy: boolean): void {
  state.busy = busy;
  elements.refresh.disabled = busy;
}

function setStatus(value: string): void {
  elements.status.textContent = value;
}

function writeActivity(value: string): void {
  elements.activity.textContent = value;
}

function emptyToNull(value: string): string | null {
  const trimmed = value.trim();

  return trimmed.length === 0 ? null : trimmed;
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

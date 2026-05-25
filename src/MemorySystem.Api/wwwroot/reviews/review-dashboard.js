// Generated from tools/ui/src/review-dashboard.ts. Run npm run build in tools/ui.


















































const actions                 = ["approve", "reject", "edit", "expire", "delete", "supersede"];
const actionLabels                               = {
  approve: "Approve",
  reject: "Reject",
  edit: "Edit",
  expire: "Expire",
  delete: "Delete",
  supersede: "Supersede"
};

const state                 = {
  reviews: [],
  selectedReviewId: null,
  selectedAction: "approve",
  busy: false,
  actionIdempotencyKeys: {}
};

const elements = {
  apiBase: byId                  ("api-base"),
  apiKey: byId                  ("api-key"),
  refresh: byId                   ("refresh"),
  status: byId             ("status"),
  reviewList: byId             ("review-list"),
  detail: byId             ("review-detail"),
  actionTabs: byId             ("action-tabs"),
  actionForm: byId                 ("action-form"),
  sourceEventId: byId                  ("action-source-event-id"),
  notes: byId                     ("action-notes"),
  subject: byId                  ("action-subject"),
  predicate: byId                  ("action-predicate"),
  object: byId                     ("action-object"),
  contentFields: byId             ("content-fields"),
  submit: byId                   ("action-submit"),
  activity: byId             ("activity")
};

elements.refresh.addEventListener("click", () => void loadReviews());
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

function byId                       (id        )    {
  const element = document.getElementById(id);

  if (!element) {
    throw new Error(`Missing element '${id}'.`);
  }

  return element     ;
}

async function loadReviews()                {
  setBusy(true);
  setStatus("Loading");

  try {
    const response = await apiFetch                        ("/api/reviews/pending?limit=50");
    state.reviews = response.reviews;
    state.selectedReviewId = response.reviews[0]?.id ?? null;
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

async function submitAction()                {
  if (state.busy) {
    return;
  }

  const review = selectedReview();

  if (!review) {
    return;
  }

  setBusy(true);

  try {
    const body                                = {
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

    const response = await apiFetch                      (
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

async function apiFetch   (path        , init              = {})             {
  const apiBase = elements.apiBase.value.trim().replace(/\/$/, "");
  const headers = new Headers(init.headers);
  const apiKey = elements.apiKey.value.trim();

  if (apiKey) {
    headers.set("X-Api-Key", apiKey);
  }

  if (init.body && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
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
  renderReviewList();
  renderDetail();
  renderAction();
}

function renderReviewList()       {
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
      fillActionDefaults(review);
      render();
    });

    button.append(
      line(review.memory.subject, "review-title"),
      line(`${review.memory.scopeType} / ${review.memory.memoryType} / ${review.memory.status}`, "review-meta"),
      line(shortDate(review.createdAt), "review-date"));
    elements.reviewList.append(button);
  }
}

function renderDetail()       {
  const review = selectedReview();
  elements.detail.replaceChildren();

  if (!review) {
    elements.detail.append(emptyPanel("Select a review"));
    return;
  }

  const rows                     = [
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

function renderAction()       {
  const review = selectedReview();

  for (const button of elements.actionTabs.querySelectorAll                   ("button")) {
    button.classList.toggle("active", button.dataset.action === state.selectedAction);
  }

  elements.actionForm.hidden = review === null;
  elements.contentFields.hidden = !requiresContent(state.selectedAction);
  elements.submit.textContent = actionLabels[state.selectedAction];
  elements.submit.disabled = state.busy || review === null;

  if (review) {
    fillActionDefaults(review);
  }
}

function fillActionDefaults(review               )       {
  if (!elements.sourceEventId.value) {
    elements.sourceEventId.value = review.sourceEventId;
  }

  if (!elements.subject.value) {
    elements.subject.value = review.memory.subject;
  }

  if (!elements.predicate.value) {
    elements.predicate.value = review.memory.predicate;
  }

  if (!elements.object.value) {
    elements.object.value = review.memory.object;
  }
}

function selectAction(action              )       {
  state.selectedAction = action;
  const review = selectedReview();

  if (review) {
    elements.sourceEventId.value = review.sourceEventId;
    elements.subject.value = review.memory.subject;
    elements.predicate.value = review.memory.predicate;
    elements.object.value = review.memory.object;
  }

  render();
}

function selectedReview()                       {
  return state.reviews.find(review => review.id === state.selectedReviewId) ?? null;
}

function requiresContent(action              )          {
  return action === "edit" || action === "supersede";
}

function createIdempotencyKey(reviewId        , action              )         {
  const randomValue = globalThis.crypto?.randomUUID?.()
    ?? `${Date.now().toString(36)}-${Math.random().toString(16).slice(2)}`;

  return `review:${reviewId}:${action}:${randomValue}`;
}

function actionAttemptKey(reviewId        , action              , bodyJson        )         {
  return `${reviewId}:${action}:${bodyJson}`;
}

function setBusy(busy         )       {
  state.busy = busy;
  elements.refresh.disabled = busy;
}

function setStatus(value        )       {
  elements.status.textContent = value;
}

function writeActivity(value        )       {
  elements.activity.textContent = value;
}

function emptyToNull(value        )                {
  const trimmed = value.trim();

  return trimmed.length === 0 ? null : trimmed;
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

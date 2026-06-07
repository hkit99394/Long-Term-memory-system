const credentialKey = "memorySystem.consoleCredential";
const credentialKindKey = "memorySystem.consoleCredentialKind";
const status = document.getElementById("status");

document.getElementById("password-form")?.addEventListener("submit", event => {
  event.preventDefault();
  const form = event.currentTarget;
  void login(
    "/api/auth/console/password",
    {
      username: form.username.value,
      password: form.password.value,
      returnUrl: form.returnUrl.value
    },
    "",
    "console_password_api_key");
});

document.getElementById("oidc-form")?.addEventListener("submit", event => {
  event.preventDefault();
  const form = event.currentTarget;
  void login(
    "/api/auth/console/oidc-token",
    {
      token: form.token.value,
      returnUrl: form.returnUrl.value
    },
    form.token.value,
    "oidc_jwt");
});

document.getElementById("api-key-form")?.addEventListener("submit", event => {
  event.preventDefault();
  const form = event.currentTarget;
  void login(
    "/api/auth/console/break-glass-key",
    {
      apiKey: form.apiKey.value,
      returnUrl: form.returnUrl.value
    },
    form.apiKey.value,
    "break_glass_api_key");
});

async function login(path, body, submittedCredential, fallbackCredentialKind) {
  setStatus("");

  try {
    const response = await fetch(path, {
      method: "POST",
      credentials: "same-origin",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body)
    });
    const text = await response.text();
    const payload = text ? JSON.parse(text) : {};

    if (!response.ok) {
      setStatus(payload.detail || payload.title || response.statusText);
      return;
    }

    const credential = payload.credential || submittedCredential;
    if (!credential || !credential.trim()) {
      setStatus("Login completed but no console credential was returned.");
      return;
    }

    sessionStorage.setItem(credentialKey, credential.trim());
    sessionStorage.setItem(credentialKindKey, payload.credentialKind || fallbackCredentialKind);
    window.location.assign(normalizeReturnUrl(payload.returnUrl));
  } catch {
    setStatus("Login request could not be completed.");
  }
}

function normalizeReturnUrl(value) {
  return typeof value === "string"
    && value.startsWith("/")
    && !value.startsWith("//")
    && !value.includes("\\")
      ? value
      : "/admin/";
}

function setStatus(message) {
  if (status) {
    status.textContent = message;
    if (message) {
      status.focus();
    }
  }
}

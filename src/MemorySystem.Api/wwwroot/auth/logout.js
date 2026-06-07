const credentialKey = "memorySystem.consoleCredential";
const credentialKindKey = "memorySystem.consoleCredentialKind";
const status = document.getElementById("status");

sessionStorage.removeItem(credentialKey);
sessionStorage.removeItem(credentialKindKey);

const returnUrl = normalizeReturnUrl(status?.dataset.returnUrl);
if (status) {
  status.textContent = "Signed out.";
}

window.location.replace(`/auth/login?returnUrl=${encodeURIComponent(returnUrl)}`);

function normalizeReturnUrl(value) {
  return typeof value === "string"
    && value.startsWith("/")
    && !value.startsWith("//")
    && !value.includes("\\")
      ? value
      : "/admin/";
}

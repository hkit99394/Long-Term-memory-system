#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${MEMORYSYSTEM_API_BASE_URL:-http://127.0.0.1:5099}"
API_KEY="${MEMORYSYSTEM_API_KEY:-private-alpha-local-key}"
PRINCIPAL_ID="${MEMORYSYSTEM_PRINCIPAL_ID:-11111111-1111-4111-8111-111111111111}"
PROJECT_A_ID="${MEMORYSYSTEM_PROJECT_A_ID:-33333333-3333-4333-8333-333333333333}"
RUN_ID="${MEMORYSYSTEM_EXAMPLE_RUN_ID:-lmss03-v1}"

json_field() {
  local field="$1"
  python3 -c '
import json
import sys

field = sys.argv[1]
value = json.load(sys.stdin)
for part in field.split("."):
    if value is None:
        break
    value = value.get(part)

if value is None:
    sys.exit(1)

print(value)
' "$field"
}

first_context_source() {
  python3 -c '
import json
import sys

packet = json.load(sys.stdin)
for group in ["userPreferences", "projectMemory", "roleMemory", "relevantDecisions"]:
    for item in packet.get(group, []):
        print(item["sourceType"], item["sourceId"])
        sys.exit(0)

sys.exit(1)
'
}

pretty_json() {
  python3 -m json.tool
}

post_idempotent() {
  local path="$1"
  local idempotency_key="$2"
  local body="$3"

  curl --fail-with-body -sS -X POST "${BASE_URL}${path}" \
    -H "X-Api-Key: ${API_KEY}" \
    -H "Idempotency-Key: ${idempotency_key}" \
    -H "Content-Type: application/json" \
    --data "${body}"
}

post_json() {
  local path="$1"
  local body="$2"

  curl --fail-with-body -sS -X POST "${BASE_URL}${path}" \
    -H "X-Api-Key: ${API_KEY}" \
    -H "Content-Type: application/json" \
    --data "${body}"
}

get_json() {
  local path="$1"

  curl --fail-with-body -sS "${BASE_URL}${path}" \
    -H "X-Api-Key: ${API_KEY}"
}

event_body="$(cat <<JSON
{
  "principalId": "${PRINCIPAL_ID}",
  "eventType": "user_message",
  "scopeType": "user",
  "scopeId": "${PRINCIPAL_ID}",
  "trustLevel": "user_scoped",
  "retentionClass": "standard",
  "sensitivity": "none",
  "payload": {
    "text": "When showing memory-derived claims, preserve source ids.",
    "source": "LMSS-03 client example"
  }
}
JSON
)"

echo "1. Append source evidence"
event_response="$(post_idempotent "/api/events" "${RUN_ID}-append-event" "${event_body}")"
event_id="$(printf '%s' "${event_response}" | json_field id)"
printf '%s\n' "${event_response}" | pretty_json

echo
echo "2. Replay append with the same Idempotency-Key"
event_retry_response="$(post_idempotent "/api/events" "${RUN_ID}-append-event" "${event_body}")"
event_retry_id="$(printf '%s' "${event_retry_response}" | json_field id)"

if [[ "${event_retry_id}" != "${event_id}" ]]; then
  echo "Expected replayed event id ${event_id}, got ${event_retry_id}." >&2
  exit 1
fi

echo "Replayed event id: ${event_retry_id}"

proposal_body="$(cat <<JSON
{
  "sourceEventId": "${event_id}",
  "memoryType": "preference",
  "scopeType": "user",
  "scopeId": "${PRINCIPAL_ID}",
  "namespace": "/user/${PRINCIPAL_ID}/preferences",
  "visibility": "private",
  "subject": "api client examples",
  "predicate": "should",
  "object": "preserve source ids when displaying memory-derived claims",
  "confidence": 0.9,
  "trustLevel": "user_scoped",
  "sensitivity": "none"
}
JSON
)"

echo
echo "3. Propose durable memory"
proposal_response="$(post_idempotent "/api/memory/proposals" "${RUN_ID}-propose-memory" "${proposal_body}")"
decision="$(printf '%s' "${proposal_response}" | json_field decision)"
memory_id="$(printf '%s' "${proposal_response}" | json_field memoryId || true)"
printf '%s\n' "${proposal_response}" | pretty_json

echo
echo "4. Replay proposal with the same Idempotency-Key"
proposal_retry_response="$(post_idempotent "/api/memory/proposals" "${RUN_ID}-propose-memory" "${proposal_body}")"
proposal_retry_decision="$(printf '%s' "${proposal_retry_response}" | json_field decision)"

if [[ "${proposal_retry_decision}" != "${decision}" ]]; then
  echo "Expected replayed proposal decision ${decision}, got ${proposal_retry_decision}." >&2
  exit 1
fi

echo "Replayed proposal decision: ${proposal_retry_decision}"

echo
echo "5. Read source evidence"
get_json "/api/events/${event_id}" | pretty_json

if [[ "${decision}" == "stored" && -n "${memory_id}" ]]; then
  echo
  echo "6. Read stored memory fact"
  get_json "/api/memory/${memory_id}" | pretty_json
else
  echo
  echo "6. Broker decision was '${decision}', so there is no stored memory fact to read."
fi

context_query="How should I write the next API client example for Project A?"

echo
echo "7. Retrieve Project A CTO context"
context_response="$(curl --fail-with-body -sS --get "${BASE_URL}/api/memory/context" \
  -H "X-Api-Key: ${API_KEY}" \
  --data-urlencode "q=${context_query}" \
  --data-urlencode "scopeType=project" \
  --data-urlencode "scopeId=${PROJECT_A_ID}" \
  --data-urlencode "roleId=cto" \
  --data-urlencode "limit=12")"
printf '%s\n' "${context_response}" | pretty_json

source_pair="$(printf '%s' "${context_response}" | first_context_source || true)"

if [[ -n "${source_pair}" ]]; then
  read -r feedback_source_type feedback_source_id <<< "${source_pair}"
  feedback_body="$(cat <<JSON
{
  "query": "${context_query}",
  "targetScopeType": "project",
  "targetScopeId": "${PROJECT_A_ID}",
  "roleId": "cto",
  "sourceType": "${feedback_source_type}",
  "sourceId": "${feedback_source_id}",
  "feedbackType": "useful"
}
JSON
)"
else
  feedback_body="$(cat <<JSON
{
  "query": "${context_query}",
  "targetScopeType": "project",
  "targetScopeId": "${PROJECT_A_ID}",
  "roleId": "cto",
  "feedbackType": "missing"
}
JSON
)"
fi

echo
echo "8. Record context feedback"
post_json "/api/memory/context/feedback" "${feedback_body}" | pretty_json

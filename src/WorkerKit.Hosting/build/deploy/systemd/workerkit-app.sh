# shellcheck shell=bash
# Reads string fields from workerkit.app.json next to the published app.
# The file is generated with one property per line: two-space indent, no escaped quotes.

workerkit_manifest_path() {
  local script_dir publish_dir
  script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
  publish_dir="$(cd "${script_dir}/../.." && pwd)"
  printf '%s\n' "${publish_dir}/workerkit.app.json"
}

workerkit_manifest_get() {
  local manifest="$1"
  local key="$2"
  local line value
  if [[ ! -f "$manifest" ]]; then
    echo "Missing ${manifest}. Publish the connector so WorkerKit writes workerkit.app.json next to the app." >&2
    exit 1
  fi

  line="$(grep -E "^  \"${key}\": \"" "$manifest" | head -n 1 || true)"
  if [[ -z "$line" ]]; then
    echo "workerkit.app.json is missing \"${key}\"." >&2
    exit 1
  fi

  value="${line#*: \"}"
  value="${value%\",}"
  value="${value%\"}"
  printf '%s\n' "$value"
}

# 0 when the pid file names a running default-instance process whose executable is $2.
# A named instance (--instance-dir in the command line) is not the default instance.
# A missing file is not a trace. Anything else is stale: warn and return 1.
workerkit_pid_is_live() {
  local pidfile="$1"
  local expected="$2"
  local pid exe expected_full cmdline
  if [[ ! -f "$pidfile" ]]; then
    return 1
  fi

  pid="$(tr -d '[:space:]' < "$pidfile" || true)"
  if [[ ! "$pid" =~ ^[0-9]+$ ]] || [[ "$pid" -le 0 ]]; then
    echo "Ignoring stale instance.pid at ${pidfile} (not a process id)." >&2
    return 1
  fi

  if [[ ! -d "/proc/${pid}" ]]; then
    echo "Ignoring stale instance.pid at ${pidfile} (process ${pid} is not running)." >&2
    return 1
  fi

  if [[ ! -e "/proc/${pid}/exe" ]]; then
    echo "Ignoring stale instance.pid at ${pidfile} (could not read the path of process ${pid})." >&2
    return 1
  fi

  exe="$(readlink -f "/proc/${pid}/exe" || true)"
  expected_full="$(readlink -f "$expected" || true)"
  if [[ -z "$exe" || "$exe" != "$expected_full" ]]; then
    echo "Ignoring stale instance.pid at ${pidfile} (process ${pid} is '${exe}', not '${expected}')." >&2
    return 1
  fi

  cmdline=""
  if [[ -r "/proc/${pid}/cmdline" ]]; then
    cmdline="$(tr '\0' ' ' < "/proc/${pid}/cmdline" || true)"
  fi
  if [[ "$cmdline" == *"--instance-dir"* ]]; then
    echo "Ignoring instance.pid at ${pidfile} (process ${pid} was started with --instance-dir)." >&2
    return 1
  fi

  return 0
}

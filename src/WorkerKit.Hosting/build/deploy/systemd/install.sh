#!/usr/bin/env bash
# Installs the published host as a systemd service (Type=notify). Run as root.
# Names and the default install path come from workerkit.app.json.
# This installs the default instance only. The generated @ template stays next to this script.
#
#   sudo ./deploy/systemd/install.sh
#   sudo ./deploy/systemd/install.sh --publish-dir /tmp/publish
#   sudo ./deploy/systemd/install.sh --publish-dir /tmp/publish --install-dir /opt/my-connector
#   ./deploy/systemd/install.sh --install-dir /path/to/publish --dry-run
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=workerkit-app.sh
source "${script_dir}/workerkit-app.sh"

dry_run=0
install_dir=""
publish_dir=""

usage() {
  echo "Usage: install.sh [--install-dir DIR] [--publish-dir DIR] [--dry-run]" >&2
  exit 1
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --dry-run) dry_run=1; shift ;;
    --install-dir)
      [[ $# -ge 2 ]] || usage
      install_dir="$2"
      shift 2
      ;;
    --publish-dir)
      [[ $# -ge 2 ]] || usage
      publish_dir="$2"
      shift 2
      ;;
    -h|--help) usage ;;
    *)
      echo "Unexpected argument: $1" >&2
      usage
      ;;
  esac
done

manifest="$(workerkit_manifest_path)"
service_name="$(workerkit_manifest_get "$manifest" serviceName)"
linux_exe="$(workerkit_manifest_get "$manifest" linuxExecutable)"
assembly_file="$(workerkit_manifest_get "$manifest" assemblyFile)"
default_install="$(workerkit_manifest_get "$manifest" linuxInstallPath)"
kebab="$(workerkit_manifest_get "$manifest" kebabName)"
if [[ -z "$install_dir" ]]; then
  install_dir="$default_install"
fi

if [[ -n "$publish_dir" ]]; then
  if [[ ! -d "$publish_dir" ]]; then
    echo "Publish directory not found: $publish_dir" >&2
    exit 1
  fi
  if [[ "$publish_dir" != "$install_dir" ]]; then
    fresh=0
    if [[ ! -x "${install_dir}/${linux_exe}" && ! -f "${install_dir}/${assembly_file}" ]]; then
      fresh=1
    fi
    running=0
    if systemctl is-active --quiet "${service_name}.service"; then
      running=1
    fi
    if [[ "$running" -eq 0 ]]; then
      mapfile -t active_named < <(systemctl list-units --state=active --no-legend "${kebab}@*" 2>/dev/null | awk '{print $1}' | grep -E "^${kebab}@.+\.service$" || true)
      if [[ ${#active_named[@]} -gt 0 ]]; then
        running=1
      fi
    fi
    if [[ "$running" -eq 1 && "$fresh" -eq 0 ]]; then
      echo "Cannot copy into ${install_dir} while ${service_name} is running. Stop it first." >&2
      exit 1
    fi
    copy_excludes=(--exclude '.lock' --exclude 'instance.pid')
    if [[ -d "$install_dir" ]]; then
      shopt -s nullglob
      for settings in "${install_dir}"/appsettings*.json; do
        copy_excludes+=(--exclude "$(basename "$settings")")
      done
      shopt -u nullglob
    fi
    if [[ "$dry_run" -eq 1 ]]; then
      echo "dry-run: mkdir -p ${install_dir}"
      echo "dry-run: rsync -a ${copy_excludes[*]} ${publish_dir}/ ${install_dir}/"
    else
      mkdir -p "$install_dir"
      rsync -a "${copy_excludes[@]}" "$publish_dir"/ "$install_dir"/
    fi
  fi
fi

unit_source="${script_dir}/${service_name}.service"
if [[ ! -f "$unit_source" ]]; then
  echo "Missing unit ${unit_source}." >&2
  exit 1
fi

check_dir="$install_dir"
if [[ "$dry_run" -eq 1 && -n "$publish_dir" ]]; then
  check_dir="$publish_dir"
fi
if [[ ! -x "${check_dir}/${linux_exe}" && ! -f "${check_dir}/${assembly_file}" ]]; then
  echo "Missing ${check_dir}/${linux_exe} (or ${assembly_file}). Publish the host into ${check_dir} first, or pass --publish-dir." >&2
  exit 1
fi

unit_dest="/etc/systemd/system/${service_name}.service"
if [[ "$dry_run" -eq 1 ]]; then
  echo "dry-run: useradd --system --home ${install_dir} --shell /usr/sbin/nologin ${service_name}"
  echo "dry-run: install -d -o ${service_name} -g ${service_name} ${install_dir}/logs"
  echo "dry-run: chown -R ${service_name}:${service_name} ${install_dir}"
  echo "dry-run: install unit ${unit_source} -> ${unit_dest}"
  if [[ "$install_dir" != "$default_install" ]]; then
    echo "dry-run: rewrite ${default_install} -> ${install_dir} in the installed unit"
  fi
  echo "dry-run: systemctl daemon-reload"
  echo "dry-run: systemctl enable --now ${service_name}.service"
else
  if ! id "${service_name}" >/dev/null 2>&1; then
    useradd --system --home "${install_dir}" --shell /usr/sbin/nologin "${service_name}"
  fi
  install -d -o "${service_name}" -g "${service_name}" "${install_dir}/logs"
  chown -R "${service_name}:${service_name}" "${install_dir}"
  if [[ "$install_dir" == "$default_install" ]]; then
    install -m 0644 "${unit_source}" "${unit_dest}"
  else
    sed "s#${default_install}#${install_dir}#g" "${unit_source}" > "${unit_dest}"
    chmod 0644 "${unit_dest}"
  fi
  systemctl daemon-reload
  systemctl enable --now "${service_name}.service"
  systemctl status "${service_name}.service" --no-pager
fi

echo "Named instances: install ${script_dir}/${kebab}@.service and systemctl enable --now ${kebab}@<instance>"

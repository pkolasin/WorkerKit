#!/usr/bin/env bash
# Updates binaries in a shared install directory. Same rules as deploy/windows/update.ps1:
# instance "default" (configuration in the install directory) is refused unless --default-instance
# is set. With that flag, appsettings*.json and instance data are kept.
# Names, the executable, and the default paths come from workerkit.app.json.
#
#   sudo ./deploy/systemd/update.sh /tmp/publish
#   sudo ./deploy/systemd/update.sh /tmp/publish --default-instance
#   ./deploy/systemd/update.sh /tmp/publish --install-dir /tmp/app --dry-run
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=workerkit-app.sh
source "${script_dir}/workerkit-app.sh"

manifest="$(workerkit_manifest_path)"
service_name="$(workerkit_manifest_get "$manifest" serviceName)"
kebab="$(workerkit_manifest_get "$manifest" kebabName)"
linux_exe="$(workerkit_manifest_get "$manifest" linuxExecutable)"
assembly_file="$(workerkit_manifest_get "$manifest" assemblyFile)"

publish_dir=""
install_dir="$(workerkit_manifest_get "$manifest" linuxInstallPath)"
instances_root="$(workerkit_manifest_get "$manifest" linuxInstancesRoot)"
default_instance=0
dry_run=0
legacy_unit="${service_name}.service"

usage() {
  echo "Usage: update.sh <publish-dir> [--install-dir DIR] [--instances-root DIR] [--default-instance] [--dry-run]" >&2
  exit 1
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --default-instance) default_instance=1; shift ;;
    --dry-run) dry_run=1; shift ;;
    --install-dir)
      [[ $# -ge 2 ]] || usage
      install_dir="$2"
      shift 2
      ;;
    --instances-root)
      [[ $# -ge 2 ]] || usage
      instances_root="$2"
      shift 2
      ;;
    -h|--help) usage ;;
    *)
      if [[ -z "$publish_dir" ]]; then
        publish_dir="$1"
        shift
      else
        echo "Unexpected argument: $1" >&2
        usage
      fi
      ;;
  esac
done

if [[ -z "$publish_dir" ]]; then
  usage
fi

if [[ ! -d "$publish_dir" ]]; then
  echo "Publish directory not found: $publish_dir" >&2
  exit 1
fi

if [[ "$dry_run" -eq 1 ]]; then
  systemctl() { return 1; }
fi

legacy_default=0
legacy_active=0
if systemctl cat "$legacy_unit" >/dev/null 2>&1; then
  execstart="$(systemctl show "$legacy_unit" -p ExecStart --value || true)"
  if [[ "$execstart" != *"--instance-dir"* && "$execstart" != *"--instance "* && "$execstart" != *"--instance="* ]]; then
    legacy_default=1
  fi
  if systemctl is-active --quiet "$legacy_unit"; then
    legacy_active=1
  fi
fi

traces=()
for name in .lock logs data temp; do
  if [[ -e "$install_dir/$name" ]]; then
    traces+=("$name")
  fi
done
if workerkit_pid_is_live "$install_dir/instance.pid" "$install_dir/$linux_exe"; then
  traces+=("instance.pid")
fi

mapfile -t templated < <(systemctl list-units --all --no-legend "${kebab}@*" 2>/dev/null | awk '{print $1}' | grep -E "^${kebab}@.+\.service$" || true)
shopt -s nullglob
instance_dirs=("${instances_root}"/*/)
shopt -u nullglob
no_instances=0
if [[ ${#templated[@]} -eq 0 && ${#instance_dirs[@]} -eq 0 ]]; then
  no_instances=1
fi

uses_default=0
if [[ $legacy_default -eq 1 || ${#traces[@]} -gt 0 || $no_instances -eq 1 ]]; then
  uses_default=1
fi

if [[ $uses_default -eq 1 && $default_instance -eq 0 ]]; then
  echo "Instance 'default' (configuration in $install_dir) is in use." >&2
  echo "Legacy unit without --instance-dir: $([[ $legacy_default -eq 1 ]] && echo "$legacy_unit" || echo none)" >&2
  echo "Traces: ${traces[*]:-none}" >&2
  echo "A single-instance install is updated manually. Use --default-instance to update binaries only and keep appsettings*.json." >&2
  exit 1
fi

mapfile -t active_templated < <(systemctl list-units --state=active --no-legend "${kebab}@*" 2>/dev/null | awk '{print $1}' | grep -E "^${kebab}@.+\.service$" || true)
active_units=()
if [[ ${#active_templated[@]} -gt 0 ]]; then
  active_units=("${active_templated[@]}")
fi
if [[ $legacy_active -eq 1 ]]; then
  active_units+=("$legacy_unit")
fi

if [[ ${#active_units[@]} -gt 0 ]]; then
  if [[ "$dry_run" -eq 1 ]]; then
    echo "dry-run: systemctl stop ${active_units[*]}"
  else
    systemctl stop "${active_units[@]}"
  fi
fi

excludes=(--exclude '.lock' --exclude 'instance.pid')
if [[ $default_instance -eq 1 ]]; then
  excludes+=(--exclude 'appsettings*.json' --exclude 'logs/' --exclude 'data/' --exclude 'temp/')
fi

if [[ "$dry_run" -eq 1 ]]; then
  echo "dry-run: mkdir -p ${install_dir}"
  echo "dry-run: rsync -a --delete ${excludes[*]} ${publish_dir}/ ${install_dir}/"
  if [[ -x "$install_dir/$linux_exe" || -f "$publish_dir/$linux_exe" ]]; then
    echo "dry-run: ${install_dir}/${linux_exe} version"
  elif [[ -f "$install_dir/$assembly_file" || -f "$publish_dir/$assembly_file" ]]; then
    echo "dry-run: dotnet ${install_dir}/${assembly_file} version"
  else
    echo "dry-run: version (no host binary yet)"
  fi
else
  mkdir -p "$install_dir"
  rsync -a --delete "${excludes[@]}" "$publish_dir"/ "$install_dir"/
  if [[ -x "$install_dir/$linux_exe" ]]; then
    "$install_dir/$linux_exe" version
  elif [[ -f "$install_dir/$assembly_file" ]]; then
    dotnet "$install_dir/$assembly_file" version
  else
    echo "No host binary in $install_dir (expected ${linux_exe} or ${assembly_file})." >&2
    exit 1
  fi
fi

if [[ ${#active_units[@]} -gt 0 ]]; then
  if [[ "$dry_run" -eq 1 ]]; then
    echo "dry-run: systemctl start ${active_units[*]}"
  else
    systemctl start "${active_units[@]}"
  fi
fi

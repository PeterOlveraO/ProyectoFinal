#!/usr/bin/env bash
set -euo pipefail
cd -- "$(dirname -- "$(readlink -f -- "${BASH_SOURCE[0]}")")"

# Import the Unity Hub Flatpak profile only on the first native launch.
# An explicit XDG_CONFIG_HOME isolates QA sessions and skips this import.
if [[ -z "${XDG_CONFIG_HOME:-}" ]]; then
    source_dir="$HOME/.var/app/com.unity.UnityHub/config/unity3d/DefaultCompany/ProyectoFinal"
    destination="$HOME/.config/unity3d/DefaultCompany/ProyectoFinal"
    if [[ -f "$source_dir/pokemon-progression-v1.json" && ! -f "$destination/pokemon-progression-v1.json" ]]; then
        mkdir -p -- "$destination"
        cp -n -- "$source_dir/pokemon-progression-v1.json" "$destination/pokemon-progression-v1.json"
        if [[ -f "$source_dir/prefs" && ! -f "$destination/prefs" ]]; then
            cp -n -- "$source_dir/prefs" "$destination/prefs"
        fi
    fi
fi
exec ./PKETER.x86_64 "$@"

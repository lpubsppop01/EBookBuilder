#!/usr/bin/env bash
#
# Installs EBookBuilder into the user's own environment, and updates it in place.
#
#   ./install.sh                 install, and update from then on
#   ./install.sh --uninstall     remove everything it installed
#   ./install.sh --dry-run       show what it would do
#
# Re-running it is the update path: it pulls the source, publishes a self-contained build,
# and replaces the installed copy. Nothing is written inside the repository.
#
# ~/.profile is deliberately not sourced: a file that grows secrets and edits over time is
# easy to break, and dotnet is looked up directly instead. For the same reason the publish
# is self-contained, so that the installed app needs no DOTNET_ROOT at run time -- which is
# also why the launcher can be started straight from the GNOME application list.

set -euo pipefail

APP_NAME=ebookbuilder
APP_LABEL=EBookBuilder

log() { printf '==> %s\n' "$*"; }
warn() { printf '%s: warning: %s\n' "$APP_NAME" "$*" >&2; }
die() { printf '%s: error: %s\n' "$APP_NAME" "$*" >&2; exit 1; }

usage() {
    cat <<USAGE
Usage: $(basename "$0") [options]

Installs $APP_LABEL under \$HOME/.local, so that it starts without .NET on PATH and
appears in the GNOME application list. Running the script again updates that install.

Options:
  --prefix DIR   Install under DIR instead of \$HOME/.local
  --no-pull      Build the current checkout without running 'git pull'
  --dry-run      Print what would be done, and change nothing
  --uninstall    Remove the installed files (settings are kept)
  -h, --help     Show this message
USAGE
}

# ---------------------------------------------------------------- options

PREFIX="$HOME/.local"
prefix_given=0
do_pull=1
dry_run=0
do_uninstall=0

while [ $# -gt 0 ]; do
    case "$1" in
        --prefix)
            [ $# -ge 2 ] || { usage >&2; exit 2; }
            PREFIX="$2"; prefix_given=1; shift 2 ;;
        --prefix=*)
            PREFIX="${1#*=}"; prefix_given=1; shift ;;
        --no-pull) do_pull=0; shift ;;
        --dry-run) dry_run=1; shift ;;
        --uninstall) do_uninstall=1; shift ;;
        -h|--help) usage; exit 0 ;;
        *) usage >&2; exit 2 ;;
    esac
done

# ---------------------------------------------------------------- paths

case "$PREFIX" in
    /*) ;;
    *) PREFIX="$PWD/$PREFIX" ;;
esac
PREFIX="${PREFIX%/}"
[ -n "$PREFIX" ] || die "the install prefix is empty"

# The desktop environment looks under $XDG_DATA_HOME, so follow it unless the caller asked
# for a prefix of their own.
if [ "$prefix_given" -eq 0 ] && [ -n "${XDG_DATA_HOME:-}" ]; then
    DATA_HOME="${XDG_DATA_HOME%/}"
else
    DATA_HOME="$PREFIX/share"
fi

BIN_DIR="$PREFIX/bin"
LAUNCHER="$BIN_DIR/$APP_NAME"
LAUNCHER_TMP="$LAUNCHER.new"
PAYLOAD_DIR="$DATA_HOME/$APP_NAME"
DESKTOP_DIR="$DATA_HOME/applications"
DESKTOP_FILE="$DESKTOP_DIR/$APP_NAME.desktop"
# desktop-file-validate insists on the .desktop extension, and the leading dot keeps the
# file out of the application list while it is being written.
DESKTOP_TMP="$DESKTOP_DIR/.$APP_NAME.tmp.desktop"
ICON_FILE="$DATA_HOME/icons/hicolor/scalable/apps/$APP_NAME.svg"

# The new build is staged next to the payload, so that replacing it is a rename within one
# filesystem rather than a copy, and so that a failed build never touches the install.
BUILD_DIR="$DATA_HOME/.$APP_NAME.new.$$"
OLD_DIR="$DATA_HOME/.$APP_NAME.old.$$"

cleanup() { rm -rf -- "$BUILD_DIR" "$OLD_DIR" "$LAUNCHER_TMP" "$DESKTOP_TMP"; }
trap cleanup EXIT

# The script is normally run from the checkout, but resolve its own location so that the
# working directory does not matter and a symlink to the script keeps working.
script_path="$(readlink -f -- "${BASH_SOURCE[0]}")"
REPO_DIR="$(dirname -- "$script_path")"
[ -f "$REPO_DIR/EBookBuilder.App/EBookBuilder.App.csproj" ] \
    || die "$REPO_DIR does not look like the EBookBuilder checkout"

LAUNCHER_TEMPLATE="$REPO_DIR/packaging/$APP_NAME.launcher.in"
DESKTOP_TEMPLATE="$REPO_DIR/packaging/$APP_NAME.desktop.in"
ICON_SOURCE="$REPO_DIR/packaging/$APP_NAME.svg"

# ---------------------------------------------------------------- helpers

detect_rid() {
    case "$(uname -m)" in
        x86_64|amd64) printf 'linux-x64' ;;
        aarch64|arm64) printf 'linux-arm64' ;;
        *) die "unsupported architecture: $(uname -m)" ;;
    esac
}

find_dotnet() {
    local candidate
    for candidate in "$(command -v dotnet 2>/dev/null || true)" "$HOME/.dotnet/dotnet"; do
        if [ -n "$candidate" ] && [ -x "$candidate" ]; then
            DOTNET="$candidate"
            break
        fi
    done
    [ -n "${DOTNET:-}" ] || die "the .NET SDK was not found (looked on PATH and at \$HOME/.dotnet/dotnet)"

    # The SDK is installed under $HOME/.dotnet on this machine, and its own child processes
    # need DOTNET_ROOT. It is set here rather than by sourcing ~/.profile.
    if [ "$DOTNET" = "$HOME/.dotnet/dotnet" ]; then
        export DOTNET_ROOT="$HOME/.dotnet"
        export PATH="$HOME/.dotnet:$PATH"
    fi
    export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
}

check_sdk() {
    local sdks
    sdks="$("$DOTNET" --list-sdks 2>/dev/null || true)"
    if ! printf '%s\n' "$sdks" | grep -q '^10\.'; then
        die "the .NET 10 SDK is required; '$DOTNET --list-sdks' reported:
${sdks:-  (nothing)}"
    fi
}

# Writes a template with its @NAME@ placeholders replaced. The replacements use shell
# expansion rather than sed, so that a path containing & or \ survives intact.
render_template() {
    local template="$1" destination="$2"
    shift 2
    local content pair name value
    content="$(<"$template")"
    for pair in "$@"; do
        name="${pair%%=*}"
        value="${pair#*=}"
        content="${content//"$name"/"$value"}"
    done
    printf '%s\n' "$content" > "$destination"
}

pull_source() {
    if [ ! -d "$REPO_DIR/.git" ]; then
        warn "$REPO_DIR is not a git checkout; building what is here"
        return 0
    fi
    # Only tracked changes are considered: an untracked file does not stop a fast-forward,
    # and if one would be overwritten the pull fails and is reported below.
    if [ -n "$(git -C "$REPO_DIR" status --porcelain --untracked-files=no)" ]; then
        warn "the working tree has local changes; skipping the update"
        return 0
    fi
    if ! git -C "$REPO_DIR" rev-parse --abbrev-ref --symbolic-full-name '@{u}' >/dev/null 2>&1; then
        warn "the current branch has no upstream; skipping the update"
        return 0
    fi

    local before after
    before="$(git -C "$REPO_DIR" rev-parse HEAD)"
    log "updating the source"
    # A failed pull is not fatal: the current checkout still builds, and --no-pull exists
    # for when the network is not wanted at all.
    if ! git -C "$REPO_DIR" pull --ff-only; then
        warn "'git pull --ff-only' failed; building the current checkout"
        return 0
    fi
    after="$(git -C "$REPO_DIR" rev-parse HEAD)"
    if [ "$before" = "$after" ]; then
        printf '    already up to date (%s)\n' "${after:0:9}"
    else
        printf '    %s -> %s\n' "${before:0:9}" "${after:0:9}"
    fi
}

publish_app() {
    log "publishing a self-contained build for $RID"
    rm -rf -- "$BUILD_DIR"
    "$DOTNET" publish "$REPO_DIR/EBookBuilder.App/EBookBuilder.App.csproj" \
        -c Release -r "$RID" --self-contained true -o "$BUILD_DIR"

    # Never replace a working install with an incomplete publish.
    local required
    for required in "$APP_NAME" "$APP_NAME.dll" "$APP_NAME.deps.json" libhostfxr.so libSkiaSharp.so; do
        [ -e "$BUILD_DIR/$required" ] || die "the publish output is missing $required"
    done
    printf '    %s in %s\n' "$(du -sh -- "$BUILD_DIR" | cut -f1)" "$BUILD_DIR"
}

swap_in() {
    if pgrep -x "$APP_NAME" >/dev/null 2>&1; then
        warn "$APP_LABEL is running; restart it to pick up the new build"
    fi
    rm -rf -- "$OLD_DIR"
    if [ -e "$PAYLOAD_DIR" ]; then
        mv -T -- "$PAYLOAD_DIR" "$OLD_DIR"
    fi
    # Both directories are under $DATA_HOME, so this is a rename: the installed copy is
    # never missing, not even for an instant.
    mv -T -- "$BUILD_DIR" "$PAYLOAD_DIR"
    rm -rf -- "$OLD_DIR"
}

install_launcher() {
    install -d -- "$BIN_DIR"
    if [ -e "$LAUNCHER" ] && ! grep -q 'install\.sh' -- "$LAUNCHER"; then
        warn "$LAUNCHER was not written by this script, and is being replaced"
    fi
    render_template "$LAUNCHER_TEMPLATE" "$LAUNCHER_TMP" "@PAYLOAD_DIR@=$PAYLOAD_DIR"
    install -m 755 -- "$LAUNCHER_TMP" "$LAUNCHER"
}

install_icon() {
    install -d -- "$(dirname -- "$ICON_FILE")"
    install -m 644 -- "$ICON_SOURCE" "$ICON_FILE"
}

install_desktop_file() {
    install -d -- "$DESKTOP_DIR"
    render_template "$DESKTOP_TEMPLATE" "$DESKTOP_TMP" \
        "@BIN@=$LAUNCHER" "@ICON@=$ICON_FILE"
    if command -v desktop-file-validate >/dev/null 2>&1; then
        desktop-file-validate "$DESKTOP_TMP" || die "the generated desktop entry is invalid"
    fi
    mv -f -- "$DESKTOP_TMP" "$DESKTOP_FILE"
}

# The .desktop file is enough for the application list, but mimeinfo.cache is what makes
# the "Open With" entry for a folder visible to GIO.
refresh_desktop_database() {
    command -v update-desktop-database >/dev/null 2>&1 || return 0
    [ -d "$DESKTOP_DIR" ] || return 0
    update-desktop-database "$DESKTOP_DIR" >/dev/null 2>&1 \
        || warn "update-desktop-database failed"
}

was_written_here() {
    [ -e "$1" ] && grep -q 'install\.sh' -- "$1"
}

uninstall() {
    log "removing $APP_LABEL"
    if [ -e "$LAUNCHER" ] && ! was_written_here "$LAUNCHER"; then
        warn "leaving $LAUNCHER alone: it was not written by this script"
    else
        rm -f -- "$LAUNCHER"
    fi
    rm -f -- "$DESKTOP_FILE" "$ICON_FILE"
    rm -rf -- "$PAYLOAD_DIR" "$BUILD_DIR" "$OLD_DIR"
    refresh_desktop_database

    cat <<EOF
    removed:
      $LAUNCHER
      $DESKTOP_FILE
      $ICON_FILE
      $PAYLOAD_DIR
    kept:
      $HOME/.config/EBookBuilder/settings.json
EOF
}

show_plan() {
    cat <<EOF
$APP_LABEL would be installed as:

  $PAYLOAD_DIR/
      the published application ($RID, self-contained)
  $LAUNCHER
      launcher, for a terminal or the application list
  $DESKTOP_FILE
      application list entry
  $ICON_FILE
      icon

from the checkout at $REPO_DIR, by running:

  dotnet publish EBookBuilder.App/EBookBuilder.App.csproj -c Release -r $RID --self-contained true

which is staged in $BUILD_DIR and then renamed into place.
EOF
}

print_summary() {
    log "$APP_LABEL is installed"
    cat <<EOF

    application   $PAYLOAD_DIR ($(du -sh -- "$PAYLOAD_DIR" | cut -f1))
    launcher      $LAUNCHER
    entry         $DESKTOP_FILE
    icon          $ICON_FILE

Start it from the application list, or from a terminal:

    $APP_NAME [folder]

A folder given on the command line is opened at startup. Settings are kept in
$HOME/.config/EBookBuilder/settings.json.

Run this script again to update, or './install.sh --uninstall' to remove the install.
EOF
}

# ---------------------------------------------------------------- main

if [ "$do_uninstall" -eq 1 ]; then
    if [ "$dry_run" -eq 1 ]; then
        printf 'would remove:\n  %s\n  %s\n  %s\n  %s\n' \
            "$LAUNCHER" "$DESKTOP_FILE" "$ICON_FILE" "$PAYLOAD_DIR"
        exit 0
    fi
    uninstall
    exit 0
fi

RID="$(detect_rid)"

if [ "$dry_run" -eq 1 ]; then
    show_plan
    exit 0
fi

find_dotnet
check_sdk

install -d -- "$DATA_HOME"
if [ "$do_pull" -eq 1 ]; then
    pull_source
else
    log "building the current checkout (--no-pull)"
fi

publish_app
swap_in
install_launcher
install_icon
# The entry is written last, so that the shell never sees one whose icon is not there yet.
install_desktop_file
refresh_desktop_database

print_summary

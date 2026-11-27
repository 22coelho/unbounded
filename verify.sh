#!/usr/bin/env bash
# verify.sh -- the project's definition of done, in one command.
#
#   ./verify.sh            rebuild the scene, then run both test suites
#   ./verify.sh scene      rebuild Assets/_Project/Scenes/Main.unity
#   ./verify.sh tests      run the EditMode and PlayMode suites
#   ./verify.sh editmode   run the fast suite (the pre-push hook runs this)
#   ./verify.sh playmode   run the suite that drives the circuit
#   ./verify.sh capture    record a clip from the built macOS player
#
# Unity is pinned to 6000.6.9f1. Override the binary with UNITY=/path/to/Unity.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
UNITY="${UNITY:-/Applications/Unity/Hub/Editor/6000.6.9f1/Unity.app/Contents/MacOS/Unity}"
LOGS="$ROOT/Logs/verify"
mkdir -p "$LOGS"

if [ ! -x "$UNITY" ]; then
  echo "verify: Unity not found at $UNITY" >&2
  echo "        install 6000.6.9f1, or set UNITY=/path/to/Unity" >&2
  exit 1
fi

usage() {
  sed -n '2,10p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
}

run_scene() {
  echo "==> building the main scene"
  "$UNITY" -batchmode -quit -projectPath "$ROOT" \
    -executeMethod Unbounded.Editor.MainSceneBuilder.Build \
    -logFile "$LOGS/scene.log"
  if ! git -C "$ROOT" diff --quiet -- Assets/_Project/Scenes/Main.unity; then
    echo "    Main.unity is generated: inspect with Tools/scene-diff, discard with"
    echo "    git checkout -- Assets/_Project/Scenes/Main.unity"
  fi
  echo "    ok (log: Logs/verify/scene.log)"
}

run_tests() {
  local platform="$1"
  local xml="$LOGS/$platform.xml"
  local status=0

  rm -f "$xml"
  echo "==> $platform tests"
  "$UNITY" -batchmode -projectPath "$ROOT" -runTests \
    -testPlatform "$platform" -testResults "$xml" \
    -logFile "$LOGS/$platform.log" || status=$?

  if [ ! -f "$xml" ]; then
    echo "    no results written -- see Logs/verify/$platform.log" >&2
    return 1
  fi

  local total failed
  total="$(sed -n 's/.*<test-run[^>]* total="\([0-9]*\)".*/\1/p' "$xml" | head -n 1)"
  failed="$(sed -n 's/.*<test-run[^>]* failed="\([0-9]*\)".*/\1/p' "$xml" | head -n 1)"

  if [ "$status" -ne 0 ] || [ "${failed:-1}" -ne 0 ]; then
    echo "    FAILED: ${failed:-?} of ${total:-?} (log: Logs/verify/$platform.log)" >&2
    return 1
  fi

  echo "    ok: ${total:-?} passed"
}

run_capture() {
  local app="$ROOT/Builds/macOS/Unbounded.app/Contents/MacOS/Unbounded"
  local out="${CAPTURE_DIR:-$LOGS/capture}"
  local seconds="${CAPTURE_SECONDS:-10}"
  local session=$((seconds / 60 + 2))

  if [ ! -x "$app" ]; then
    echo "verify: no macOS player at Builds/macOS/Unbounded.app" >&2
    echo "        build it with Unbounded > Build macOS Player first" >&2
    exit 1
  fi

  rm -rf "$out"
  echo "==> capturing ${seconds}s of gameplay to $out"
  "$app" -captureClip -capturePath "$out" \
    -captureSeconds "$seconds" -sessionMinutes "$session"

  local frames
  frames="$(find "$out" -name 'frame-*.jpg' 2>/dev/null | wc -l | tr -d ' ')"
  if [ "$frames" -eq 0 ]; then
    echo "    no frames captured -- see the player log" >&2
    return 1
  fi
  echo "    ok: $frames frames"
}

case "${1:-all}" in
  all)
    run_scene
    run_tests EditMode
    run_tests PlayMode
    ;;
  scene) run_scene ;;
  tests)
    run_tests EditMode
    run_tests PlayMode
    ;;
  editmode|EditMode) run_tests EditMode ;;
  playmode|PlayMode) run_tests PlayMode ;;
  capture) run_capture ;;
  -h|--help|help) usage ;;
  *)
    usage >&2
    exit 2
    ;;
esac

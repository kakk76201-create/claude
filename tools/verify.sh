#!/bin/bash
# Проверка проекта без окна Unity. Шаги: scene edit play player net
#   bash tools/verify.sh scene edit play      — пересобрать сцену и прогнать тесты
#   bash tools/verify.sh player net           — собрать игру и сыграть матч двумя копиями по сети
# Unity-проект не должен быть открыт в редакторе (иначе он заблокирован).
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY="${UNITY:-/c/Program Files/Unity/Hub/Editor/6000.6.3f1/Editor/Unity.exe}"
PROJ="$ROOT/MakeupSniper"
OUT="${OUT:-$ROOT/Builds/verify}"
mkdir -p "$OUT"
for step in "$@"; do
  case $step in
    scene)
      "$UNITY" -batchmode -quit -projectPath "$PROJ" -executeMethod MakeupSniper.EditorTools.SceneBuilder.Build -logFile "$OUT/build.log" > /dev/null 2>&1
      echo "SCENE exit=$?"; grep -E "error CS|Нет поля|Exception:" "$OUT/build.log" | sort -u | head -20 ;;
    edit|play)
      platform=EditMode; [ "$step" = play ] && platform=PlayMode
      rm -f "$OUT/$step-results.xml"
      "$UNITY" -batchmode -projectPath "$PROJ" -runTests -testPlatform $platform -testResults "$OUT/$step-results.xml" -logFile "$OUT/$step.log" > /dev/null 2>&1
      echo "$platform exit=$? $(grep -o '<test-run [^>]*>' "$OUT/$step-results.xml" | grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*"')"
      grep -o '<message><!\[CDATA\[[^]]*' "$OUT/$step-results.xml" | grep -v "child tests" | head -8
      grep -E "error CS" "$OUT/$step.log" | sort -u | head ;;
    player)
      "$UNITY" -batchmode -quit -projectPath "$PROJ" -executeMethod MakeupSniper.EditorTools.BuildTools.BuildWindows -logFile "$OUT/player.log" > /dev/null 2>&1
      echo "PLAYER exit=$?"; grep -E "error CS|\[MakeupSniper\]|Build Finished" "$OUT/player.log" | sort -u | head ;;
    net)
      bash "$ROOT/tools/net-test.sh" | grep -E "CODE=|REVEAL|SUMMARY|CHECK|DONE|FAIL|VISION|SPLAT" ;;
  esac
done

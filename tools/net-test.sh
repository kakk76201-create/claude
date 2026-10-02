#!/bin/bash
# Сетевой автотест: копии собранной игры на одном компьютере.
# Хост создаёт игру, гости входят по коду, каждый по разу сидит в кресле Модели.
# PLAYERS=3 — третий игрок забрызгивает другого стрелка из базуки (проверка «видения»).
# В отчётах REVEAL строка hash= должна совпасть у всех (лицо одинаковое у всех).
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
EXE="$ROOT/Builds/MakeupSniper_Windows/MakeupSniper.exe"
OUT="${OUT:-$ROOT/Builds/verify}/net"
PLAYERS="${PLAYERS:-2}"
EXTRA=""; [ "$PLAYERS" = 3 ] && EXTRA="-ms-expect-splat"
rm -rf "$OUT"; mkdir -p "$OUT"
"$EXE" -batchmode -ms-autotest host -ms-players "$PLAYERS" $EXTRA -ms-out "$OUT/host.log" -logFile "$OUT/host-player.log" > /dev/null 2>&1 &
PIDS="$!"
for i in $(seq 1 120); do [ -f "$OUT/host.log.code" ] && break; sleep 0.5; done
if [ ! -f "$OUT/host.log.code" ]; then echo "FAIL no code"; cat "$OUT/host.log" 2>/dev/null; kill $PIDS 2>/dev/null; exit 1; fi
CODE=$(cat "$OUT/host.log.code"); echo "CODE=$CODE PLAYERS=$PLAYERS"
"$EXE" -batchmode -ms-autotest join -ms-code "$CODE" $EXTRA -ms-out "$OUT/join.log" -logFile "$OUT/join-player.log" > /dev/null 2>&1 &
PIDS="$PIDS $!"
if [ "$PLAYERS" = 3 ]; then
  sleep 2
  mkdir -p "$OUT/j2"
  "$EXE" -batchmode -ms-autotest join -ms-code "$CODE" -ms-splat 1 $EXTRA -ms-out "$OUT/j2/join2.log" -logFile "$OUT/join2-player.log" > /dev/null 2>&1 &
  PIDS="$PIDS $!"
fi
for i in $(seq 1 900); do
  alive=0; for p in $PIDS; do kill -0 $p 2>/dev/null && alive=1; done
  [ $alive = 0 ] && break
  sleep 0.5
done
kill $PIDS 2>/dev/null
echo "=== HOST"; cat "$OUT/host.log"
echo "=== JOIN"; cat "$OUT/join.log"
[ "$PLAYERS" = 3 ] && { echo "=== JOIN2"; cat "$OUT/j2/join2.log"; }
h1=$(grep REVEAL "$OUT/host.log" | grep -o "hash=[0-9]*" | tr '\n' ' ')
h2=$(grep REVEAL "$OUT/join.log" | grep -o "hash=[0-9]*" | tr '\n' ' ')
ok=1; [ -n "$h1" ] && [ "$h1" = "$h2" ] || ok=0
if [ "$PLAYERS" = 3 ]; then h3=$(grep REVEAL "$OUT/j2/join2.log" | grep -o "hash=[0-9]*" | tr '\n' ' '); [ "$h1" = "$h3" ] || ok=0; fi
if [ $ok = 1 ]; then echo "CHECK hashes match: $h1"; else echo "FAIL hashes differ: host[$h1] join[$h2] join2[$h3]"; fi

#!/usr/bin/env bash
# Equivalent of Tree.bat for Linux (fixed: use -gui, not --gui)
# Usage: ./tree.sh [Grammar.g4] [StartRule] [InputFile] [-gui|-tree]
set -euo pipefail

GRAMMAR_FILE=${1:-CryptoScript.g4}
START_RULE=${2:-program}
INPUT_FILE=${3:-input.txt}
FLAG=${4:--gui}

# normalize flag
if [[ "$FLAG" == "--gui" ]]; then
  FLAG="-gui"
fi

# quick sanity checks
if ! command -v antlr4-parse >/dev/null 2>&1; then
  echo "antlr4-parse not found in PATH"
  exit 1
fi

# try to detect headless Java; ignore errors if property missing
if java -XshowSettings:properties -version 2>&1 | grep -q "java.awt.headless = true"; then
  echo "Warnung: Java läuft im Headless-Modus. Installiere eine GUI-JRE (z.B. 'sudo apt install openjdk-17-jre')."
fi

antlr4-parse "$GRAMMAR_FILE" "$START_RULE" "$FLAG" "$INPUT_FILE"

#!/usr/bin/env bash
# Equivalent of CompileGrammar.bat for Linux
# Usage: ./compile-grammar.sh [Grammar.g4]
set -euo pipefail

GRAMMAR_FILE=${1:-CryptoScript.g4}

# Run ANTLR4 to generate C# lexer/parser with visitor pattern
antlr4 -Dlanguage=CSharp -visitor "$GRAMMAR_FILE"
echo "Generated C# sources for: $GRAMMAR_FILE"

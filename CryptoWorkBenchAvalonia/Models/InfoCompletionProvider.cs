using CryptoScript.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CryptoWorkBenchAvalonia.Models;

public sealed record InfoCompletionContext(
    int InvocationStartOffset,
    int ArgumentStartOffset,
    int CaretOffset,
    int ArgumentEndOffset,
    string Prefix);

public sealed class InfoCompletionProvider
{
    private static readonly (string Text, string Description)[] ListCommands =
    {
        ("types", "Lists all supported CryptoScript types."),
        ("functions", "Lists all supported CryptoScript functions."),
        ("mechanisms", "Lists all supported cryptographic mechanisms."),
        ("parameters", "Lists all supported CryptoScript parameters.")
    };

    public IReadOnlyList<string> GetCompletionItems(string text, int caretOffset)
    {
        if (!TryGetContext(text, caretOffset, out _))
            return Array.Empty<string>();

        return GetAllCompletionItems();
    }

    public IReadOnlyList<string> GetMatchingCompletionItems(InfoCompletionContext context) =>
        GetAllCompletionItems()
            .Where(item => MatchesPrefix(item, context.Prefix))
            .ToArray();

    public string GetDescription(string completionText)
    {
        foreach (var command in ListCommands)
        {
            if (string.Equals(
                    command.Text,
                    completionText,
                    StringComparison.Ordinal))
            {
                return command.Description;
            }
        }

        return completionText;
    }

    public bool TryGetPrefix(string text, int caretOffset, out string prefix)
    {
        bool found = TryGetContext(text, caretOffset, out InfoCompletionContext? context);
        prefix = found ? context.Prefix : string.Empty;
        return found;
    }

    public bool TryGetContext(
        string text,
        int caretOffset,
        out InfoCompletionContext context)
    {
        context = null!;
        if (caretOffset < 0 || caretOffset > text.Length)
            return false;

        int openingParenthesis = caretOffset == 0
            ? -1
            : text.LastIndexOf('(', caretOffset - 1);
        if (openingParenthesis < 0)
            return false;

        if (GetLexicalContext(text, openingParenthesis) != LexicalContext.Code)
            return false;

        string prefix = text.Substring(
            openingParenthesis + 1,
            caretOffset - openingParenthesis - 1);
        if (!prefix.All(IsArgumentCharacter))
        {
            return false;
        }

        int functionNameEnd = openingParenthesis - 1;
        while (functionNameEnd >= 0 && char.IsWhiteSpace(text[functionNameEnd]))
            functionNameEnd--;

        const string functionName = "Info";
        int functionNameStart = functionNameEnd - functionName.Length + 1;
        if (functionNameStart < 0 ||
            !text.AsSpan(functionNameStart, functionName.Length).SequenceEqual(functionName))
        {
            return false;
        }

        bool hasIdentifierCharacterBeforeName = functionNameStart > 0 &&
            (char.IsAsciiLetterOrDigit(text[functionNameStart - 1]) ||
             text[functionNameStart - 1] == '_');
        if (hasIdentifierCharacterBeforeName)
            return false;

        int argumentEndOffset = caretOffset;
        while (argumentEndOffset < text.Length &&
               IsArgumentCharacter(text[argumentEndOffset]))
        {
            argumentEndOffset++;
        }

        context = new InfoCompletionContext(
            functionNameStart,
            openingParenthesis + 1,
            caretOffset,
            argumentEndOffset,
            prefix);
        return true;
    }

    private static bool IsArgumentCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || character == '-';

    private static LexicalContext GetLexicalContext(string text, int offset)
    {
        bool insideString = false;
        for (int index = 0; index < offset; index++)
        {
            char character = text[index];
            if (!insideString)
            {
                if (IsPathValueStart(text, index, offset))
                {
                    while (index + 1 < offset &&
                           text[index + 1] != '\r' &&
                           text[index + 1] != '\n')
                    {
                        index++;
                    }

                    if (index + 1 == offset)
                        return LexicalContext.PathValue;

                    continue;
                }

                if (character == '"')
                    insideString = true;

                continue;
            }

            if (character == '\\' &&
                index + 1 < offset &&
                IsNormalStringEscapeCharacter(text[index + 1]))
            {
                index++;
                continue;
            }

            if (character == '"')
                insideString = false;
        }

        return insideString
            ? LexicalContext.NormalString
            : LexicalContext.Code;
    }

    private enum LexicalContext
    {
        Code,
        NormalString,
        PathValue
    }

    private static bool IsNormalStringEscapeCharacter(char character) =>
        character is 'b' or 't' or 'n' or 'r' or 'f' or '"' or '\'' or '\\';

    private static bool IsPathValueStart(string text, int index, int offset) =>
        text[index] == '/' ||
        index + 1 < offset &&
        (char.IsAsciiLetter(text[index]) &&
         text[index + 1] == ':' &&
         (index == 0 || !IsIdentifierCharacter(text[index - 1])) ||
         text[index] == '\\' && text[index + 1] == '\\');

    private static bool IsIdentifierCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || character == '_';

    private static IReadOnlyList<string> GetAllCompletionItems() =>
        ListCommands.Select(command => command.Text)
            .Concat(MechanismList.Instance.Mechanisms)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    public static bool MatchesPrefix(string completionText, string prefix) =>
        completionText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
}

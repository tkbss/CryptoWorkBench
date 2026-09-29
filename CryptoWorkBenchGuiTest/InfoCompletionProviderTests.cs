using CryptoScript.Model;
using CryptoWorkBenchAvalonia.Models;
using CryptoWorkBenchAvalonia.Services;
using AvaloniaEdit.Document;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

public class InfoCompletionProviderTests
{
    private readonly InfoCompletionProvider _provider = new();

    [Test]
    public void InfoStartOffersListCommands()
    {
        IReadOnlyList<string> items = GetItems("Info(");

        Assert.That(items, Does.Contain("types"));
        Assert.That(items, Does.Contain("functions"));
        Assert.That(items, Does.Contain("mechanisms"));
        Assert.That(items, Does.Contain("parameters"));
    }

    [Test]
    public void InfoStartOffersEveryMechanismFromLanguageMetadata()
    {
        IReadOnlyList<string> items = GetItems("Info(");

        Assert.That(items, Is.SupersetOf(MechanismList.Instance.Mechanisms));
        Assert.That(
            items.Except(new[] { "types", "functions", "mechanisms", "parameters" }),
            Is.EqualTo(MechanismList.Instance.Mechanisms));
    }

    [TestCase("WRAP-AES")]
    [TestCase("WRAP-DES3")]
    [TestCase("RSA-PSS")]
    [TestCase("RSA-OAEP")]
    [TestCase("ECDSA")]
    public void InfoCompletionDoesNotOfferRoadmapMechanisms(string mechanism)
    {
        Assert.That(GetItems("Info("), Does.Not.Contain(mechanism));
    }

    [TestCase("Info(A", "A")]
    [TestCase("Info(AES", "AES")]
    [TestCase("Info(DES3-", "DES3-")]
    [TestCase("Info(DUKPT-", "DUKPT-")]
    [TestCase("    Info(", "")]
    [TestCase("Info (", "")]
    [TestCase("Info (AES", "AES")]
    [TestCase("Info(types) Info(", "")]
    [TestCase("Info(types) Info(DES3-", "DES3-")]
    [TestCase("prefix Info(A", "A")]
    [TestCase("VAR value=\"Info(\" Info(", "")]
    [TestCase("VAR value=\"preceding text\"\nInfo(A", "A")]
    [TestCase("Info(types)\n\n    Info(DUKPT-", "DUKPT-")]
    public void RecognizesSupportedCompletionPrefixes(string text, string expectedPrefix)
    {
        bool found = _provider.TryGetPrefix(text, text.Length, out string prefix);

        Assert.That(found, Is.True);
        Assert.That(prefix, Is.EqualTo(expectedPrefix));
    }

    [Test]
    public void ContextContainsArgumentStartCaretAndPrefix()
    {
        const string text = "Info(types) Info(AES-EC)";
        int caretOffset = text.IndexOf(')', text.IndexOf("AES", StringComparison.Ordinal));

        bool found = _provider.TryGetContext(text, caretOffset, out var context);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(context.InvocationStartOffset, Is.EqualTo(text.LastIndexOf("Info", StringComparison.Ordinal)));
            Assert.That(context.ArgumentStartOffset, Is.EqualTo(text.IndexOf("AES", StringComparison.Ordinal)));
            Assert.That(context.CaretOffset, Is.EqualTo(caretOffset));
            Assert.That(context.ArgumentEndOffset, Is.EqualTo(caretOffset));
            Assert.That(context.Prefix, Is.EqualTo("AES-EC"));
        });
    }

    [Test]
    public void ContextSeparatesFilterPrefixFromTheCompleteArgumentRange()
    {
        const string text = "Info(DES3-ECB)";
        int caretOffset = text.IndexOf("-ECB", StringComparison.Ordinal);

        bool found = _provider.TryGetContext(text, caretOffset, out var context);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(context.Prefix, Is.EqualTo("DES3"));
            Assert.That(context.CaretOffset, Is.EqualTo(caretOffset));
            Assert.That(
                text.Substring(
                    context.ArgumentStartOffset,
                    context.ArgumentEndOffset - context.ArgumentStartOffset),
                Is.EqualTo("DES3-ECB"));
            Assert.That(text[context.ArgumentEndOffset], Is.EqualTo(')'));
        });
    }

    [Test]
    public void ReplacingCompletionAfterEditingInTheMiddleDoesNotDuplicateTheSuffix()
    {
        string editedText = "Info(AES-ECB)";
        int caretOffset = editedText.IndexOf("-ECB", StringComparison.Ordinal);
        for (int index = 0; index < "AES".Length; index++)
        {
            editedText = editedText.Remove(caretOffset - 1, 1);
            caretOffset--;
        }

        editedText = editedText.Insert(caretOffset, "DES3");
        caretOffset += "DES3".Length;
        _provider.TryGetContext(editedText, caretOffset, out var context);

        string completedText = ReplaceArgument(
            editedText,
            context,
            "DES3-ECB");

        Assert.Multiple(() =>
        {
            Assert.That(editedText, Is.EqualTo("Info(DES3-ECB)"));
            Assert.That(context.Prefix, Is.EqualTo("DES3"));
            Assert.That(completedText, Is.EqualTo("Info(DES3-ECB)"));
        });
    }

    [Test]
    public void AcceptedCompletionCanReopenAfterBackspaceInsideAClosedCall()
    {
        var document = new TextDocument("Info(AES-ECB)");
        int caretOffset = document.Text.IndexOf('B');
        var policy = new InfoCompletionReopenPolicy();
        _provider.TryGetContext(document.Text, caretOffset, out var acceptedContext);
        policy.MarkAccepted(
            document,
            acceptedContext.InvocationStartOffset,
            acceptedContext.ArgumentStartOffset);

        document.Remove(caretOffset - 1, 1);
        caretOffset--;

        bool found = _provider.TryGetContext(
            document.Text,
            caretOffset,
            out var context);
        bool shouldReopen = policy.ShouldReopen(
            document,
            context,
            caretOffset,
            removalLength: 1,
            insertionLength: 0);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(context.Prefix, Is.EqualTo("AES-E"));
            Assert.That(context.ArgumentEndOffset, Is.GreaterThan(caretOffset));
            Assert.That(document.Text[context.ArgumentEndOffset], Is.EqualTo(')'));
            Assert.That(shouldReopen, Is.True);
        });
    }

    [Test]
    public void MatchingItemsUseContextPrefixImmediately()
    {
        const string text = "Info(AES-EC)";
        int caretOffset = text.IndexOf(')');
        _provider.TryGetContext(text, caretOffset, out var context);

        IReadOnlyList<string> matches = _provider.GetMatchingCompletionItems(context);

        Assert.That(matches, Is.EqualTo(new[] { "AES-ECB" }));
    }

    [TestCase("Info(")]
    [TestCase("Info(f")]
    [TestCase("Info(AES-")]
    public void MatchingItemsRespectTheCurrentContextPrefix(string text)
    {
        _provider.TryGetContext(text, text.Length, out var context);

        IReadOnlyList<string> matches = _provider.GetMatchingCompletionItems(context);

        Assert.That(
            matches,
            Has.All.Matches<string>(item =>
                item.StartsWith(context.Prefix, StringComparison.OrdinalIgnoreCase)));
        if (context.Prefix == "f")
            Assert.That(matches, Is.EqualTo(new[] { "functions" }));
    }

    [TestCase("")]
    [TestCase("AES-CBC")]
    [TestCase("Print(A")]
    [TestCase("Info(AES-CBC)")]
    [TestCase("Info(AES CBC")]
    public void OutsideInfoArgumentOffersNoCompletion(string text)
    {
        Assert.That(GetItems(text), Is.Empty);
    }

    [TestCase("Print(\"Info(")]
    [TestCase("VAR value=\"text before Info(")]
    [TestCase("VAR value=\"first line\nInfo(")]
    public void InfoInsideNormalStringOffersNoCompletion(string text)
    {
        Assert.That(GetItems(text), Is.Empty);
    }

    [Test]
    public void EscapedQuoteDoesNotEndNormalString()
    {
        const string text = "Print(\"escaped \\\" quote Info(";

        Assert.That(GetItems(text), Is.Empty);
    }

    [Test]
    public void RegularInfoAfterEscapedQuoteAndClosedStringIsRecognized()
    {
        const string text = "Print(\"escaped \\\" quote\")\nInfo (";

        bool found = _provider.TryGetPrefix(text, text.Length, out string prefix);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(prefix, Is.Empty);
        });
    }

    [Test]
    public void EscapedBackslashAllowsFollowingQuoteToCloseString()
    {
        const string text = """
            Print("escaped \\")
            Info(
            """;

        Assert.That(GetItems(text), Is.Not.Empty);
    }

    [TestCase("PATH p=/tmp/a\"b\nVAR s=\"Info(")]
    [TestCase("PATH p=C:\\tmp\\a\"b\nVAR s=\"Info(")]
    [TestCase("PATH p=\\\\server\\share\"name\nVAR s=\"Info(")]
    public void QuotesInsidePathValuesDoNotCloseFollowingNormalString(string text)
    {
        Assert.That(GetItems(text), Is.Empty);
    }

    [TestCase("PATH p=/tmp/Info(")]
    [TestCase("PATH p=C:\\tmp\\Info(")]
    [TestCase("PATH p=\\\\server\\share\\Info(")]
    public void InfoInsidePathValueDoesNotOfferCompletion(string text)
    {
        Assert.That(GetItems(text), Is.Empty);
    }

    [TestCase("PATH p=/tmp/a\"b\nInfo (")]
    [TestCase("PATH p=C:\\tmp\\a\"b\nInfo (")]
    [TestCase("PATH p=\\\\server\\share\"name\nInfo (")]
    public void QuoteInsidePathValueDoesNotHideInfoOnFollowingLine(string text)
    {
        Assert.That(GetItems(text), Is.Not.Empty);
    }

    [Test]
    public void EscapedQuoteInsideMultilineNormalStringKeepsInfoInsideString()
    {
        const string text = "VAR s=\"first line\nescaped \\\" quote Info(";

        Assert.That(GetItems(text), Is.Empty);
    }

    [Test]
    public void InfoAfterClosedMultilineStringWithEscapeIsRecognized()
    {
        const string text = "VAR s=\"first line\nescaped \\\" quote\"\nInfo (";

        Assert.That(GetItems(text), Is.Not.Empty);
    }

    [Test]
    public void MixedCaseArgumentPrefixUsesCaseInsensitiveMatching()
    {
        const string text = "Info(aEs-e";
        _provider.TryGetContext(text, text.Length, out var context);

        IReadOnlyList<string> matches = _provider.GetMatchingCompletionItems(context);

        Assert.Multiple(() =>
        {
            Assert.That(context.Prefix, Is.EqualTo("aEs-e"));
            Assert.That(matches, Does.Contain("AES-ECB"));
            Assert.That(
                matches,
                Has.All.Matches<string>(item =>
                    item.StartsWith("aEs-e", StringComparison.OrdinalIgnoreCase)));
        });
    }

    [Test]
    public void UsesCaretPositionInsideLongerDocumentAndIgnoresTextToTheRight()
    {
        const string text = "Info(types)\nInfo(AES-CBC)\nInfo(DES3-CBC)";
        int caretOffset = text.IndexOf("AES-CBC", StringComparison.Ordinal) + "AES".Length;

        bool found = _provider.TryGetPrefix(text, caretOffset, out string prefix);

        Assert.That(found, Is.True);
        Assert.That(prefix, Is.EqualTo("AES"));
    }

    [TestCase(-1)]
    [TestCase(6)]
    public void InvalidCaretOffsetOffersNoCompletion(int caretOffset)
    {
        Assert.That(_provider.GetCompletionItems("Info(", caretOffset), Is.Empty);
    }

    [TestCase("Info(A", "AES-CBC")]
    [TestCase("Info(AES-C", "AES-CBC")]
    [TestCase("Info(DES3-", "DES3-CBC")]
    public void IncrementalPrefixesKeepProvidingCompletionCandidates(
        string text,
        string expectedCandidate)
    {
        IReadOnlyList<string> items = GetItems(text);

        Assert.That(items, Does.Contain(expectedCandidate));
    }

    [Test]
    public void ClosingParenthesisEndsIncrementalCompletionContext()
    {
        Assert.That(GetItems("Info(AES-C)"), Is.Empty);
    }

    [TestCase("", "functions", true)]
    [TestCase("f", "functions", true)]
    [TestCase("fu", "functions", true)]
    [TestCase("f", "KDF-HKDF", false)]
    [TestCase("A", "AES-CBC", true)]
    [TestCase("A", "HASH-SHA256", false)]
    [TestCase("AES", "AES-GCM", true)]
    [TestCase("D", "DES3-CBC", true)]
    [TestCase("D", "DUKPT-AES-INITIAL-KEY", true)]
    [TestCase("D", "KDF-HKDF", false)]
    public void PrefixMatchingUsesOnlyTheBeginningOfAnEntry(
        string prefix,
        string completionText,
        bool expected)
    {
        Assert.That(
            InfoCompletionProvider.MatchesPrefix(completionText, prefix),
            Is.EqualTo(expected));
    }

    [Test]
    public void BackspacePrefixSequenceRestoresMatchingEntries()
    {
        IReadOnlyList<string> allItems = GetItems("Info(");
        string[] prefixes = { "AES-ECB", "AES-EC", "AES-", "AES", "A", "" };

        foreach (string prefix in prefixes)
        {
            string[] matches = allItems
                .Where(item => InfoCompletionProvider.MatchesPrefix(item, prefix))
                .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(matches, Does.Contain("AES-ECB"), $"Prefix: {prefix}");
                Assert.That(
                    matches,
                    Has.All.Matches<string>(item =>
                        item.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)),
                    $"Prefix: {prefix}");
            });
        }

        Assert.That(
            allItems.Where(item => InfoCompletionProvider.MatchesPrefix(item, string.Empty)),
            Is.EqualTo(allItems));
    }

    [Test]
    public void CompletionDataUsesReusableTextInsteadOfAControl()
    {
        var completionData = new InfoCompletionData("AES-CMAC");

        Assert.Multiple(() =>
        {
            Assert.That(completionData.Content, Is.TypeOf<string>());
            Assert.That(completionData.Content, Is.EqualTo("AES-CMAC"));
            Assert.That(completionData.Description, Is.TypeOf<string>());
        });
    }

    [TestCase("types", "Lists all supported CryptoScript types.")]
    [TestCase("functions", "Lists all supported CryptoScript functions.")]
    [TestCase("mechanisms", "Lists all supported cryptographic mechanisms.")]
    [TestCase("parameters", "Lists all supported CryptoScript parameters.")]
    [TestCase("AES-CBC", "AES-CBC")]
    public void CompletionDescriptionsComeFromInfoCompletionData(
        string completionText,
        string expectedDescription)
    {
        var completionData = new InfoCompletionData(
            completionText,
            _provider.GetDescription(completionText));

        Assert.That(completionData.Description, Is.EqualTo(expectedDescription));
    }

    [Test]
    public void AcceptedCompletionReopensOnlyForPureDeletion()
    {
        var policy = new InfoCompletionReopenPolicy();
        var document = new TextDocument("Info(AES-ECB)");
        _provider.TryGetContext(document.Text, 8, out var context);
        policy.MarkAccepted(
            document,
            context.InvocationStartOffset,
            context.ArgumentStartOffset);

        Assert.Multiple(() =>
        {
            Assert.That(policy.ShouldReopen(document, context, 7, 1, 0), Is.True);
            Assert.That(policy.ShouldReopen(document, context, 7, 0, 0), Is.False);
            Assert.That(policy.ShouldReopen(document, context, 7, 1, 7), Is.False);
            Assert.That(policy.ShouldReopen(document, context, 4, 1, 0), Is.False);
        });
    }

    [Test]
    public void AcceptedCompletionIsBoundToDocumentAndInfoCall()
    {
        var policy = new InfoCompletionReopenPolicy();
        var document = new TextDocument("Info(AES-ECB) Info(DES3-ECB)");
        _provider.TryGetContext(document.Text, 8, out var acceptedContext);
        int otherCaret = document.Text.LastIndexOf(')');
        _provider.TryGetContext(document.Text, otherCaret, out var otherContext);
        policy.MarkAccepted(
            document,
            acceptedContext.InvocationStartOffset,
            acceptedContext.ArgumentStartOffset);

        Assert.Multiple(() =>
        {
            Assert.That(
                policy.ShouldReopen(
                    new TextDocument(document.Text),
                    acceptedContext,
                    7,
                    1,
                    0),
                Is.False);
            Assert.That(
                policy.ShouldReopen(document, otherContext, otherCaret - 1, 1, 0),
                Is.False);
        });
    }

    [Test]
    public void AcceptedCompletionTracksEditsBeforeItsInfoCall()
    {
        var document = new TextDocument("Info(AES-ECB)");
        var policy = new InfoCompletionReopenPolicy();
        _provider.TryGetContext(document.Text, document.Text.IndexOf(')'), out var acceptedContext);
        policy.MarkAccepted(
            document,
            acceptedContext.InvocationStartOffset,
            acceptedContext.ArgumentStartOffset);

        document.Insert(0, "prefix ");
        int caretOffset = document.Text.IndexOf(')');
        document.Remove(caretOffset - 1, 1);
        caretOffset--;
        _provider.TryGetContext(document.Text, caretOffset, out var currentContext);

        Assert.That(
            policy.ShouldReopen(document, currentContext, caretOffset, 1, 0),
            Is.True);
    }

    [Test]
    public void AcceptedCompletionSurvivesChangesBeforeAndAfterBoundCall()
    {
        var document = new TextDocument("head\nInfo(AES-ECB)\ntail");
        var policy = new InfoCompletionReopenPolicy();
        int acceptedCaret = document.Text.IndexOf(')');
        _provider.TryGetContext(document.Text, acceptedCaret, out var acceptedContext);
        policy.MarkAccepted(
            document,
            acceptedContext.InvocationStartOffset,
            acceptedContext.ArgumentStartOffset);

        document.Insert(0, "before ");
        document.Insert(document.TextLength, " after");
        document.Remove(0, 1);
        document.Remove(document.TextLength - 1, 1);

        int argumentCaret = document.Text.IndexOf(')');
        document.Remove(argumentCaret - 1, 1);
        argumentCaret--;
        _provider.TryGetContext(document.Text, argumentCaret, out var currentContext);

        Assert.That(
            policy.ShouldReopen(document, currentContext, argumentCaret, 1, 0),
            Is.True);
    }

    [Test]
    public void AcceptedCompletionCanReopenAfterDeletingTheFirstArgumentCharacter()
    {
        var document = new TextDocument("Info(A)");
        var policy = new InfoCompletionReopenPolicy();
        _provider.TryGetContext(document.Text, document.Text.IndexOf(')'), out var acceptedContext);
        policy.MarkAccepted(
            document,
            acceptedContext.InvocationStartOffset,
            acceptedContext.ArgumentStartOffset);

        document.Remove(acceptedContext.ArgumentStartOffset, 1);
        _provider.TryGetContext(
            document.Text,
            acceptedContext.ArgumentStartOffset,
            out var emptyContext);

        Assert.That(
            policy.ShouldReopen(
                document,
                emptyContext,
                acceptedContext.ArgumentStartOffset,
                1,
                0),
            Is.True);
    }

    [Test]
    public void ReplacingTheBoundInvocationInvalidatesReopeningAtTheSameOffset()
    {
        var document = new TextDocument("Info(AES-ECB)");
        var policy = new InfoCompletionReopenPolicy();
        _provider.TryGetContext(document.Text, document.Text.IndexOf(')'), out var acceptedContext);
        policy.MarkAccepted(
            document,
            acceptedContext.InvocationStartOffset,
            acceptedContext.ArgumentStartOffset);

        document.Replace(0, "Info(".Length, "Info(");
        int caretOffset = document.Text.IndexOf(')');
        document.Remove(caretOffset - 1, 1);
        caretOffset--;
        _provider.TryGetContext(document.Text, caretOffset, out var replacementContext);

        Assert.That(
            policy.ShouldReopen(document, replacementContext, caretOffset, 1, 0),
            Is.False);
    }

    [Test]
    public void CancelledCompletionDoesNotReopenForDeletion()
    {
        var policy = new InfoCompletionReopenPolicy();
        var document = new TextDocument("Info(AES-ECB)");
        _provider.TryGetContext(document.Text, document.Text.IndexOf(')'), out var context);
        policy.MarkAccepted(
            document,
            context.InvocationStartOffset,
            context.ArgumentStartOffset);
        policy.Cancel();

        Assert.That(policy.ShouldReopen(document, context, 10, 1, 0), Is.False);
    }

    private IReadOnlyList<string> GetItems(string text) =>
        _provider.GetCompletionItems(text, text.Length);

    private static string ReplaceArgument(
        string text,
        InfoCompletionContext context,
        string completion) =>
        text.Remove(
                context.ArgumentStartOffset,
                context.ArgumentEndOffset - context.ArgumentStartOffset)
            .Insert(context.ArgumentStartOffset, completion);
}

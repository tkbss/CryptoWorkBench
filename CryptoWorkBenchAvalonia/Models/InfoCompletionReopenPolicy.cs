using AvaloniaEdit.Document;
using System;

namespace CryptoWorkBenchAvalonia.Models;

public sealed class InfoCompletionReopenPolicy : IDisposable
{
    private TextDocument? _document;
    private TextAnchor? _invocationStart;
    private TextAnchor? _argumentStart;

    public void MarkAccepted(
        TextDocument document,
        int invocationStartOffset,
        int argumentStartOffset)
    {
        Cancel();
        _document = document;
        _invocationStart = document.CreateAnchor(invocationStartOffset);
        _invocationStart.MovementType = AnchorMovementType.AfterInsertion;
        _argumentStart = document.CreateAnchor(argumentStartOffset);
        _argumentStart.MovementType = AnchorMovementType.BeforeInsertion;
        _argumentStart.SurviveDeletion = true;
        document.Changing += Document_Changing;
    }

    public void Cancel()
    {
        if (_document != null)
            _document.Changing -= Document_Changing;

        _document = null;
        _invocationStart = null;
        _argumentStart = null;
    }

    public bool ShouldReopen(
        TextDocument document,
        InfoCompletionContext context,
        int changeOffset,
        int removalLength,
        int insertionLength)
    {
        if (!ReferenceEquals(_document, document) ||
            _invocationStart == null ||
            _argumentStart == null ||
            _invocationStart.IsDeleted ||
            _argumentStart.IsDeleted)
        {
            return false;
        }

        return _invocationStart.Offset == context.InvocationStartOffset &&
               _argumentStart.Offset == context.ArgumentStartOffset &&
               changeOffset >= context.ArgumentStartOffset &&
               removalLength > 0 &&
               insertionLength == 0;
    }

    public void Dispose() => Cancel();

    private void Document_Changing(object? sender, DocumentChangeEventArgs e)
    {
        if (_invocationStart == null ||
            _argumentStart == null ||
            _invocationStart.IsDeleted ||
            _argumentStart.IsDeleted)
        {
            Cancel();
            return;
        }

        int invocationStart = _invocationStart.Offset;
        int argumentStart = _argumentStart.Offset;
        int removalEnd = e.Offset + e.RemovalLength;
        bool removesInvocation =
            e.RemovalLength > 0 &&
            e.Offset < argumentStart &&
            removalEnd > invocationStart;
        bool insertsIntoInvocation =
            e.InsertionLength > 0 &&
            e.Offset > invocationStart &&
            e.Offset < argumentStart;

        if (removesInvocation || insertsIntoInvocation)
            Cancel();
    }
}

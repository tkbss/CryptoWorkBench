using CryptoScript.Model;

namespace CryptoScript.Variables;

/// <summary>Roots and declaration reservations belonging to one VariableState.</summary>
public sealed class VariableDictionary : Statement
{
    private static readonly VariableDictionary compatibility = new();
    private readonly HashSet<string> reservations = new(StringComparer.Ordinal);
    private Dictionary<string, VariableDeclaration> variables = new(StringComparer.Ordinal);
    private long revision;

    public VariableDictionary() : this(Guid.NewGuid()) { }
    public VariableDictionary(Guid bookId) : this(bookId, Guid.NewGuid()) { }
    internal VariableDictionary(Guid bookId, Guid variableStateId)
    {
        BookId = bookId;
        VariableStateId = variableStateId;
    }
    public Guid BookId { get; }
    public Guid SessionGeneration { get; } = Guid.NewGuid();
    public Guid VariableStateId { get; }
    private bool retired;

    internal void EnsureLive()
    {
        if (retired) throw new ExecutionBusyException();
    }

    public void Retire()
    {
        lock (InterpreterExecution.Gate)
        {
            Verify();
            if (this == compatibility || InterpreterExecution.IsBusy) throw new ExecutionBusyException();
            retired = true;
            variables.Clear();
        }
    }
    public static Guid ProcessInstanceId { get; } = Guid.NewGuid();
    internal bool AtRevision(long expected) => revision == expected;
    public long Revision { get { lock (InterpreterExecution.Gate) { Verify(); return revision; } } }

    // Fixed standalone workspace. It never follows a runner or a selected editor.
    public static VariableDictionary Instance() => compatibility;
    private void Verify() => InterpreterExecution.VerifyStore(this);

    public void Clear() => ClearCore(null);

    internal void ClearAdmitted(InterpreterExecution.HostAdmission admission) => ClearCore(admission);

    private void ClearCore(InterpreterExecution.HostAdmission? admission)
    {
        lock (InterpreterExecution.Gate)
        {
            Verify();
            if (admission == null) InterpreterExecution.VerifyExecution();
            if (variables.Count == 0) return;
            variables.Clear();
            revision++;
        }
    }

    public VariableDeclaration Get(string id)
    {
        lock (InterpreterExecution.Gate) { Verify(); return variables.GetValueOrDefault(id)!; }
    }

    public bool Contains(string id)
    {
        lock (InterpreterExecution.Gate) { Verify(); return variables.ContainsKey(id); }
    }

    public IEnumerable<VariableDeclaration> GetVariables()
    {
        lock (InterpreterExecution.Gate) { Verify(); return variables.Values.ToArray(); }
    }

    internal bool IsReserved(string id)
    {
        lock (InterpreterExecution.Gate) { Verify(); return reservations.Contains(id); }
    }

    internal Reservation? Reserve(string id)
    {
        lock (InterpreterExecution.Gate)
        {
            Verify();
            InterpreterExecution.VerifyExecution();
            if (variables.ContainsKey(id) || !reservations.Add(id)) return null;
            return new Reservation(this, id, InterpreterExecution.CaptureFrame());
        }
    }

    internal sealed class Reservation(VariableDictionary store, string id, InterpreterExecution.Frame? frame) : IDisposable
    {
        private bool active = true;
        internal void Publish(VariableDeclaration value)
        {
            lock (InterpreterExecution.Gate)
            {
                InterpreterExecution.VerifyFrame(frame);
                if (!active || value.Id != id || !store.reservations.Contains(id))
                    throw new InvalidOperationException("Invalid declaration reservation.");
                store.ValidateNewRoot(value);
                store.PrepareBinding(value);
                store.variables.Add(id, value);
                store.Bind(value);
                store.revision++;
                Dispose();
            }
        }
        public void Dispose()
        {
            lock (InterpreterExecution.Gate)
            {
                if (!active) return;
                InterpreterExecution.VerifyFrame(frame);
                store.reservations.Remove(id);
                active = false;
            }
        }
    }

    internal void ValidateNewRoot(VariableDeclaration value)
    {
        VerifyDetached(value);
        if (value is ParameterVariableDeclaration { HeaderReference: { Owner: null } detachedHeader }) VerifyDetached(detachedHeader);
        if (value is ParameterVariableDeclaration { HeaderReference: { } header } &&
            header.Owner != null && (header.Owner != this || !variables.Values.Any(v =>
                ReferenceEquals(v, header) || v is ParameterVariableDeclaration p && ReferenceEquals(p.HeaderReference, header))))
            throw new InvalidOperationException("The header dependency belongs to another binding.");
    }

    internal void VerifyResult(VariableDeclaration value)
    {
        VerifyDetached(value);
        if (this != compatibility && value.CreatedIn == null)
            throw new InvalidOperationException("Expected a result created by this execution.");
    }

    internal void VerifyDetached(VariableDeclaration value)
    {
        if (value.Owner != null || value.CreatedIn is { } origin && origin != InterpreterExecution.CaptureFrame())
            throw new InvalidOperationException("Expected a detached result of the current operation.");
    }

    internal bool ContainsObject(VariableDeclaration value) => value.BoundId != null &&
        (ReferenceEquals(variables.GetValueOrDefault(value.BoundId), value) ||
        value is BlockHeaderVariableDeclaration && variables.Values.OfType<ParameterVariableDeclaration>()
            .Any(p => ReferenceEquals(p.BoundHeader, value)));

    internal void ValidateArgument(VariableDeclaration? value)
    {
        if (value == null) return;
        lock (InterpreterExecution.Gate)
        {
            Verify();
            if (value.Owner == null)
            {
                VerifyDetached(value);
                if (this != compatibility && value.CreatedIn == null)
                    throw new InvalidOperationException("Unbound variable argument.");
                if (value is ParameterVariableDeclaration) ValidateNewRoot(value);
            }
            else if (value.Owner != this || !ReferenceEquals(variables.GetValueOrDefault(value.BoundId!), value))
                throw new InvalidOperationException("Variable argument is not a current root of this store.");
        }
    }

    private void PrepareBinding(VariableDeclaration value)
    {
        if (value.Owner == null)
        {
            if (value is KeyVariableDeclaration key) key.DetachAttributes();
            if (value is ParameterVariableDeclaration parameter) parameter.DetachParameters();
        }
    }

    private void Bind(VariableDeclaration value)
    {
        // Read the dependency before attaching the root (transfer has no generic read authority).
        var header = (value as ParameterVariableDeclaration)?.BoundHeader;
        value.Bind(this);
        if (header is { Owner: null }) header.Bind(this);
    }

    public void Add(VariableDeclaration value)
    {
        lock (InterpreterExecution.Gate)
        {
            Verify();
            InterpreterExecution.VerifyExecution();
            if (reservations.Contains(value.Id)) throw new InvalidOperationException("Identifier is reserved.");
            if (ReferenceEquals(variables.GetValueOrDefault(value.Id), value)) return;
            // Standalone hosts historically restore saved objects into their own idle workspace.
            if (!(this == compatibility && value.Owner == this && InterpreterExecution.CaptureFrame() == null))
                ValidateNewRoot(value);
            if ((this != compatibility || InterpreterExecution.IsBusy) && variables.ContainsKey(value.Id))
                throw new InvalidOperationException("Identifier already exists.");
            PrepareBinding(value);
            variables[value.Id] = value;
            Bind(value);
            revision++;
        }
    }

    public void Update(VariableDeclaration value)
    {
        lock (InterpreterExecution.Gate)
        {
            Verify();
            InterpreterExecution.VerifyExecution();
            if (variables.TryGetValue(value.Id, out var target) && !ReferenceEquals(value, target))
            {
                if (target.Type?.Name != value.Type?.Name) throw new ArgumentException("Incompatible variable type.");
                ValidateNewRoot(value);
                if (!ValidLiveReplacement(target, value))
                    throw new ArgumentException("Replacement does not satisfy the declared type contract.", nameof(value));
                if (value is KeyVariableDeclaration key && value.Type is null or CryptoTypeKey)
                    Serialization.KeySnapshotCodec.ValidateMaterial(key);
                TryAssign(target, value);
            }
        }
    }

    private static bool ValidLiveReplacement(VariableDeclaration target, VariableDeclaration value)
    {
        var type = value.Type;
        if (type == null && target.GetType() != value.GetType()) return false;

        if (value is BlockHeaderVariableDeclaration header && type is null or CryptoTypeTR31Header)
        {
            // Snapshot v1 restores legacy header text verbatim. Keep validated roots validated.
            return header.Definition != null ||
                (header.Value != null && target is BlockHeaderVariableDeclaration { Definition: null });
        }

        return type switch
        {
            // Expression.PrepareVariable uses string or path storage for PARAM literals.
            CryptoTypeParameters => value is ParameterVariableDeclaration or StringVariableDeclaration ||
                value is PathVariableDeclaration path && FormatConversions.IsPathRegex(path.Value),
            null => value is KeyVariableDeclaration { KeySizeInBits.IsKnown: true } or
                StringVariableDeclaration or PathVariableDeclaration or ParameterVariableDeclaration or SecretVariableDeclaration,
            _ => AssignmentEvaluator.ValidRepresentation(value)
        };
    }

    internal bool TryAssign(VariableDeclaration expected, VariableDeclaration result)
    {
        lock (InterpreterExecution.Gate)
        {
            Verify();
            InterpreterExecution.VerifyExecution();
            if (expected.BoundId is not { } id || !ReferenceEquals(variables.GetValueOrDefault(id), expected)) return false;
            ValidateNewRoot(result);
            PrepareBinding(result);
            variables[id] = result;
            Bind(result);
            revision++;
            return true;
        }
    }

    internal void RecordMutation(VariableDeclaration value)
    {
        lock (InterpreterExecution.Gate)
        {
            InterpreterExecution.VerifyExecution();
            if (this == compatibility && InterpreterExecution.CaptureFrame() == null &&
                !ReferenceEquals(Get(value.Id), value)) return;
            ValidateArgument(value);
            revision++;
        }
    }

    internal void PublishBatch(IEnumerable<VariableDeclaration> state, CancellationToken cancellationToken = default)
    {
        Verify();
        InterpreterExecution.VerifyExecution();
        var prepared = state.ToDictionary(v => v.Id, StringComparer.Ordinal);
        lock (InterpreterExecution.Gate)
        {
            Verify();
            CommitBatch(prepared, cancellationToken);
        }
    }

    private void CommitBatch(Dictionary<string, VariableDeclaration> prepared, CancellationToken token)
    {
        bool changed = prepared.Count != variables.Count;
        foreach (var (id, value) in prepared)
        {
            if (ReferenceEquals(variables.GetValueOrDefault(id), value)) continue;
            if (reservations.Contains(id)) throw new InvalidOperationException("Identifier is reserved.");
            ValidateNewRoot(value);
            PrepareBinding(value);
            changed = true;
        }
        token.ThrowIfCancellationRequested();
        if (!changed) return;
        foreach (var value in prepared.Values)
            if (value.Owner == null) Bind(value);
        variables = prepared;
        revision++;
    }

    internal string ExportForTransfer(InterpreterExecution.Transfer permit)
    {
        permit.VerifyEndpoint(this, importing: false);
        return new Serialization.VariableSnapshotJsonSerializer().Serialize(variables.Values.ToArray());
    }
    internal void ImportForTransfer(InterpreterExecution.Transfer permit, IReadOnlyList<VariableDeclaration> roots)
    {
        permit.VerifyEndpoint(this, importing: true);
        var prepared = new Dictionary<string, VariableDeclaration>(variables, StringComparer.Ordinal);
        foreach (var root in roots) prepared.Add(root.Id, root);
        CommitBatch(prepared, default);
    }
}

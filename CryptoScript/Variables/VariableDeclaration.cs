using CryptoScript.Model;
using Newtonsoft.Json;

namespace CryptoScript.Variables
{
    public class VariableDeclaration : Statement
    {
        internal VariableDictionary? Owner { get; private set; }
        internal InterpreterExecution.Frame? CreatedIn { get; private set; } = InterpreterExecution.CaptureFrame();
        internal void Bind(VariableDictionary store)
        {
            if (Owner == null) type?.Freeze();
            if (Owner == null || store == VariableDictionary.Instance()) BoundId = Id;
            Owner = store;
            CreatedIn = null;
        }

        protected T ReadOwned<T>(T value)
        {
            if (Owner != null) InterpreterExecution.VerifyObjectRead(Owner, this);
            return value;
        }

        protected void Change<T>(ref T field, T next)
        {
            if (Owner == null) { field = next; return; }
            lock (InterpreterExecution.Gate)
            {
                InterpreterExecution.VerifyObjectRead(Owner, this);
                if (EqualityComparer<T>.Default.Equals(field, next)) return;
                Owner.RecordMutation(this);
                field = next;
            }
        }

        [JsonProperty(Order = 1)] // Keep Text in its historical position after the declaration properties.
        public override string Text { get => ReadOwned(field); set => Change(ref field, value); } = string.Empty;

        public string Id
        {
            get => ReadOwned(field);
            set
            {
                if (Owner != null && field != value &&
                    !(Owner == VariableDictionary.Instance() && InterpreterExecution.CaptureFrame() == null && !Owner.Contains(field)))
                    throw new InvalidOperationException("A bound identifier cannot be changed.");
                field = value;
            }
        } = string.Empty;
        internal string? BoundId { get; private set; }
        private CryptoType? type;
        public virtual CryptoType? Type
        {
            get => ReadOwned(type);
            set
            {
                lock (InterpreterExecution.Gate)
                {
                    if (KeepBoundType(type, value)) return;
                    if (value is CryptoTypeVal)
                        throw new ArgumentException("VAL requires a Boolean result object.");
                    type = value;
                }
            }
        }
        // Called under the interpreter gate by both type setters.
        private protected bool KeepBoundType(CryptoType? current, CryptoType? proposed)
        {
            if (Owner == null) return false;
            InterpreterExecution.VerifyExecution();
            InterpreterExecution.VerifyObjectRead(Owner, this);
            if (!ReferenceEquals(current, proposed))
                throw new InvalidOperationException("A bound declaration's type cannot be replaced.");
            return true;
        }

        private string value = string.Empty;
        public virtual string Value
        {
            get => ReadOwned(value);
            set => Change(ref this.value, value);
        }
        internal void SetPreparedValue(string prepared) => value = prepared;
        public string ValueFormat { get => ReadOwned(field); set => Change(ref field, value); } = string.Empty;
        public virtual string PrintOutput() {  return string.Empty; }
        

    }
}


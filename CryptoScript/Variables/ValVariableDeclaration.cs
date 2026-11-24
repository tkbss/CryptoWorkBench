using CryptoScript.Model;

namespace CryptoScript.Variables;

/// <summary>A Boolean result, with no arbitrary string or type mutation.</summary>
public sealed class ValVariableDeclaration : VariableDeclaration
{
    private bool value;
    private readonly CryptoTypeVal type = new();
    public ValVariableDeclaration(bool value)
    {
        type.Freeze();
        this.value = value;
        ValueFormat = FormatConversions.STR;
    }

    public override CryptoType? Type
    {
        get => ReadOwned(type);
        set
        {
            lock (InterpreterExecution.Gate)
            {
                if (KeepBoundType(type, value)) return;
                if (value is not CryptoTypeVal) throw new ArgumentException("VAL requires its Boolean type.");
            }
        }
    }

    public override string Value
    {
        get => ReadOwned(value) ? "true" : "false";
        set
        {
            bool next = value switch
        {
            "true" => true,
            "false" => false,
            _ => throw new ArgumentException("VAL requires true or false.")
            };
            Change(ref this.value, next);
        }
    }
}

using CryptoScript.Model;

namespace CryptoScript.Variables;

/// <summary>A Boolean result, with no arbitrary string or type mutation.</summary>
public sealed class ValVariableDeclaration : VariableDeclaration
{
    private bool value;
    private readonly CryptoTypeVal type = new();
    public ValVariableDeclaration(bool value)
    {
        this.value = value;
        ValueFormat = FormatConversions.STR;
    }

    public override CryptoType? Type
    {
        get => type;
        set
        {
            if (value is not CryptoTypeVal) throw new ArgumentException("VAL requires its Boolean type.");
        }
    }

    public override string Value
    {
        get => value ? "true" : "false";
        set => this.value = value switch
        {
            "true" => true,
            "false" => false,
            _ => throw new ArgumentException("VAL requires true or false.")
        };
    }
}

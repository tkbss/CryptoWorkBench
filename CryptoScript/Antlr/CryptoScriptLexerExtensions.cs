using Antlr4.Runtime;

public partial class CryptoScriptLexer
{
    public override IToken NextToken()
    {
        if (Matches("DES3-RETAIL"))
            return ConsumeToken("DES3-RETAIL", MECHANISM);
        if (Matches("#MACLEN"))
            return ConsumeToken("#MACLEN", PARAM_TYPE);
        return base.NextToken();
    }

    private bool Matches(string text)
    {
        bool matches = true;
        for (int i = 0; i < text.Length; i++)
        {
            if (InputStream.LA(i + 1) != text[i])
            {
                matches = false;
                break;
            }
        }

        return matches;
    }

    private IToken ConsumeToken(string text, int tokenType)
    {
        int start = InputStream.Index;
        int line = Line;
        int column = Column;
        for (int i = 0; i < text.Length; i++)
            InputStream.Consume();

        return new CommonToken(
            Tuple.Create<ITokenSource, ICharStream>(this, (ICharStream)InputStream),
            tokenType,
            DefaultTokenChannel,
            start,
            InputStream.Index - 1)
        {
            Line = line,
            Column = column,
            Text = text
        };
    }
}

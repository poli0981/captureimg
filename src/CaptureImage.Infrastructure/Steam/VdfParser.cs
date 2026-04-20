using System.Text;

namespace CaptureImage.Infrastructure.Steam;

/// <summary>
/// Recursive-descent parser for Valve KeyValues v1 files (<c>.vdf</c>, <c>.acf</c>).
/// </summary>
/// <remarks>
/// Grammar (simplified):
/// <code>
/// node   := string (string | '{' node* '}')
/// string := '"' char* '"'            ; supports \\, \", \n, \t escapes
/// </code>
/// <para>
/// Whitespace and C++ style <c>//</c> line comments are skipped between tokens.
/// Conditional blocks (<c>[$OSX]</c> etc.) after a value are tolerated and ignored.
/// </para>
/// </remarks>
public static class VdfParser
{
    /// <summary>
    /// Parse <paramref name="text"/> and return the top-level node. If the document
    /// contains multiple top-level keys (rare but legal), they are wrapped in a synthetic
    /// <c>"&lt;root&gt;"</c> branch.
    /// </summary>
    /// <exception cref="VdfParseException">When the text is malformed.</exception>
    public static VdfNode Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var reader = new VdfReader(text);
        reader.SkipTrivia();

        if (reader.Eof)
        {
            throw new VdfParseException("Empty document.", reader.Position);
        }

        var first = ParseNode(reader);
        reader.SkipTrivia();

        if (reader.Eof)
        {
            return first;
        }

        // Multiple top-level nodes: wrap.
        var children = new Dictionary<string, VdfNode>(StringComparer.OrdinalIgnoreCase)
        {
            [first.Key] = first,
        };
        while (!reader.Eof)
        {
            var node = ParseNode(reader);
            children[node.Key] = node;
            reader.SkipTrivia();
        }
        return VdfNode.Branch("<root>", children);
    }

    private static VdfNode ParseNode(VdfReader reader)
    {
        reader.SkipTrivia();
        var key = reader.ReadQuotedString();
        reader.SkipTrivia();

        if (reader.Eof)
        {
            throw new VdfParseException($"Unexpected end of file after key '{key}'.", reader.Position);
        }

        var next = reader.Peek();
        if (next == '"')
        {
            var value = reader.ReadQuotedString();
            // Tolerate trailing conditional like `"value" [$WIN32]`.
            reader.SkipTrivia();
            reader.SkipConditional();
            return VdfNode.Leaf(key, value);
        }

        if (next == '{')
        {
            reader.Advance();
            var children = new Dictionary<string, VdfNode>(StringComparer.OrdinalIgnoreCase);
            while (true)
            {
                reader.SkipTrivia();
                if (reader.Eof)
                {
                    throw new VdfParseException($"Unterminated branch '{key}'.", reader.Position);
                }
                if (reader.Peek() == '}')
                {
                    reader.Advance();
                    break;
                }
                var child = ParseNode(reader);
                children[child.Key] = child;
            }
            return VdfNode.Branch(key, children);
        }

        throw new VdfParseException(
            $"Expected '\"' or '{{' after key '{key}', got '{next}'.",
            reader.Position);
    }

    /// <summary>Cursor over the input text with helpers for quoted strings and trivia.</summary>
    private sealed class VdfReader
    {
        private readonly string _text;
        private int _pos;

        public VdfReader(string text) { _text = text; _pos = 0; }

        public int Position => _pos;
        public bool Eof => _pos >= _text.Length;

        public char Peek() => _text[_pos];

        public void Advance() => _pos++;

        public void SkipTrivia()
        {
            while (!Eof)
            {
                var c = _text[_pos];
                if (char.IsWhiteSpace(c))
                {
                    _pos++;
                    continue;
                }
                // C++ line comment //
                if (c == '/' && _pos + 1 < _text.Length && _text[_pos + 1] == '/')
                {
                    _pos += 2;
                    while (!Eof && _text[_pos] != '\n')
                    {
                        _pos++;
                    }
                    continue;
                }
                break;
            }
        }

        /// <summary>
        /// Skip an optional conditional block like <c>[$WIN32]</c> after a value. The block may
        /// contain boolean ops but we don't evaluate it — whatever is inside is kept.
        /// </summary>
        public void SkipConditional()
        {
            if (Eof || _text[_pos] != '[') return;
            while (!Eof && _text[_pos] != ']')
            {
                _pos++;
            }
            if (!Eof)
            {
                _pos++; // consume ']'
            }
        }

        public string ReadQuotedString()
        {
            if (Eof || _text[_pos] != '"')
            {
                throw new VdfParseException(
                    $"Expected '\"' at position {_pos}.", _pos);
            }
            _pos++; // consume opening quote

            var sb = new StringBuilder();
            while (!Eof)
            {
                var c = _text[_pos++];
                if (c == '"')
                {
                    return sb.ToString();
                }
                if (c == '\\' && !Eof)
                {
                    var escaped = _text[_pos++];
                    sb.Append(escaped switch
                    {
                        '\\' => '\\',
                        '"'  => '"',
                        'n'  => '\n',
                        't'  => '\t',
                        'r'  => '\r',
                        _    => escaped,
                    });
                    continue;
                }
                sb.Append(c);
            }
            throw new VdfParseException("Unterminated string literal.", _pos);
        }
    }
}

/// <summary>Thrown when a VDF document fails to parse.</summary>
public sealed class VdfParseException : Exception
{
    public int Position { get; }

    public VdfParseException(string message, int position) : base(message)
    {
        Position = position;
    }
}

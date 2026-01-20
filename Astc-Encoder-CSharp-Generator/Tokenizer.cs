using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Astc_Encoder_CSharp_Generator
{
    internal enum TokenType
    {
        Identifier,
        Keyword,
        Number,
        StringLiteral,
        CharLiteral,
        Punctuator,
        Preprocessor,
        Comment,
        Word,
        EndOfFile
    }

    internal sealed class Token
    {
        public TokenType Kind { get; }
        public string Value { get; }
        public int StartLine { get; }
        public int StartColumn { get; }
        public int EndLine { get; }
        public int EndColumn { get; }
        public List<TokenWord> Tokens { get; } = new List<TokenWord>();

        public Token(TokenType kind, string value, int startLine, int startColumn, int endLine, int endColumn)
        {
            Kind = kind;
            Value = value ?? string.Empty;
            StartLine = startLine;
            StartColumn = startColumn;
            EndLine = endLine;
            EndColumn = endColumn;
        }

        public override string ToString()
        {
            return $"{Kind} [{StartLine}:{StartColumn}-{EndLine}:{EndColumn}] \"{Value}\"";
        }
    }

    internal sealed class TokenWord
    {
        public TokenType Kind { get; }
        public string Value { get; }
        public int StartLine { get; }
        public int StartColumn { get; }
        public int EndLine { get; }
        public int EndColumn { get; }

        public TokenWord(TokenType kind, string value, int startLine, int startColumn, int endLine, int endColumn)
        {
            Kind = kind;
            Value = value ?? string.Empty;
            StartLine = startLine;
            StartColumn = startColumn;
            EndLine = endLine;
            EndColumn = endColumn;
        }

        public override string ToString()
        {
            return $"{Kind} [{StartLine}:{StartColumn}-{EndLine}:{EndColumn}] \"{Value}\"";
        }
    }

    internal class Tokenizer
    {
        private readonly string _text;
        private int _i;
        private int _line;
        private int _col;
        private bool _lineHasOnlyWhitespace; // true if, since the most recent newline, we've only seen whitespace

        private static readonly HashSet<string> Keywords = new HashSet<string>(StringComparer.Ordinal)
        {
            // Common C/C++/C header tokens; used only if caller wants to treat them as keywords.
            "typedef", "struct", "enum", "union", "const", "volatile", "static", "inline", "extern",
            "return", "if", "else", "while", "for", "do", "switch", "case", "break", "continue", "default",
            "sizeof"
        };

        private static readonly string[] MultiCharPunctuators = new[]
        {
            "<<=", ">>=", "->*", "->", "++", "--", "<<", ">>", "<=", ">=", "==", "!=", "&&", "||",
            "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "::", ".*", "##"
        };

        public Tokenizer(string text)
        {
            _text = text ?? string.Empty;
            _i = 0;
            _line = 1;
            _col = 1;
            _lineHasOnlyWhitespace = true;
        }

        public static List<Token> Tokenize(string text)
        {
            var t = new Tokenizer(text);
            return t.TokenizeAll();
        }

        public static List<Token> TokenizeFile(string path)
        {
            var text = File.ReadAllText(path, Encoding.UTF8);
            return Tokenize(text);
        }

        private char Current => _i < _text.Length ? _text[_i] : '\0';
        private char Peek(int offset = 1) => (_i + offset) < _text.Length ? _text[_i + offset] : '\0';
        private bool IsAtEnd => _i >= _text.Length;

        private void Advance()
        {
            if (IsAtEnd) return;

            // Update whitespace-tracking for the current character before moving past it.
            if (Current == '\n')
            {
                _line++;
                _col = 1;
                _lineHasOnlyWhitespace = true;
            }
            else
            {
                // If current char is non-whitespace, mark that the line has non-whitespace content.
                if (!char.IsWhiteSpace(Current))
                {
                    _lineHasOnlyWhitespace = false;
                }

                _col++;
            }

            _i++;
        }

        private void AdvanceNoTrack() => _i++; // rare use, not updating line/col (avoid unless appropriate)

        private List<Token> TokenizeAll()
        {
            var tokens = new List<Token>();

            while (!IsAtEnd)
            {
                var c = Current;

                if (char.IsWhiteSpace(c) || ((int)c).ToString("X4") == "\u00A0" || ((int)c).ToString("X4") == "\u2003" || ((int)c).ToString("X4") == "\u0009")
                {
                    // consume whitespace but don't emit tokens. Track newlines so preprocessor detection works.
                    Advance();
                    continue;
                }

                // Record start position and index
                var startLine = _line;
                var startCol = _col;
                var startIndex = _i;

                // Preprocessor: '#' when at line start or only whitespace since line start.
                // C preprocessor allows leading whitespace before '#', so accept lines that have only whitespace so far.
                if (c == '#' && _lineHasOnlyWhitespace)
                {
                    var value = ReadPreprocessor();
                    Token group = new Token(TokenType.Preprocessor, value, startLine, startCol, _line, _col - 1);
                    tokens.Add(group);
                    // split natural-language words from the preprocessor text for documentation
                    EmitWordsFromText(group.Tokens, value, startLine, startCol);
                    continue;
                }

                // Comments
                if (c == '/' && Peek() == '/')
                {
                    var value = ReadLineComment();

                    Token group = new Token(TokenType.Comment, value, startLine, startCol, _line, _col - 1);
                    tokens.Add(group);
                    // split comment into words
                    EmitWordsFromText(group.Tokens, value, startLine, startCol);
                    continue;
                }

                if (c == '/' && Peek() == '*')
                {
                    var value = ReadBlockComment();
                    Token group = new Token(TokenType.Comment, value, startLine, startCol, _line, _col - 1);
                    tokens.Add(group);
                    // split comment into words
                    EmitWordsFromText(group.Tokens, value, startLine, startCol);
                    continue;
                }

                // String literal
                if (c == '"')
                {
                    var value = ReadStringLiteral();
                    Token group = new Token(TokenType.StringLiteral, value, startLine, startCol, _line, _col - 1);
                    tokens.Add(group);
                    // split words inside the string literal (quotes and escapes are punctuation and will be ignored)
                    EmitWordsFromText(group.Tokens, value, startLine, startCol);
                    continue;
                }

                // Char literal
                if (c == '\'')
                {
                    var value = ReadCharLiteral();
                    tokens.Add(new Token(TokenType.CharLiteral, value, startLine, startCol, _line, _col - 1));
                    // usually char literals are symbolic; do not split into words by default
                    continue;
                }

                // Number (simple detection)
                if (char.IsDigit(c) || (c == '.' && char.IsDigit(Peek())))
                {
                    var value = ReadNumber();
                    tokens.Add(new Token(TokenType.Number, value, startLine, startCol, _line, _col - 1));
                    continue;
                }

                // Identifier
                if (IsIdentifierStart(c))
                {
                    var value = ReadIdentifier();
                    var kind = Keywords.Contains(value) ? TokenType.Keyword : TokenType.Identifier;
                    tokens.Add(new Token(kind, value, startLine, startCol, _line, _col - 1));
                    continue;
                }

                // Punctuators / operators: try to match longest multi-char punctuator first
                var punct = ReadPunctuator();
                if (!string.IsNullOrEmpty(punct))
                {
                    tokens.Add(new Token(TokenType.Punctuator, punct, startLine, startCol, _line, _col - 1));
                    continue;
                }

                // Fallback: unknown single character punctuator
                var unknown = Current.ToString();
                Advance();
                tokens.Add(new Token(TokenType.Punctuator, unknown, startLine, startCol, _line, _col - 1));
            }

            // EOF token
            tokens.Add(new Token(TokenType.EndOfFile, string.Empty, _line, _col, _line, _col));
            return tokens;
        }

        private string ReadPreprocessor()
        {
            var sb = new StringBuilder();
            while (!IsAtEnd)
            {
                var c = Current;
                sb.Append(c);

                // handle line continuation: '\' followed by newline continues
                if (c == '\\' && Peek() == '\n')
                {
                    Advance(); // backslash
                    Advance(); // newline
                    continue;
                }

                Advance();

                if (c == '\n')
                    break;
            }

            return sb.ToString();
        }

        private string ReadLineComment()
        {
            var sb = new StringBuilder();
            // consume //
            sb.Append(Current);
            sb.Append(Peek());
            Advance();
            Advance();

            while (!IsAtEnd && Current != '\n')
            {
                sb.Append(Current);
                Advance();
            }

            // newline not consumed here; main loop will consume and update line/col
            return sb.ToString();
        }

        private string ReadBlockComment()
        {
            var sb = new StringBuilder();
            // consume /*
            sb.Append(Current);
            sb.Append(Peek());
            Advance();
            Advance();

            while (!IsAtEnd)
            {
                if (Current == '*' && Peek() == '/')
                {
                    sb.Append(Current);
                    sb.Append(Peek());
                    Advance();
                    Advance();
                    break;
                }

                sb.Append(Current);
                Advance();
            }

            return sb.ToString();
        }

        private string ReadStringLiteral()
        {
            var sb = new StringBuilder();
            sb.Append(Current);
            Advance(); // consume opening "

            while (!IsAtEnd)
            {
                var c = Current;
                sb.Append(c);

                if (c == '\\')
                {
                    // escape: include next char if exists
                    Advance();
                    if (!IsAtEnd)
                    {
                        sb.Append(Current);
                        Advance();
                    }
                    continue;
                }

                if (c == '"')
                {
                    Advance();
                    break;
                }

                Advance();
            }

            return sb.ToString();
        }

        private string ReadCharLiteral()
        {
            var sb = new StringBuilder();
            sb.Append(Current);
            Advance(); // consume opening '

            while (!IsAtEnd)
            {
                var c = Current;
                sb.Append(c);

                if (c == '\\')
                {
                    Advance();
                    if (!IsAtEnd)
                    {
                        sb.Append(Current);
                        Advance();
                    }
                    continue;
                }

                if (c == '\'')
                {
                    Advance();
                    break;
                }

                Advance();
            }

            return sb.ToString();
        }

        private string ReadNumber()
        {
            var sb = new StringBuilder();
            bool seenDot = false;
            bool seenExp = false;

            // Handle hex 0x or octal/decimal start
            if (Current == '0' && (Peek() == 'x' || Peek() == 'X'))
            {
                sb.Append(Current);
                sb.Append(Peek());
                Advance();
                Advance();

                while (IsHexDigit(Current))
                {
                    sb.Append(Current);
                    Advance();
                }

                // optional integer suffixes (u, l, etc.)
                while (char.IsLetter(Current))
                {
                    sb.Append(Current);
                    Advance();
                }

                return sb.ToString();
            }

            while (!IsAtEnd)
            {
                var c = Current;
                if (char.IsDigit(c))
                {
                    sb.Append(c);
                    Advance();
                    continue;
                }

                if (c == '.' && !seenDot && !seenExp)
                {
                    seenDot = true;
                    sb.Append(c);
                    Advance();
                    continue;
                }

                if ((c == 'e' || c == 'E') && !seenExp)
                {
                    seenExp = true;
                    sb.Append(c);
                    Advance();
                    if (Current == '+' || Current == '-')
                    {
                        sb.Append(Current);
                        Advance();
                    }
                    continue;
                }

                // suffix letters like f, F, u, l
                if (char.IsLetter(c))
                {
                    sb.Append(c);
                    Advance();
                    continue;
                }

                break;
            }

            return sb.ToString();
        }

        private string ReadIdentifier()
        {
            var sb = new StringBuilder();
            while (!IsAtEnd && IsIdentifierPart(Current))
            {
                sb.Append(Current);
                Advance();
            }
            return sb.ToString();
        }

        private string ReadPunctuator()
        {
            // Try to match longest multi-char punctuator
            foreach (var op in MultiCharPunctuators)
            {
                if (MatchesAhead(op))
                {
                    for (int k = 0; k < op.Length; k++) Advance();
                    return op;
                }
            }

            // Single char punctuators
            var c = Current;
            if (IsSinglePunctuator(c))
            {
                Advance();
                return c.ToString();
            }

            return string.Empty;
        }

        private bool MatchesAhead(string s)
        {
            if (_i + s.Length > _text.Length) return false;
            for (int k = 0; k < s.Length; k++)
            {
                if (_text[_i + k] != s[k]) return false;
            }
            return true;
        }

        private static bool IsSinglePunctuator(char c)
        {
            // typical C/C++ punctuators
            switch (c)
            {
                case '~':
                case '!':
                case '%':
                case '^':
                case '&':
                case '*':
                case '(':
                case ')':
                case '-':
                case '+':
                case '=':
                case '{':
                case '}':
                case '[':
                case ']':
                case '|':
                case '\\':
                case ':':
                case ';':
                case ',':
                case '<':
                case '>':
                case '.':
                case '?':
                case '/':
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsIdentifierStart(char c)
        {
            return c == '_' || char.IsLetter(c);
        }

        private static bool IsIdentifierPart(char c)
        {
            return c == '_' || char.IsLetterOrDigit(c);
        }

        private static bool IsHexDigit(char c)
        {
            return (c >= '0' && c <= '9') ||
                   (c >= 'a' && c <= 'f') ||
                   (c >= 'A' && c <= 'F');
        }

        // Natural-language word splitting for comments, preprocessor lines, and string literals.
        // Splits on whitespace and punctuation; words are runs of letters/digits and internal apostrophes (e.g. "don't").
        private static bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '\'' || c == '_';
        }

        private void EmitWordsFromText(List<TokenWord> tokens, string text, int startLine, int startCol)
        {
            if (string.IsNullOrEmpty(text)) return;

            int curLine = startLine;
            int curCol = startCol;
            StringBuilder sb = null;
            int wordStartLine = 0;
            int wordStartCol = 0;
            int lastCharLine = curLine;
            int lastCharCol = curCol;

            for (int k = 0; k < text.Length; k++)
            {
                char ch = text[k];

                if (IsWordChar(ch))
                {
                    if (sb == null)
                    {
                        sb = new StringBuilder();
                        wordStartLine = curLine;
                        wordStartCol = curCol;
                        lastCharLine = curLine;
                        lastCharCol = curCol;
                    }

                    sb.Append(ch);
                    lastCharLine = curLine;
                    lastCharCol = curCol;
                }
                else
                {
                    if (sb != null)
                    {
                        tokens.Add(new TokenWord(TokenType.Word, sb.ToString(), wordStartLine, wordStartCol, lastCharLine, lastCharCol));
                        sb = null;
                    }
                }

                // advance position after handling character
                if (ch == '\n')
                {
                    curLine++;
                    curCol = 1;
                }
                else
                {
                    curCol++;
                }
            }

            if (sb != null)
            {
                tokens.Add(new TokenWord(TokenType.Word, sb.ToString(), wordStartLine, wordStartCol, lastCharLine, lastCharCol));
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Documents;
using System.Windows.Media;

namespace EditorApp.CodeStyling;

public sealed class CodeStylingApi
{
    private readonly HashSet<string> _keywords = new(StringComparer.Ordinal);
    private readonly HashSet<string> _types = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Brush> _wordOverrides = new(StringComparer.Ordinal);
    private Brush _keywordBrush = new SolidColorBrush(Color.FromRgb(86, 156, 214));
    private Brush _typeBrush = new SolidColorBrush(Color.FromRgb(78, 201, 176));
    private Brush _stringBrush = new SolidColorBrush(Color.FromRgb(214, 157, 133));
    private Brush _commentBrush = new SolidColorBrush(Color.FromRgb(106, 153, 85));
    private Brush _numberBrush = new SolidColorBrush(Color.FromRgb(181, 206, 168));
    private Brush _defaultBrush = Brushes.Gainsboro;
    private bool _glowEnabled;

    public CodeStylingApi()
    {
        LoadDefaultCSharpKeywords();
    }

    public void EnableGlow(bool enabled)
    {
        _glowEnabled = enabled;
    }

    public void SetDefaultColor(Color color)
    {
        _defaultBrush = new SolidColorBrush(color);
    }

    public void SetKeywordColor(Color color)
    {
        _keywordBrush = new SolidColorBrush(color);
    }

    public void SetTypeColor(Color color)
    {
        _typeBrush = new SolidColorBrush(color);
    }

    public void SetStringColor(Color color)
    {
        _stringBrush = new SolidColorBrush(color);
    }

    public void SetCommentColor(Color color)
    {
        _commentBrush = new SolidColorBrush(color);
    }

    public void SetNumberColor(Color color)
    {
        _numberBrush = new SolidColorBrush(color);
    }

    public void AddKeyword(string keyword)
    {
        _keywords.Add(keyword);
    }

    public void RemoveKeyword(string keyword)
    {
        _keywords.Remove(keyword);
    }

    public void AddType(string typeName)
    {
        _types.Add(typeName);
    }

    public void RemoveType(string typeName)
    {
        _types.Remove(typeName);
    }

    public void SetWordColor(string word, Color color)
    {
        _wordOverrides[word] = new SolidColorBrush(color);
    }

    public void RemoveWordColor(string word)
    {
        _wordOverrides.Remove(word);
    }

    public void ClearWordColors()
    {
        _wordOverrides.Clear();
    }

    public FlowDocument BuildDocument(string text, double lineHeight, Brush? defaultBrush = null)
    {
        FlowDocument document = new();
        Paragraph paragraph = new() { LineHeight = lineHeight };

        foreach (Token token in Tokenize(text))
        {
            Brush brush = ResolveBrush(token, defaultBrush ?? _defaultBrush);
            AddTextWithLineBreaks(paragraph, token.Value, brush);
        }

        document.Blocks.Add(paragraph);
        return document;
    }

    private Brush ResolveBrush(Token token, Brush fallback)
    {
        if (token.Kind == TokenKind.Word)
        {
            if (_wordOverrides.TryGetValue(token.Value, out Brush? overrideBrush))
            {
                return overrideBrush;
            }

            if (_keywords.Contains(token.Value))
            {
                return _keywordBrush;
            }

            if (_types.Contains(token.Value))
            {
                return _typeBrush;
            }
        }

        return token.Kind switch
        {
            TokenKind.String => _stringBrush,
            TokenKind.Comment => _commentBrush,
            TokenKind.Number => _numberBrush,
            _ => fallback
        };
    }

    private void AddTextWithLineBreaks(Paragraph paragraph, string text, Brush brush)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        string[] parts = text.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i];
            if (part.Length > 0)
            {
                AddRunWithGlow(paragraph, part, brush);
            }

            if (i < parts.Length - 1)
            {
                paragraph.Inlines.Add(new LineBreak());
            }
        }
    }

    private void AddRunWithGlow(Paragraph paragraph, string text, Brush brush)
    {
        Run run = new(text) { Foreground = brush };
        if (_glowEnabled)
        {
            Color color = brush is SolidColorBrush solid ? solid.Color : Colors.Gainsboro;
            run.TextEffects = new TextEffectCollection
            {
                CreateGlowEffect(color, -1.2, 0),
                CreateGlowEffect(color, 1.2, 0),
                CreateGlowEffect(color, 0, -1.2),
                CreateGlowEffect(color, 0, 1.2),
                CreateGlowEffect(color, -0.8, -0.8),
                CreateGlowEffect(color, 0.8, 0.8)
            };
        }

        paragraph.Inlines.Add(run);
    }

    private static TextEffect CreateGlowEffect(Color color, double dx, double dy)
    {
        return new TextEffect
        {
            PositionStart = 0,
            PositionCount = int.MaxValue,
            Foreground = new SolidColorBrush(Color.FromArgb(140, color.R, color.G, color.B)),
            Transform = new TranslateTransform(dx, dy)
        };
    }

    private IEnumerable<Token> Tokenize(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }

        int i = 0;
        while (i < text.Length)
        {
            char ch = text[i];

            if (ch == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                int start = i;
                i += 2;
                while (i < text.Length && text[i] != '\n')
                {
                    i++;
                }
                yield return new Token(text[start..i], TokenKind.Comment);
                continue;
            }

            if (ch == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                int start = i;
                i += 2;
                while (i + 1 < text.Length && !(text[i] == '*' && text[i + 1] == '/'))
                {
                    i++;
                }
                i = Math.Min(text.Length, i + 2);
                yield return new Token(text[start..i], TokenKind.Comment);
                continue;
            }

            if (IsStringStart(text, i, out int prefixLength))
            {
                int start = i;
                bool verbatim = IsVerbatimString(text, i);
                bool interpolated = IsInterpolatedString(text, i);
                i += prefixLength;

                if (verbatim)
                {
                    while (i < text.Length)
                    {
                        if (text[i] == '"')
                        {
                            if (i + 1 < text.Length && text[i + 1] == '"')
                            {
                                i += 2;
                                continue;
                            }

                            i++;
                            break;
                        }

                        i++;
                    }
                }
                else
                {
                    while (i < text.Length)
                    {
                        if (text[i] == '\\')
                        {
                            i = Math.Min(text.Length, i + 2);
                            continue;
                        }

                        if (text[i] == '"')
                        {
                            i++;
                            break;
                        }

                        i++;
                    }
                }

                _ = interpolated; // placeholder for future tokenization
                yield return new Token(text[start..i], TokenKind.String);
                continue;
            }

            if (char.IsDigit(ch))
            {
                int start = i;
                i++;
                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '.' || text[i] == '_'))
                {
                    i++;
                }
                yield return new Token(text[start..i], TokenKind.Number);
                continue;
            }

            if (IsWordChar(ch))
            {
                int start = i;
                i++;
                while (i < text.Length && IsWordChar(text[i]))
                {
                    i++;
                }
                yield return new Token(text[start..i], TokenKind.Word);
                continue;
            }

            int singleStart = i;
            i++;
            yield return new Token(text[singleStart..i], TokenKind.Text);
        }
    }

    private static bool IsStringStart(string text, int index, out int prefixLength)
    {
        prefixLength = 0;
        if (text[index] == '"')
        {
            prefixLength = 1;
            return true;
        }

        if (text[index] == '@' && index + 1 < text.Length && text[index + 1] == '"')
        {
            prefixLength = 2;
            return true;
        }

        if (text[index] == '$' && index + 1 < text.Length && text[index + 1] == '"')
        {
            prefixLength = 2;
            return true;
        }

        if (text[index] == '$' && index + 2 < text.Length && text[index + 1] == '@' && text[index + 2] == '"')
        {
            prefixLength = 3;
            return true;
        }

        if (text[index] == '@' && index + 2 < text.Length && text[index + 1] == '$' && text[index + 2] == '"')
        {
            prefixLength = 3;
            return true;
        }

        return false;
    }

    private static bool IsVerbatimString(string text, int index)
    {
        return (text[index] == '@') || (text[index] == '$' && index + 1 < text.Length && text[index + 1] == '@');
    }

    private static bool IsInterpolatedString(string text, int index)
    {
        return (text[index] == '$') || (text[index] == '@' && index + 1 < text.Length && text[index + 1] == '$');
    }

    private static bool IsWordChar(char ch)
    {
        return char.IsLetterOrDigit(ch) || ch == '_';
    }

    private void LoadDefaultCSharpKeywords()
    {
        string[] keywords =
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum",
            "event", "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto",
            "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace",
            "new", "null", "object", "operator", "out", "override", "params", "private", "protected", "public",
            "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string",
            "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe",
            "ushort", "using", "virtual", "void", "volatile", "while", "var", "record", "init", "with", "when",
            "required", "global", "file", "nint", "nuint"
        };

        string[] types =
        {
            "int", "string", "float", "double", "bool", "byte", "char", "decimal", "long", "short", "object",
            "void", "uint", "ulong", "ushort", "sbyte", "nint", "nuint"
        };

        foreach (string keyword in keywords)
        {
            _keywords.Add(keyword);
        }

        foreach (string typeName in types)
        {
            _types.Add(typeName);
        }
    }

    private readonly record struct Token(string Value, TokenKind Kind);

    private enum TokenKind
    {
        Text,
        Word,
        String,
        Comment,
        Number
    }
}

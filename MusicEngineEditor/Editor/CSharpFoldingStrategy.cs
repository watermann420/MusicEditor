// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code folding strategy for C# scripts.

using System.Collections.Generic;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;

namespace MusicEngineEditor.Editor;

/// <summary>
/// Code folding strategy for C# that supports folding of:
/// - Regions (#region ... #endregion)
/// - Braces { ... }
/// - Using statements
/// - Multi-line comments /* ... */
/// - XML documentation comments /// ...
/// </summary>
public class CSharpFoldingStrategy
{
    /// <summary>
    /// Creates folding data for the document
    /// </summary>
    public IEnumerable<NewFolding> CreateFoldings(ITextSource document)
    {
        var foldings = new List<NewFolding>();
        var text = document.Text;

        // Track brace positions for { } folding
        var braceStack = new Stack<int>();

        // Track region positions
        var regionStack = new Stack<int>();

        // Track multi-line comment positions
        int commentStart = -1;

        // Track using block
        int usingStart = -1;
        int lastUsingEnd = -1;

        int i = 0;
        while (i < text.Length)
        {
            // Check for region start
            if (i + 7 < text.Length && text.Substring(i, 8) == "#region ")
            {
                regionStack.Push(i);
                i += 7;
                continue;
            }

            // Check for endregion
            if (i + 10 < text.Length && text.Substring(i, 10) == "#endregion")
            {
                if (regionStack.Count > 0)
                {
                    int start = regionStack.Pop();
                    // Get the region name from the line
                    int lineEnd = text.IndexOf('\n', start);
                    string regionLine = lineEnd > start ? text.Substring(start, lineEnd - start) : text.Substring(start);
                    string name = regionLine.Length > 8 ? regionLine.Substring(8).Trim() : "region";

                    foldings.Add(new NewFolding(start, i + 10)
                    {
                        Name = $"#region {name}",
                        DefaultClosed = false
                    });
                }
                i += 10;
                continue;
            }

            // Check for multi-line comment start /*
            if (i + 1 < text.Length && text[i] == '/' && text[i + 1] == '*')
            {
                commentStart = i;
                i += 2;
                continue;
            }

            // Check for multi-line comment end */
            if (i + 1 < text.Length && text[i] == '*' && text[i + 1] == '/')
            {
                if (commentStart >= 0)
                {
                    foldings.Add(new NewFolding(commentStart, i + 2)
                    {
                        Name = "/* ... */",
                        DefaultClosed = false
                    });
                    commentStart = -1;
                }
                i += 2;
                continue;
            }

            // Skip if we're inside a multi-line comment
            if (commentStart >= 0)
            {
                i++;
                continue;
            }

            // Check for using statements (at the beginning of file)
            if (text.Substring(i).StartsWith("using ") && (i == 0 || text[i - 1] == '\n'))
            {
                if (usingStart < 0)
                {
                    usingStart = i;
                }
                // Find end of using statement
                int semicolon = text.IndexOf(';', i);
                if (semicolon > 0)
                {
                    lastUsingEnd = semicolon + 1;
                    i = semicolon + 1;
                    continue;
                }
            }
            else if (usingStart >= 0 && lastUsingEnd > usingStart)
            {
                // End of using block - only fold if there are multiple usings
                var usingBlock = text.Substring(usingStart, lastUsingEnd - usingStart);
                if (usingBlock.Split('\n').Length > 2)
                {
                    foldings.Add(new NewFolding(usingStart, lastUsingEnd)
                    {
                        Name = "using ...",
                        DefaultClosed = true
                    });
                }
                usingStart = -1;
                lastUsingEnd = -1;
            }

            // Check for opening brace
            if (text[i] == '{')
            {
                braceStack.Push(i);
                i++;
                continue;
            }

            // Check for closing brace
            if (text[i] == '}')
            {
                if (braceStack.Count > 0)
                {
                    int openBrace = braceStack.Pop();
                    int closeBrace = i;

                    // Only fold if it spans multiple lines
                    if (CountLines(text, openBrace, closeBrace) > 1)
                    {
                        // Try to get the name (function/class name before the brace)
                        string name = GetFoldingName(text, openBrace);

                        foldings.Add(new NewFolding(openBrace, closeBrace + 1)
                        {
                            Name = name,
                            DefaultClosed = false
                        });
                    }
                }
                i++;
                continue;
            }

            i++;
        }

        // Handle any remaining using block at end of processing
        if (usingStart >= 0 && lastUsingEnd > usingStart)
        {
            var usingBlock = text.Substring(usingStart, lastUsingEnd - usingStart);
            if (usingBlock.Split('\n').Length > 2)
            {
                foldings.Add(new NewFolding(usingStart, lastUsingEnd)
                {
                    Name = "using ...",
                    DefaultClosed = true
                });
            }
        }

        // Sort by start offset
        foldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));

        return foldings;
    }

    private static int CountLines(string text, int start, int end)
    {
        int count = 0;
        for (int i = start; i < end && i < text.Length; i++)
        {
            if (text[i] == '\n') count++;
        }
        return count;
    }

    private static string GetFoldingName(string text, int bracePosition)
    {
        // Look back from the brace to find the context
        int lineStart = bracePosition;
        while (lineStart > 0 && text[lineStart - 1] != '\n')
            lineStart--;

        // Get the line before the brace
        string beforeBrace = text.Substring(lineStart, bracePosition - lineStart).Trim();

        // Try to identify what kind of block this is
        if (beforeBrace.Contains("class "))
            return ExtractName(beforeBrace, "class");
        if (beforeBrace.Contains("struct "))
            return ExtractName(beforeBrace, "struct");
        if (beforeBrace.Contains("interface "))
            return ExtractName(beforeBrace, "interface");
        if (beforeBrace.Contains("namespace "))
            return ExtractName(beforeBrace, "namespace");
        if (beforeBrace.Contains("enum "))
            return ExtractName(beforeBrace, "enum");
        if (beforeBrace.Contains("(") && beforeBrace.Contains(")"))
        {
            // Likely a method
            int parenStart = beforeBrace.IndexOf('(');
            string beforeParen = beforeBrace.Substring(0, parenStart).Trim();
            var parts = beforeParen.Split(' ');
            if (parts.Length > 0)
            {
                return parts[^1] + "(...)";
            }
        }
        if (beforeBrace.Contains("if ") || beforeBrace.Contains("if("))
            return "if { ... }";
        if (beforeBrace.Contains("else"))
            return "else { ... }";
        if (beforeBrace.Contains("for ") || beforeBrace.Contains("for("))
            return "for { ... }";
        if (beforeBrace.Contains("foreach ") || beforeBrace.Contains("foreach("))
            return "foreach { ... }";
        if (beforeBrace.Contains("while ") || beforeBrace.Contains("while("))
            return "while { ... }";
        if (beforeBrace.Contains("switch ") || beforeBrace.Contains("switch("))
            return "switch { ... }";
        if (beforeBrace.Contains("try"))
            return "try { ... }";
        if (beforeBrace.Contains("catch"))
            return "catch { ... }";
        if (beforeBrace.Contains("finally"))
            return "finally { ... }";

        return "{ ... }";
    }

    private static string ExtractName(string line, string keyword)
    {
        int keywordIndex = line.IndexOf(keyword);
        if (keywordIndex < 0) return $"{keyword} {{ ... }}";

        string afterKeyword = line.Substring(keywordIndex + keyword.Length).Trim();
        // Get the first word (the name)
        int spaceIndex = afterKeyword.IndexOfAny(new[] { ' ', '<', ':', '{', '(' });
        if (spaceIndex > 0)
        {
            return $"{keyword} {afterKeyword.Substring(0, spaceIndex)}";
        }
        return $"{keyword} {afterKeyword}";
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LaunchPadBooster.Analyzers
{
  public static class CodeGenerator
  {
    private static IEnumerable<CodeElement> Flatten(CodeElement code)
    {
      if (code.Text != null)
        yield return code;
      else
        foreach (var el in code.Elements)
          foreach (var flatEl in Flatten(el))
            yield return flatEl.Indent(code.IndentLevel);
    }

    private static IEnumerable<CodeElement> AutoIndent(IEnumerable<CodeElement> code)
    {
      var level = 0;
      foreach (var el in Flatten(CodeElement.List(code)))
      {
        if (IsUnindent(el.Text))
          level--;

        yield return el.Indent(level);

        if (IsIndent(el.Text))
          level++;
      }
    }

    private static bool IsIndent(string text) => text.EndsWith("{");
    private static bool IsUnindent(string text) => text.StartsWith("}");

    public static CodeElement Element(this IEnumerable<CodeElement> elements) => CodeElement.List(elements);

    public static string ToCodeString(this IEnumerable<CodeElement> elements)
    {
      var sb = new StringBuilder();
      var skipEmpty = true;
      foreach (var el in AutoIndent(elements))
      {
        if (el.Text == "")
        {
          if (!skipEmpty)
            sb.AppendLine();
          skipEmpty = true;
          continue;
        }
        skipEmpty = IsIndent(el.Text);
        for (var i = 0; i < el.IndentLevel; i++)
          sb.Append("  ");
        sb.AppendLine(el.Text);
      }
      return sb.ToString();
    }
  }

  public readonly struct CodeElement
  {
    public readonly int IndentLevel;
    public readonly string Text;
    public readonly IEnumerable<CodeElement> Elements;

    private CodeElement(string text)
    {
      IndentLevel = 0;
      Text = text;
      Elements = null;
    }

    private CodeElement(IEnumerable<CodeElement> elements)
    {
      IndentLevel = 0;
      Text = null;
      Elements = elements;
    }

    private CodeElement(CodeElement other, int indent)
    {
      IndentLevel = other.IndentLevel + indent;
      Text = other.Text;
      Elements = other.Elements;
    }

    public CodeElement Indent(int by) => new(this, by);

    public static implicit operator CodeElement(string text)
    {
      var lines = text.Split("\n", StringSplitOptions.TrimEntries);
      if (lines.Length == 1)
        return new(lines[0]);
      else
        return new(lines.Select(line => new CodeElement(line)));
    }
    public static CodeElement List(IEnumerable<CodeElement> elements) => new(elements);

    public static CodeElement BlankLine => "";
  }
}
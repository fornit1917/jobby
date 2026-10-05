using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Jobby.Dashboard.Client.Shared;

public static class JsonHighlighter
{
    public static string Highlight(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return string.Empty;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var sb = new StringBuilder();
            WriteElement(sb, doc.RootElement, 0);
            return sb.ToString();
        }
        catch
        {
            return Encode(json);
        }
    }

    private const string IndentUnit = "  ";

    private static void WriteElement(StringBuilder sb, JsonElement element, int depth)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                WriteObject(sb, element, depth);
                break;
            case JsonValueKind.Array:
                WriteArray(sb, element, depth);
                break;
            case JsonValueKind.String:
                WriteSpan(sb, "jobby-json-str", Quote(element.GetString()));
                break;
            case JsonValueKind.Number:
                WriteSpan(sb, "jobby-json-num", element.GetRawText());
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                WriteSpan(sb, "jobby-json-bool", element.GetRawText());
                break;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
            default:
                WriteSpan(sb, "jobby-json-null", "null");
                break;
        }
    }

    private static void WriteObject(StringBuilder sb, JsonElement element, int depth)
    {
        sb.Append('{');
        var first = true;
        foreach (var property in element.EnumerateObject())
        {
            if (!first)
            {
                sb.Append(',');
            }

            first = false;
            AppendNewLineIndent(sb, depth + 1);
            WriteSpan(sb, "jobby-json-key", Quote(property.Name));
            sb.Append(": ");
            WriteElement(sb, property.Value, depth + 1);
        }

        if (!first)
        {
            AppendNewLineIndent(sb, depth);
        }

        sb.Append('}');
    }

    private static void WriteArray(StringBuilder sb, JsonElement element, int depth)
    {
        sb.Append('[');
        var first = true;
        foreach (var item in element.EnumerateArray())
        {
            if (!first)
            {
                sb.Append(',');
            }

            first = false;
            AppendNewLineIndent(sb, depth + 1);
            WriteElement(sb, item, depth + 1);
        }

        if (!first)
        {
            AppendNewLineIndent(sb, depth);
        }

        sb.Append(']');
    }

    private static void AppendNewLineIndent(StringBuilder sb, int depth)
    {
        sb.Append('\n');
        for (var i = 0; i < depth; i++)
        {
            sb.Append(IndentUnit);
        }
    }

    private static void WriteSpan(StringBuilder sb, string cssClass, string rawValue)
    {
        sb.Append("<span class=\"")
            .Append(cssClass)
            .Append("\">")
            .Append(Encode(rawValue))
            .Append("</span>");
    }

    private static string Quote(string? value) => "\"" + value + "\"";

    private static string Encode(string value) => HtmlEncoder.Default.Encode(value);
}
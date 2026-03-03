using System.Text;

namespace IssuePit.Api.Services;

public enum BadgeStyle
{
    Flat,
    FlatSquare,
    Plastic,
}

/// <summary>Generates shields.io-compatible SVG status badges.</summary>
public static class BadgeSvgService
{
    // Approximate character width table (DejaVu Sans 11px) in tenths of a pixel.
    // These match the widths used by shields.io for consistent sizing.
    private static readonly Dictionary<char, int> CharWidths = new()
    {
        [' '] = 33, ['!'] = 37, ['"'] = 47, ['#'] = 82, ['$'] = 65, ['%'] = 82, ['&'] = 78,
        ['\''] = 28, ['('] = 39, [')'] = 39, ['*'] = 55, ['+'] = 82, [','] = 37, ['-'] = 42,
        ['.'] = 37, ['/'] = 45, ['0'] = 65, ['1'] = 65, ['2'] = 65, ['3'] = 65, ['4'] = 65,
        ['5'] = 65, ['6'] = 65, ['7'] = 65, ['8'] = 65, ['9'] = 65, [':'] = 37, [';'] = 37,
        ['<'] = 82, ['='] = 82, ['>'] = 82, ['?'] = 55, ['@'] = 109, ['A'] = 78, ['B'] = 71,
        ['C'] = 71, ['D'] = 78, ['E'] = 65, ['F'] = 61, ['G'] = 78, ['H'] = 78, ['I'] = 28,
        ['J'] = 45, ['K'] = 74, ['L'] = 61, ['M'] = 87, ['N'] = 78, ['O'] = 82, ['P'] = 65,
        ['Q'] = 82, ['R'] = 74, ['S'] = 65, ['T'] = 65, ['U'] = 78, ['V'] = 74, ['W'] = 100,
        ['X'] = 74, ['Y'] = 74, ['Z'] = 71, ['['] = 37, ['\\'] = 45, [']'] = 37, ['^'] = 82,
        ['_'] = 55, ['`'] = 55, ['a'] = 61, ['b'] = 65, ['c'] = 55, ['d'] = 65, ['e'] = 61,
        ['f'] = 37, ['g'] = 65, ['h'] = 65, ['i'] = 28, ['j'] = 28, ['k'] = 61, ['l'] = 28,
        ['m'] = 96, ['n'] = 65, ['o'] = 65, ['p'] = 65, ['q'] = 65, ['r'] = 41, ['s'] = 55,
        ['t'] = 45, ['u'] = 65, ['v'] = 61, ['w'] = 82, ['x'] = 61, ['y'] = 61, ['z'] = 55,
    };

    private static int MeasureText(string text)
    {
        var width = 0;
        foreach (var c in text)
            width += CharWidths.TryGetValue(c, out var w) ? w : 65;
        return width;
    }

    public static string Generate(
        string label,
        string value,
        string color,
        BadgeStyle style = BadgeStyle.Flat)
    {
        // Convert color shorthand to hex
        var colorHex = color switch
        {
            "brightgreen" => "#4c1",
            "green" => "#97CA00",
            "yellow" => "#dfb317",
            "yellowgreen" => "#a4a61d",
            "orange" => "#fe7d37",
            "red" => "#e05d44",
            "blue" => "#007ec6",
            "lightgrey" => "#9f9f9f",
            "grey" => "#555",
            _ => color.StartsWith('#') ? color : "#" + color,
        };

        var labelWidthTenths = MeasureText(label) + 100; // 10px padding on each side
        var valueWidthTenths = MeasureText(value) + 100;
        var totalWidthTenths = labelWidthTenths + valueWidthTenths;

        // Convert tenths to actual pixel values
        var labelWidth = (int)Math.Ceiling(labelWidthTenths / 10.0);
        var valueWidth = (int)Math.Ceiling(valueWidthTenths / 10.0);
        var totalWidth = labelWidth + valueWidth;

        var labelCenter = (int)(labelWidthTenths / 2.0);
        var valueCenter = (int)(labelWidthTenths + valueWidthTenths / 2.0);

        return style switch
        {
            BadgeStyle.FlatSquare => RenderFlatSquare(label, value, colorHex, labelWidth, valueWidth, totalWidth, labelCenter, valueCenter, labelWidthTenths, valueWidthTenths),
            BadgeStyle.Plastic => RenderPlastic(label, value, colorHex, labelWidth, valueWidth, totalWidth, labelCenter, valueCenter, labelWidthTenths, valueWidthTenths),
            _ => RenderFlat(label, value, colorHex, labelWidth, valueWidth, totalWidth, labelCenter, valueCenter, labelWidthTenths, valueWidthTenths),
        };
    }

    private static string RenderFlat(
        string label, string value, string color,
        int labelWidth, int valueWidth, int totalWidth,
        int labelCenter, int valueCenter,
        int labelTextLen, int valueTextLen)
    {
        var sb = new StringBuilder();
        sb.Append($"""<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="{totalWidth}" height="20" role="img" aria-label="{EscapeXml(label)}: {EscapeXml(value)}">""");
        sb.Append($"""<title>{EscapeXml(label)}: {EscapeXml(value)}</title>""");
        sb.Append("""<linearGradient id="s" x2="0" y2="100%"><stop offset="0" stop-color="#bbb" stop-opacity=".1"/><stop offset="1" stop-opacity=".1"/></linearGradient>""");
        sb.Append($"""<clipPath id="r"><rect width="{totalWidth}" height="20" rx="3" fill="#fff"/></clipPath>""");
        sb.Append("""<g clip-path="url(#r)">""");
        sb.Append($"""<rect width="{labelWidth}" height="20" fill="#555"/>""");
        sb.Append($"""<rect x="{labelWidth}" width="{valueWidth}" height="20" fill="{color}"/>""");
        sb.Append($"""<rect width="{totalWidth}" height="20" fill="url(#s)"/>""");
        sb.Append("</g>");
        sb.Append("""<g fill="#fff" text-anchor="middle" font-family="DejaVu Sans,Verdana,Geneva,sans-serif" font-size="110">""");
        sb.Append($"""<text aria-hidden="true" x="{labelCenter}" y="150" fill="#010101" fill-opacity=".3" transform="scale(.1)" textLength="{labelTextLen}" lengthAdjust="spacing">{EscapeXml(label)}</text>""");
        sb.Append($"""<text x="{labelCenter}" y="140" transform="scale(.1)" textLength="{labelTextLen}" lengthAdjust="spacing">{EscapeXml(label)}</text>""");
        sb.Append($"""<text aria-hidden="true" x="{valueCenter}" y="150" fill="#010101" fill-opacity=".3" transform="scale(.1)" textLength="{valueTextLen}" lengthAdjust="spacing">{EscapeXml(value)}</text>""");
        sb.Append($"""<text x="{valueCenter}" y="140" transform="scale(.1)" textLength="{valueTextLen}" lengthAdjust="spacing">{EscapeXml(value)}</text>""");
        sb.Append("</g></svg>");
        return sb.ToString();
    }

    private static string RenderFlatSquare(
        string label, string value, string color,
        int labelWidth, int valueWidth, int totalWidth,
        int labelCenter, int valueCenter,
        int labelTextLen, int valueTextLen)
    {
        var sb = new StringBuilder();
        sb.Append($"""<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="{totalWidth}" height="20" role="img" aria-label="{EscapeXml(label)}: {EscapeXml(value)}">""");
        sb.Append($"""<title>{EscapeXml(label)}: {EscapeXml(value)}</title>""");
        sb.Append("""<g shape-rendering="crispEdges">""");
        sb.Append($"""<rect width="{labelWidth}" height="20" fill="#555"/>""");
        sb.Append($"""<rect x="{labelWidth}" width="{valueWidth}" height="20" fill="{color}"/>""");
        sb.Append("</g>");
        sb.Append("""<g fill="#fff" text-anchor="middle" font-family="DejaVu Sans,Verdana,Geneva,sans-serif" font-size="110">""");
        sb.Append($"""<text aria-hidden="true" x="{labelCenter}" y="150" fill="#010101" fill-opacity=".3" transform="scale(.1)" textLength="{labelTextLen}" lengthAdjust="spacing">{EscapeXml(label)}</text>""");
        sb.Append($"""<text x="{labelCenter}" y="140" transform="scale(.1)" textLength="{labelTextLen}" lengthAdjust="spacing">{EscapeXml(label)}</text>""");
        sb.Append($"""<text aria-hidden="true" x="{valueCenter}" y="150" fill="#010101" fill-opacity=".3" transform="scale(.1)" textLength="{valueTextLen}" lengthAdjust="spacing">{EscapeXml(value)}</text>""");
        sb.Append($"""<text x="{valueCenter}" y="140" transform="scale(.1)" textLength="{valueTextLen}" lengthAdjust="spacing">{EscapeXml(value)}</text>""");
        sb.Append("</g></svg>");
        return sb.ToString();
    }

    private static string RenderPlastic(
        string label, string value, string color,
        int labelWidth, int valueWidth, int totalWidth,
        int labelCenter, int valueCenter,
        int labelTextLen, int valueTextLen)
    {
        var sb = new StringBuilder();
        sb.Append($"""<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="{totalWidth}" height="18" role="img" aria-label="{EscapeXml(label)}: {EscapeXml(value)}">""");
        sb.Append($"""<title>{EscapeXml(label)}: {EscapeXml(value)}</title>""");
        sb.Append("""<linearGradient id="s" x2="0" y2="100%"><stop offset="0" stop-color="#fff" stop-opacity=".7"/><stop offset=".1" stop-color="#aaa" stop-opacity=".1"/><stop offset=".9" stop-opacity=".3"/><stop offset="1" stop-opacity=".5"/></linearGradient>""");
        sb.Append($"""<clipPath id="r"><rect width="{totalWidth}" height="18" rx="4" fill="#fff"/></clipPath>""");
        sb.Append("""<g clip-path="url(#r)">""");
        sb.Append($"""<rect width="{labelWidth}" height="18" fill="#555"/>""");
        sb.Append($"""<rect x="{labelWidth}" width="{valueWidth}" height="18" fill="{color}"/>""");
        sb.Append($"""<rect width="{totalWidth}" height="18" fill="url(#s)"/>""");
        sb.Append("</g>");
        sb.Append("""<g fill="#fff" text-anchor="middle" font-family="DejaVu Sans,Verdana,Geneva,sans-serif" font-size="110">""");
        sb.Append($"""<text aria-hidden="true" x="{labelCenter}" y="140" fill="#010101" fill-opacity=".3" transform="scale(.1)" textLength="{labelTextLen}" lengthAdjust="spacing">{EscapeXml(label)}</text>""");
        sb.Append($"""<text x="{labelCenter}" y="130" transform="scale(.1)" textLength="{labelTextLen}" lengthAdjust="spacing">{EscapeXml(label)}</text>""");
        sb.Append($"""<text aria-hidden="true" x="{valueCenter}" y="140" fill="#010101" fill-opacity=".3" transform="scale(.1)" textLength="{valueTextLen}" lengthAdjust="spacing">{EscapeXml(value)}</text>""");
        sb.Append($"""<text x="{valueCenter}" y="130" transform="scale(.1)" textLength="{valueTextLen}" lengthAdjust="spacing">{EscapeXml(value)}</text>""");
        sb.Append("</g></svg>");
        return sb.ToString();
    }

    private static string EscapeXml(string value)
        => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");
}

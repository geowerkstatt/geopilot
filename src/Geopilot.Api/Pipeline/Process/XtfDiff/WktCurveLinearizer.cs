using System.Globalization;

namespace Geopilot.Api.Pipeline.Process.XtfDiff;

/// <summary>
/// Converts the curve WKT types emitted by the XTF-Diff-Tool (CIRCULARSTRING, COMPOUNDCURVE,
/// CURVEPOLYGON, MULTICURVE, MULTISURFACE) into their linear equivalents (LINESTRING, POLYGON,
/// MULTILINESTRING, MULTIPOLYGON) by stroking arc segments into short line segments. The map
/// client parses feature geometries with the OpenLayers WKT parser which, like most WKT
/// implementations, does not support curve types — without linearization such features are
/// silently dropped from the map. Already-linear WKT is returned unchanged.
/// </summary>
/// <remarks>
/// The supported dialect is the one produced by the tool's <c>WKTWriterJtsext</c> (iox-ili):
/// arcs are CIRCULARSTRING segments of three points each; COMPOUNDCURVE components and
/// CURVEPOLYGON rings are either bare coordinate lists or nested curve elements.
/// </remarks>
internal static class WktCurveLinearizer
{
    /// <summary>
    /// Maximum sagitta (distance between an arc and its replacing chord) of stroked arc
    /// segments, in coordinate units (meters for LV95).
    /// </summary>
    private const double ArcStrokeTolerance = 0.01;

    /// <summary>Upper bound of generated segments per arc, guarding against degenerate radii.</summary>
    private const int MaxSegmentsPerArc = 256;

    /// <summary>Decimal places of the formatted coordinates (millimeters for LV95).</summary>
    private const int OutputDecimals = 3;

    /// <summary>
    /// Returns the linear-geometry equivalent of <paramref name="wkt"/>. Linear types pass
    /// through unchanged; malformed input is also returned unchanged (the map client skips
    /// features it cannot parse, so this degrades to the previous behavior).
    /// </summary>
    public static string Linearize(string wkt)
    {
        if (string.IsNullOrWhiteSpace(wkt))
            return wkt;

        try
        {
            return TryLinearize(wkt) ?? wkt;
        }
        catch (FormatException)
        {
            return wkt;
        }
        catch (OverflowException)
        {
            return wkt;
        }
    }

    /// <summary>
    /// Linearizes the given curve WKT, or returns null for non-curve (already linear) types.
    /// </summary>
    private static string? TryLinearize(string wkt)
    {
        var text = wkt.Trim();
        var keyword = ReadKeyword(text);
        if (text[keyword.Length..].TrimStart().StartsWith("EMPTY", StringComparison.OrdinalIgnoreCase))
            return null;

        switch (keyword)
        {
            case "CIRCULARSTRING":
            case "COMPOUNDCURVE":
                return "LINESTRING " + FormatPointList(ParseCurve(text));
            case "CURVEPOLYGON":
                return "POLYGON " + FormatRingList(ParseRings(Body(text)));
            case "MULTICURVE":
                var curves = SplitTopLevel(Body(text)).Select(ParseCurve);
                return "MULTILINESTRING (" + string.Join(", ", curves.Select(FormatPointList)) + ")";
            case "MULTISURFACE":
                var surfaces = SplitTopLevel(Body(text)).Select(ParseSurfaceMember);
                return "MULTIPOLYGON (" + string.Join(", ", surfaces.Select(FormatRingList)) + ")";
            default:
                return null;
        }
    }

    /// <summary>
    /// Parses a curve into its stroked point chain. Accepts a CIRCULARSTRING, a COMPOUNDCURVE
    /// (whose components are again curves) or a bare coordinate list <c>(x y, x y, ...)</c>.
    /// </summary>
    private static List<(double X, double Y)> ParseCurve(string text)
    {
        var keyword = ReadKeyword(text);
        switch (keyword)
        {
            case "CIRCULARSTRING":
                return StrokeCircularString(ParsePoints(Body(text)));
            case "COMPOUNDCURVE":
                var points = new List<(double X, double Y)>();
                foreach (var component in SplitTopLevel(Body(text)))
                    AppendChain(points, ParseCurve(component));
                return points;
            case "":
                return ParsePoints(Body(text));
            default:
                throw new FormatException($"Unsupported curve component <{keyword}>.");
        }
    }

    /// <summary>
    /// Parses the rings of a CURVEPOLYGON body; each ring is a curve (see <see cref="ParseCurve"/>).
    /// </summary>
    private static List<List<(double X, double Y)>> ParseRings(string body)
    {
        return SplitTopLevel(body).Select(ParseCurve).ToList();
    }

    /// <summary>
    /// Parses one member of a MULTISURFACE: a CURVEPOLYGON, a POLYGON or a bare ring list
    /// <c>((x y, ...), (x y, ...))</c>.
    /// </summary>
    private static List<List<(double X, double Y)>> ParseSurfaceMember(string text)
    {
        var keyword = ReadKeyword(text);
        return keyword switch
        {
            "CURVEPOLYGON" or "POLYGON" or "" => ParseRings(Body(text)),
            _ => throw new FormatException($"Unsupported surface member <{keyword}>."),
        };
    }

    /// <summary>
    /// Appends a point chain to <paramref name="points"/>, dropping the chain's first point if it
    /// repeats the current last point (consecutive curve components share their junction point).
    /// </summary>
    private static void AppendChain(List<(double X, double Y)> points, List<(double X, double Y)> chain)
    {
        foreach (var point in chain)
        {
            if (points.Count > 0)
            {
                var last = points[^1];
                if (last.X == point.X && last.Y == point.Y)
                    continue;
            }

            points.Add(point);
        }
    }

    /// <summary>
    /// Strokes a CIRCULARSTRING point list (an odd number of points; each consecutive triple
    /// start/mid/end defines one arc) into a dense chain of line-segment points.
    /// </summary>
    private static List<(double X, double Y)> StrokeCircularString(List<(double X, double Y)> points)
    {
        if (points.Count < 3)
            return points;

        var result = new List<(double X, double Y)> { points[0] };
        for (var i = 0; i + 2 < points.Count; i += 2)
            AppendStrokedArc(result, points[i], points[i + 1], points[i + 2]);

        return result;
    }

    /// <summary>
    /// Appends the stroked arc from <paramref name="start"/> through <paramref name="mid"/> to
    /// <paramref name="end"/> to <paramref name="points"/> (which already ends with <paramref name="start"/>).
    /// Collinear points degrade to straight segments.
    /// </summary>
    private static void AppendStrokedArc(List<(double X, double Y)> points, (double X, double Y) start, (double X, double Y) mid, (double X, double Y) end)
    {
        // Circumcenter of the three arc points; near-zero d means collinear points (a straight "arc").
        var d = 2 * ((start.X * (mid.Y - end.Y)) + (mid.X * (end.Y - start.Y)) + (end.X * (start.Y - mid.Y)));
        if (Math.Abs(d) < 1e-6)
        {
            points.Add(mid);
            points.Add(end);
            return;
        }

        var squaredStart = (start.X * start.X) + (start.Y * start.Y);
        var squaredMid = (mid.X * mid.X) + (mid.Y * mid.Y);
        var squaredEnd = (end.X * end.X) + (end.Y * end.Y);
        var centerX = ((squaredStart * (mid.Y - end.Y)) + (squaredMid * (end.Y - start.Y)) + (squaredEnd * (start.Y - mid.Y))) / d;
        var centerY = ((squaredStart * (end.X - mid.X)) + (squaredMid * (start.X - end.X)) + (squaredEnd * (mid.X - start.X))) / d;
        var radius = Math.Sqrt(((start.X - centerX) * (start.X - centerX)) + ((start.Y - centerY) * (start.Y - centerY)));

        var startAngle = Math.Atan2(start.Y - centerY, start.X - centerX);
        var midAngle = Math.Atan2(mid.Y - centerY, mid.X - centerX);
        var endAngle = Math.Atan2(end.Y - centerY, end.X - centerX);

        // Sweep counter-clockwise from start to end; if the mid point does not lie on that sweep,
        // the arc runs clockwise instead.
        var sweepCcw = NormalizeAngle(endAngle - startAngle);
        var midOffsetCcw = NormalizeAngle(midAngle - startAngle);
        var sweep = midOffsetCcw <= sweepCcw ? sweepCcw : sweepCcw - (2 * Math.PI);

        // Angular step so the chord of each generated segment stays within the stroke tolerance.
        var maxStep = radius > ArcStrokeTolerance ? 2 * Math.Acos(1 - (ArcStrokeTolerance / radius)) : Math.PI / 4;
        var segments = (int)Math.Clamp(Math.Ceiling(Math.Abs(sweep) / maxStep), 1, MaxSegmentsPerArc);

        for (var i = 1; i < segments; i++)
        {
            var angle = startAngle + (sweep * i / segments);
            points.Add((centerX + (radius * Math.Cos(angle)), centerY + (radius * Math.Sin(angle))));
        }

        // The exact end point closes the arc, avoiding floating-point drift at ring junctions.
        points.Add(end);
    }

    /// <summary>Normalizes an angle to the range [0, 2π).</summary>
    private static double NormalizeAngle(double angle)
    {
        var normalized = angle % (2 * Math.PI);
        return normalized < 0 ? normalized + (2 * Math.PI) : normalized;
    }

    /// <summary>Reads the leading WKT keyword; empty for bare parenthesized lists.</summary>
    private static string ReadKeyword(string text)
    {
        var index = 0;
        while (index < text.Length && char.IsWhiteSpace(text[index]))
            index++;
        var start = index;
        while (index < text.Length && char.IsLetter(text[index]))
            index++;
        return text[start..index].ToUpperInvariant();
    }

    /// <summary>Returns the content between the first opening and the last closing parenthesis.</summary>
    private static string Body(string text)
    {
        var open = text.IndexOf('(');
        var close = text.LastIndexOf(')');
        if (open < 0 || close <= open)
            throw new FormatException($"Missing parentheses in WKT segment <{text}>.");
        return text[(open + 1)..close];
    }

    /// <summary>Splits a WKT body at the commas of the current nesting level.</summary>
    private static List<string> SplitTopLevel(string body)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < body.Length; i++)
        {
            var character = body[i];
            if (character == '(')
            {
                depth++;
            }
            else if (character == ')')
            {
                depth--;
            }
            else if (character == ',' && depth == 0)
            {
                parts.Add(body[start..i]);
                start = i + 1;
            }
        }

        parts.Add(body[start..]);
        return parts.Select(part => part.Trim()).ToList();
    }

    /// <summary>Parses a bare coordinate list <c>x y[ z], x y[ z], ...</c>; extra dimensions are dropped.</summary>
    private static List<(double X, double Y)> ParsePoints(string body)
    {
        var points = new List<(double X, double Y)>();
        foreach (var token in body.Split(','))
        {
            var ordinates = token.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (ordinates.Length < 2)
                throw new FormatException($"Invalid WKT coordinate <{token}>.");
            points.Add((
                double.Parse(ordinates[0], CultureInfo.InvariantCulture),
                double.Parse(ordinates[1], CultureInfo.InvariantCulture)));
        }

        return points;
    }

    private static string FormatPointList(List<(double X, double Y)> points)
    {
        return "(" + string.Join(", ", points.Select(point => FormatOrdinate(point.X) + " " + FormatOrdinate(point.Y))) + ")";
    }

    private static string FormatRingList(List<List<(double X, double Y)>> rings)
    {
        return "(" + string.Join(", ", rings.Select(FormatPointList)) + ")";
    }

    private static string FormatOrdinate(double value)
    {
        return Math.Round(value, OutputDecimals).ToString(CultureInfo.InvariantCulture);
    }
}

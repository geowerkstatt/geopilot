using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace Geopilot.Pipeline.Channels.Mail;

/// <summary>
/// The template of a mail: HTML whose <c>&lt;title&gt;</c> is the subject, with placeholders such as
/// <c>{{delivery_id}}</c>. A placeholder puts in finished text, encoded for HTML in the body and as plain text in the
/// subject. There are no loops or conditions.
/// </summary>
internal sealed partial class MailTemplate
{
    private const string DateFormat = "dd.MM.yyyy HH:mm";

    private readonly string html;
    private readonly string subject;

    private MailTemplate(string html, string subject)
    {
        this.html = html;
        this.subject = subject;
    }

    /// <summary>
    /// Reads a template and checks it: the title must hold text, because it is the subject, and every placeholder must
    /// be known.
    /// </summary>
    /// <param name="template">The HTML of the template.</param>
    /// <exception cref="ArgumentException">The title is missing or empty, or a placeholder is unknown.</exception>
    internal static MailTemplate Parse(string template)
    {
        ArgumentNullException.ThrowIfNull(template);

        var unknown = PlaceholderPattern().Matches(template)
            .Where(placeholder => !KnownPlaceholderPattern().IsMatch(placeholder.Groups[1].Value))
            .Select(placeholder => placeholder.Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (unknown.Count > 0)
            throw new ArgumentException($"The template uses unknown placeholders: {string.Join(", ", unknown)}.", nameof(template));

        var title = TitlePattern().Match(template);
        var subject = title.Success ? WebUtility.HtmlDecode(title.Groups[1].Value).Trim() : string.Empty;
        if (subject.Length == 0)
            throw new ArgumentException("The template has no <title> with text, which the mail takes as its subject.", nameof(template));

        return new MailTemplate(template, subject);
    }

    /// <summary>
    /// The subject, the title of the template with its placeholders filled in as plain text on a single line.
    /// </summary>
    internal string RenderSubject(DeliveryInfo delivery, IReadOnlyList<string> fileNames, TimeZoneInfo timeZone)
        => PlaceholderPattern().Replace(subject, placeholder => Fill(placeholder, delivery, fileNames, timeZone, asHtml: false)).ReplaceLineEndings(" ");

    /// <summary>
    /// The HTML of the template with its placeholders filled in, encoded for HTML.
    /// </summary>
    internal string RenderBody(DeliveryInfo delivery, IReadOnlyList<string> fileNames, TimeZoneInfo timeZone)
        => PlaceholderPattern().Replace(html, placeholder => Fill(placeholder, delivery, fileNames, timeZone, asHtml: true));

    private static string Fill(Match placeholder, DeliveryInfo delivery, IReadOnlyList<string> fileNames, TimeZoneInfo timeZone, bool asHtml)
    {
        var known = KnownPlaceholderPattern().Match(placeholder.Groups[1].Value);
        var comment = delivery.Comment ?? string.Empty;
        return known.Groups["name"].Value switch
        {
            "delivery_id" => delivery.Id.ToString(CultureInfo.InvariantCulture),
            "declared_at" => TimeZoneInfo.ConvertTime(delivery.DeclaredAt, timeZone).ToString(DateFormat, CultureInfo.InvariantCulture),
            "mandate_name" => Encode(delivery.Mandate.Name.GetDisplayText(known.Groups["language"].Success ? known.Groups["language"].Value : null), asHtml),
            "declarer_name" => Encode(delivery.Declarer.Name, asHtml),
            "comment" => asHtml ? WebUtility.HtmlEncode(comment).ReplaceLineEndings("<br>") : comment,
            "files" => asHtml ? $"<ul>{string.Concat(fileNames.Select(name => $"<li>{WebUtility.HtmlEncode(name)}</li>"))}</ul>" : string.Join(", ", fileNames),
            _ => throw new UnreachableException($"Parse refuses the placeholder {placeholder.Value}."),
        };
    }

    private static string Encode(string value, bool asHtml) => asHtml ? WebUtility.HtmlEncode(value) : value;

    // Matches anything between double braces, so a misspelt placeholder is found as well and does not end up in a mail.
    [GeneratedRegex(@"\{\{(.*?)\}\}", RegexOptions.Singleline)]
    private static partial Regex PlaceholderPattern();

    // The placeholders a template may use; only the name of the mandate takes a language, the one tried first.
    [GeneratedRegex(@"^(?:(?<name>delivery_id|declared_at|declarer_name|comment|files)|(?<name>mandate_name)(?::(?<language>[a-z]{2}))?)$")]
    private static partial Regex KnownPlaceholderPattern();

    // Captures the text of the first title element.
    [GeneratedRegex(@"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitlePattern();
}

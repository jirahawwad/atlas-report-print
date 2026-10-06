namespace Atlas.Report.Print.Domain;

/// <summary>
/// Request body for <c>POST /print/generate</c>.
/// Supports two mutually-usable content modes:
/// <list type="bullet">
/// <item><description>Inline — <see cref="HtmlPayload"/>/<see cref="HeaderHtml"/>/<see cref="FooterHtml"/> carry HTML content directly. Use when the caller and this service don't share a filesystem (e.g. PROD reporting calling a QA-hosted instance).</description></item>
/// <item><description>File-based — <see cref="JobDirectory"/> plus the <c>*HtmlFile</c> fields reference files on a filesystem shared with this service. Use once caller and service are co-located, to avoid transferring large HTML payloads over HTTP.</description></item>
/// </list>
/// Exactly one mode must resolve a value for the body; mixing is allowed per-field
/// (e.g. inline body with a file-based header) since each is resolved independently.
/// </summary>
public sealed class PrintRequest
{
	/// <summary>Inline HTML document to render (report body — regenerated per category). Ignored if <see cref="BodyHtmlFile"/> is set.</summary>
	public string? HtmlPayload { get; init; }

	/// <summary>
	/// Inline header HTML — injected into every page header. Regenerated per report content (per category).
	/// Ignored if <see cref="HeaderHtmlFile"/> is set.
	/// Supports Playwright template variables:
	/// <c>&lt;span class='pageNumber'/&gt;</c>,
	/// <c>&lt;span class='totalPages'/&gt;</c>,
	/// <c>&lt;span class='date'/&gt;</c>.
	/// </summary>
	public string? HeaderHtml { get; init; }

	/// <summary>Inline footer HTML — injected into every page footer. Generated once per report. Ignored if <see cref="FooterHtmlFile"/> is set.</summary>
	public string? FooterHtml { get; init; }

	/// <summary>
	/// Directory shared with the caller, containing the input HTML files referenced below.
	/// Required if any of <see cref="BodyHtmlFile"/>/<see cref="HeaderHtmlFile"/>/<see cref="FooterHtmlFile"/> are set.
	/// </summary>
	public string? JobDirectory { get; init; }

	/// <summary>Filename of the report body HTML, relative to <see cref="JobDirectory"/>. Takes precedence over <see cref="HtmlPayload"/> if both are set.</summary>
	public string? BodyHtmlFile { get; init; }

	/// <summary>Filename of the header HTML, relative to <see cref="JobDirectory"/>. Takes precedence over <see cref="HeaderHtml"/> if both are set.</summary>
	public string? HeaderHtmlFile { get; init; }

	/// <summary>Filename of the footer HTML, relative to <see cref="JobDirectory"/>. Takes precedence over <see cref="FooterHtml"/> if both are set.</summary>
	public string? FooterHtmlFile { get; init; }

	/// <summary>
	/// Filename for the output PDF, relative to <see cref="JobDirectory"/>. If set (and <see cref="JobDirectory"/> is set),
	/// the PDF is written directly to that path instead of being returned as base64 — avoids transferring
	/// large PDF bytes over HTTP when caller and service share a filesystem.
	/// </summary>
	public string? PdfFile { get; init; }

	/// <summary>Page orientation: <c>PORTRAIT</c> or <c>LANDSCAPE</c>. Defaults to PORTRAIT.</summary>
	public string PrintFormat { get; init; } = "PORTRAIT";

	/// <summary>Top margin. Defaults to "1.10in".</summary>
	public string MarginTop { get; init; } = "1.10in";

	/// <summary>Bottom margin. Defaults to "0.50in".</summary>
	public string MarginBottom { get; init; } = "0.50in";

	/// <summary>Left margin. Defaults to "0.20in".</summary>
	public string MarginLeft { get; init; } = "0.20in";

	/// <summary>Right margin. Defaults to "0.20in".</summary>
	public string MarginRight { get; init; } = "0.20in";
}

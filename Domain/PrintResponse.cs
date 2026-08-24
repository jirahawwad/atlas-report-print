namespace Atlas.Report.Print.Domain;

/// <summary>
/// Response from <c>POST /print/generate</c>. Exactly one of <see cref="Base64Document"/>
/// or <see cref="PdfPath"/> is populated, depending on whether the request used
/// inline output (base64 returned) or file-based output (PDF written directly
/// to a shared filesystem path).
/// </summary>
public sealed class PrintResponse
{
	/// <summary>Base64-encoded PDF content. Set when the request did not provide a JobDirectory+PdfFile output path.</summary>
	public string? Base64Document { get; init; }

	/// <summary>Absolute path where the PDF was written. Set when the request provided a JobDirectory+PdfFile output path.</summary>
	public string? PdfPath { get; init; }
}

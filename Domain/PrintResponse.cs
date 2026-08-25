namespace Atlas.Report.Print.Domain;

/// <summary>
/// Response from <c>POST /print/generate</c>. Exactly one of <see cref="Base64Document"/>
/// or <see cref="PdfPath"/> is populated, depending on whether the request used
/// inline output (base64 returned) or file-based output (PDF written directly
/// to a shared filesystem path).
/// </summary>
public sealed class PrintResponse
{
	// Base64Document removed — inline requests now return raw PDF bytes directly as
	// the HTTP response body (Content-Type: application/pdf), not wrapped in JSON.
	// This DTO is now only used for the file-based output mode's small JSON response.
	/// <summary>Absolute path where the PDF was written. Set when the request provided a JobDirectory+PdfFile output path.</summary>
	public string? PdfPath { get; init; }
}

namespace Atlas.Report.Print.Services;

/// <summary>
/// Result of a render — exactly one of <see cref="Base64Document"/> or <see cref="PdfPath"/> is set.
/// </summary>
public sealed class PdfRenderResult
{
	// Raw bytes, not base64 — avoids ever holding a base64-inflated string in memory
	// at all, and lets the controller stream this directly as the HTTP response body
	// for inline requests rather than wrapping it in JSON (which has a hard ceiling
	// on individual string-value length that a large base64-encoded PDF could exceed).
	public byte[]? PdfBytes { get; init; }

	public string? PdfPath { get; init; }
}

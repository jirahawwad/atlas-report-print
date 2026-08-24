namespace Atlas.Report.Print.Services;

/// <summary>
/// Result of a render — exactly one of <see cref="Base64Document"/> or <see cref="PdfPath"/> is set.
/// </summary>
public sealed class PdfRenderResult
{
	public string? Base64Document { get; init; }

	public string? PdfPath { get; init; }
}

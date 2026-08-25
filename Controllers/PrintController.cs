using Atlas.Report.Print.Domain;
using Atlas.Report.Print.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;

namespace Atlas.Report.Print.Controllers;

[ApiController]
[Route("print")]
public sealed class PrintController : ControllerBase
{
	private readonly PlaywrightPrintRenderer _renderer;
	private readonly ILogger<PrintController> _logger;

	public PrintController(PlaywrightPrintRenderer renderer, ILogger<PrintController> logger)
	{
		if (renderer is null)
		{
			throw new ArgumentNullException(nameof(renderer));
		}

		if (logger is null)
		{
			throw new ArgumentNullException(nameof(logger));
		}

		_renderer = renderer;
		_logger = logger;
	}

	// Return type is now the two possible success shapes: a small JSON PrintResponse
	// for file-based output, or the raw PDF bytes directly for inline output. The
	// ProducesResponseType(typeof(PrintResponse)) attribute below is now only
	// accurate for the file-based case — Swagger doesn't cleanly express "one of
	// two different content types depending on the request" in a single attribute,
	// so this is a minor, accepted imprecision in the generated API doc.
	[HttpPost("generate")]
	[ProducesResponseType(typeof(PrintResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status500InternalServerError)]
	public async Task<IActionResult> Generate(
		[FromBody] PrintRequest request,
		CancellationToken cancellationToken)
	{
		if (request is null)
		{
			return BadRequest("Request body is required.");
		}

		bool hasInlineBody = !string.IsNullOrWhiteSpace(request.HtmlPayload);
		bool hasFileBody = !string.IsNullOrWhiteSpace(request.BodyHtmlFile) && !string.IsNullOrWhiteSpace(request.JobDirectory);

		_logger.LogInformation(
			"PrintController|method:{Method}|request received|isInline:{IsInline}|bodyChars:{BodyChars}|headerChars:{HeaderChars}|footerChars:{FooterChars}|printFormat:{PrintFormat}",
			nameof(Generate),
			hasInlineBody,
			request.HtmlPayload?.Length ?? 0,
			request.HeaderHtml?.Length ?? 0,
			request.FooterHtml?.Length ?? 0,
			request.PrintFormat);

		if (!hasInlineBody && !hasFileBody)
		{
			return BadRequest("Either HtmlPayload or JobDirectory+BodyHtmlFile is required.");
		}

		try
		{
			PdfRenderResult result = await _renderer.RenderAsync(request, cancellationToken);

			if (result.PdfPath is not null)
			{
				// File-based output — small JSON response, unaffected by the large-payload issue.
				return Ok(new PrintResponse { PdfPath = result.PdfPath });
			}

			// Inline output — raw PDF bytes directly as the response body, not JSON-wrapped.
			// Avoids System.Text.Json's hard limit on individual string-value length, which a
			// base64-encoded large PDF (previously wrapped in a JSON field) could exceed —
			// this is exactly what threw "The JSON value of length ... is too large" under
			// stress testing with a ~128MB PDF.
			return File(result.PdfBytes!, "application/pdf");
		}
		catch (ArgumentException ex)
		{
			return BadRequest(ex.Message);
		}
		catch (FileNotFoundException ex)
		{
			return NotFound(ex.Message);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (PlaywrightException ex)
		{
			_logger.LogError(ex, "PrintController|method:{Method}|reason:{Reason}", nameof(Generate), "RenderFailed");
			return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
		}
	}
}

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

	/// <summary>
	/// Renders the supplied HTML (body + optional header/footer) to a PDF and
	/// returns it as a Base64-encoded document.
	/// </summary>
	[HttpPost("generate")]
	[ProducesResponseType(typeof(PrintResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status500InternalServerError)]
	public async Task<ActionResult<PrintResponse>> Generate(
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
			return Ok(new PrintResponse { Base64Document = result.Base64Document, PdfPath = result.PdfPath });
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

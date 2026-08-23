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

		if (string.IsNullOrWhiteSpace(request.HtmlPayload))
		{
			return BadRequest("HtmlPayload is required.");
		}

		try
		{
			string base64Document = await _renderer.RenderAsync(request, cancellationToken);
			return Ok(new PrintResponse { Base64Document = base64Document });
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

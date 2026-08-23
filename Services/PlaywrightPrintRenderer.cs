using Atlas.Report.Print.Domain;

using Microsoft.Playwright;

namespace Atlas.Report.Print.Services;

/// <summary>
/// Renders HTML to PDF using Playwright/Chromium.
/// </summary>
public sealed class PlaywrightPrintRenderer(
	IBrowserPool browserPool,
	ILogger<PlaywrightPrintRenderer> logger)
{
	// Forces Chromium to render exact background colors instead of muting them
	// for print economy — without this, background-color fills on tables/cells
	// render washed out in the generated PDF regardless of PrintBackground:true.
	private const string ColorAdjustStyle =
		"<style>*{-webkit-print-color-adjust:exact !important;print-color-adjust:exact !important;}</style>";

	private const string ColorAdjustInlineStyle =
		"* { -webkit-print-color-adjust: exact !important; print-color-adjust: exact !important; }";

	private readonly IBrowserPool _browserPool = browserPool;
	private readonly ILogger<PlaywrightPrintRenderer> _logger = logger;

	/// <summary>
	/// Renders the given <see cref="PrintRequest"/> to a Base64-encoded PDF.
	/// </summary>
	public async Task<string> RenderAsync(
		PrintRequest request,
		CancellationToken cancellationToken = default)
	{
		if (request is null)
		{
			throw new ArgumentNullException(nameof(request));
		}

		cancellationToken.ThrowIfCancellationRequested();

		IPage? page = null;
		bool slotAcquired = false;

		try
		{
			page = await _browserPool.AcquirePageAsync(cancellationToken);
			slotAcquired = true;

			cancellationToken.ThrowIfCancellationRequested();

			await page.SetContentAsync(request.HtmlPayload, new PageSetContentOptions
			{
				WaitUntil = WaitUntilState.NetworkIdle
			});

			// Belt-and-suspenders: covers any background colors in HtmlPayload itself,
			// on top of the <style> block prepended to header/footer below.
			await page.AddStyleTagAsync(new PageAddStyleTagOptions { Content = ColorAdjustInlineStyle });

			cancellationToken.ThrowIfCancellationRequested();

			bool isLandscape = request.PrintFormat.Equals("LANDSCAPE", StringComparison.OrdinalIgnoreCase);

			string? headerTemplate = request.HeaderHtml is null ? null : ColorAdjustStyle + request.HeaderHtml;
			string? footerTemplate = request.FooterHtml is null ? null : ColorAdjustStyle + request.FooterHtml;

			PagePdfOptions options = new()
			{
				Format = "Letter",
				Landscape = isLandscape,
				PrintBackground = true,
				DisplayHeaderFooter = true,
				HeaderTemplate = headerTemplate ?? "<span/>",
				FooterTemplate = footerTemplate ?? "<span/>",
				Margin = new Margin
				{
					Top = request.MarginTop,
					Bottom = request.MarginBottom,
					Left = request.MarginLeft,
					Right = request.MarginRight
				}
			};

			byte[] pdfBytes = await page.PdfAsync(options);
			string base64 = Convert.ToBase64String(pdfBytes);

			_logger.LogInformation(
				"PlaywrightPrintRenderer|method:{Method}|pdfBytes:{PdfBytes}",
				nameof(RenderAsync),
				pdfBytes.Length);

			return base64;
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"PlaywrightPrintRenderer|method:{Method}|reason:{Reason}",
				nameof(RenderAsync),
				"RenderFailed");

			throw;
		}
		finally
		{
			if (page is not null)
			{
				await page.CloseAsync();
			}

			if (slotAcquired)
			{
				_browserPool.ReleasePage();
			}
		}
	}
}

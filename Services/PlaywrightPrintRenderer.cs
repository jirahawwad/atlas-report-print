using System.Diagnostics;

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
	/// Renders the given <see cref="PrintRequest"/>. Returns either a base64-encoded
	/// PDF, or writes the PDF directly to <c>JobDirectory/PdfFile</c> and returns
	/// that path, depending on which output mode the request specifies.
	/// </summary>
	public async Task<PdfRenderResult> RenderAsync(
		PrintRequest request,
		CancellationToken cancellationToken = default)
	{
		if (request is null)
		{
			throw new ArgumentNullException(nameof(request));
		}

		cancellationToken.ThrowIfCancellationRequested();

		Stopwatch totalStopwatch = Stopwatch.StartNew();
		Stopwatch resolveStopwatch = Stopwatch.StartNew();

		string bodyHtml = await ResolveContentAsync(
			request.JobDirectory, request.BodyHtmlFile, request.HtmlPayload,
			fieldName: "body", required: true);

		string? headerHtml = await ResolveContentAsync(
			request.JobDirectory, request.HeaderHtmlFile, request.HeaderHtml,
			fieldName: "header", required: false);

		string? footerHtml = await ResolveContentAsync(
			request.JobDirectory, request.FooterHtmlFile, request.FooterHtml,
			fieldName: "footer", required: false);

		resolveStopwatch.Stop();

		bool writeToFile = !string.IsNullOrWhiteSpace(request.JobDirectory) && !string.IsNullOrWhiteSpace(request.PdfFile);
		bool isInlineContent = string.IsNullOrWhiteSpace(request.BodyHtmlFile);

		IPage? page = null;
		bool slotAcquired = false;

		try
		{
			page = await _browserPool.AcquirePageAsync(cancellationToken);
			slotAcquired = true;

			cancellationToken.ThrowIfCancellationRequested();

			Stopwatch setContentStopwatch = Stopwatch.StartNew();

			await page.SetContentAsync(bodyHtml, new PageSetContentOptions
			{
				WaitUntil = WaitUntilState.NetworkIdle
			});

			// Belt-and-suspenders: covers any background colors in the body HTML itself,
			// on top of the <style> block prepended to header/footer below.
			await page.AddStyleTagAsync(new PageAddStyleTagOptions { Content = ColorAdjustInlineStyle });

			setContentStopwatch.Stop();

			cancellationToken.ThrowIfCancellationRequested();

			bool isLandscape = request.PrintFormat.Equals("LANDSCAPE", StringComparison.OrdinalIgnoreCase);

			string? headerTemplate = headerHtml is null ? null : ColorAdjustStyle + headerHtml;
			string? footerTemplate = footerHtml is null ? null : ColorAdjustStyle + footerHtml;

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

			Stopwatch pdfStopwatch = Stopwatch.StartNew();
			byte[] pdfBytes = await page.PdfAsync(options);
			pdfStopwatch.Stop();

			totalStopwatch.Stop();

			_logger.LogInformation(
				"PlaywrightPrintRenderer|method:{Method}|pdfBytes:{PdfBytes}|writeToFile:{WriteToFile}|isInlineContent:{IsInlineContent}|resolveMs:{ResolveMs}|setContentMs:{SetContentMs}|pdfGenMs:{PdfGenMs}|totalMs:{TotalMs}",
				nameof(RenderAsync),
				pdfBytes.Length,
				writeToFile,
				isInlineContent,
				resolveStopwatch.ElapsedMilliseconds,
				setContentStopwatch.ElapsedMilliseconds,
				pdfStopwatch.ElapsedMilliseconds,
				totalStopwatch.ElapsedMilliseconds);

			if (writeToFile)
			{
				string pdfPath = Path.Combine(request.JobDirectory!, request.PdfFile!);
				await File.WriteAllBytesAsync(pdfPath, pdfBytes, cancellationToken);
				return new PdfRenderResult { PdfPath = pdfPath };
			}

			return new PdfRenderResult { Base64Document = Convert.ToBase64String(pdfBytes) };
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

	/// <summary>
	/// Resolves a single piece of content (body/header/footer) from either a file
	/// (when <paramref name="fileName"/> and <paramref name="jobDirectory"/> are both
	/// set) or inline content, preferring the file if both are provided.
	/// </summary>
	private static async Task<string?> ResolveContentAsync(
		string? jobDirectory, string? fileName, string? inlineContent, string fieldName, bool required)
	{
		if (!string.IsNullOrWhiteSpace(fileName))
		{
			if (string.IsNullOrWhiteSpace(jobDirectory))
			{
				throw new ArgumentException($"{fieldName}: JobDirectory is required when {fieldName} is specified by filename.");
			}

			string path = Path.Combine(jobDirectory, fileName);
			if (!File.Exists(path))
			{
				throw new FileNotFoundException($"{fieldName} HTML file not found: {path}", path);
			}

			return await File.ReadAllTextAsync(path);
		}

		if (!string.IsNullOrWhiteSpace(inlineContent))
		{
			return inlineContent;
		}

		if (required)
		{
			throw new ArgumentException($"{fieldName}: either an inline value or a JobDirectory+filename pair is required.");
		}

		return null;
	}
}

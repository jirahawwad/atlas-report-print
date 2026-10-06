using Microsoft.Playwright;

namespace Atlas.Report.Print.Services;

public sealed class BrowserPool : IBrowserPool, IHostedService, IAsyncDisposable
{
	private static readonly string[] _chromiumArgs =
	[
		"--no-sandbox",
		"--disable-setuid-sandbox",
		"--disable-dev-shm-usage",
		"--disable-gpu"
	];

	private readonly ILogger<BrowserPool> _logger;
	private readonly SemaphoreSlim _pageSemaphore;
	private IPlaywright? _playwright;
	private IBrowser? _browser;
	private int _disposed;

	public BrowserPool(ILogger<BrowserPool> logger, IConfiguration configuration)
	{
		if (logger is null)
		{
			throw new ArgumentNullException(nameof(logger));
		}
		if (configuration is null)
		{
			throw new ArgumentNullException(nameof(configuration));
		}
		_logger = logger;
		// Bounds how many Chromium pages can be open concurrently — mirrors the
		// semaphore-gated pattern already used by the Undertow pdf-service (HeapMonitor),
		// which BrowserPool's AcquirePageAsync did not previously have.
		int maxConcurrentPages = configuration.GetValue<int?>("Playwright:MaxConcurrentPages") ?? 4;
		_pageSemaphore = new SemaphoreSlim(maxConcurrentPages, maxConcurrentPages);
	}

	public async Task StartAsync(CancellationToken cancellationToken)
	{
		_playwright = await Playwright.CreateAsync();
		_browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
		{
			Headless = true,
			Args = _chromiumArgs
		});
		_logger.LogInformation("Playwright Chromium browser launched for Atlas.Report.Print.");
	}

	public Task StopAsync(CancellationToken cancellationToken)
	{
		// Deliberately a no-op — BrowserPool is registered as both a hosted service
		// (StopAsync runs on host shutdown) and a DI singleton (DisposeAsync runs
		// when the service provider tears down). Both fire during shutdown; tearing
		// the browser down here as well as in DisposeAsync caused a double-close —
		// the second call threw ObjectDisposedException on Playwright's internal
		// transport semaphore, since it isn't built to be shut down twice.
		// DisposeAsync is the single, sufficient teardown path.
		return Task.CompletedTask;
	}

	public async Task<IPage> AcquirePageAsync(CancellationToken cancellationToken = default)
	{
		if (_browser is null)
		{
			throw new InvalidOperationException("BrowserPool has not been initialised.");
		}
		await _pageSemaphore.WaitAsync(cancellationToken);
		try
		{
			return await _browser.NewPageAsync();
		}
		catch
		{
			// Only release here on failure to acquire — on success, the caller
			// owns the page and releases the slot when it closes the page
			// (see PlaywrightPrintRenderer's ReleasePage call).
			_pageSemaphore.Release();
			throw;
		}
	}

	/// <summary>
	/// Releases a concurrency slot after the caller has closed its page.
	/// Must be called exactly once per successful <see cref="AcquirePageAsync"/> call.
	/// </summary>
	public void ReleasePage()
	{
		_pageSemaphore.Release();
	}

	public async ValueTask DisposeAsync()
	{
		// The DI container can invoke DisposeAsync more than once when a singleton
		// is registered under multiple service types (BrowserPool, IBrowserPool,
		// IHostedService) that all resolve to this same instance — its internal
		// disposal tracking isn't guaranteed to deduplicate by object identity
		// across separate registration slots. Idempotency here, not an assumption
		// that only one caller will ever tear this down, is the correct fix —
		// same underlying lesson as the StopAsync no-op fix, one level deeper.
		if (Interlocked.Exchange(ref _disposed, 1) != 0)
		{
			return;
		}

		if (_browser is not null)
		{
			try
			{
				await _browser.DisposeAsync();
			}
			catch (ObjectDisposedException)
			{
				// Confirmed harmless: the service stops cleanly (systemctl reports
				// "Deactivated successfully") whether or not this throws. Root cause
				// not pinned down after three targeted attempts (double-dispose guard,
				// extended ShutdownTimeout, KillMode=process) — this looks like Playwright's
				// own internal Connection/TaskQueue reacting to its Node driver process
				// exiting via some path we haven't identified, independent of what
				// actually triggers that exit. Logged at Debug rather than left to
				// surface as an uncaught FATAL, since we've confirmed it isn't one.
				_logger.LogDebug(
					"BrowserPool|method:{Method}|reason:KnownBenignPlaywrightShutdownException",
					nameof(DisposeAsync));
			}
		}
		_playwright?.Dispose();
	}
}

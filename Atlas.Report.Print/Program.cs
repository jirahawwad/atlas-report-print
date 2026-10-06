using Atlas.Report.Print.Services;

using Microsoft.OpenApi.Models;

using Serilog;

try
{
	AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
	{
		Log.Fatal("UNHANDLED EXCEPTION: {ExceptionObject}", e.ExceptionObject);
	};

	Serilog.Debugging.SelfLog.Enable(msg => Console.Error.WriteLine(msg));

	WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

	builder.Host.UseSystemd();

	// Explicit, in-code Kestrel limit — the equivalent appsettings.json
	// "Kestrel:Limits:MaxRequestBodySize" setting was confirmed present and correctly
	// deployed, service confirmed restarted, yet the decompression middleware's
	// SizeLimitedStream (which shares this same limit per Microsoft's own docs) still
	// enforced the old ~28.6MB default. Root cause of the JSON-config path not taking
	// effect wasn't conclusively identified; setting it directly in code sidesteps
	// that uncertainty rather than continuing to trust configuration-binding behavior
	// that's demonstrably not working as expected here.
	builder.WebHost.ConfigureKestrel(serverOptions =>
	{
		serverOptions.Limits.MaxRequestBodySize = 262_144_000; // 250MB
	});

	// Note: no builder.Host.UseWindowsService() — this service is hosted via
	// Kestrel + systemd on RHEL, not IIS/Windows Service.
	builder.Host.UseSerilog((ctx, services, lc) =>
		lc.ReadFrom.Configuration(ctx.Configuration)
		  .ReadFrom.Services(services)
		  .Enrich.FromLogContext()
		  .Enrich.WithThreadId()
		  .Enrich.WithProcessId());

	builder.Services.AddControllers();
	builder.Services.AddEndpointsApiExplorer();
	builder.Services.AddSwaggerGen(c =>
	{
		c.SwaggerDoc("v1", new OpenApiInfo { Title = "Atlas.Report.Print API", Version = "v1" });
	});

	// The default 5s shutdown timeout can cut off Playwright's async browser-close
	// mid-flight — BrowserPool.DisposeAsync() has to round-trip a message to the
	// underlying Node.js driver process, which can genuinely take longer than that
	// under normal teardown overhead. If the host abandons shutdown while that's
	// still in-flight, something else in the pipeline can dispose the underlying
	// connection out from under it, throwing ObjectDisposedException on Playwright's
	// own internal semaphore rather than completing cleanly.
	builder.Services.Configure<HostOptions>(options =>
	{
		options.ShutdownTimeout = TimeSpan.FromSeconds(30);
	});

	builder.Services.AddSingleton<BrowserPool>();
	builder.Services.AddSingleton<IBrowserPool>(sp => sp.GetRequiredService<BrowserPool>());
	builder.Services.AddHostedService(sp => sp.GetRequiredService<BrowserPool>());
	builder.Services.AddSingleton<PlaywrightPrintRenderer>();

	// Decompresses gzip-encoded request bodies before JSON model binding — the Java
	// client compresses inline-mode HTML payloads, since HTML compresses very well.
	// No controller/DTO changes needed; this happens transparently in the pipeline.
	builder.Services.AddRequestDecompression();

	// Compresses outgoing responses. Lower payoff than the request side, since the
	// response body is mostly an already-compressed base64 PDF, but effectively free
	// to enable. Explicitly include application/json since it's not always in the
	// default MIME type list depending on framework version.
	builder.Services.AddResponseCompression(options =>
	{
		options.MimeTypes = Microsoft.AspNetCore.ResponseCompression.ResponseCompressionDefaults.MimeTypes
			.Concat(new[] { "application/json" });
	});

	WebApplication app = builder.Build();

	Log.Information("Atlas.Report.Print starting — ASPNETCORE_ENVIRONMENT={Environment}", builder.Environment.EnvironmentName);

	// Both must run early in the pipeline, before anything reads the request body
	// or writes the response.
	app.UseRequestDecompression();
	app.UseResponseCompression();

	app.UseSwagger();
	app.UseSwaggerUI(c =>
	{
		c.SwaggerEndpoint("/swagger/v1/swagger.json", "Atlas.Report.Print API V1");
	});

	app.MapControllers();
	app.Run();
}
catch (Exception ex)
{
	Log.Fatal(ex, "FATAL STARTUP ERROR");
}
finally
{
	Log.CloseAndFlush();
}

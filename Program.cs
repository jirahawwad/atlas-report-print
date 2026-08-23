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

	builder.Services.AddSingleton<BrowserPool>();
	builder.Services.AddSingleton<IBrowserPool>(sp => sp.GetRequiredService<BrowserPool>());
	builder.Services.AddHostedService(sp => sp.GetRequiredService<BrowserPool>());
	builder.Services.AddSingleton<PlaywrightPrintRenderer>();

	WebApplication app = builder.Build();
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

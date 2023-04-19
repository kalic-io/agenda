using Agenda.API;
using Agenda.DataStores;

using FastEndpoints;
using FastEndpoints.Swagger;

using Fluxera.StronglyTypedId.SystemTextJson;

using NodaTime;
using NodaTime.Serialization.SystemTextJson;

using Serilog;

using System.Text.Json;
using System.Text.Json.Serialization;
/// <summary>
/// Entry point
/// </summary>

public class Program
{
    private static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ApplicationName = "Agenda.API" });

        builder.Services.AddFastEndpoints();
        builder.Services.AddSwaggerDoc();
        builder.Services.AddAsyncInitializer<DataStoreMigrateInitializerAsync<AgendaDataStore>>();
        builder.Services.AddCustomOptions(builder.Configuration);
        builder.Services.AddDataStores(builder.Configuration);
        builder.Services.AddCustomizedDependencyInjection();

        builder.Host.UseSerilog((hosting, loggerConfig) => loggerConfig.MinimumLevel.Verbose()
                                                                       .Enrich.WithProperty("ApplicationContext", hosting.HostingEnvironment.ApplicationName)
                                                                       .Enrich.FromLogContext()
                                                                       .Enrich.WithCorrelationIdHeader()
                                                                       .WriteTo.Conditional(_ => hosting.HostingEnvironment.IsDevelopment(),
                                                                                            config => config.Console())
                                                                       .ReadFrom.Configuration(hosting.Configuration));

        WebApplication app = builder.Build();

        app.UseSerilogRequestLogging();
        // TODO Add authorization
        app.UseFastEndpoints(opts =>
        {
            JsonSerializerOptions jsonSerializerOptions = opts.Serializer.Options;

            jsonSerializerOptions.UseStronglyTypedId();
            jsonSerializerOptions.ConfigureForNodaTime(DateTimeZoneProviders.Bcl);

            jsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            jsonSerializerOptions.IgnoreReadOnlyFields = true;
            jsonSerializerOptions.IgnoreReadOnlyProperties = true;
            jsonSerializerOptions.AllowTrailingCommas = true;
            jsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            jsonSerializerOptions.PropertyNameCaseInsensitive = true;
            jsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        });
        app.UseSwaggerGen();

        using IServiceScope scope = app.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        ILogger<Program> logger = services.GetRequiredService<ILogger<Program>>();
        IHostEnvironment env = services.GetRequiredService<IHostEnvironment>();

        try
        {
            logger?.LogInformation("Starting {ApplicationContext}", env.ApplicationName);
            await app.InitAsync().ConfigureAwait(false);

            await app.RunAsync().ConfigureAwait(false);

            logger?.LogInformation("{ApplicationContext} started", env.ApplicationName);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "An error occurred on startup.");
        }
    }
}
namespace Agenda.API.IntegrationTests.Fixtures;

using Agenda.DataStores;

using Fluxera.StronglyTypedId.SystemTextJson;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

using NodaTime;
using NodaTime.Serialization.SystemTextJson;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Mime;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Testcontainers.PostgreSql;

using Xunit;

public class AgendaWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _database;
    /// <summary>
    /// Timezone used by default for serialization/deserialization from/to NodaTime types.
    /// </summary>
    public static IDateTimeZoneProvider DefaultDateTimeZone => DateTimeZoneProviders.Tzdb;

    public static Action<JsonSerializerOptions> SerializerOptionsConfigurator => options =>
    {
        options.UseStronglyTypedId();
        options.ConfigureForNodaTime(DefaultDateTimeZone);

        options.Converters.Add(new JsonStringEnumConverter());
        options.IgnoreReadOnlyFields = true;
        options.IgnoreReadOnlyProperties = true;
        options.AllowTrailingCommas = true;
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.PropertyNameCaseInsensitive = true;
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    };

    public AgendaWebApplicationFactory()
    {
        _database = new PostgreSqlBuilder()
            .WithName(Guid.NewGuid().ToString("D"))
            .WithImage("postgres:15-alpine")
            .WithDatabase("test-database")
            .WithUsername("username")
            .WithPassword("p4ssW0rd!")
            .WithPortBinding(5432, true)
            .Build();
    }

    ///<inheritdoc/>
    public async Task InitializeAsync()
    {
        await _database.StartAsync().ConfigureAwait(false);
    }

    ///<inheritdoc/>
    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json));
    }

    ///<inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<AgendaDataStore>();
            IConfiguration configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(new[]
                    {
                        KeyValuePair.Create("ConnectionStrings:Agenda", _database.GetConnectionString())
                    })
                    .Build();
            services.AddDataStores(configuration);
        });
    }

    ///<inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        await _database.StopAsync().ConfigureAwait(false);
    }

    ///<inheritdoc/>
    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

}

namespace Agenda.API.IntegrationTests.Fixtures;

using Agenda.DataStores;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Testcontainers.PostgreSql;

using Xunit;

public class AgendaWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _database;
    private readonly AgendaDataStore _store;

    public AgendaWebApplicationFactory()
    {
        _database = new PostgreSqlBuilder()
            .WithName(Guid.NewGuid().ToString("D"))
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

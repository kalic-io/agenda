namespace Agenda.API.IntegrationTests.Appointments.v1.GetById;

using Agenda.API.IntegrationTests.Fixtures;
using Agenda.API.Resources;
using Agenda.API.Resources.Appointments.v1.Create;
using Agenda.API.Resources.v1.Appointments;

using Bogus;

using Candoumbe.Forms;

using FastEndpoints;

using FluentAssertions;

using NodaTime;
using NodaTime.Serialization.SystemTextJson;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

using Xunit;
using Xunit.Abstractions;
using Xunit.Categories;

[IntegrationTest]
[Feature(nameof(Appointments))]
public class CreateAppointmentEndpointShould : IClassFixture<AgendaWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _outputHelper;
    private readonly AgendaWebApplicationFactory _applicationFactory;
    private static readonly Faker Faker = new();
    private static readonly IDateTimeZoneProvider DefaultDateTimeZone = DateTimeZoneProviders.Tzdb;
    private static readonly System.Text.Json.JsonSerializerOptions JsonSerializerOptions = new System.Text.Json.JsonSerializerOptions().ConfigureForNodaTime(DefaultDateTimeZone);

    public CreateAppointmentEndpointShould(ITestOutputHelper outputHelper, AgendaWebApplicationFactory applicationFactory)
    {
        _outputHelper = outputHelper;
        _applicationFactory = applicationFactory;
        _client = _applicationFactory.CreateClient();
    }

    [Fact]
    public async Task Returns_the_appointment_when_Id_exists()
    {
        // Arrange
        Instant startDate = Faker.Noda().Instant.Soon();
        Instant endDate = Faker.Noda().Instant.Future(reference: startDate);

        NewAppointmentInfo newAppointmentInfo = new()
        {
            StartDate = startDate.InUtc(),
            EndDate = endDate.InUtc(),
            Location = Faker.Address.City(),
            Attendees = Faker.Make(2, () => new AttendeeInfo()
            {
                Name = Faker.Person.FullName,
                Email = Faker.Person.Email,
                PhoneNumber = Faker.Person.Phone
            }),
            Subject = Faker.Lorem.Sentence()
        };

        // Act
        (HttpResponseMessage response, Browsable<AppointmentInfo> browsable) = await _client.POSTAsync<CreateAppointmentEndpoint, NewAppointmentInfo, Browsable<AppointmentInfo>>(newAppointmentInfo);

        // Assert
        _outputHelper.WriteLine($"Created resource : {browsable.Jsonify(JsonSerializerOptions)}");
        response.StatusCode.Should()
                           .Be(System.Net.HttpStatusCode.Created);

        IEnumerable<Link> links = browsable.Links;
        links.Should()
             .Contain(link => link.Relations.Once(rel => rel == LinkRelation.Self)).And
             .Contain(link => link.Relations.Once(rel => string.Equals(rel, "delete", StringComparison.OrdinalIgnoreCase))).And
             .Contain(link => link.Relations.Once(rel => string.Equals(rel, "attendees", StringComparison.OrdinalIgnoreCase)));

        AppointmentInfo resource = browsable.Resource;
        resource.Id.Value.Should().NotBeEmpty();
        resource.Subject.Should().Be(newAppointmentInfo.Subject);
        resource.StartDate.Should().Be(newAppointmentInfo.StartDate);
        resource.EndDate.Should().Be(newAppointmentInfo.EndDate);
    }
}

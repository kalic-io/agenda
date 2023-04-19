namespace Agenda.API.IntegrationTests.Appointments.v1.GetById;

using Agenda.API.IntegrationTests.Fixtures;
using Agenda.API.Resources;
using Agenda.API.Resources.Appointments.v1.Create;
using Agenda.API.Resources.Appointments.v1.GetById;
using Agenda.API.Resources.v1.Appointments;
using Agenda.Ids;

using Bogus;

using FastEndpoints;

using FluentAssertions;

using NodaTime;
using NodaTime.Serialization.SystemTextJson;

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Xunit;
using Xunit.Abstractions;
using Xunit.Categories;

[IntegrationTest]
[Feature(nameof(Appointments))]
public class GetByIdEndpointShould : IClassFixture<AgendaWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _outputHelper;
    private readonly AgendaWebApplicationFactory _applicationFactory;
    private static readonly Faker Faker = new();
    private static readonly System.Text.Json.JsonSerializerOptions JsonSerializerOptions = new System.Text.Json.JsonSerializerOptions().ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
    private static readonly DateTimeZone DefaultDateTimeZone = DateTimeZone.ForOffset(Offset.FromHours(2));

    public GetByIdEndpointShould(ITestOutputHelper outputHelper, AgendaWebApplicationFactory applicationFactory)
    {
        _outputHelper = outputHelper;
        _applicationFactory = applicationFactory;
        _client = _applicationFactory.CreateClient();
        _client.DefaultRequestHeaders.Add(CurrentRequestMetadataInfoProvider.TimeZoneHeaderName, DefaultDateTimeZone.Id);
    }

    [Fact]
    public async Task Returns_NotFound_when_Id_does_not_exist()
    {
        // Arrange
        AppointmentId appointmentId = AppointmentId.New();

        // Act
        Func<Task> get = async () => await _client.GETAsync<GetAppointmentByIdEndpoint, AppointmentId, AppointmentInfo>(appointmentId).ConfigureAwait(false);

        await get.Should()
                 .ThrowAsync<InvalidOperationException>("The resource does not exist")
                 .Where(ex => ex.Response().StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Returns_the_appointment_when_Id_exists()
    {
        // Arrange
        Instant startDate = Faker.Noda().Instant.Soon();
        Instant endDate = Faker.Noda().Instant.Future(reference: startDate);

        NewAppointmentInfo newAppointmentInfo = new()
        {
            StartDate = startDate.InZone(DefaultDateTimeZone),
            EndDate = endDate.InZone(DefaultDateTimeZone),
            Location = Faker.Address.City(),
            Attendees = Faker.Make(2, action: () => new AttendeeInfo()
            {
                Id = AttendeeId.New(),
                Name = Faker.Name.FullName(),
                Email = Faker.Internet.Email(),
                PhoneNumber = Faker.Person.Phone
            }),
            Subject = Faker.Lorem.Sentence()
        };

        (HttpResponseMessage _, Browsable<AppointmentInfo> browsable) = await _client.POSTAsync<CreateAppointmentEndpoint, NewAppointmentInfo, Browsable<AppointmentInfo>>(newAppointmentInfo);

        _outputHelper.WriteLine($"Created resource : {browsable.Jsonify(JsonSerializerOptions)}");

        // Act
        (HttpResponseMessage response, Browsable<AppointmentInfo> browsableResult) = await _client.GETAsync<GetAppointmentByIdEndpoint, AppointmentId, Browsable<AppointmentInfo>>(browsable.Resource.Id);

        // Assert
        response.StatusCode.Should()
                           .Be(System.Net.HttpStatusCode.OK);
        AppointmentInfo resource = browsableResult.Resource;
        resource.Id.Value.Should().NotBeEmpty();
        //resource.StartDate.Should().Be(newAppointmentInfo.StartDate);
        //resource.EndDate.Should().Be(newAppointmentInfo.EndDate);
        resource.Attendees.Should()
                          .HaveSameCount(newAppointmentInfo.Attendees).And
                          .BeEquivalentTo(newAppointmentInfo.Attendees);

    }
}

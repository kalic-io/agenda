namespace Agenda.API.IntegrationTests.Appointments.v1.DeleteAnAppointment
{
    using Agenda.API.IntegrationTests.Fixtures;
    using Agenda.API.Resources;
    using Agenda.API.Resources.Appointments.v1.Create;
    using Agenda.API.Resources.Appointments.v1.Delete;
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
    public class DeleteEndpointShould : IClassFixture<AgendaWebApplicationFactory>
    {
        private readonly HttpClient _client;
        private static readonly Faker Faker = new();
        private static readonly System.Text.Json.JsonSerializerOptions JsonSerializerOptions = new System.Text.Json.JsonSerializerOptions().ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
        private readonly ITestOutputHelper _outputHelper;

        public DeleteEndpointShould(ITestOutputHelper outputHelper, AgendaWebApplicationFactory applicationFactory)
        {
            _client = applicationFactory.CreateClient();
            _outputHelper = outputHelper;
        }

        [Fact]
        public async Task Returns_NoFound_when_Id_does_not_exist()
        {
            // Act
            HttpResponseMessage response = await _client.DELETEAsync<DeleteEndpoint, AppointmentId>(AppointmentId.New());

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound, "the resource does not exist");
        }

        [Fact]
        public async Task Returns_NoContent_when_Id_exists()
        {
            // Arrange
            Instant startDate = Faker.Noda().Instant.Soon();
            Instant endDate = Faker.Noda().Instant.Future(reference: startDate);

            NewAppointmentInfo newAppointmentInfo = new()
            {
                StartDate = startDate.InUtc(),
                EndDate = endDate.InUtc(),
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

            // Act
            HttpResponseMessage response = await _client.DELETEAsync<DeleteEndpoint, AppointmentId>(browsable.Resource.Id);

            // Assert
            response.StatusCode.Should()
                               .Be(System.Net.HttpStatusCode.NoContent);

            Func<Task> get = async () => await _client.GETAsync<GetAppointmentByIdEndpoint, AppointmentId, AppointmentInfo>(browsable.Resource.Id).ConfigureAwait(false);

            await get.Should()
                     .ThrowAsync<InvalidOperationException>("The resource was deleted")
                     .Where(ex => ex.Response().StatusCode == HttpStatusCode.NotFound);
        }
    }
}

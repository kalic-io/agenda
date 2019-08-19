using Agenda.API.Resources.v1;
using Agenda.API.Resources.v1.Appointments;
using Agenda.Models.v1.Appointments;
using Agenda.Models.v1.Attendees;
using Bogus;
using FluentAssertions;
using FluentAssertions.Extensions;
using MedEasy.Core.Filters;
using MedEasy.IntegrationTests.Core;
using MedEasy.RestObjects;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Xunit.Categories;
using static Microsoft.AspNetCore.Http.StatusCodes;

namespace Agenda.API.IntegrationTests.v1
{
    [IntegrationTest]
    [Feature("Agenda")]
    [Feature("Appointments")]
    public class AppointmentsControllerTests : IClassFixture<IntegrationFixture<Startup>>
    {
        private IntegrationFixture<Startup> _server;
        private ITestOutputHelper _outputHelper;
        private const string _endpointUrl = "/v1/appointments";

        private static readonly JSchema _errorObjectSchema = new JSchema
        {
            Type = JSchemaType.Object,
            Properties =
            {
                [nameof(ValidationProblemDetails.Title).ToLower()] = new JSchema { Type = JSchemaType.String},
                [nameof(ValidationProblemDetails.Status).ToLower()] = new JSchema { Type = JSchemaType.Number},
                [nameof(ValidationProblemDetails.Detail).ToLower()] = new JSchema { Type = JSchemaType.String },
                [nameof(ValidationProblemDetails.Errors).ToLower()] = new JSchema { Type = JSchemaType.Object },
            },
            Required =
            {
                nameof(ValidationProblemDetails.Title).ToLower(),
                nameof(ValidationProblemDetails.Status).ToLower()
            }
        };

        /// <summary>
        /// Schema of an <see cref="AppointmentModel"/> resource once translated to json
        /// </summary>
        private static readonly JSchema _appointmentResourceSchema = new JSchema
        {
            Type = JSchemaType.Object,
            Properties =
            {
                [nameof(AppointmentModel.Id).ToCamelCase()] = new JSchema { Type = JSchemaType.String },
                [nameof(AppointmentModel.Subject).ToCamelCase()] = new JSchema { Type = JSchemaType.String },
                [nameof(AppointmentModel.Location).ToCamelCase()] = new JSchema { Type = JSchemaType.String },
                [nameof(AppointmentModel.StartDate).ToCamelCase()] = new JSchema { Type = JSchemaType.String,  },
                [nameof(AppointmentModel.EndDate).ToCamelCase()] = new JSchema { Type = JSchemaType.String,  },
                [nameof(AppointmentModel.UpdatedDate).ToCamelCase()] = new JSchema { Type = JSchemaType.String, },
                [nameof(AppointmentModel.Attendees).ToCamelCase()] = new JSchema { Type = JSchemaType.Array,  MinimumItems = 1}
            },
            Required =
            {
                nameof(AppointmentModel.Id).ToCamelCase(),
                nameof(AppointmentModel.Subject).ToCamelCase(),
                nameof(AppointmentModel.Location).ToCamelCase(),
                nameof(AppointmentModel.StartDate).ToCamelCase(),
                nameof(AppointmentModel.EndDate).ToCamelCase(),
                nameof(AppointmentModel.Attendees).ToCamelCase(),
            }
        };

        private static readonly JSchema _browsableResourceSchema = new JSchema
        {
            Type = JSchemaType.Object,
            Properties =
            {
                [nameof(Browsable<AppointmentModel>.Resource).ToLower()] = _appointmentResourceSchema,
                [nameof(Browsable<AppointmentModel>.Links).ToLower()] = new JSchema
                {
                    Type = JSchemaType.Array,
                }
            }
        };

        public AppointmentsControllerTests(ITestOutputHelper outputHelper, IntegrationFixture<Startup> fixture)
        {
            _outputHelper = outputHelper;
            _server = fixture;
        }

        public static IEnumerable<object[]> GetAll_With_Invalid_Pagination_Returns_BadRequestCases
        {
            get
            {
                int[] invalidPages = { int.MinValue, -1, -10, 0, 1, 5, 10 };

                IEnumerable<(int page, int pageSize)> invalidCases = invalidPages.CrossJoin(invalidPages)
                    .Where(tuple => tuple.Item1 <= 0 || tuple.Item2 <= 0);

                foreach ((int page, int pageSize) in invalidCases)
                {
                    yield return new object[] { page, pageSize };
                }
            }
        }

        [Theory]
        [MemberData(nameof(GetAll_With_Invalid_Pagination_Returns_BadRequestCases))]
        public async Task GetAll_With_Invalid_Pagination_Returns_BadRequest(int page, int pageSize)
        {
            _outputHelper.WriteLine($"Paging configuration : {new { page, pageSize }.Stringify()}");

            // Arrange
            string url = $"{_endpointUrl}?page={page}&pageSize={pageSize}";
            _outputHelper.WriteLine($"Url under test : <{url}>");

            // Act
            using (HttpClient client = _server.CreateClient())
            {
                HttpResponseMessage response = await client.GetAsync(url)
                        .ConfigureAwait(false);

                // Assert

                string content = await response.Content.ReadAsStringAsync()
                    .ConfigureAwait(false);
                _outputHelper.WriteLine($"Response content : {content}");

                response.IsSuccessStatusCode.Should().BeFalse("Invalid page and/or pageSize");
                ((int)response.StatusCode).Should().Be(Status400BadRequest);

                content.Should()
                    .NotBeNullOrEmpty("BAD REQUEST content must provide additional information on errors");

                JToken token = JToken.Parse(content);
                token.IsValid(_errorObjectSchema)
                    .Should().BeTrue("Error object must be provided when API returns BAD REQUEST");

                ValidationProblemDetails errorObject = token.ToObject<ValidationProblemDetails>();
                errorObject.Title.Should()
                    .Be("Validation failed");
                errorObject.Errors.Should()
                    .NotBeEmpty();

                if (page <= 0)
                {
                    errorObject.Errors.ContainsKey("page").Should().BeTrue("page <= 0 is not a valid value");
                }

                if (pageSize <= 0)
                {
                    errorObject.Errors.ContainsKey("pageSize").Should().BeTrue("pageSize <= 0 is not a valid value");
                }
            }
        }

        public static IEnumerable<object[]> InvalidSearchCases
        {
            get
            {
                yield return new object[]
                {
                    "?page=-1" ,
                    (Expression<Func<ValidationProblemDetails, bool>>)(err => err.Status == Status400BadRequest
                        && err.Title == "Validation failed"
                        && err.Errors != null
                        && err.Errors.ContainsKey("page")
                    ),
                    $"{nameof(SearchAppointmentModel.Page)} must be greater than 1"
                };

                yield return new object[]
                {
                    "?pageSize=-1" ,
                    (Expression<Func<ValidationProblemDetails, bool>>)(err => err.Status == Status400BadRequest
                        && err.Title == "Validation failed"
                        && err.Errors != null
                        && err.Errors.ContainsKey("pageSize")
                    ),
                    $"{nameof(SearchAppointmentModel.PageSize)} must be greater than 1"
                };
            }
        }

        [Theory]
        [MemberData(nameof(InvalidSearchCases))]
        public async Task GivenInvalidCriteria_Search_Returns_BadRequest(string queryString, Expression<Func<ValidationProblemDetails, bool>> errorObjectExpectation, string reason)
        {
            _outputHelper.WriteLine($"search query string : {queryString}");

            // Arrange
            string url = $"{_endpointUrl}/{nameof(AppointmentsController.Search)}{queryString}";
            _outputHelper.WriteLine($"Url under test : <{url}>");

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, url);

            // Act
            using (HttpClient client = _server.CreateClient())
            {
                HttpResponseMessage response = await client.SendAsync(request)
                        .ConfigureAwait(false);

                // Assert
                response.IsSuccessStatusCode.Should().BeFalse("Invalid search criteria");
                ((int)response.StatusCode).Should().Be(Status400BadRequest);

                string content = await response.Content.ReadAsStringAsync()
                    .ConfigureAwait(false);

                _outputHelper.WriteLine($"Response content : {content}");
                content.Should()
                    .NotBeNullOrEmpty("BAD REQUEST content must provide additional information on errors");

                JToken token = JToken.Parse(content);
                token.IsValid(_errorObjectSchema)
                    .Should().BeTrue($"Error object must be provided when HTTP GET <{url}> returns BAD REQUEST");

                ValidationProblemDetails errorObject = token.ToObject<ValidationProblemDetails>();
                errorObject.Should().Match(errorObjectExpectation, reason);
            }
        }

        [Theory]
        [InlineData("GET")]
        [InlineData("HEAD")]
        public async Task Search_Handles_Verb(string verb)
        {
            // Arrange
            string url = $"{_endpointUrl}/search?sort={Uri.EscapeDataString("+startDate")}";

            HttpRequestMessage request = new HttpRequestMessage(new HttpMethod(verb), url);

            using (HttpClient client = _server.CreateClient())
            {
                // Act
                HttpResponseMessage response = await client.SendAsync(request)
                    .ConfigureAwait(false);

                string content = await response.Content.ReadAsStringAsync()
                    .ConfigureAwait(false);

                _outputHelper.WriteLine($"Response content : {content}");

                // Assert
                response.IsSuccessStatusCode.Should()
                    .BeTrue($"HTTP {response.Version} {verb} /{url} must be supported");
            }
        }

        public static IEnumerable<object[]> GetCountCases
        {
            get
            {
                yield return new object[]
                {
                    "?page=1&pageSize=10",
                };

                yield return new object[]
                {
                    $"/search?{new { page=1, pageSize=10, from = 1.January(DateTimeOffset.UtcNow.Year)}.ToQueryString()}",
                };
            }
        }

        [Theory]
        [MemberData(nameof(GetCountCases))]
        public async Task Enpoint_Provides_CountsHeaders(string url)
        {
            // Arrange
            string path = $"{_endpointUrl}{url}";
            _outputHelper.WriteLine($"path under test : {path}");
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, path);

            using (HttpClient client = _server.CreateClient())
            {
                // Act
                HttpResponseMessage response = await client.SendAsync(request)
                    .ConfigureAwait(false);

                // Assert
                response.IsSuccessStatusCode.Should().BeTrue();

                _outputHelper.WriteLine($"Response status code : {response.StatusCode}");
                response.IsSuccessStatusCode.Should().BeTrue();

                _outputHelper.WriteLine($"Response headers :{response.Headers.Stringify()}");

                response.Headers.Should()
                    .ContainSingle(header => header.Key == AddCountHeadersFilterAttribute.TotalCountHeaderName).And
                    .ContainSingle(header => header.Key == AddCountHeadersFilterAttribute.CountHeaderName);

                response.Headers.GetValues(AddCountHeadersFilterAttribute.TotalCountHeaderName).Should()
                    .HaveCount(1, $"{AddCountHeadersFilterAttribute.TotalCountHeaderName} must contain only a single value");

                response.Headers.GetValues(AddCountHeadersFilterAttribute.CountHeaderName).Should()
                    .HaveCount(1, $"{AddCountHeadersFilterAttribute.CountHeaderName} must contain only a single value");
            }
        }

        [Fact]
        public async Task WhenPostingValidData_Post_CreateTheResource()
        {
            // Arrange

            Faker<AttendeeModel> participantFaker = new Faker<AttendeeModel>()
                .RuleFor(x => x.Id, () => Guid.NewGuid())
                .RuleFor(x => x.Name, faker => faker.Name.FullName())
                .RuleFor(x => x.UpdatedDate, faker => faker.Date.Recent());

            Faker<NewAppointmentModel> appointmentFaker = new Faker<NewAppointmentModel>("en")
                .RuleFor(x => x.Attendees, participantFaker.Generate(count: 3))
                .RuleFor(x => x.Location, faker => faker.Address.City())
                .RuleFor(x => x.Subject, faker => faker.Lorem.Sentence())
                .RuleFor(x => x.StartDate, faker => faker.Date.Future(refDate: 1.January(DateTimeOffset.UtcNow.Year + 1).Add(1.Hours())))
                .RuleFor(x => x.EndDate, (_, appointment) => appointment.StartDate.Add(1.Hours()));

            NewAppointmentModel newAppointment = appointmentFaker.Generate();

            _outputHelper.WriteLine($"{nameof(newAppointment)} : {newAppointment.Stringify()}");

            // Act
            using (HttpClient client = _server.CreateClient())
            {
                HttpResponseMessage response = await client.PostAsync(_endpointUrl, new StringContent(newAppointment.Stringify(), Encoding.UTF8, "application/json"))
                    .ConfigureAwait(false);

                // Assert
                string json = await response.Content.ReadAsStringAsync()
                    .ConfigureAwait(false);

                _outputHelper.WriteLine($"content : {json}");

                _outputHelper.WriteLine($"Response Status code :  {response.StatusCode}");
                response.IsSuccessStatusCode.Should()
                    .BeTrue("The resource creation must succeed");
                ((int)response.StatusCode).Should().Be(Status201Created);

                response.Content.Should()
                    .NotBeNull("API must return a content");

                json.Should()
                    .NotBeNullOrWhiteSpace();

                JToken token = JToken.Parse(json);
                bool tokenIsValid = token.IsValid(_browsableResourceSchema, out IList<string> errors);
                tokenIsValid.Should()
                    .BeTrue("content returned by the API must conform to appointment jschema");

                Browsable<AppointmentModel> browsableResource = token.ToObject<Browsable<AppointmentModel>>();
                IEnumerable<Link> links = browsableResource.Links;
                Link linkToSelf = links.Single(link => link.Relation == LinkRelation.Self);

                Uri location = response.Headers.Location;
                linkToSelf.Href.Should().Be(location.ToString());

                response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, location))
                    .ConfigureAwait(false);

                response.IsSuccessStatusCode.Should()
                    .BeTrue($"location <{location}> must point to the created resource");

                // Cleanup
                await client.DeleteAsync(location)
                    .ConfigureAwait(false);
            }
        }

        [Fact]
        public async Task RemoveParticipantFromAppointment_MustComplete()
        {
            // Arrange
            Faker<AttendeeModel> participantFaker = new Faker<AttendeeModel>("en")
                        .RuleFor(x => x.Name, faker => faker.Name.FullName())
                        .RuleFor(x => x.Id, () => Guid.NewGuid());

            Faker<NewAppointmentModel> appointmentFaker = new Faker<NewAppointmentModel>("en")
                .RuleFor(x => x.Attendees, (faker) => participantFaker.Generate(faker.Random.Int(min: 2, max: 5)))
                .RuleFor(x => x.Location, faker => faker.Address.City())
                .RuleFor(x => x.Subject, faker => faker.Lorem.Sentence())
                .RuleFor(x => x.StartDate, faker => faker.Date.Future(refDate: 1.January(DateTimeOffset.UtcNow.Year + 1).Add(1.Hours())))
                .RuleFor(x => x.EndDate, (_, appointment) => appointment.StartDate.Add(1.Hours()));

            NewAppointmentModel NewAppointmentModel = appointmentFaker;

            using (HttpClient client = _server.CreateClient())
            {
                HttpResponseMessage response = await client.PostAsync(_endpointUrl, new StringContent(NewAppointmentModel.Stringify(), Encoding.UTF8, "application/json"))
                        .ConfigureAwait(false);

                _outputHelper.WriteLine($"Creation of the appointment for the integration test response stats : {response.StatusCode}");
                string json = await response.Content.ReadAsStringAsync()
                    .ConfigureAwait(false);

                _outputHelper.WriteLine($"Response content : {json}");

                Browsable<AppointmentModel> browsableAppointmentModel = JToken.Parse(json)
                    .ToObject<Browsable<AppointmentModel>>();

                _outputHelper.WriteLine($"Resource created : {browsableAppointmentModel.Stringify()}");

                AppointmentModel resource = browsableAppointmentModel.Resource;
                IEnumerable<AttendeeModel> attendees = resource.Attendees;
                int participantCountBeforeDelete = attendees.Count();

                AttendeeModel attendeeToDelete = new Faker().PickRandom(attendees);

                Guid appointmentId = resource.Id;
                Guid attendeeId = attendeeToDelete.Id;
                string deletePath = $"{_endpointUrl}/{appointmentId}/attendees/{attendeeId}";

                _outputHelper.WriteLine($"delete url : <{deletePath}>");

                // Act
                response = await client.DeleteAsync(deletePath)
                    .ConfigureAwait(false);

                // Assert
                _outputHelper.WriteLine("Starting assertions");
                _outputHelper.WriteLine($"Delete status code : {response.StatusCode}");

                response.IsSuccessStatusCode.Should()
                    .BeTrue($"Appointment <{appointmentId}> and Attendee <{attendeeId}> exist.");

                ((int)response.StatusCode).Should()
                    .Be(Status204NoContent);
            }
        }
    }
}
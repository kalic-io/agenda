﻿namespace Agenda.CQRS.UnitTests.Features.Appointments.Handlers
{
    using Agenda.CQRS.Features.Appointments.Handlers;
    using Agenda.CQRS.Features.Appointments.Queries;
    using Agenda.DataStores;
    using Agenda.DTO;
    using Agenda.Ids;
    using Agenda.Mapping;
    using Agenda.Objects;

    using Bogus;

    using FluentAssertions;
    using FluentAssertions.Extensions;

    using MedEasy.DAL.EFStore;
    using MedEasy.DAL.Interfaces;
    using MedEasy.DAL.Repositories;
    using MedEasy.IntegrationTests.Core;
    using MedEasy.RestObjects;

    using Moq;

    using NodaTime;
    using NodaTime.Extensions;
    using NodaTime.Testing;

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading.Tasks;

    using Xunit;
    using Xunit.Abstractions;
    using Xunit.Categories;

    using static Moq.MockBehavior;

    [Feature("Agenda")]
    [UnitTest]
    public class HandleGetPageOfAppointmentInfoQueryTests : IAsyncLifetime, IClassFixture<SqliteEfCoreDatabaseFixture<AgendaContext>>
    {
        private IUnitOfWorkFactory _uowFactory;
        private Mock<IClock> _dateTimeServiceMock;
        private HandleGetPageOfAppointmentInfoQuery _sut;
        private readonly ITestOutputHelper _outputHelper;

        public HandleGetPageOfAppointmentInfoQueryTests(ITestOutputHelper outputHelper, SqliteEfCoreDatabaseFixture<AgendaContext> database)
        {
            _uowFactory = new EFUnitOfWorkFactory<AgendaContext>(database.OptionsBuilder.Options, (options) =>
            {
                AgendaContext context = new(options, new FakeClock(new Instant()));
                context.Database.EnsureCreated();
                return context;
            });
            _dateTimeServiceMock = new Mock<IClock>(Strict);
            _sut = new HandleGetPageOfAppointmentInfoQuery(_uowFactory, AutoMapperConfig.Build().ExpressionBuilder, _dateTimeServiceMock.Object);
            _outputHelper = outputHelper;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task DisposeAsync()
        {
            using (IUnitOfWork uow = _uowFactory.NewUnitOfWork())
            {
                uow.Repository<Attendee>().Clear();
                uow.Repository<Appointment>().Clear();

                await uow.SaveChangesAsync()
                    .ConfigureAwait(false);
            }

            _uowFactory = null;
            _dateTimeServiceMock = null;
            _sut = null;
        }

        public static IEnumerable<object[]> HandleCases
        {
            get
            {
                yield return new object[]
                {
                    Enumerable.Empty<Appointment>(),
                    1.January(2010).AsUtc().ToInstant(),
                    (1, 10),
                    (Expression<Func<Page<AppointmentInfo>, bool>>)(page => page.Count == 1
                                                                            && page.Total == 0
                                                                            && page.Entries != null
                                                                            && page.Entries.Exactly(0)
                    ),
                    "DataStore is empty"
                };

                yield return new object[]
                {
                    Enumerable.Empty<Appointment>(),
                    1.January(2010).AsUtc().ToInstant(),
                    (2, 10),
                    (Expression<Func<Page<AppointmentInfo>, bool>>)(page => page.Count == 1
                        && page.Total == 0
                        && page.Entries != null && !page.Entries.Any()
                    ),
                    "DataStore is empty"
                };
                {
                    Faker<Appointment> appointmentFaker = new Faker<Appointment>()
                        .CustomInstantiator(faker => new Appointment(
                            id: AppointmentId.New(),
                            subject: faker.Lorem.Sentence(),
                            location: faker.Address.City(),
                            startDate: 10.April(2000).At(13.Hours()).AsUtc().ToInstant(),
                            endDate: 10.April(2000).At(14.Hours()).AsUtc().ToInstant()));


                    IEnumerable<Appointment> items = appointmentFaker.Generate(50);
                    yield return new object[]
                    {
                        items,
                        10.April(2000).AsUtc().ToInstant(),
                        (2, 10),
                        (Expression<Func<Page<AppointmentInfo>, bool>>)(page => page.Count == 5
                            && page.Total == 50
                            && page.Entries != null && page.Entries.Exactly(10)
                        ),
                        "DataStore contains elements"
                    };
                }
            }
        }

        [Theory]
        [MemberData(nameof(HandleCases))]
        public async Task TestHandle(IEnumerable<Appointment> appointments, Instant currentDateTime, (int page, int pageSize) pagination, Expression<Func<Page<AppointmentInfo>, bool>> pageExpectation, string reason)
        {
            // Arrange
            _outputHelper.WriteLine($"page : {pagination.page}");
            _outputHelper.WriteLine($"pageSize : {pagination.pageSize}");
            _outputHelper.WriteLine($"Current date time : {currentDateTime.ToDateTimeUtc()}");

            using IUnitOfWork uow = _uowFactory.NewUnitOfWork();

            uow.Repository<Appointment>().Create(appointments);
            await uow.SaveChangesAsync()
                .ConfigureAwait(false);

            int appointmentsCount = await uow.Repository<Appointment>().CountAsync()
                .ConfigureAwait(false);
            _outputHelper.WriteLine($"DataStore count : {appointmentsCount}");

            _dateTimeServiceMock.Setup(mock => mock.GetCurrentInstant()).Returns(currentDateTime);
            GetPageOfAppointmentInfoQuery request = new(new PaginationConfiguration { Page = pagination.page, PageSize = pagination.pageSize });

            // Act
            Page<AppointmentInfo> page = await _sut.Handle(request, cancellationToken: default)
                                                   .ConfigureAwait(false);

            // Assert
            page.Should()
                .Match(pageExpectation, reason);
        }
    }
}

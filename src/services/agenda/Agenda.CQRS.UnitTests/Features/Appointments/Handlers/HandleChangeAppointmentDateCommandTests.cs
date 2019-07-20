﻿using Agenda.CQRS.Features.Appointments.Commands;
using Agenda.CQRS.Features.Appointments.Handlers;
using Agenda.DataStores;
using Agenda.Mapping;
using Agenda.Objects;
using AutoMapper;
using Bogus;
using FakeItEasy;
using FluentAssertions;
using FluentAssertions.Extensions;
using MedEasy.CQRS.Core.Commands.Results;
using MedEasy.CQRS.Core.Exceptions;
using MedEasy.DAL.EFStore;
using MedEasy.DAL.Interfaces;
using MedEasy.IntegrationTests.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Xunit.Categories;

namespace Agenda.CQRS.UnitTests.Features.Appointments.Handlers
{
    [UnitTest]
    [Feature("Agenda")]
    [Feature("Appointments")]
    public class HandleChangeAppointmentDateCommandTests : IDisposable, IClassFixture<SqliteDatabaseFixture>
    {
        private HandleChangeAppointmentDateCommand _sut;
        private readonly ITestOutputHelper _outputHelper;
        private IUnitOfWorkFactory _unitOfWorkFactoryMock;
        private IUnitOfWorkFactory _unitOfWorkFactory;
        private IMapper _mapper;

        public HandleChangeAppointmentDateCommandTests(ITestOutputHelper outputHelper, SqliteDatabaseFixture database)
        {
            _outputHelper = outputHelper;
            DbContextOptionsBuilder<AgendaContext> dbContextOptionsBuilder = new DbContextOptionsBuilder<AgendaContext>();
            dbContextOptionsBuilder.UseSqlite(database.Connection)
                .EnableSensitiveDataLogging()
                .ConfigureWarnings(warnings => warnings.Throw());

            _unitOfWorkFactory = new EFUnitOfWorkFactory<AgendaContext>(dbContextOptionsBuilder.Options, (options) =>
            {
                AgendaContext context = new AgendaContext(options);
                context.Database.EnsureCreated();

                return context;
            });
            _mapper = A.Fake<IMapper>(x => x.Wrapping(AutoMapperConfig.Build().CreateMapper()));
            _unitOfWorkFactoryMock = A.Fake<IUnitOfWorkFactory>(x => x.Wrapping(_unitOfWorkFactory));

            _sut = new HandleChangeAppointmentDateCommand(_unitOfWorkFactoryMock, _mapper);
        }

        public async void Dispose()
        {
            using (IUnitOfWork uow = _unitOfWorkFactoryMock.NewUnitOfWork())
            {
                uow.Repository<Attendee>().Clear();
                uow.Repository<Appointment>().Clear();

                await uow.SaveChangesAsync()
                    .ConfigureAwait(false);
            }

            _unitOfWorkFactoryMock = null;
            _unitOfWorkFactory = null;
            _sut = null;
        }

        [Fact]
        public void Handle_ChangeAppointmentDateCommand() => typeof(HandleChangeAppointmentDateCommand).Should()
            .Implement<IRequestHandler<ChangeAppointmentDateCommand, ModifyCommandResult>>();

        public static IEnumerable<object[]> InvalidDataCommandCases
        {
            get
            {
                Guid[] appointmentIds = { Guid.NewGuid(), default };
                DateTimeOffset[] starts = { 23.February(2017).Add(15.Hours()), default };
                DateTimeOffset[] ends = { 23.February(2017).Add(15.Hours().Add(15.Minutes())), default };

                IEnumerable<object[]> cases = appointmentIds
                    .CrossJoin(starts, (appointmentId, start) => (appointmentId, start))
                    .CrossJoin(ends, (tuple, end) => (tuple.appointmentId, tuple.start, end))
                    .Where(tuple => !tuple.Equals((default, default, default)) && (tuple.appointmentId == default || tuple.start == default || tuple.end == default))
                    .Select(tuple => new object[] { tuple, "One or more properties is not set" });

                foreach (object[] @case in cases)
                {
                    yield return @case;
                }

                yield return new object[] { (appointmentId: Guid.NewGuid(), start: (DateTimeOffset)17.August(2007), end: (DateTimeOffset)17.August(2007).Add(-1.Hours())), "Start property is after end" };
            }
        }

        [Theory]
        [MemberData(nameof(InvalidDataCommandCases))]
        public void Handle_Throws_CommandNotValidException((Guid appointmentId, DateTimeOffset start, DateTimeOffset end) data, string reason)
        {
            _outputHelper.WriteLine($"{nameof(data)} : {data}");

            // Arrange
            ChangeAppointmentDateCommand cmd = new ChangeAppointmentDateCommand(data);

            // Act
            Func<Task> action = async () => await _sut.Handle(cmd, default)
                .ConfigureAwait(false);

            // Assert
            action.Should()
                .Throw<CommandNotValidException<Guid>>(reason);

            A.CallTo(_unitOfWorkFactoryMock)
                .MustNotHaveHappened();
            A.CallTo(_mapper).MustNotHaveHappened();
        }

        public static IEnumerable<object[]> HandleCases
        {
            get
            {
                Faker faker = new Faker();
                yield return new object[]
                {
                    Enumerable.Empty<Appointment>(),
                    new ChangeAppointmentDateCommand((Guid.NewGuid(), start: 17.February(2012).Add(13.Hours()), end: 17.February(2012).Add(13.Hours().Add(15.Minutes())))),
                    ModifyCommandResult.Failed_NotFound,
                    "Appointment not found in the datastore"
                };
                {
                    Appointment appointment = new Appointment
                    (
                        id: Guid.NewGuid(),
                        startDate: 25.April(2012).At(14.Hours()),
                        endDate: 25.April(2012).At(14.Hours().And(15.Minutes())),
                        subject: "JLA relocation",
                        location: "None"
                    );

                    Attendee batman = new Attendee(id: Guid.NewGuid(), name:"Bruce Wayne");
                    Attendee superman = new Attendee(id: Guid.NewGuid(), name: "Clark Kent");

                    appointment.AddAttendee(batman);
                    appointment.AddAttendee(superman);

                    yield return new object[]
                    {
                        new []{ appointment },
                        new ChangeAppointmentDateCommand((appointment.Id, start: 17.February(2012).Add(13.Hours()), end: 17.February(2012).Add(13.Hours().Add(15.Minutes())))),
                        ModifyCommandResult.Done,
                        "The appointment exists and the change won't overlap with any existing appointment"
                    };
                }

                {
                    Appointment appointmentRelocation = new Appointment
                    (
                        id: Guid.NewGuid(),
                        startDate: 25.April(2012).At(14.Hours()),
                        endDate: 25.April(2012).At(14.Hours().And(15.Minutes())),
                        subject: "JLA relocation",
                        location: "None"
                    );

                    Attendee batman = new Attendee(id: Guid.NewGuid(), name: "Bruce Wayne");
                    Attendee superman = new Attendee(id: Guid.NewGuid(), name: "Clark Kent");

                    appointmentRelocation.AddAttendee(batman);
                    appointmentRelocation.AddAttendee(superman);

                    Appointment appointmentEmancipation = new Appointment
                    (
                        id: Guid.NewGuid(),
                        startDate: 25.April(2012).At(13.Hours()),
                        endDate: 25.April(2012).At(14.Hours().And(5.Minutes())),
                        subject: "I want to leave the JLA",
                        location: "None"
                    );

                    Attendee robin = new Attendee(id: Guid.NewGuid(), name: "Dick grayson");

                    appointmentEmancipation.AddAttendee(batman);
                    appointmentEmancipation.AddAttendee(robin);

                    yield return new object[]
                    {
                        new []{ appointmentRelocation, appointmentEmancipation },
                        new ChangeAppointmentDateCommand((appointmentEmancipation.Id, start: faker.Date.RecentOffset(refDate: appointmentRelocation.StartDate), end: faker.Date.BetweenOffset(appointmentRelocation.StartDate, appointmentRelocation.EndDate))),
                        ModifyCommandResult.Failed_Conflict,
                        "The appointment would end when another appointemtn is ongoing"
                    };
                }

                {
                    Appointment appointmentRelocation = new Appointment
                    (
                        id: Guid.NewGuid(),
                        startDate: 25.April(2012).At(14.Hours()),
                        endDate: 25.April(2012).At(14.Hours().And(15.Minutes())),
                        subject: "JLA relocation",
                        location: "None"
                    );

                    Attendee batman = new Attendee(id: Guid.NewGuid(), name: "Bruce Wayne");
                    Attendee superman = new Attendee(id: Guid.NewGuid(), name: "Clark Kent");

                    appointmentRelocation.AddAttendee(batman);
                    appointmentRelocation.AddAttendee(superman);

                    Appointment appointmentEmancipation = new Appointment
                    (
                        id: Guid.NewGuid(),
                        startDate: 25.April(2012).At(13.Hours()),
                        endDate: 25.April(2012).At(14.Hours().And(5.Minutes())),
                        subject: "I want to leave the JLA",
                        location: "None"
                    );


                    Attendee robin = new Attendee(id: Guid.NewGuid(), "Dick grayson");

                    appointmentEmancipation.AddAttendee(batman);
                    appointmentEmancipation.AddAttendee(robin);

                    yield return new object[]
                    {
                        new []{ appointmentRelocation, appointmentEmancipation },
                        new ChangeAppointmentDateCommand((appointmentEmancipation.Id, start: faker.Date.BetweenOffset(appointmentRelocation.StartDate, appointmentRelocation.EndDate), end: faker.Date.SoonOffset(refDate : appointmentRelocation.EndDate))),
                        ModifyCommandResult.Failed_Conflict,
                        "The appointment would start whilst another appointment is ongoing"
                    };
                }
            }
        }

        [Theory]
        [MemberData(nameof(HandleCases))]
        public async Task HandleTests(IEnumerable<Appointment> appointments, ChangeAppointmentDateCommand cmd, ModifyCommandResult expected, string reason)
        {
            // Arrange
            using (IUnitOfWork uow = _unitOfWorkFactory.NewUnitOfWork())
            {
                uow.Repository<Appointment>().Create(appointments);

                await uow.SaveChangesAsync()
                    .ConfigureAwait(false);
            }

            // Act
            ModifyCommandResult actual = await _sut.Handle(cmd, default)
                .ConfigureAwait(false);

            // Assert
            A.CallTo(() => _unitOfWorkFactoryMock
                .NewUnitOfWork())
                .MustHaveHappenedOnceExactly();
            A.CallTo(_mapper).MustNotHaveHappened();

            actual.Should()
                .Be(expected, reason);

            if (actual == ModifyCommandResult.Done)
            {
                using (IUnitOfWork uow = _unitOfWorkFactory.NewUnitOfWork())
                {
                    bool changesOk = await uow.Repository<Appointment>()
                        .AnyAsync(app => app.Id == cmd.Data.appointmentId && app.StartDate == cmd.Data.start && app.EndDate == cmd.Data.end)
                        .ConfigureAwait(false);

                    changesOk.Should()
                        .BeTrue("Changes must reflect in the datastore");
                }

            }

        }
    }
}
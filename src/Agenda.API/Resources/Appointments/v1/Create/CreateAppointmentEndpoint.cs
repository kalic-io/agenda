﻿namespace Agenda.API.Resources.Appointments.v1.Create
{
    using Agenda.API.Resources.Appointments.v1.Delete;
    using Agenda.API.Resources.Appointments.v1.GetById;
    using Agenda.API.Resources.v1.Appointments;
    using Agenda.Ids;
    using Agenda.Objects;

    using Candoumbe.DataAccess.Abstractions;
    using Candoumbe.Forms;

    using FastEndpoints;

    /// <summary>
    /// Creates new appointment
    /// </summary>
    public class CreateAppointmentEndpoint : Endpoint<NewAppointmentInfo, Browsable<AppointmentInfo>>
    {
        private readonly IUnitOfWorkFactory _unitOfWorkFactory;
        private readonly LinkGenerator _linkGenerator;
        private readonly CurrentRequestMetadataInfoProvider _currentRequestMetadataInfoProvider;

        /// <summary>
        /// Builds a new <see cref="CreateAppointmentEndpoint"/>
        /// </summary>
        /// <param name="unitOfWorkFactory"></param>
        /// <param name="linkGenerator"></param>
        /// <param name="currentRequestMetadataInfoProvider"></param>
        public CreateAppointmentEndpoint(IUnitOfWorkFactory unitOfWorkFactory, LinkGenerator linkGenerator, CurrentRequestMetadataInfoProvider currentRequestMetadataInfoProvider)
        {
            _unitOfWorkFactory = unitOfWorkFactory;
            _linkGenerator = linkGenerator;
            _currentRequestMetadataInfoProvider = currentRequestMetadataInfoProvider;
        }

        ///<inheritdoc/>
        public override void Configure()
        {
            Post("/appointments");
            AllowAnonymous();
        }

        ///<inheritdoc/>
        public override async Task HandleAsync(NewAppointmentInfo req, CancellationToken ct)
        {
            using IUnitOfWork unitOfWork = _unitOfWorkFactory.NewUnitOfWork();

            Appointment newAppointment = new(AppointmentId.New(), req.Subject, req.Location, req.StartDate.ToInstant(), req.EndDate.ToInstant());
            foreach (AttendeeInfo attendee in req.Attendees)
            {
                newAppointment.AddAttendee(new Attendee(attendee.Id, attendee.Name, attendee.Email, attendee.PhoneNumber));
            }

            await unitOfWork.Repository<Appointment>().Create(newAppointment, ct);
            await unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);

            NodaTime.DateTimeZone zone = _currentRequestMetadataInfoProvider.GetCurrentDateTimeZone();
            AppointmentInfo appointmentInfo = new()
            {
                Id = newAppointment.Id,
                Location = newAppointment.Location,
                StartDate = newAppointment.StartDate.InZone(zone),
                EndDate = newAppointment.EndDate.InZone(zone),
                Subject = newAppointment.Subject,
                Attendees = newAppointment.Attendees.Select(attendee => new AttendeeInfo
                {
                    Id = attendee.Id,
                    Email = attendee.Email,
                    Name = attendee.Name,
                    PhoneNumber = attendee.PhoneNumber
                })
            };

            Browsable<AppointmentInfo> browsable = new()
            {
                Resource = appointmentInfo,
                Links = new[]
                {
                    new Link
                    {
                        Href = _linkGenerator.GetUriByName(HttpContext, nameof(GetAppointmentByIdEndpoint), new { newAppointment.Id }),
                        Method = nameof(Http.GET),
                        Relations = new[] { LinkRelation.Self }
                    },
                    new Link
                    {
                        Href = _linkGenerator.GetUriByName(HttpContext, nameof(DeleteEndpoint), new { newAppointment.Id }),
                        Method = nameof(Http.DELETE),
                        Relations = new[] { LinkRelation.Self }
                    },
                }
            };

            await SendCreatedAtAsync<GetAppointmentByIdEndpoint>(new { newAppointment.Id }, responseBody: browsable, verb: Http.GET, cancellation: ct).ConfigureAwait(false);
        }
    }
}

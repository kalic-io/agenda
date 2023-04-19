namespace Agenda.API.Resources.Appointments.v1.GetById;
using Agenda.API.Resources;
using Agenda.API.Resources.v1.Appointments;
using Agenda.Ids;
using Agenda.Objects;

using Candoumbe.DataAccess.Abstractions;
using Candoumbe.DataAccess.Repositories;
using Candoumbe.Forms;

using FastEndpoints;

using Optional;

/// <summary>
/// Gets an appointment by its id
/// </summary>
public class GetAppointmentByIdEndpoint : Endpoint<AppointmentId, Browsable<AppointmentInfo>>
{
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly LinkGenerator _linkGenerator;
    private readonly CurrentRequestMetadataInfoProvider _currentRequestMetadataInfoProvider;

    /// <summary>
    /// Builds a new <see cref="GetAppointmentByIdEndpoint"/> instance.
    /// </summary>
    /// <param name="unitOfWorkFactory"></param>
    /// <param name="linkGenerator"></param>
    /// <param name="currentRequestMetadataInfoProvider"></param>
    public GetAppointmentByIdEndpoint(IUnitOfWorkFactory unitOfWorkFactory, LinkGenerator linkGenerator, CurrentRequestMetadataInfoProvider currentRequestMetadataInfoProvider)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        _linkGenerator = linkGenerator;
        _currentRequestMetadataInfoProvider = currentRequestMetadataInfoProvider;
    }

    /// <inheritdoc/>
    public override void Configure()
    {
        Get("/appointments/{id}");
        AllowAnonymous();

        Options(builder =>
        {
            builder.WithName(nameof(GetAppointmentByIdEndpoint));
        });
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(AppointmentId req, CancellationToken ct)
    {
        using IUnitOfWork unitOfWork = _unitOfWorkFactory.NewUnitOfWork();
        Option<Appointment> mayBeAppointment = await unitOfWork.Repository<Appointment>()
                                                               .SingleOrDefault(predicate: (Appointment x) => x.Id == req,
                                                                                includedProperties: new[] { IncludeClause<Appointment>.Create(x => x.Attendees) },
                                                                                cancellationToken: ct)
                                                               .ConfigureAwait(false);

        await mayBeAppointment.Match(
            some: async entity =>
            {
                NodaTime.DateTimeZone zone = _currentRequestMetadataInfoProvider.GetCurrentDateTimeZone();
                AppointmentInfo appointment = new AppointmentInfo
                {
                    Id = entity.Id,
                    StartDate = entity.StartDate.InZone(zone),
                    EndDate = entity.EndDate.InZone(zone),
                    Attendees = entity.Attendees.Select(attendee => new AttendeeInfo
                    {
                        Email = attendee.Email,
                        Id = attendee.Id,
                        Name = attendee.Name,
                        PhoneNumber = attendee.PhoneNumber
                    })
                };

                Browsable<AppointmentInfo> browsable = new()
                {
                    Resource = appointment,
                    Links = new[]
                    {
                        new Link
                        {
                            Href = _linkGenerator.GetUriByName(HttpContext, nameof(GetAppointmentByIdEndpoint), new { Id = req.Value }),
                            Method = "GET",
                            Relations = (new [] { LinkRelation.Self }).ToHashSet()
                        },
                    }
                };
                await SendOkAsync(browsable, ct).ConfigureAwait(false);
            },
            none: async () => await SendNotFoundAsync(ct).ConfigureAwait(false))
                              .ConfigureAwait(false);
    }
}

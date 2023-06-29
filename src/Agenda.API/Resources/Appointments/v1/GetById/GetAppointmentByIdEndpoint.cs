namespace Agenda.API.Resources.Appointments.v1.GetById;
using Agenda.API.Resources;
using Agenda.API.Resources.v1.Appointments;
using Agenda.Ids;
using Agenda.Objects;

using Ardalis.ApiEndpoints;

using Candoumbe.DataAccess.Abstractions;
using Candoumbe.DataAccess.Repositories;
using Candoumbe.Forms;

using Microsoft.AspNetCore.Mvc;

using Optional;


/// <summary>
/// Gets an appointment by its id
/// </summary>
public class GetAppointmentByIdEndpoint : EndpointBaseAsync.WithRequest<AppointmentId>
                                                           .WithActionResult<Browsable<GetAppointmentByIdResponse>>
{
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly LinkGenerator _linkGenerator;
    private readonly CurrentRequestMetadataInfoProvider _currentRequestMetadataInfoProvider;

    /// <summary>
    /// Name of the route to this endpoint
    /// </summary>
    public const string RouteName = nameof(GetAppointmentByIdEndpoint);

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
    [HttpGet("/appointments/{id}", Name = RouteName)]
    public override async Task<ActionResult<Browsable<GetAppointmentByIdResponse>>> HandleAsync(AppointmentId id, CancellationToken ct)
    {
        using IUnitOfWork unitOfWork = _unitOfWorkFactory.NewUnitOfWork();
        Option<Appointment> mayBeAppointment = await unitOfWork.Repository<Appointment>()
                                                               .SingleOrDefault(predicate: (Appointment x) => x.Id == id,
                                                                                includedProperties: new[] { IncludeClause<Appointment>.Create(x => x.Attendees) },
                                                                                cancellationToken: ct)
                                                               .ConfigureAwait(false);

        return mayBeAppointment.Match<ActionResult<Browsable<GetAppointmentByIdResponse>>>(
            some: entity =>
            {
                NodaTime.DateTimeZone zone = _currentRequestMetadataInfoProvider.GetCurrentDateTimeZone();
                GetAppointmentByIdResponse appointment = new GetAppointmentByIdResponse
                {
                    Id = entity.Id,
                    StartDate = entity.StartDate.InZone(zone),
                    EndDate = entity.EndDate.InZone(zone),
                    Subject = entity.Subject,
                    Location = entity.Location
                };

                return new Browsable<GetAppointmentByIdResponse>()
                {
                    Resource = appointment,
                    Links = new[]
                    {
                        new Link
                        {
                            Href = _linkGenerator.GetUriByName(HttpContext, nameof(GetAppointmentByIdEndpoint), new { Id = id.Value }),
                            Method = "GET",
                            Relations = (new [] { LinkRelation.Self }).ToHashSet()
                        },
                    }
                };
            },
            none: () => new NotFoundResult());
    }
}

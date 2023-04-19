namespace Agenda.API.Resources.Appointments.v1.Delete
{
    using Agenda.Ids;
    using Agenda.Objects;

    using Candoumbe.DataAccess.Abstractions;

    using FastEndpoints;

    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Deletes an appointment by its identifier
    /// </summary>
    public class DeleteEndpoint : Endpoint<AppointmentId>
    {
        private readonly IUnitOfWorkFactory _unitOfWorkFactory;

        /// <summary>
        /// Builds a new <see cref="DeleteEndpoint"/> instance.
        /// </summary>
        /// <param name="unitOfWorkFactory"></param>
        public DeleteEndpoint(IUnitOfWorkFactory unitOfWorkFactory)
        {
            _unitOfWorkFactory = unitOfWorkFactory;
        }

        ///<inheritdoc/>
        public override void Configure()
        {
            Delete("/appointments/{id}");
            AllowAnonymous();
        }

        ///<inheritdoc/>
        public override async Task HandleAsync(AppointmentId req, CancellationToken ct)
        {
            using IUnitOfWork unitOfWork = _unitOfWorkFactory.NewUnitOfWork();

            IRepository<Appointment> repository = unitOfWork.Repository<Appointment>();
            if (await repository.Any(appointment => appointment.Id == req))
            {
                await repository.Delete(appointment => appointment.Id == req, ct).ConfigureAwait(false);
                await unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
                await SendNoContentAsync(ct).ConfigureAwait(false);
            }
            else
            {
                await SendNotFoundAsync(ct).ConfigureAwait(false);
            }
        }
    }
}

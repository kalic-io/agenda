namespace Agenda.Ids
{
    using Fluxera.StronglyTypedId;

    using System;

    public class AppointmentId : StronglyTypedId<AppointmentId, Guid>
    {
        ///<inheritdoc/>
        public AppointmentId(Guid value) : base(value)
        {
        }

        public static AppointmentId New() => new AppointmentId(Guid.NewGuid());
    }

}

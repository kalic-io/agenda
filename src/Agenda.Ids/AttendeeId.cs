namespace Agenda.Ids
{
    using Fluxera.StronglyTypedId;

    using System;

    public class AttendeeId : StronglyTypedId<AttendeeId, Guid>
    {
        ///<inheritdoc/>
        public AttendeeId(Guid value) : base(value)
        {
        }

        /// <summary>
        /// Create a new <see cref="AttendeeId"/>.
        /// </summary>
        /// <returns></returns>
        public static AttendeeId New() => new AttendeeId(Guid.NewGuid());
    }

}

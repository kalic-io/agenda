namespace Agenda.DTO
{
    using Agenda.Ids;


    public class AttendeeInfo : Resource<AttendeeId>
    {
        public string Name { get; set; }

        public string PhoneNumber { get; set; }

        public string Email { get; set; }
    }
}
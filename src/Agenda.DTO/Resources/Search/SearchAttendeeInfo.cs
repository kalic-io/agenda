namespace Agenda.DTO.Resources.Search
{
    using Candoumbe.Types.Numerics;

    /// <summary>
    /// Wraps criteria to search
    /// </summary>
    public class SearchAttendeeInfo : AbstractSearchInfo<AttendeeInfo>
    {
        /// <summary>
        /// Pattern
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Email
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// Phone number
        /// </summary>
        public string PhoneNumber { get; set; }

        public void Deconstruct(out PositiveInteger page, out NonNegativeInteger pageSize, out string sort, out string name, out string email, out string phoneNumber)
        {
            page = Page;
            pageSize = PageSize;
            sort = Sort;
            name = Name;
            email = Email;
            phoneNumber = PhoneNumber;
        }
    }
}

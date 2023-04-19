namespace Agenda.DTO.Resources.Search
{
    using Candoumbe.Types.Numerics;

    public class AbstractSearchInfo<T>
    {
        public NonNegativeInteger PageSize { get; set; }

        public PositiveInteger Page { get; set; }

        public string Sort { get; set; }
    }
}

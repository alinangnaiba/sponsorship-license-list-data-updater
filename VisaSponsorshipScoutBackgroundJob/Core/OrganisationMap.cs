using CsvHelper.Configuration;
using VisaSponsorshipScoutBackgroundJob.Core.Model;

namespace VisaSponsorshipScoutBackgroundJob.Core
{
    internal class OrganisationMap : ClassMap<OrganisationModel>
    {
        internal OrganisationMap()
        {
            Map(o => o.Name).Name("Organisation Name");
            Map(o => o.TownCity).Name("Town/City");
            Map(o => o.County).Name("County");
            Map(o => o.TypeAndRating).Name("Type & Rating");
            Map(o => o.Route).Name("Route");
        }
    }
}

using Raven.Client.Documents.Indexes;
using VisaSponsorshipScoutBackgroundJob.Core.Entities;

namespace VisaSponsorshipScoutBackgroundJob.Infrastructure.Configuration
{
    public class Organisation_ByName : AbstractIndexCreationTask<Organisation>
    {
        public Organisation_ByName()
        {
            Map = organisations => from org in organisations
                                   select new
                                   {
                                       org.Name,
                                   };

            Indexes.Add(x => x.Name, FieldIndexing.Search);
        }
    }
}
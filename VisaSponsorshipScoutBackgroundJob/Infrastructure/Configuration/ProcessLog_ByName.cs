using Raven.Client.Documents.Indexes;
using VisaSponsorshipScoutBackgroundJob.Core.Entities;

namespace VisaSponsorshipScoutBackgroundJob.Infrastructure.Configuration
{
    internal class ProcessLog_ByName : AbstractIndexCreationTask<ProcessLog>
    {
        public ProcessLog_ByName()
        {
            Map = processLog => from log in processLog
                                select new
                                   {
                                       log.Status,
                                       log.FileName
                                   };

            Indexes.Add(x => x.Status, FieldIndexing.Exact);
            Indexes.Add(x => x.FileName, FieldIndexing.Search);
        }
    }
}

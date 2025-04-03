using System.Collections.Concurrent;

namespace VisaSponsorshipScoutBackgroundJob.Core.Entities
{
    public class AddedRecord
    {
        public int Count { get => OrganisationNames.Count; }
        public ConcurrentBag<string> OrganisationNames { get; set; } = new ConcurrentBag<string>();
    }

    public class ProcessLog
    {
        public AddedRecord AddedRecords { get; set; } = new();
        public DeletedOrganisations DeletedRecords { get; set; }
        public List<ProcessLogError> Errors { get; set; } = [];
        public string FileName { get; set; }
        public DateTime? FinishedAt { get; set; }
        public string Id { get; set; }
        public DateTime? SourceLastUpdate { get; set; }
        public DateTime StartedAt { get; set; }
        public string Status { get; set; }
        public int TotalRecordsProcessed { get; set; }
        public UpdateRecord UpdatedRecords { get; set; } = new();
    }

    public class UpdateRecord
    {
        public int Count { get => UpdateRecordDetails.Count; }
        public List<UpdateRecordDetail> UpdateRecordDetails { get; set; } = [];
    }

    public record UpdateRecordDetail(RecordDetail Current, RecordDetail New);
    public record RecordDetail(string Name, string County, List<string> TownCity, List<string> TypeAndRating, List<string> Routes);
    public record DeletedOrganisations(int Count, List<string> OrganisationNames);

    public record ProcessLogError(string Message, string? Method = null, string? Trace = null);
}
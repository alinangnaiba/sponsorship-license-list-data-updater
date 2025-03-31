namespace VisaSponsorshipScoutBackgroundJob.Core.Entities
{
    public class ProcessLog
    {
        public int AddedRecords { get; set; }
        public int DeletedRecords { get; set; }
        public List<ProcessLogError> Errors { get; set; } = new();
        public string FileName { get; set; }
        public DateTime? FinishedAt { get; set; }
        public string Id { get; set; }
        public DateTime? SourceLastUpdate { get; set; }
        public DateTime StartedAt { get; set; }
        public string Status { get; set; }
        public int TotalRecordsProcessed { get; set; }
        public int UpdatedRecords { get; set; }
    }

    public record ProcessLogError(string Message, string? Method = null, string? Trace = null);
}
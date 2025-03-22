namespace VisaSponsorshipScoutBackgroundJob.Core.Entities
{
    public class ProcessLog
    {
        public string Id { get; set; }
        public int AddedRecords { get; set; }
        public int DeletedRecords { get; set; }
        public  string ErrorMessage { get; set; }

        public string FileName { get; set; }
        public DateTime? FinishedAt { get; set; }
        public DateTime StartedAt { get; set; }
        public string Status { get; set; }
        public int TotalRecordsProcessed { get; set; }
    }
}

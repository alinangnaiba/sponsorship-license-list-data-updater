namespace VisaSponsorshipScoutBackgroundJob.Core.Entities
{
    public class Organisation
    {
        public string Id { get; set; } // RavenDB uses a string ID
        public string Name { get; set; }
        public List<string> TownCities { get; set; }
        public string County { get; set; }
        public List<string> TypeAndRatings { get; set; }
        public List<string> Routes { get; set; }
    }
}

namespace VisaSponsorshipScoutBackgroundJob.Core.Entities
{
    public class Organisation
    {
        public string County { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public List<string> Routes { get; set; }
        public List<string> TownCities { get; set; }
        public List<string> TypeAndRatings { get; set; }
    }
}
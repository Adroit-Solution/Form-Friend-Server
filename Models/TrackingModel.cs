namespace Server.Models
{
    public class TrackingModel
    {
        public string GroupName { get; set; }
        public Guid GroupId { get; set; }
        public List<Tracker> Participants { get; set; }
    }

    public class Tracker
    {
        public string Email { get; set; }
        public bool Seen { get; set; } = false;
        public bool Filled { get; set; } = false;
    }
}

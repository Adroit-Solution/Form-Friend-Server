namespace Server.Models
{
    public class Email
    {
        public string Message { get; set; }
        public List<Credential> Participants { get; set; }
    }

    public class Credential
    {
        public string Name { get; set; }
        public string Email { get; set; }
    }
}

namespace Server.Models
{
    public class SettingsModel
    {
        #region Settings

        public bool IsAnonymous { get; set; } = true;

        public bool IsMultiple { get; set; } = false;

        public bool IsEditable { get; set; } = false;

        public bool IsGroup { get; set; } = false;

        public bool IsTimeBound { get; set; } = false;

        public DateTime StartTime { get; set; } = DateTime.UtcNow;

        public DateTime EndTime { get; set; } = DateTime.UtcNow;

        public bool IsResponseLimit { get; set; } = false;

        public int ResponseLimit { get; set; } = 0;

        public bool IsResponseLimitPerUser { get; set; } = false;

        public int ResponseLimitPerUser { get; set; } = 0;

        public bool ShowProgressBar { get; set; } = false;

        public string ConfirmationMessage { get; set; } = "Your response has been recorded";

        #endregion
    }
}

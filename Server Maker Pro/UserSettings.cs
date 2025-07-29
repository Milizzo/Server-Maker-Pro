namespace Server_Maker_Pro
{
    /// <summary>
    /// Represents the configuration settings for the user, specifically pertaining
    /// to the file system path where server-related data will be stored.
    /// </summary>
    public class UserSettings
    {
        /// <summary>
        /// Gets the file system path where server data and configurations are stored.
        /// This property is used to manage the location of server folders and related resources.
        /// </summary>
        public string ServersPath { get; init; } = string.Empty;
    }
}
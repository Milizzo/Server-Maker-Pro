namespace Server_Maker_Pro
{
    /// <summary>
    /// Represents information about a server, including its version and loader type.
    /// </summary>
    public class ServerInfo
    {
        /// <summary>
        /// Gets the server version. This specifies the version identifier associated with the server configuration.
        /// </summary>
        /// <remarks>
        /// The version property is used to determine the compatibility of plugins or components with the server.
        /// It supports nullable strings and is initialized to an empty string by default.
        /// </remarks>
        public string? Version { get; init; } = string.Empty;

        /// <summary>
        /// Gets the loader type associated with the server configuration.
        /// </summary>
        /// <remarks>
        /// The loader property specifies the type of mod loader or plugin loader used by the server.
        /// </remarks>
        public string Loader { get; init; } = string.Empty;

        /// <summary>
        /// Represents information about a server, including its Minecraft version and the loader type.
        /// </summary>
        public ServerInfo(string? version, string loader)
        {
            Version = version;
            Loader = loader;
        }

        /// <summary>
        /// Represents information about a server, including its version and the type of loader it uses.
        /// </summary>
        public ServerInfo()
        {
        }
    }
}
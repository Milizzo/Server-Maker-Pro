using System.Text.Json.Serialization;
using System;

namespace Server_Maker_Pro
{
    public class ServerInfo
    {
        public string Version { get; set; } = string.Empty;
        public string Loader { get; set; } = string.Empty;

        public ServerInfo(string version, string loader)
        {
            Version = version;
            Loader = loader;
        }

        public ServerInfo()
        {

        }
    }
}

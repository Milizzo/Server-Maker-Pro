using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Modrinth;

namespace Server_Maker_Pro
{
    static class Program
    {
        static string serversPath = string.Empty;

        const string infoFileName = "info.json";

        static void Main()
        {
            // FirstSetup();
            serversPath = "/Users/milol/Documents/Servers/";

            if (!Directory.Exists(serversPath))
            {
                Console.WriteLine($"Servers folder not found. Creating new servers folder at path: {serversPath}.");
                Directory.CreateDirectory(serversPath);
            }

            string[] thingsToDo =
            [
                "play",
                "create",
                "exit",
            ];

            while (true)
            {
                string response = AskForOptions(thingsToDo);

                if (response == "play")
                {
                    ServerSelection();
                }
                if (response == "create")
                {
                    CreateServer();
                }
                else if (response == "exit")
                {
                    Console.WriteLine("Goodbye!");
                    break;
                }
            }
        }

        static void CreateServer()
        {
            string? name = null;
            while (name == null || InvalidFileName(name) || Directory.Exists(Path.Combine(serversPath, name)))
            {
                Console.Write("Enter valid server name: ");
                name = (Console.ReadLine() ?? string.Empty).Trim();

                if (name.Equals("exit", StringComparison.InvariantCultureIgnoreCase)) return;
            }

            System.Console.WriteLine("Getting Paper versions...");

            List<string> versionListCook = [];
            List<string> mcVerLCook = [];

            var client = new HttpClient();

            // Step 1: Get all versions
            string versionListJson = client.GetStringAsync("https://api.papermc.io/v2/projects/paper").Result;
            using var versionDoc = JsonDocument.Parse(versionListJson);
            var versions = versionDoc.RootElement.GetProperty("versions");

            foreach (var versionElement in versions.EnumerateArray())
            {
                string version = versionElement.GetString() ?? string.Empty;

                // Step 2: Get builds for this version
                string buildListJson = client.GetStringAsync($"https://api.papermc.io/v2/projects/paper/versions/{version}").Result;
                using var buildDoc = JsonDocument.Parse(buildListJson);
                var builds = buildDoc.RootElement.GetProperty("builds").EnumerateArray();

                int lastBuild = -1;
                foreach (var build in builds)
                    lastBuild = build.GetInt32(); // get the last one

                if (lastBuild != -1)
                {
                    // Step 3: Construct download URL
                    string downloadUrl = $"https://api.papermc.io/v2/projects/paper/versions/{version}/builds/{lastBuild}/downloads/paper-{version}-{lastBuild}.jar";
                    // Console.WriteLine($"{version}: {downloadUrl}");
                    versionListCook.Add(version);
                    mcVerLCook.Add(downloadUrl);
                }
            }

            System.Console.WriteLine("Creating server directory...");

            string fullPath = Path.Combine(serversPath, name);
            Directory.CreateDirectory(fullPath);

            System.Console.WriteLine("Select a Minecraft version:");

            string mcVer = AskForOptions([.. versionListCook]);

            System.Console.WriteLine("Downloading server software...");

            string url = mcVerLCook[Array.IndexOf([.. versionListCook], mcVer)];
            string softwarePath = Path.Combine(fullPath, Path.GetFileName(url));
            byte[] bytes = client.GetByteArrayAsync(url).Result;
            File.WriteAllBytes(softwarePath, bytes);

            System.Console.WriteLine("Adding server properties file...");

            string propertiesFilePath = Path.Combine(fullPath, "server.properties");
            File.WriteAllText(propertiesFilePath, File.ReadAllText("default_properties.txt"));

            System.Console.WriteLine("Generating and adding server info file...");

            string infoFilePath = Path.Combine(fullPath, infoFileName);
            string jsonInfo = JsonSerializer.Serialize(new ServerInfo(mcVer, "paper"));
            File.WriteAllText(infoFilePath, jsonInfo);

            System.Console.WriteLine($"Successfully created server: {name}.");

            ServerMenu(fullPath);
        }

        public static int? GetIndex<T>(this T[] array, T of) where T : IEquatable<T>
        {
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i].Equals(of)) return i;
            }
            return null;
        }

        static bool InvalidFileName(string fileName)
        {
            if (fileName.EndsWith('.') || fileName.EndsWith(' ')) return true;

            foreach (char inv in Path.GetInvalidFileNameChars())
            {
                if (fileName.Contains(inv)) return true;
            }
            return false;
        }

        static void ServerSelection()
        {
            string[] files = Directory.GetDirectories(serversPath);

            if (files.Length <= 0)
            {
                Console.WriteLine("No servers available!");
                return;
            }

#pragma warning disable CS8601 // Possible null reference assignment.
            files = [.. files.Select(Path.GetFileName)];
#pragma warning restore CS8601 // Possible null reference assignment.

            string response = AskForOptions(files, "Select a server (by name): ");
            string fullPath = Path.Combine(serversPath, response);

            if (!Directory.Exists(fullPath))
            {
                Console.WriteLine($"Server could not be found: {response}");
                return;
            }

            ServerMenu(fullPath);
        }

        static void ServerMenu(string server)
        {
            string[] options =
            [
                "back",
                "start",
                "config",
                "plugins",
                "folder",
            ];

            while (true)
            {
                string response = AskForOptions(options);

                if (response == "back")
                {
                    break;
                }
                else if (response == "start")
                {
                    Console.WriteLine("Nice try.");
                }
                else if (response == "config")
                {
                    string propertiesPath = Path.Combine(server, "server.properties");

                    if (!File.Exists(propertiesPath))
                    {
                        Console.WriteLine("Server properties file not found. You may need to start the server first.");
                        continue;
                    }

                    OpenTextFile(propertiesPath);
                }
                else if (response == "plugins")
                {
                    ServerPlugins(server);
                }
                else if (response == "folder")
                {
                    OpenFolder(server);
                }
            }
        }

        static void ServerPlugins(string server)
        {
            while (true)
            {
                string[] options =
                [
                    "back",
                    "install",
                    "list",
                ];

                string response = AskForOptions(options);

                if (response == "back")
                {
                    return;
                }
                else if (response == "install")
                {
                    InstallPlugins(server);
                }
                else if (response == "list")
                {
                    string[] plugins = Directory.GetFiles(server, "*.jar");

                    foreach (string plugin in plugins)
                    {
                        System.Console.WriteLine(Path.GetFileNameWithoutExtension(plugin));
                    }
                }
            }
        }

        static void InstallPlugins(string server)
        {
            try
            {
                string infoPath = Path.Combine(server, infoFileName);
                string jsonInfo = File.ReadAllText(infoPath);
                ServerInfo info = JsonSerializer.Deserialize<ServerInfo>(jsonInfo) ?? throw new Exception("Server info file could not be deserialized.");

                string version = info.Version;
                string loader = info.Loader;

                var modrinth = new ModrinthClient();

                var facets = new FacetCollection
                {
                    { Facet.ProjectType(Modrinth.Models.Enums.Project.ProjectType.Plugin) },
                    { Facet.Version(version) },
                    { Facet.Category(loader) },
                };

                string query = string.Empty;

                while (true)
                {
                    var searchResult = modrinth.Project.SearchAsync(query, facets: facets, limit: 10).Result;

                    foreach (var proj in searchResult.Hits)
                    {
                        System.Console.WriteLine(proj.Title);
                    }

                    System.Console.Write("Type to search (\"exit\" to break): ");
                    query = Console.ReadLine() ?? string.Empty;

                    if (query == "exit") break;
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"An error occured: {ex.Message}{Environment.NewLine}Returning to server menu...");
            }
        }

        static void FirstSetup()
        {
            string? path = null;

            while (string.IsNullOrWhiteSpace(path))
            {
                Console.Write("Path to server folders: ");
                path = Console.ReadLine() ?? string.Empty;
            }

            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            serversPath = path;
        }

        static string AskForOptions(string[] options, string question = "Type an option name: ")
        {
            Console.WriteLine(Environment.NewLine + "Choose an option:");

            for (int i = 0; i < options.Length; i++)
            {
                Console.WriteLine(/*(optionPrefix != null ? (i + 1).ToString() + optionPrefix : "") + */"- " + options[i]);
            }

            string? currentResponse = null;
            while (currentResponse == null || !options.Contains(currentResponse))
            {
                Console.Write(question);
                currentResponse = Console.ReadLine() ?? string.Empty;
            }

            Console.WriteLine("");

            return currentResponse;
        }

        static void OpenFolder(string path)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start("explorer", path);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", path);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", path);
            }
            else
            {
                Console.WriteLine("Unsupported OS.");
            }
        }

        static void OpenTextFile(string path)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Force open with Notepad
                Process.Start("notepad.exe", $"\"{path}\"");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // Force open with TextEdit
                Process.Start("open", ["-e", path]);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Try to use a common text editor (adjust if needed)
                Process.Start("gedit", [path]); // Or "nano", "kate", "mousepad", etc.
            }
            else
            {
                Console.WriteLine("Unsupported OS.");
            }
        }
    }

    public class PaperProjectInfo
    {
        public List<string> Versions { get; set; } = [];
    }

    public class BuildInfo
    {
        public List<int> Builds { get; set; } = [];
    }
}

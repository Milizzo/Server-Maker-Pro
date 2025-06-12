using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Modrinth;
using Modrinth.Models;

namespace Server_Maker_Pro
{
    static class Program
    {
        static string serversPath = string.Empty;

        const string infoFileName = "info.json";

        public static string GetMinecraftPath()
        {
            string homeDir;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                homeDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); // Roaming
                return Path.Combine(homeDir, ".minecraft");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                homeDir = Environment.GetEnvironmentVariable("HOME") ?? "";
                return Path.Combine(homeDir, "Library", "Application Support", "minecraft");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                homeDir = Environment.GetEnvironmentVariable("HOME") ?? "";
                return Path.Combine(homeDir, ".minecraft");
            }
            else
            {
                throw new Exception("Unsupported OS.");
            }
        }

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
                "servers",
                "create",
                "exit",
            ];

            while (true)
            {
                try
                {
                    string response = AskForOptions(thingsToDo);

                    if (response == "servers")
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
                catch (Exception ex)
                {
                    System.Console.WriteLine($"The software has returned to the main menu due to an uncaught error:{Environment.NewLine}{ex.Message}");
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

            System.Console.WriteLine("Starting server software download...");

            string url = mcVerLCook[Array.IndexOf([.. versionListCook], mcVer)];
            Task<byte[]> downloadedSoftware = client.GetByteArrayAsync(url);

            System.Console.WriteLine("Adding server properties file...");

            string propertiesFilePath = Path.Combine(fullPath, "server.properties");
            System.IO.File.WriteAllText(propertiesFilePath, System.IO.File.ReadAllText("default_properties.txt"));

            System.Console.WriteLine("Generating and adding server info file...");

            string infoFilePath = Path.Combine(fullPath, infoFileName);
            string jsonInfo = JsonSerializer.Serialize(new ServerInfo(mcVer, "paper"));
            System.IO.File.WriteAllText(infoFilePath, jsonInfo);

            System.Console.WriteLine("Finishing server software download...");

            byte[] softwareResult = downloadedSoftware.Result;
            string softwarePath = Path.Combine(fullPath, Path.GetFileName(url));
            System.IO.File.WriteAllBytes(softwarePath, softwareResult);

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
                "import world",
                "folder",
            ];

            while (true)
            {
                System.Console.WriteLine($"Current server: {Path.GetFileName(server)}");

                string response = AskForOptions(options);

                if (response == "back")
                {
                    break;
                }
                else if (response == "start")
                {
                    System.Console.WriteLine($"Are you sure you want to start the server \"{Path.GetFileName(server)}\"?");

                    string[] options2 =
                    [
                        "cancel",
                        "start",
                    ];

                    string response2 = AskForOptions(options2);

                    if (response2 == "cancel")
                    {
                        continue;
                    }
                    else if (response2 == "start")
                    {
                        StartServer(server);
                    }
                }
                else if (response == "config")
                {
                    string propertiesPath = Path.Combine(server, "server.properties");

                    if (!System.IO.File.Exists(propertiesPath))
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
                else if (response == "import world")
                {
                    ImportWorld(server);
                }
                else if (response == "folder")
                {
                    OpenFolder(server);
                }
            }
        }

        static void StartServer(string server)
        {
            string? jarPath = Directory.GetFiles(server, "*.jar").FirstOrDefault();

            if (jarPath == null)
            {
                System.Console.WriteLine("No server .jar file could be found for this server. Try installing a new one from papermc.io or creating a new server.");
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "java",
                Arguments = $"-Xmx4G -Xms4G -jar \"{jarPath}\" nogui",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = false,
                WorkingDirectory = server,
            };

            Process serverProcess = new() { StartInfo = startInfo };

            serverProcess.OutputDataReceived += (sender, e) =>
            {
                System.Console.WriteLine($"Server: {e}");
            };

            serverProcess.ErrorDataReceived += (sender, e) =>
            {
                System.Console.WriteLine($"Server Error: {e}");
            };

            serverProcess.Start();
            serverProcess.BeginOutputReadLine();
            serverProcess.BeginErrorReadLine();

            void KillAction(object? sender, object e)
            {
                if (serverProcess != null)
                {
                    serverProcess.Kill();
                    serverProcess.Dispose();
                }
            }

            AppDomain.CurrentDomain.ProcessExit += KillAction;
            Console.CancelKeyPress += KillAction;

            while (!serverProcess.HasExited)
            {
                string input = Console.ReadLine() ?? string.Empty;
                serverProcess.StandardInput.WriteLine(input);
            }

            AppDomain.CurrentDomain.ProcessExit -= KillAction;
            Console.CancelKeyPress -= KillAction;

            serverProcess.WaitForExit();
            int exitCode = serverProcess.ExitCode;

            System.Console.WriteLine($"Server process stopped with exit code {exitCode} ({(exitCode == 0 ? "no errors" : "server crashed")}).");
        }

        static void ImportWorld(string server)
        {
            string worldPath = Path.Combine(server, "world");

            if (Directory.Exists(worldPath) && Directory.GetFiles(worldPath).Length > 0)
            {
                System.Console.WriteLine("Your server already has a world. Please create a new server or manually delete your current server's world folder(s).");
                return;
            }

            string[] options1 =
            [
                "back",
                "from minecraft",
                "from folder path",
            ];

            string response1 = AskForOptions(options1, "How to import: ");

            if (response1 == "back")
            {
                return;
            }
            else if (response1 == "from minecraft")
            {
                string minecraftPath = GetMinecraftPath();
                string savesPath = Path.Combine(minecraftPath, "saves");
                string[] saves = Directory.GetDirectories(savesPath);

                string[] options2 = [.. saves.Select(s => Path.GetFileName(s))];

                string response2 = AskForOptions(options2);

                string selectedSavePath = saves[Array.IndexOf(options2, response2)];

                CopyDirectory(selectedSavePath, worldPath);
            }
            else if (response1 == "from folder path")
            {
                System.Console.Write("Enter path to world folder: ");
                string path = Console.ReadLine() ?? string.Empty;

                if (!Directory.Exists(path))
                {
                    System.Console.WriteLine($"No directory could be found at path: {path}.");
                    return;
                }

                CopyDirectory(path, worldPath);
            }
        }

        static void ServerPlugins(string server)
        {
            string pluginsDir = Path.Combine(server, "plugins");

            if (!Directory.Exists(pluginsDir)) Directory.CreateDirectory(pluginsDir);

            while (true)
            {
                string[] options =
                [
                    "back",
                    "install",
                    "list",
                    "folder",
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
                    string[] plugins = Directory.GetFiles(pluginsDir, "*.jar");

                    if (plugins.Length == 0)
                    {
                        System.Console.WriteLine("No plugins installed!");
                    }

                    foreach (string plugin in plugins)
                    {
                        System.Console.WriteLine(Path.GetFileNameWithoutExtension(plugin));
                    }
                }
                else if (response == "folder")
                {
                    OpenFolder(pluginsDir);
                }
            }
        }

        static void InstallPlugins(string server)
        {
            try
            {
                string infoPath = Path.Combine(server, infoFileName);
                string jsonInfo = System.IO.File.ReadAllText(infoPath);
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

                string? query = null;

                SearchResult[] searched;

                while (true)
                {
                    if (query != null) System.Console.WriteLine($"Results for \"{query}\":{Environment.NewLine}");

                    var searchResult = modrinth.Project.SearchAsync(query ?? string.Empty, facets: facets, limit: 30).Result;

                    foreach (var proj in searchResult.Hits)
                    {
                        System.Console.WriteLine(proj.Title);
                    }

                    System.Console.Write("Type to search (\"exit\" to break, \"next\" to be able to choose a plugin from the current list): ");
                    query = Console.ReadLine() ?? string.Empty;

                    if (query == "exit") return;

                    if (query == "next")
                    {
                        searched = searchResult.Hits;
                        break;
                    }
                }

                List<string> options = [.. searched.Select(s => s.Title ?? "Failed to load title")];
                options.Add("cancel");

                string response = AskForOptions([.. options], "Choose a plugin to download (by name): ");

                if (response.Equals("cancel", StringComparison.InvariantCultureIgnoreCase)) return;

                HashSet<string> downloaded = [];
                var plugin = searched[options.IndexOf(response)];

                string pluginsDir = Path.Combine(server, "plugins");
                DownloadPluginAndDependenciesRecursive(plugin.Slug ?? throw new Exception("Failed to load project slug."), version, loader, downloaded, pluginsDir);

                System.Console.WriteLine(Environment.NewLine + $"{downloaded.Count} plugins have been installed to server \"{Path.GetFileName(server)}\".");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"An error occured: {ex.Message}{Environment.NewLine}Returning...");
            }
        }

        static void DownloadPluginAndDependenciesRecursive(string slug, string minecraftVersion, string loader, HashSet<string> downloadedSlugs, string pluginsPath)
        {
            if (downloadedSlugs.Contains(slug)) return;

            System.Console.WriteLine($"Downloading plugin \"{slug}\"...");

            var client = new ModrinthClient();
            var project = client.Project.GetAsync(slug).GetAwaiter().GetResult();
            var versions = client.Version.GetProjectVersionListAsync(slug).GetAwaiter().GetResult();

            var matchingVersion = versions.FirstOrDefault(v =>
                v.GameVersions.Contains(minecraftVersion, StringComparer.OrdinalIgnoreCase) &&
                v.Loaders.Contains(loader, StringComparer.OrdinalIgnoreCase));

            if (!downloadedSlugs.Contains(project.Slug)) downloadedSlugs.Add(project.Slug);

            if (matchingVersion == null)
            {
                System.Console.WriteLine($"No matching versions could be found for plugin \"{project.Slug}\". Skipping download.");

                return;
            }

            var file = matchingVersion.Files.FirstOrDefault(f => f.Url.EndsWith(".jar"));

            if (file != null)
            {
                string fileName = file.FileName ?? Path.GetFileName(file.Url);

                if (!Directory.Exists(pluginsPath)) Directory.CreateDirectory(pluginsPath);

                string destination = Path.Combine(pluginsPath, fileName);

                if (!System.IO.File.Exists(destination))
                {
                    using var httpClient = new HttpClient();
                    byte[] pluginBytes = httpClient.GetByteArrayAsync(file.Url).GetAwaiter().GetResult();
                    System.IO.File.WriteAllBytes(destination, pluginBytes);

                    System.Console.WriteLine($"Successfully installed plugin \"{slug}\".");
                }
                else
                {
                    System.Console.WriteLine($"Skipped downloading plugin \"{slug}\" because it is already downloaded.");
                }
            }

            if (matchingVersion.Dependencies != null)
            {
                foreach (var dep in matchingVersion.Dependencies.Where(d => d.ProjectId is not null))
                {
                    var depProject = client.Project.GetAsync(dep.ProjectId!).GetAwaiter().GetResult();
                    DownloadPluginAndDependenciesRecursive(depProject.Slug!, minecraftVersion, loader, downloadedSlugs, pluginsPath);
                }
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

        public static void CopyDirectory(string p1, string p2)
        {
            if (!Directory.Exists(p1)) throw new Exception("Directory not found: " + p1 + ".");
            if (!Directory.Exists(p2)) Directory.CreateDirectory(p2);

            if (Directory.GetFiles(p2).Length > 0) throw new Exception("There are already files present in directory: " + p1 + ".");

            string[] files = Directory.GetFiles(p1);
            string[] directories = Directory.GetDirectories(p1);

            foreach (string file in files)
            {
                string originalPath = file;
                string newPath = Path.Combine(p2, Path.GetFileName(file));

                System.IO.File.Copy(originalPath, newPath);
            }

            foreach (string directory in directories)
            {
                string originalPath = directory;
                string newPath = Path.Combine(p2, Path.GetFileName(directory));

                CopyDirectory(originalPath, newPath);
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

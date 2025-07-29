using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Modrinth;
using Modrinth.Models;

namespace Server_Maker_Pro
{
    internal static class Program
    {
        /// <summary>
        /// Represents the file system path where server data and configurations are stored.
        /// </summary>
        private static string _serversPath = string.Empty;

        /// <summary>
        /// Specifies the file name used to store server metadata in JSON format.
        /// </summary>
        private const string InfoFileName = "info.json";

        /// <summary>
        /// Retrieves the file path to the Minecraft directory based on the operating system.
        /// For Windows, it returns the path to the .minecraft folder inside the AppData\Roaming directory.
        /// For macOS, it retrieves the path to the Minecraft directory inside the "Library/Application Support" folder.
        /// For Linux, it resolves to the .minecraft directory within the user's home folder.
        /// Throws an exception if the operating system is unsupported.
        /// </summary>
        /// <returns>
        /// The file path to the Minecraft directory specific to the operating system.
        /// </returns>
        public static string GetMinecraftPath()
        {
            string homeDir;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                homeDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); // Roaming
                return Path.Combine(homeDir, ".minecraft");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                homeDir = Environment.GetEnvironmentVariable("HOME") ?? "";
                return Path.Combine(homeDir, "Library", "Application Support", "minecraft");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                homeDir = Environment.GetEnvironmentVariable("HOME") ?? "";
                return Path.Combine(homeDir, ".minecraft");
            }

            throw new("Unsupported OS.");
        }

        /// <summary>
        /// Entry point of the Server Maker Pro application.
        /// Initializes the application by loading or creating user settings, setting up the necessary directories, and
        /// presenting the main menu to the user in a loop.
        /// Allows users to manage servers, create new servers, or exit the application.
        /// Handles unhandled exceptions during user interactions by returning to the main menu.
        /// </summary>
        private static void Main()
        {
            Console.WriteLine("Server Maker Pro v0.1 by @milizzo" + Environment.NewLine);

            var settingsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Server Maker Pro");
            if (!Directory.Exists(settingsDir)) Directory.CreateDirectory(settingsDir);

            var settingsFile = Path.Combine(settingsDir, "user.json");
            if (System.IO.File.Exists(settingsFile))
            {
                try
                {
                    var jsonSettings = System.IO.File.ReadAllText(settingsFile);
                    var settings = JsonSerializer.Deserialize<UserSettings>(jsonSettings) ??
                                   throw new("Failed to load user settings.");
                    _serversPath = settings.ServersPath;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load user settings: {ex.Message}. Resetting settings...");
                    FirstSetup(settingsFile);
                }
            }
            else
            {
                FirstSetup(settingsFile);
            }

            if (!Directory.Exists(_serversPath))
            {
                Console.WriteLine($"Servers folder not found. Creating new servers folder at path: {_serversPath}.");
                Directory.CreateDirectory(_serversPath);
            }

            string?[] thingsToDo =
            [
                "servers",
                "create",
                "exit",
            ];

            while (true)
            {
                try
                {
                    var response = AskForOptions(thingsToDo);

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
                    Console.WriteLine(
                        $"The software has returned to the main menu due to an uncaught error:{Environment.NewLine}{ex.Message}");
                }
            }
        }

        /// <summary>
        /// Creates a new Minecraft server using the PaperMC server software.
        /// Prompts the user for a server name, validating against invalid names or pre-existing directories.
        /// Sets up the server directory, retrieves a list of supported Minecraft versions, and allows the user to select one.
        /// Downloads the appropriate server software, generates default configuration files, and finalizes the server setup.
        /// Handles user guidance via console messages throughout the process and indicates completion upon success.
        /// </summary>
        /// <remarks>
        /// The method involves network operations to retrieve supported Minecraft server versions and to download the server software.
        /// It also performs file system operations to configure the server directory and write necessary files.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown if there are issues in retrieving server versions or downloading the server software.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown if there are errors during file or directory creation or modification.
        /// </exception>
        private static void CreateServer()
        {
            string? name = null;
            while (name == null || InvalidFileName(name) || Directory.Exists(Path.Combine(_serversPath, name)))
            {
                Console.Write("Enter valid server name: ");
                name = (Console.ReadLine() ?? string.Empty).Trim();

                if (name.Equals("exit", StringComparison.InvariantCultureIgnoreCase)) return;
            }

            Console.WriteLine("Getting Paper versions...");

            List<string?> versionListCook = [];
            List<string> mcVerLCook = [];

            var client = new HttpClient();

            // Step 1: Get all versions
            var versionListJson = client.GetStringAsync("https://api.papermc.io/v2/projects/paper").Result;
            using var versionDoc = JsonDocument.Parse(versionListJson);
            var versions = versionDoc.RootElement.GetProperty("versions");

            foreach (var versionElement in versions.EnumerateArray())
            {
                var version = versionElement.GetString() ?? string.Empty;

                // Step 2: Get builds for this version
                var buildListJson = client
                    .GetStringAsync($"https://api.papermc.io/v2/projects/paper/versions/{version}").Result;
                using var buildDoc = JsonDocument.Parse(buildListJson);
                var builds = buildDoc.RootElement.GetProperty("builds").EnumerateArray();

                var lastBuild = -1;
                foreach (var build in builds)
                    lastBuild = build.GetInt32(); // get the last one

                if (lastBuild != -1)
                {
                    // Step 3: Construct download URL
                    var downloadUrl =
                        $"https://api.papermc.io/v2/projects/paper/versions/{version}/builds/{lastBuild}/downloads/paper-{version}-{lastBuild}.jar";
                    versionListCook.Add(version);
                    mcVerLCook.Add(downloadUrl);
                }
            }

            Console.WriteLine("Creating server directory...");

            var fullPath = Path.Combine(_serversPath, name);
            Directory.CreateDirectory(fullPath);

            Console.WriteLine("Select a Minecraft version:");

            var mcVer = AskForOptions([.. versionListCook]);

            Console.WriteLine("Starting server software download...");

            var url = mcVerLCook[Array.IndexOf([.. versionListCook], mcVer)];
            var downloadedSoftware = client.GetByteArrayAsync(url);

            Console.WriteLine("Adding server properties file...");

            var propertiesFilePath = Path.Combine(fullPath, "server.properties");
            System.IO.File.WriteAllText(propertiesFilePath, System.IO.File.ReadAllText("default_properties.txt"));

            Console.WriteLine("Generating and adding server info file...");

            var infoFilePath = Path.Combine(fullPath, InfoFileName);
            var jsonInfo = JsonSerializer.Serialize(new ServerInfo(mcVer, "paper"));
            System.IO.File.WriteAllText(infoFilePath, jsonInfo);

            Console.WriteLine("Finishing server software download...");

            var softwareResult = downloadedSoftware.Result;
            var softwarePath = Path.Combine(fullPath, Path.GetFileName(url));
            System.IO.File.WriteAllBytes(softwarePath, softwareResult);

            Console.WriteLine($"Successfully created server: {name}.");

            ServerMenu(fullPath);
        }

        /// <summary>
        /// Retrieves the index of the first occurrence of a specified element in an array.
        /// If the element is not found, returns null.
        /// </summary>
        /// <typeparam name="T">
        /// The type of elements in the array. The type must implement the IEquatable interface to allow equality comparison.
        /// </typeparam>
        /// <param name="array">
        /// The array in which to search for the specified element.
        /// </param>
        /// <param name="of">
        /// The element to locate within the array using equality comparison.
        /// </param>
        /// <returns>
        /// The zero-based index of the first occurrence of the specified element in the array if found; otherwise, null.
        /// </returns>
        public static int? GetIndex<T>(this T[] array, T of) where T : IEquatable<T>
        {
            for (var i = 0; i < array.Length; i++)
            {
                if (array[i].Equals(of)) return i;
            }

            return null;
        }

        /// <summary>
        /// Determines if a given file name is invalid according to the operating system's file name restrictions.
        /// A file name is considered invalid if it ends with a period or space, or if it contains any characters
        /// that are not allowed in file names as per the operating system's constraints.
        /// </summary>
        /// <param name="fileName">
        /// The file name to validate.
        /// </param>
        /// <returns>
        /// A boolean value indicating whether the file name is invalid. Returns true if the file name is invalid;
        /// otherwise, returns false.
        /// </returns>
        private static bool InvalidFileName(string fileName)
        {
            if (fileName.EndsWith('.') || fileName.EndsWith(' ')) return true;
            return Path.GetInvalidFileNameChars().Any(fileName.Contains);
        }

        /// <summary>
        /// Provides functionality for users to select a server from a list of available servers
        /// within the specified servers directory. Checks if the directory contains any servers
        /// and prompts the user to choose one. If the selected server does not exist or is invalid,
        /// a corresponding error message is displayed. Upon successful selection, it initiates
        /// the server menu for further actions.
        /// </summary>
        /// <remarks>
        /// The method assumes that the servers directory path is properly set and accessible.
        /// If there are no servers available, a message is displayed, and the method exits.
        /// Null and invalid selections are appropriately handled.
        /// </remarks>
        private static void ServerSelection()
        {
            string?[] files = Directory.GetDirectories(_serversPath);

            if (files.Length <= 0)
            {
                Console.WriteLine("No servers available!");
                return;
            }

#pragma warning disable CS8601 // Possible null reference assignment.
            files = [.. files.Select(Path.GetFileName)];
#pragma warning restore CS8601 // Possible null reference assignment.

            var response = AskForOptions(files, "Select a server (by name): ");
            var fullPath = Path.Combine(_serversPath, response ?? throw new InvalidOperationException());

            if (!Directory.Exists(fullPath))
            {
                Console.WriteLine($"Server could not be found: {response}");
                return;
            }

            ServerMenu(fullPath);
        }

        /// <summary>
        /// Manages the server menu operations for a specified server directory.
        /// Provides various options such as starting the server, configuring its properties,
        /// managing plugins, importing a world, or opening the server folder.
        /// Continuously runs until the "back" option is selected.
        /// </summary>
        /// <param name="server">
        /// The file path to the server directory to perform operations on.
        /// </param>
        private static void ServerMenu(string server)
        {
            string?[] options =
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
                Console.WriteLine($"Current server: {Path.GetFileName(server)}");

                var response = AskForOptions(options);

                if (response == "back")
                {
                    break;
                }

                if (response == "start")
                {
                    Console.WriteLine(
                        $"Are you sure you want to start the server \"{Path.GetFileName(server)}\"?");

                    string?[] options2 =
                    [
                        "cancel",
                        "start",
                    ];

                    var response2 = AskForOptions(options2);

                    if (response2 == "cancel")
                    {
                        continue;
                    }

                    if (response2 == "start")
                    {
                        StartServer(server);
                    }
                }
                else if (response == "config")
                {
                    var propertiesPath = Path.Combine(server, "server.properties");

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

        /// <summary>
        /// Starts a Minecraft server located in the specified directory.
        /// The method performs necessary setup if it's the first time the server is being started,
        /// such as prompting for EULA agreement and creating required files.
        /// It also ensures the correct execution environment by checking the operating system and preparing startup scripts.
        /// </summary>
        /// <param name="server">
        /// The directory path where the server files are located. This directory should
        /// contain the server's .jar file and other necessary resources.
        /// </param>
        private static void StartServer(string server)
        {
            var eulaPath = Path.Join(server, "eula.txt");

            if (!System.IO.File.Exists(eulaPath))
            {
                Console.WriteLine(
                    "Before starting a server for the first time, you must agree to Minecraft's EULA (https://aka.ms/MinecraftEULA).");
                Console.WriteLine("Do you agree to the Minecraft EULA?");

                string?[] options =
                [
                    "cancel",
                    "agree",
                ];

                var response = AskForOptions(options);

                switch (response)
                {
                    case "cancel":
                        return;
                    case "agree":
                        System.IO.File.WriteAllText(eulaPath,
                            $@"#By changing the setting below to TRUE you are indicating your agreement to our EULA (https://aka.ms/MinecraftEULA).
#{DateTime.Now:ddd MMM dd HH:mm:ss zzz yyyy}
eula=true
");
                        Console.WriteLine("You have agreed to Minecraft's EULA. Starting server...");
                        break;
                }
            }
            else
            {
                Console.WriteLine("Starting server...");
            }

            Console.WriteLine(
                "Note that you might need to install Java if you haven't already (from https://www.oracle.com/java/technologies/downloads/#jdk24-windows).");

            var jarPath = Directory.GetFiles(server, "*.jar").FirstOrDefault();

            if (jarPath == null)
            {
                Console.WriteLine(
                    "No server .jar file could be found for this server. Try installing a new one from papermc.io or creating a new server.");
                return;
            }

            if (OperatingSystem.IsWindows())
            {
                var batchFilePath = Path.Combine(server, "start.bat");

                if (!System.IO.File.Exists(batchFilePath))
                {
                    Console.WriteLine("Creating server startup file...");

                    // 1. Create the .bat file
                    var batchScript = $"""
                                       @echo off
                                       cd /d "{server}"
                                       java -Xmx4G -Xms4G -jar "{jarPath}" nogui
                                       pause
                                       """;

                    System.IO.File.WriteAllText(batchFilePath, batchScript);
                }


                // 2. Start it in a new window using cmd
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/C start \"Minecraft Server\" \"{batchFilePath}\"",
                    UseShellExecute = true, // Required to open in a new window
                });

                Console.WriteLine(
                    "Server started in a new Command Prompt window. Use \"stop\" in that window to shut it down safely.");
            }
            else if (OperatingSystem.IsMacOS())
            {
                // 1. Create the start.command script
                var commandScriptPath = Path.Combine(server, "start.command");
                if (!System.IO.File.Exists(commandScriptPath))
                {
                    Console.WriteLine("Creating server startup file...");

                    var bashScript = $"""
                                      #!/bin/bash
                                      cd "{server}"
                                      java -Xmx4G -Xms4G -jar "{jarPath}" nogui
                                      """;

                    System.IO.File.WriteAllText(commandScriptPath, bashScript);

                    // 2. Make it executable
                    Process chmod = new()
                    {
                        StartInfo = new()
                        {
                            FileName = "chmod",
                            Arguments = $"+x \"{commandScriptPath}\"",
                            UseShellExecute = false,
                        },
                    };
                    chmod.Start();
                    chmod.WaitForExit();
                }

                // 3. Launch it using `open`, which opens the Terminal and runs it
                Process.Start(new ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = $"\"{commandScriptPath}\"",
                    UseShellExecute = false,
                });
            }
            else if (OperatingSystem.IsLinux())
            {
                var scriptPath = Path.Combine(server, "start.sh");

                if (!System.IO.File.Exists((scriptPath)))
                {
                    Console.WriteLine("Creating server startup file...");

                    // 1. Create the .sh file
                    var bashScript = $"""
                                      #!/bin/bash
                                      cd "{server}"
                                      java -Xmx4G -Xms4G -jar "{jarPath}" nogui
                                      """;
                    System.IO.File.WriteAllText(scriptPath, bashScript);

                    // 2. Make it executable
                    Process chmod = new()
                    {
                        StartInfo = new()
                        {
                            FileName = "chmod",
                            Arguments = $"+x \"{scriptPath}\"",
                            UseShellExecute = false,
                        },
                    };
                    chmod.Start();
                    chmod.WaitForExit();
                }

                // 3. Launch in a new terminal window (using gnome-terminal)
                Process.Start(new ProcessStartInfo
                {
                    FileName = "gnome-terminal",
                    Arguments = $"-- bash -c '\"{scriptPath}\"; exec bash'",
                    UseShellExecute = false,
                });
            }
            else
            {
                Console.WriteLine("Unsupported OS.");
                return;
            }

            Console.WriteLine(
                "Server console started in new window. Type \"stop\" in the console to save and close it. Just pressing the quit button in the corner will not save your world. Returning to menu...");
        }

        /// <summary>
        /// Imports a Minecraft world into the specified server directory.
        /// Provides options to import the world either by selecting from existing Minecraft saves
        /// or by specifying a folder path manually. Ensures that any existing world data in the
        /// server directory is not overwritten without user consent.
        /// </summary>
        /// <param name="server">
        /// The path to the server directory where the world will be imported.
        /// </param>
        private static void ImportWorld(string server)
        {
            var worldPath = Path.Combine(server, "world");

            if (Directory.Exists(worldPath) && Directory.GetFiles(worldPath).Length > 0)
            {
                Console.WriteLine(
                    "Your server already has a world. Please create a new server or manually delete your current server's world folder(s).");
                return;
            }

            string?[] options1 =
            [
                "back",
                "from minecraft",
                "from folder path",
            ];

            var response1 = AskForOptions(options1, "How to import: ");

            switch (response1)
            {
                case "back":
                    return;
                case "from minecraft":
                {
                    var minecraftPath = GetMinecraftPath();
                    var savesPath = Path.Combine(minecraftPath, "saves");
                    var saves = Directory.GetDirectories(savesPath);

                    string?[] options2 = [.. saves.Select(Path.GetFileName)];

                    var response2 = AskForOptions(options2);

                    var selectedSavePath = saves[Array.IndexOf(options2, response2)];

                    CopyDirectory(selectedSavePath, worldPath);
                    break;
                }
                case "from folder path":
                {
                    Console.Write("Enter path to world folder: ");
                    var path = Console.ReadLine() ?? string.Empty;

                    if (!Directory.Exists(path))
                    {
                        Console.WriteLine($"No directory could be found at path: {path}.");
                        return;
                    }

                    CopyDirectory(path, worldPath);
                    break;
                }
            }
        }

        /// <summary>
        /// Manages plugin-related operations for a given Minecraft server.
        /// Allows users to install new plugins, list existing ones, or open the plugin directory.
        /// Creates the plugins directory if it does not already exist.
        /// </summary>
        /// <param name="server">
        /// The file path to the server directory where the plugins directory is located or will be created.
        /// </param>
        private static void ServerPlugins(string server)
        {
            var pluginsDir = Path.Combine(server, "plugins");

            if (!Directory.Exists(pluginsDir)) Directory.CreateDirectory(pluginsDir);

            while (true)
            {
                string?[] options =
                [
                    "back",
                    "install",
                    "list",
                    "folder",
                ];

                var response = AskForOptions(options);

                if (response == "back")
                {
                    return;
                }

                if (response == "install")
                {
                    InstallPlugins(server);
                }
                else if (response == "list")
                {
                    var plugins = Directory.GetFiles(pluginsDir, "*.jar");

                    if (plugins.Length == 0)
                    {
                        Console.WriteLine("No plugins installed!");
                    }

                    foreach (var plugin in plugins)
                    {
                        Console.WriteLine(Path.GetFileNameWithoutExtension(plugin));
                    }
                }
                else if (response == "folder")
                {
                    OpenFolder(pluginsDir);
                }
            }
        }

        /// <summary>
        /// Installs plugins for a server by searching through Modrinth's plugin repository,
        /// allowing the user to choose and download plugins and their dependencies.
        /// </summary>
        /// <param name="server">The path to the server directory where plugins should be installed.</param>
        private static void InstallPlugins(string server)
        {
            try
            {
                var infoPath = Path.Combine(server, InfoFileName);
                var jsonInfo = System.IO.File.ReadAllText(infoPath);
                var info = JsonSerializer.Deserialize<ServerInfo>(jsonInfo) ??
                           throw new("Server info file could not be deserialized.");

                var version = info.Version;
                var loader = info.Loader;

                var modrinth = new ModrinthClient();

                var facets = new FacetCollection
                {
                    { Facet.ProjectType(Modrinth.Models.Enums.Project.ProjectType.Plugin) },
                    { Facet.Version(version ?? throw new InvalidOperationException()) },
                    { Facet.Category(loader) },
                };

                string? query = null;

                SearchResult[] searched;

                while (true)
                {
                    if (query != null) Console.WriteLine($"Results for \"{query}\":{Environment.NewLine}");

                    var searchResult = modrinth.Project.SearchAsync(query ?? string.Empty, facets: facets, limit: 30)
                        .Result;

                    foreach (var proj in searchResult.Hits)
                    {
                        Console.WriteLine(proj.Title);
                    }

                    Console.Write(
                        "Type to search (\"exit\" to break, \"next\" to be able to choose a plugin from the current list): ");
                    query = Console.ReadLine() ?? string.Empty;

                    if (query == "exit") return;

                    if (query == "next")
                    {
                        searched = searchResult.Hits;
                        break;
                    }
                }

                List<string?> options =
                [
                    .. searched.Select(s => s.Title ?? "Failed to load title"),
                    "cancel",
                ];

                var response = AskForOptions([.. options], "Choose a plugin to download (by name): ");
                if (response.Equals("cancel", StringComparison.InvariantCultureIgnoreCase)) return;

                HashSet<string> downloaded = [];
                var plugin = searched[options.IndexOf(response)];

                var pluginsDir = Path.Combine(server, "plugins");
                DownloadPluginAndDependenciesRecursive(
                    plugin.Slug ?? throw new("Failed to load project slug."), version, loader, downloaded,
                    pluginsDir);

                Console.WriteLine(Environment.NewLine +
                                  $"{downloaded.Count} plugins have been installed to server \"{Path.GetFileName(server)}\".");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occured: {ex.Message}{Environment.NewLine}Returning...");
            }
        }

        /// <summary>
        /// Downloads a plugin and its dependencies recursively from Modrinth, storing them in the specified directory.
        /// Ignores plugins that have already been downloaded to avoid duplicates.
        /// </summary>
        /// <param name="slug">The unique identifier (slug) of the main plugin to download.</param>
        /// <param name="minecraftVersion">The Minecraft version the plugin is targeting.</param>
        /// <param name="loader">The mod loader used to filter compatible plugins and dependencies (e.g., Forge or Fabric).</param>
        /// <param name="downloadedSlugs">A set of slugs representing plugins that have already been downloaded, preventing duplicate downloads.</param>
        /// <param name="pluginsPath">The directory path where the plugin and its dependencies should be downloaded.</param>
        private static void DownloadPluginAndDependenciesRecursive(string slug, string? minecraftVersion, string loader,
            HashSet<string> downloadedSlugs, string pluginsPath)
        {
            if (downloadedSlugs.Contains(slug)) return;

            Console.WriteLine($"Downloading plugin \"{slug}\"...");

            var client = new ModrinthClient();
            var project = client.Project.GetAsync(slug).GetAwaiter().GetResult();
            var versions = client.Version.GetProjectVersionListAsync(slug).GetAwaiter().GetResult();

            var matchingVersion = versions.FirstOrDefault(v =>
                v.GameVersions.Contains(minecraftVersion, StringComparer.OrdinalIgnoreCase) &&
                v.Loaders.Contains(loader, StringComparer.OrdinalIgnoreCase));

            downloadedSlugs.Add(project.Slug);

            if (matchingVersion == null)
            {
                Console.WriteLine(
                    $"No matching versions could be found for plugin \"{project.Slug}\". Skipping download.");

                return;
            }

            var file = matchingVersion.Files.FirstOrDefault(f => f.Url.EndsWith(".jar"));

            if (file != null)
            {
                var fileName = Path.GetFileName(file.Url);

                if (!Directory.Exists(pluginsPath)) Directory.CreateDirectory(pluginsPath);

                var destination = Path.Combine(pluginsPath, fileName);

                if (!System.IO.File.Exists(destination))
                {
                    using var httpClient = new HttpClient();
                    var pluginBytes = httpClient.GetByteArrayAsync(file.Url).GetAwaiter().GetResult();
                    System.IO.File.WriteAllBytes(destination, pluginBytes);

                    Console.WriteLine($"Successfully installed plugin \"{slug}\".");
                }
                else
                {
                    Console.WriteLine(
                        $"Skipped downloading plugin \"{slug}\" because it is already downloaded.");
                }
            }

            if (matchingVersion.Dependencies != null)
            {
                foreach (var dep in matchingVersion.Dependencies.Where(d => d.ProjectId is not null))
                {
                    var depProject = client.Project.GetAsync(dep.ProjectId!).GetAwaiter().GetResult();
                    DownloadPluginAndDependenciesRecursive(depProject.Slug, minecraftVersion, loader, downloadedSlugs,
                        pluginsPath);
                }
            }
        }

        /// <summary>
        /// Performs the initial setup process, prompting the user for the path where server folders should be stored.
        /// Ensures that the provided directory exists and creates it if it does not.
        /// Saves the provided path into a settings file in JSON format for future use.
        /// </summary>
        /// <param name="settingsFilePath">
        /// The file path where the settings data will be saved.
        /// </param>
        private static void FirstSetup(string settingsFilePath)
        {
            string? path = null;

            while (string.IsNullOrWhiteSpace(path))
            {
                Console.Write("Path to server folders: ");
                path = Console.ReadLine() ?? string.Empty;
            }

            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            _serversPath = path;

            var settings = new UserSettings
            {
                ServersPath = _serversPath,
            };
            var jsonSettings = JsonSerializer.Serialize(settings);

            System.IO.File.WriteAllText(settingsFilePath, jsonSettings);
        }

        private static string AskForOptions(string?[] options, string question = "Type an option name: ")
        {
            Console.WriteLine(Environment.NewLine + "Choose an option:");

            foreach (var option in options)
            {
                Console.WriteLine("- " + option);
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

        /// <summary>
        /// Opens the specified folder in the default file explorer application
        /// based on the current operating system.
        /// For Windows, the file explorer is opened with the specified path.
        /// For macOS, the path is opened using the "open" command.
        /// For Linux, the path is opened using the "xdg-open" command.
        /// If the operating system is unsupported, an error message is displayed.
        /// </summary>
        /// <param name="path">
        /// The file path of the folder to be opened in the file explorer.
        /// </param>
        private static void OpenFolder(string path)
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

        /// <summary>
        /// Opens a text file located at the specified path using the default text editor
        /// for the current operating system (e.g., Notepad for Windows, TextEdit for macOS).
        /// </summary>
        /// <param name="path">The full file path of the text file to be opened.</param>
        /// <remarks>
        /// This method determines the operating system at runtime and attempts to open the
        /// file with a text editor suitable for the platform. If the platform is unsupported,
        /// a message is logged to the console.
        /// </remarks>
        private static void OpenTextFile(string path)
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
                Process.Start("gedit", [path]);
            }
            else
            {
                Console.WriteLine("Unsupported OS.");
            }
        }

        /// <summary>
        /// Copies all files and subdirectories from a source directory to a destination directory.
        /// Ensures the destination directory is created if it does not exist.
        /// Throws exceptions if the source directory does not exist or if the destination directory is not empty.
        /// Preserves the directory structure, copying files and subdirectories recursively.
        /// </summary>
        /// <param name="p1">
        /// The path to the source directory to be copied.
        /// </param>
        /// <param name="p2">
        /// The path to the destination directory where the source directory's contents will be copied.
        /// </param>
        /// <exception cref="DirectoryNotFoundException">
        /// Thrown if the source directory does not exist.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the destination directory contains files.
        /// </exception>
        public static void CopyDirectory(string p1, string p2)
        {
            if (!Directory.Exists(p1)) throw new("Directory not found: " + p1 + ".");
            if (!Directory.Exists(p2)) Directory.CreateDirectory(p2);

            if (Directory.GetFiles(p2).Length > 0)
                throw new("There are already files present in directory: " + p1 + ".");

            var files = Directory.GetFiles(p1);
            var directories = Directory.GetDirectories(p1);

            foreach (var file in files)
            {
                var newPath = Path.Combine(p2, Path.GetFileName(file));

                System.IO.File.Copy(file, newPath);
            }

            foreach (var directory in directories)
            {
                var newPath = Path.Combine(p2, Path.GetFileName(directory));

                CopyDirectory(directory, newPath);
            }
        }
    }

    /// <summary>
    /// Represents information related to a Paper project, including its available versions.
    /// </summary>
    public class PaperProjectInfo
    {
        /// <summary>
        /// Gets or sets the collection of available versions for the Paper project.
        /// </summary>
        public List<string> Versions { get; set; } = [];
    }

    /// <summary>
    /// Represents information about builds, including a collection of build identifiers.
    /// </summary>
    public class BuildInfo
    {
        /// <summary>
        /// Gets or sets the collection of build identifiers associated with the Paper project.
        /// </summary>
        public List<int> Builds { get; set; } = [];
    }
}
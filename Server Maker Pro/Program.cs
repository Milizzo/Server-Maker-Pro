using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Server_Maker_Pro
{
    static class Program
    {
        static string serversPath = string.Empty;

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

            string fullPath = Path.Combine(serversPath, name);
            Directory.CreateDirectory(fullPath);

            string propertiesFilePath = Path.Combine(fullPath, "server.properties");
            File.WriteAllText(propertiesFilePath, File.ReadAllText("default_properties.txt"));

            ServerMenu(fullPath);
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
                else if (response == "folder")
                {
                    OpenFolder(server);
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
                Process.Start("open", new[] { "-e", path });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Try to use a common text editor (adjust if needed)
                Process.Start("gedit", new[] { path }); // Or "nano", "kate", "mousepad", etc.
            }
            else
            {
                Console.WriteLine("Unsupported OS.");
            }
        }
    }
}

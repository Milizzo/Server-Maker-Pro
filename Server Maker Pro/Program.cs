using System;
using System.IO;

namespace Server_Maker_Pro
{
    static class Program
    {
        static string serversPath = string.Empty;

        static void Main()
        {
            // FirstSetup();
            serversPath = "/Users/milol/Documents/";

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
                    Console.WriteLine("Nice try!");
                }
                else if (response == "exit")
                {
                    Console.WriteLine("Goodbye!");
                    break;
                }
            }
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

            if (!Directory.Exists(Path.Combine(serversPath, response)))
            {
                System.Console.WriteLine($"Server could not be found: {response}");
                return;
            }

            ServerMenu(response);
        }

        static void ServerMenu(string server)
        {
            
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

        static string AskForOptions(string[] options, string question = "Choose an option: ")
        {
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
            return currentResponse;
        }
    }
}

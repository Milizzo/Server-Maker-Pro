using System;
using System.IO;

namespace Server_Maker_Pro
{
    static class Program
    {
        static void Main()
        {
            FirstSetup();

            
        }

        static void FirstSetup()
        {
            string? path = null;

            while (path == null || !Directory.Exists(path))
            {
                Console.WriteLine("Path to server files: ");
                path = Console.ReadLine() ?? string.Empty;
            }
        }
    }
}

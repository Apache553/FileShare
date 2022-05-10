using System;
using System.Text;
using System.Threading.Tasks;
using FileShareLibrary;

namespace ConsoleApp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("invalid parameters.");
                return;
            }

            try
            {
                Console.WriteLine($"Try listening on {args[0]}:{args[1]}.");
                Server server = new Server();
                if (args.Length > 2)
                {
                    server.StartDirectory = args[2];
                    Console.WriteLine($"Set start directory: {server.StartDirectory}");
                }

                server.StartServer(args[0], int.Parse(args[1]));
                Console.CancelKeyPress += delegate { server.StopServer(); };
                Console.WriteLine("Press Ctrl+C to stop server.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
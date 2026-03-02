using MCPSharp.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace MCPSharp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Configure console for STDIO binary protocol
            Console.InputEncoding = System.Text.Encoding.UTF8;
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            var builder = Host.CreateApplicationBuilder(args);

            // Add logging to stderr (never stdout - reserved for MCP protocol)
            builder.Logging.AddConsole(options =>
            {
                options.LogToStandardErrorThreshold = LogLevel.Debug;
            });

            // Configure RootDirectory from args or environment
            var rootDirectory = GetRootDirectory(args);

            builder.Services.AddClassExplorerTools(options =>
            {
                options.RootDirectory = rootDirectory;
            });

            var host = builder.Build();

            // Log startup info to stderr
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("MCPSharp Server starting...");
            logger.LogInformation("Root Directory: {RootDirectory}", rootDirectory);

            await host.RunAsync();
        }

        /// <summary>
        /// Determines root directory from command line args or defaults to current directory
        /// </summary>
        private static string GetRootDirectory(string[] args)
        {
            // Check command line args: --root-dir "C:\path" or -r "C:\path"
            for (int i = 0; i < args.Length; i++)
            {
                if ((args[i] == "--root-dir" || args[i] == "-r") && i + 1 < args.Length)
                {
                    var dir = args[i + 1];
                    if (Directory.Exists(dir))
                        return Path.GetFullPath(dir);
                    throw new DirectoryNotFoundException($"Root directory not found: {dir}");
                }
            }

            // Check environment variable
            var envDir = Environment.GetEnvironmentVariable("MCPSHARP_ROOT_DIR");
            if (!string.IsNullOrWhiteSpace(envDir) && Directory.Exists(envDir))
                return Path.GetFullPath(envDir);

            // Default to current directory
            return Path.GetFullPath(Environment.CurrentDirectory);
        }
    }
}
using MCPSharp.Core.Configuration;
using MCPSharp.Core.MCP;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using System.Reflection;

namespace MCPSharp
{
    /// <summary>
    /// Extension methods for adding ClassExplorer MCP services
    /// </summary>
    public static class McpSharpServiceExtensions
    {
        /// <summary>
        /// Adds ClassExplorer tools to the MCP server with configuration
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="configureOptions">Configuration action for options</param>
        public static IServiceCollection AddClassExplorerTools(
            this IServiceCollection services,
            Action<TypeExplorerMcpOptions>? configureOptions = null)
        {
            string version = Assembly.GetEntryAssembly()?
                        .GetName()
                        .Version?
                        .ToString() ?? "1.0.0";

            // Configure options
            services.Configure<TypeExplorerMcpOptions>(options =>
            {
                configureOptions?.Invoke(options);
            });

            // Add MCP Server with STDIO transport
            services
                .AddMcpServer(options =>
                {
                    options.ServerInfo = new ModelContextProtocol.Protocol.Implementation
                    {
                        Name = "MCPSharp - .Net Types Explorer",
                        Version = version
                    };
                })
                .WithStdioServerTransport()
                .WithTools<TypeExplorerTools>();

            return services;
        }
    }
}
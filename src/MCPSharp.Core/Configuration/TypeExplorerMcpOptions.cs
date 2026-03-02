using System;

namespace MCPSharp.Core.Configuration
{
    /// <summary>
    /// Configuration options for ClassExplorer MCP Server
    /// </summary>
    public class TypeExplorerMcpOptions
    {
        /// <summary>
        /// Root directory for resolving relative project paths
        /// </summary>
        public string RootDirectory { get; set; } = Environment.CurrentDirectory;

        /// <summary>
        /// Resolves a project path - combines with RootDirectory if relative
        /// </summary>
        public string ResolveProjectPath(string projectPath)
        {
            if (string.IsNullOrWhiteSpace(projectPath))
                throw new ArgumentException("Project path cannot be null or empty", nameof(projectPath));

            // Check if already absolute
            if (Path.IsPathRooted(projectPath))
                return Path.GetFullPath(projectPath);

            // Combine with root directory
            var combined = Path.Combine(RootDirectory, projectPath);
            return Path.GetFullPath(combined);
        }
    }
}
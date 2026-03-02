using MCPSharp.Core.Configuration;
using MCPSharp.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MCPSharp.Core.MCP
{
    /// <summary>
    /// MCP Server Tools for .NET Code Analysis and Class Exploration
    /// Provides comprehensive type inspection, dependency analysis, and code generation capabilities
    /// </summary>
    [McpServerToolType, Description("Advanced .NET code analysis and class exploration tools for inspecting types, dependencies, and generating source code from assemblies")]
    public class TypeExplorerTools
    {
        private readonly ILogger<TypeExplorerTools> _logger;
        private readonly TypeExplorerMcpOptions _options;
        private readonly Dictionary<string, TypeExplorerService> _serviceCache;
        private readonly object _cacheLock = new object();

        public TypeExplorerTools(ILogger<TypeExplorerTools> logger,
            IOptions<TypeExplorerMcpOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? new TypeExplorerMcpOptions();
            _serviceCache = new Dictionary<string, TypeExplorerService>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Resolves project path using RootDirectory if relative
        /// </summary>
        private string ResolvePath(string projectPath)
        {
            return _options.ResolveProjectPath(projectPath);
        }

        /// <summary>
        /// Gets or creates a cached service instance for a project
        /// </summary>
        private TypeExplorerService GetOrCreateService(string projectPath)
        {
            // Resolve to absolute path first
            var absolutePath = ResolvePath(projectPath);

            lock (_cacheLock)
            {
                if (_serviceCache.TryGetValue(absolutePath, out var cachedService))
                {
                    _logger.LogInformation("Using cached service for project: {ProjectPath}", absolutePath);
                    return cachedService;
                }

                if (!File.Exists(absolutePath))
                {
                    throw new FileNotFoundException($"Project file not found: {absolutePath} (resolved from: {projectPath})");
                }

                var service = new TypeExplorerService();
                service.LoadProject(absolutePath);
                _serviceCache[absolutePath] = service;

                _logger.LogInformation("Created and cached new service for project: {ProjectPath}", absolutePath);
                return service;
            }
        }

        /// <summary>
        /// Clears the service cache for a specific project or all projects
        /// </summary>
        [McpServerTool(Name = "class_explorer_clear_cache"),
         Description("Clears the cached analysis data for a specific project or all projects. Use this when project files have changed.")]
        public Task<CallToolResult> ClearCache(
            [Description("Path to the project file (.csproj or .sln) to clear from cache, or 'all' to clear everything.")] string projectPath,
            CancellationToken cancellationToken = default)
        {
            try
            {
                lock (_cacheLock)
                {
                    if (projectPath.Equals("all", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var service in _serviceCache.Values)
                        {
                            service.Dispose();
                        }
                        _serviceCache.Clear();
                        _logger.LogInformation("Cleared all cached services");
                        return Task.FromResult(new CallToolResult
                        {
                            Content = new List<ContentBlock>()
                            {
                                new TextContentBlock() { Text = "All cached analysis data cleared successfully." }
                            }
                        });
                    }

                    // Resolve path before looking in cache
                    var absolutePath = ResolvePath(projectPath);

                    if (_serviceCache.TryGetValue(absolutePath, out var serviceToRemove))
                    {
                        serviceToRemove.Dispose();
                        _serviceCache.Remove(absolutePath);
                        _logger.LogInformation("Cleared cache for project: {ProjectPath}", absolutePath);

                        return Task.FromResult(new CallToolResult
                        {
                            Content = new List<ContentBlock>
                            {
                                new TextContentBlock { Text = $"Cached data cleared for project: {projectPath} (resolved: {absolutePath})" }
                            }
                        });
                    }

                    return Task.FromResult(new CallToolResult
                    {
                        Content = new List<ContentBlock>
                        {
                            new TextContentBlock { Text = $"No cached data found for: {projectPath} (resolved: {absolutePath})" }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cache for: {ProjectPath}", projectPath);
                return Task.FromResult(CreateErrorResult($"Failed to clear cache for '{projectPath}'.", ex));
            }
        }

        /// <summary>
        /// Searches for classes by name in a .NET project
        /// </summary>
        [McpServerTool(Name = "class_explorer_find"),
         Description(@"Searches for classes, interfaces, enums, or structs by name in a .NET project or solution.
Returns basic information about found types including their full name, namespace, and assembly.
Use this for initial discovery before detailed analysis.")]
        public Task<CallToolResult> FindClass(
            [Description("Absolute or relative path to the .csproj or .sln file to analyze.")] string projectPath,
            [Description("Class name or partial name to search for (case-insensitive).")] string className,
            [Description("Maximum number of results to return (default: 10).")] int maxResults = 10,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Resolve path before checking file existence
                var absolutePath = ResolvePath(projectPath);

                if (!File.Exists(absolutePath))
                {
                    return Task.FromResult(CreateErrorResult($"Project file not found: {absolutePath} (original: {projectPath})"));
                }

                var service = GetOrCreateService(projectPath);
                var results = service.FindClass(className).Take(maxResults).ToList();

                if (!results.Any())
                {
                    return Task.FromResult(new CallToolResult
                    {
                        Content = new List<ContentBlock>
                        {
                            new TextContentBlock { Text = $"No classes found matching '{className}' in {absolutePath}" }
                        }
                    });
                }

                var summary = $"Found {results.Count} type(s) matching '{className}':\n\n";

                for (int i = 0; i < results.Count; i++)
                {
                    var r = results[i];
                    var typeKind = r.IsInterface ? "interface" : r.IsEnum ? "enum" : "class";
                    summary += $"{i + 1}. [{typeKind}] {r.FullName} (Assembly: {r.AssemblyName})\n";
                }

                summary += "\nUse 'class_explorer_analyze' with the full type name for detailed analysis.";

                return Task.FromResult(new CallToolResult
                {
                    Content = new List<ContentBlock>
                    {
                        new TextContentBlock { Text = summary }
                    }
                });
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError(ex, "Project file not found: {ProjectPath}", projectPath);
                return Task.FromResult(CreateErrorResult(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding class {ClassName} in {ProjectPath}", className, projectPath);
                return Task.FromResult(CreateErrorResult($"Failed to find class '{className}' in '{projectPath}'.", ex));
            }
        }

        /// <summary>
        /// Performs detailed analysis of a class with optional dependency exploration
        /// </summary>
        [McpServerTool(Name = "class_explorer_analyze"),
         Description(@"Performs comprehensive analysis of a specific class including its source code representation,
dependencies, inheritance hierarchy, and member signatures. Can optionally include related types.")]
        public Task<CallToolResult> AnalyzeClass(
            [Description("Absolute or relative path to the .csproj or .sln file.")] string projectPath,
            [Description("Full class name (namespace.name) or simple name to analyze.")] string className,
            [Description(@"Analysis depth level:
                - 'none': Class only, no dependencies
                - 'hierarchy': Include base class and interfaces only
                - 'signature': Include parameter types, return types, and property types
                - 'full': Include all dependencies recursively
                Default: 'none'")] string analysisDepth = "none",
            [Description("Maximum recursion depth for dependency analysis (1-10, default: 3). Only used when depth is 'full' or 'signature'.")] int maxDepth = 3,
            [Description("Include System namespace dependencies (System.*, default: false).")] bool includeSystem = false,
            [Description("Include Microsoft namespace dependencies (Microsoft.*, default: false).")] bool includeMicrosoft = false,
            [Description("Include third-party dependencies (Newtonsoft, etc, default: false).")] bool includeThirdParty = false,
            [Description("Comma-separated list of namespace prefixes to exclude (e.g., 'MyApp.Tests,MyApp.Mocks').")] string? excludeNamespaces = null,
            [Description("Comma-separated list of class name patterns to exclude (e.g., 'Dto,Factory,Helper').")] string? excludePatterns = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Resolve path before checking file existence
                var absolutePath = ResolvePath(projectPath);

                if (!File.Exists(absolutePath))
                {
                    return Task.FromResult(CreateErrorResult($"Project file not found: {absolutePath} (original: {projectPath})"));
                }

                var service = GetOrCreateService(projectPath);

                // Configure filters
                var filters = new FilterConfiguration
                {
                    IncludeSystemDependencies = includeSystem,
                    IncludeMicrosoftDependencies = includeMicrosoft,
                    IncludeThirdPartyDependencies = includeThirdParty
                };

                if (!string.IsNullOrWhiteSpace(excludeNamespaces))
                {
                    filters.ExcludedNamespaces = excludeNamespaces.Split(',').Select(s => s.Trim()).ToList();
                }

                if (!string.IsNullOrWhiteSpace(excludePatterns))
                {
                    filters.ExcludedClassPatterns = excludePatterns.Split(',').Select(s => s.Trim()).ToList();
                }

                service.SetFilters(filters);

                // Map depth string to options
                var options = analysisDepth.ToLower() switch
                {
                    "hierarchy" => DependencyAnalysisOptions.HierarchyOnly,
                    "signature" => DependencyAnalysisOptions.SignatureOnly,
                    "full" => DependencyAnalysisOptions.All,
                    _ => DependencyAnalysisOptions.None
                };

                if (options == DependencyAnalysisOptions.None)
                {
                    // Simple analysis without dependencies
                    var simpleResults = service.FindClass(className).ToList();
                    if (!simpleResults.Any())
                    {
                        return Task.FromResult(new CallToolResult
                        {
                            Content = new List<ContentBlock>
                            {
                                new TextContentBlock { Text = $"Class '{className}' not found in {absolutePath}" }
                            }
                        });
                    }

                    var primary = simpleResults.First();
                    var output = $"// Analysis of: {primary.FullName}\n";
                    output += $"// Assembly: {primary.AssemblyName}\n";
                    output += $"// Type: {(primary.IsInterface ? "interface" : primary.IsEnum ? "enum" : "class")}\n";
                    output += $"// Project: {absolutePath}\n";
                    output += primary.SourceCode;

                    return Task.FromResult(new CallToolResult
                    {
                        Content = new List<ContentBlock>
                        {
                            new TextContentBlock { Text = output }
                        }
                    });
                }
                else
                {
                    // Full analysis with dependencies
                    var result = service.FindClassWithDependencies(className, options, maxDepth, filters);

                    if (result == null)
                    {
                        return Task.FromResult(new CallToolResult
                        {
                            Content = new List<ContentBlock>
                            {
                                new TextContentBlock { Text = $"Class '{className}' not found in {absolutePath}" }
                            }
                        });
                    }

                    var output = result.GenerateFullSourceCode(true, filters);

                    // Add metadata header
                    var header = $"// Class Analysis Report\n";
                    header += $"// Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n";
                    header += $"// Project: {absolutePath}\n";
                    header += $"// Target: {result.PrimaryClass.FullName}\n";
                    header += $"// Dependencies Found: {result.Dependencies.Count}\n";
                    header += $"// Filters: {filters.GetFilterSummary()}\n\n";

                    return Task.FromResult(new CallToolResult
                    {
                        Content = new List<ContentBlock>
                        {
                            new TextContentBlock { Text = header + output }
                        }
                    });
                }
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError(ex, "Project file not found: {ProjectPath}", projectPath);
                return Task.FromResult(CreateErrorResult(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing class {ClassName} in {ProjectPath}", className, projectPath);
                return Task.FromResult(CreateErrorResult($"Failed to analyze class '{className}' in '{projectPath}'.", ex));
            }
        }

        /// <summary>
        /// Gets the inheritance hierarchy of a class
        /// </summary>
        [McpServerTool(Name = "class_explorer_hierarchy"),
         Description(@"Retrieves the complete inheritance hierarchy for a class or interface,
including all base classes and implemented interfaces.")]
        public Task<CallToolResult> GetHierarchy(
            [Description("Absolute or relative path to the .csproj or .sln file.")] string projectPath,
            [Description("Full class name to analyze.")] string className,
            [Description("Include System types in hierarchy (default: false).")] bool includeSystemTypes = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Resolve path before checking file existence
                var absolutePath = ResolvePath(projectPath);

                if (!File.Exists(absolutePath))
                {
                    return Task.FromResult(CreateErrorResult($"Project file not found: {absolutePath} (original: {projectPath})"));
                }

                var service = GetOrCreateService(projectPath);

                var filters = new FilterConfiguration
                {
                    IncludeSystemDependencies = includeSystemTypes,
                    IncludeMicrosoftDependencies = includeSystemTypes,
                    IncludeThirdPartyDependencies = true
                };
                service.SetFilters(filters);

                var result = service.FindClassWithDependencies(
                    className,
                    DependencyAnalysisOptions.HierarchyOnly,
                    10, // Deep recursion for hierarchy
                    filters);

                if (result == null)
                {
                    return Task.FromResult(new CallToolResult
                    {
                        Content = new List<ContentBlock>
                        {
                            new TextContentBlock { Text = $"Class '{className}' not found in {absolutePath}" }
                        }
                    });
                }

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"// Inheritance Hierarchy for: {result.PrimaryClass.FullName}");
                sb.AppendLine($"// Project: {absolutePath}");
                sb.AppendLine();

                // Build hierarchy tree
                var current = result.PrimaryClass;
                var level = 0;

                sb.AppendLine($"{new string(' ', level * 2)}└── {current.Name} ({(current.IsInterface ? "interface" : "class")})");

                if (!string.IsNullOrEmpty(current.BaseType) && current.BaseType != "System.Object")
                {
                    level++;
                    sb.AppendLine($"{new string(' ', level * 2)}└── extends {current.BaseType}");
                }

                if (current.Interfaces?.Any() == true)
                {
                    foreach (var iface in current.Interfaces)
                    {
                        sb.AppendLine($"{new string(' ', level * 2)}└── implements {iface}");
                    }
                }

                if (result.Dependencies.Any())
                {
                    sb.AppendLine();
                    sb.AppendLine("// Related Types:");
                    foreach (var dep in result.Dependencies.Values.OrderBy(d => d.FullName))
                    {
                        var kind = dep.IsInterface ? "interface" : dep.IsEnum ? "enum" : "class";
                        sb.AppendLine($"//   [{kind}] {dep.FullName}");
                    }
                }

                return Task.FromResult(new CallToolResult
                {
                    Content = new List<ContentBlock>
                    {
                        new TextContentBlock { Text = sb.ToString() }
                    }
                });
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError(ex, "Project file not found: {ProjectPath}", projectPath);
                return Task.FromResult(CreateErrorResult(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting hierarchy for {ClassName}", className);
                return Task.FromResult(CreateErrorResult($"Failed to get hierarchy for '{className}'.", ex));
            }
        }

        /// <summary>
        /// Lists all types in a namespace
        /// </summary>
        [McpServerTool(Name = "class_explorer_list_namespace"),
         Description("Lists all classes, interfaces, enums, and structs within a specific namespace.")]
        public Task<CallToolResult> ListNamespace(
            [Description("Absolute or relative path to the .csproj or .sln file.")] string projectPath,
            [Description("Namespace to list (e.g., 'MyApp.Domain.Models').")] string namespaceName,
            [Description("Include nested namespaces (default: true).")] bool includeNested = true,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Resolve path before checking file existence
                var absolutePath = ResolvePath(projectPath);

                if (!File.Exists(absolutePath))
                {
                    return Task.FromResult(CreateErrorResult($"Project file not found: {absolutePath} (original: {projectPath})"));
                }

                var service = GetOrCreateService(projectPath);

                // Find all classes and filter by namespace
                var allTypes = service.FindClass("").Where(t =>
                    t.Namespace == namespaceName ||
                    (includeNested && t.Namespace?.StartsWith(namespaceName + ".") == true)
                ).ToList();

                if (!allTypes.Any())
                {
                    return Task.FromResult(new CallToolResult
                    {
                        Content = new List<ContentBlock>
                        {
                            new TextContentBlock { Text = $"No types found in namespace '{namespaceName}' in {absolutePath}" }
                        }
                    });
                }

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"// Types in namespace: {namespaceName}");
                sb.AppendLine($"// Project: {absolutePath}");
                sb.AppendLine($"// {(includeNested ? "Including" : "Excluding")} nested namespaces");
                sb.AppendLine($"// Total: {allTypes.Count} type(s)\n");

                var grouped = allTypes.GroupBy(t => t.Namespace).OrderBy(g => g.Key);

                foreach (var group in grouped)
                {
                    sb.AppendLine($"namespace {group.Key}");
                    sb.AppendLine("{");

                    foreach (var type in group.OrderBy(t => t.Name))
                    {
                        var kind = type.IsInterface ? "interface" : type.IsEnum ? "enum" : "class";
                        var modifiers = type.IsPublic ? "public" : "internal";
                        sb.AppendLine($"    {modifiers} {kind} {type.Name};");
                    }

                    sb.AppendLine("}");
                    sb.AppendLine();
                }

                return Task.FromResult(new CallToolResult
                {
                    Content = new List<ContentBlock>
                    {
                        new TextContentBlock { Text = sb.ToString() }
                    }
                });
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError(ex, "Project file not found: {ProjectPath}", projectPath);
                return Task.FromResult(CreateErrorResult(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing namespace {NamespaceName} in {ProjectPath}", namespaceName, projectPath);
                return Task.FromResult(CreateErrorResult($"Failed to list namespace '{namespaceName}'.", ex));
            }
        }

        /// <summary>
        /// Generates a dependency graph for a class
        /// </summary>
        [McpServerTool(Name = "class_explorer_dependencies"),
         Description(@"Generates a detailed dependency report showing all types referenced by a class,
organized by relationship type (inheritance, parameters, properties, etc).")]
        public Task<CallToolResult> GetDependencies(
            [Description("Absolute or relative path to the .csproj or .sln file.")] string projectPath,
            [Description("Full class name to analyze.")] string className,
            [Description("Include System dependencies in report (default: false).")] bool includeSystem = false,
            [Description("Maximum depth for dependency traversal (1-5, default: 2).")] int maxDepth = 2,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Resolve path before checking file existence
                var absolutePath = ResolvePath(projectPath);

                if (!File.Exists(absolutePath))
                {
                    return Task.FromResult(CreateErrorResult($"Project file not found: {absolutePath} (original: {projectPath})"));
                }

                var service = GetOrCreateService(projectPath);

                var filters = new FilterConfiguration
                {
                    IncludeSystemDependencies = includeSystem,
                    IncludeMicrosoftDependencies = includeSystem,
                    IncludeThirdPartyDependencies = true
                };
                service.SetFilters(filters);

                var result = service.FindClassWithDependencies(
                    className,
                    DependencyAnalysisOptions.All,
                    maxDepth,
                    filters);

                if (result == null)
                {
                    return Task.FromResult(new CallToolResult
                    {
                        Content = new List<ContentBlock>
                        {
                            new TextContentBlock { Text = $"Class '{className}' not found in {absolutePath}" }
                        }
                    });
                }

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"// Dependency Report for: {result.PrimaryClass.FullName}");
                sb.AppendLine($"// Project: {absolutePath}");
                sb.AppendLine($"// Analysis Depth: {maxDepth}");
                sb.AppendLine($"// Total Dependencies: {result.Dependencies.Count}");
                sb.AppendLine($"// Filtered Out: {(includeSystem ? "None" : "System types excluded")}");
                sb.AppendLine();

                // Group relationships by kind
                var grouped = result.Relationships.GroupBy(r => r.RelationshipKind).OrderBy(g => g.Key);

                foreach (var group in grouped)
                {
                    sb.AppendLine($"// {group.Key}:");
                    foreach (var rel in group)
                    {
                        var arrow = group.Key.ToString().Contains("Inherits") ? "───►" :
                                   group.Key.ToString().Contains("Implements") ? "────►" : "───►";
                        sb.AppendLine($"//   {rel.SourceType.Split('.').Last()} {arrow} {rel.TargetType}");
                        if (!string.IsNullOrEmpty(rel.MemberName) && rel.MemberName != "unknown")
                        {
                            sb.AppendLine($"//      via: {rel.MemberName}");
                        }
                    }
                    sb.AppendLine();
                }

                // List all dependency types
                if (result.Dependencies.Any())
                {
                    sb.AppendLine("// External Dependencies:");
                    foreach (var dep in result.Dependencies.Values.OrderBy(d => d.FullName))
                    {
                        var kind = dep.IsInterface ? "interface" : dep.IsEnum ? "enum" : "class";
                        sb.AppendLine($"//   [{kind}] {dep.FullName} ({dep.AssemblyName})");
                    }
                }

                return Task.FromResult(new CallToolResult
                {
                    Content = new List<ContentBlock>
                    {
                        new TextContentBlock { Text = sb.ToString() }
                    }
                });
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError(ex, "Project file not found: {ProjectPath}", projectPath);
                return Task.FromResult(CreateErrorResult(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dependencies for {ClassName}", className);
                return Task.FromResult(CreateErrorResult($"Failed to get dependencies for '{className}'.", ex));
            }
        }

        /// <summary>
        /// Exports analysis results to a file
        /// </summary>
        [McpServerTool(Name = "class_explorer_export"),
         Description("Exports the complete analysis of a class to a .cs file in the specified directory.")]
        public Task<CallToolResult> ExportAnalysis(
            [Description("Absolute or relative path to the .csproj or .sln file.")] string projectPath,
            [Description("Full class name to export.")] string className,
            [Description("Directory path for the output file (default: current directory).")] string? outputDirectory = null,
            [Description("Analysis depth: 'none', 'hierarchy', 'signature', or 'full' (default: 'full').")] string analysisDepth = "full",
            [Description("Include all dependencies including System types (default: false).")] bool includeAllDependencies = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Resolve path before checking file existence
                var absolutePath = ResolvePath(projectPath);

                if (!File.Exists(absolutePath))
                {
                    return Task.FromResult(CreateErrorResult($"Project file not found: {absolutePath} (original: {projectPath})"));
                }

                var service = GetOrCreateService(projectPath);

                var filters = new FilterConfiguration
                {
                    IncludeSystemDependencies = includeAllDependencies,
                    IncludeMicrosoftDependencies = includeAllDependencies,
                    IncludeThirdPartyDependencies = true
                };
                service.SetFilters(filters);

                var options = analysisDepth.ToLower() switch
                {
                    "hierarchy" => DependencyAnalysisOptions.HierarchyOnly,
                    "signature" => DependencyAnalysisOptions.SignatureOnly,
                    "full" => DependencyAnalysisOptions.All,
                    _ => DependencyAnalysisOptions.None
                };

                var result = service.FindClassWithDependencies(className, options, 5, filters);

                if (result == null)
                {
                    return Task.FromResult(new CallToolResult
                    {
                        Content = new List<ContentBlock>
                        {
                            new TextContentBlock { Text = $"Class '{className}' not found in {absolutePath}" }
                        }
                    });
                }

                var outputDir = string.IsNullOrWhiteSpace(outputDirectory) ? Directory.GetCurrentDirectory() : outputDirectory;
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                var safeFileName = result.PrimaryClass.FullName.Replace('.', '_').Replace('<', '[').Replace('>', ']');
                var filePath = Path.Combine(outputDir, $"{safeFileName}_Analysis.cs");

                var content = result.GenerateFullSourceCode(true, filters);
                File.WriteAllText(filePath, content);

                var successMessage = $"✅ Analysis exported successfully:\n" +
                    $"   Source Project: {absolutePath}\n" +
                    $"   Output File: {filePath}\n" +
                    $"   Size: {new FileInfo(filePath).Length} bytes\n" +
                    $"   Dependencies included: {result.Dependencies.Count}";

                return Task.FromResult(new CallToolResult
                {
                    Content = new List<ContentBlock>
                    {
                        new TextContentBlock { Text = successMessage }
                    }
                });
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError(ex, "Project file not found: {ProjectPath}", projectPath);
                return Task.FromResult(CreateErrorResult(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting analysis for {ClassName}", className);
                return Task.FromResult(CreateErrorResult($"Failed to export analysis for '{className}'.", ex));
            }
        }

        /// <summary>
        /// Helper method to create error results
        /// </summary>
        private static CallToolResult CreateErrorResult(string message, Exception? exception = null)
        {
            var errorContent = message;
            if (exception != null)
            {
                errorContent += $"\nException: {exception.Message}";
                if (exception.InnerException != null)
                {
                    errorContent += $"\nInner: {exception.InnerException.Message}";
                }
            }

            return new CallToolResult
            {
                IsError = true,
                Content = new List<ContentBlock>
                {
                    new TextContentBlock { Text = errorContent }
                }
            };
        }
    }
}
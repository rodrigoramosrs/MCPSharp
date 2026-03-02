using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using MCPSharp.Core.Services;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║    .NET Type Explorer v5.0 - Advanced Filtering        ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        //LlmTornado.Agents.TornadoAgent
        if (args.Length == 0)
            RunInteractiveMode();
        else
            RunBatchMode(args);
    }

    static void RunInteractiveMode()
    {
        using var service = new TypeExplorerService();

        string projectPath;
        do
        {
            Console.Write("Path to .csproj or .sln: ");
            projectPath = Console.ReadLine()?.Trim('"', ' ');

            if (!File.Exists(projectPath))
                Console.WriteLine("❌ File not found!");
        } while (!File.Exists(projectPath));

        try
        {
            Console.WriteLine("\n⏳ Loading assemblies...");
            service.LoadProject(projectPath);
            Console.WriteLine("✅ Project loaded successfully!\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            return;
        }

        // Configure filters
        var filters = ConfigureFilters();
        service.SetFilters(filters);

        while (true)
        {
            Console.Write("\n🔍 Class name (or 'exit'): ");
            var query = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(query) || query.ToLower() == "exit") break;

            Console.WriteLine("\n📋 Analysis options:");
            Console.WriteLine("1. Class only (no dependencies)");
            Console.WriteLine("2. Hierarchy only (inheritance + interfaces)");
            Console.WriteLine("3. Signatures (parameters, returns, properties)");
            Console.WriteLine("4. All dependencies");
            Console.WriteLine("5. Custom...");

            Console.Write("\nChoice (1-5): ");
            var choice = Console.ReadLine();

            DependencyAnalysisOptions options = choice switch
            {
                "1" => DependencyAnalysisOptions.None,
                "2" => DependencyAnalysisOptions.HierarchyOnly,
                "3" => DependencyAnalysisOptions.SignatureOnly,
                "4" => DependencyAnalysisOptions.All,
                "5" => ConfigureCustomOptions(),
                _ => DependencyAnalysisOptions.None
            };

            Console.Write("\nMax depth (1-10, default 3): ");
            var depthInput = Console.ReadLine();
            int maxDepth = int.TryParse(depthInput, out int d) ? d : 3;

            Console.WriteLine($"\n⏳ Analyzing '{query}' with filters: {filters.GetFilterSummary()}...");

            try
            {
                if (options == DependencyAnalysisOptions.None)
                {
                    var results = service.FindClass(query).ToList();
                    DisplaySimpleResults(results);
                }
                else
                {
                    var result = service.FindClassWithDependencies(query, options, maxDepth, filters);
                    DisplayDependencyResults(result, filters);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
            }

            Console.WriteLine("\n⚙️  Options:");
            Console.WriteLine("1. New search");
            Console.WriteLine("2. Change filters");
            Console.WriteLine("3. Exit");
            Console.Write("Choice: ");

            var nextAction = Console.ReadLine();
            if (nextAction == "2")
                filters = ConfigureFilters();
            else if (nextAction == "3")
                break;
        }

        Console.WriteLine("\n👋 Goodbye!");
    }

    static FilterConfiguration ConfigureFilters()
    {
        var filters = new FilterConfiguration();

        Console.WriteLine("\n⚙️  Filter Configuration:");

        Console.Write("Include System dependencies? (y/n, default n): ");
        filters.IncludeSystemDependencies = Console.ReadLine()?.ToLower() == "y";

        Console.Write("Include Microsoft dependencies? (y/n, default n): ");
        filters.IncludeMicrosoftDependencies = Console.ReadLine()?.ToLower() == "y";

        Console.Write("Include Third-Party dependencies (Newtonsoft, etc)? (y/n, default n): ");
        filters.IncludeThirdPartyDependencies = Console.ReadLine()?.ToLower() == "y";

        Console.Write("Exclude specific namespaces (comma-separated, e.g., 'MyApp.Internal,MyApp.Tests'): ");
        var nsExclusions = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(nsExclusions))
        {
            filters.ExcludedNamespaces = nsExclusions.Split(',').Select(s => s.Trim()).ToList();
        }

        Console.Write("Exclude class name patterns (comma-separated, e.g., 'Dto,Factory'): ");
        var patterns = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(patterns))
        {
            filters.ExcludedClassPatterns = patterns.Split(',').Select(s => s.Trim()).ToList();
        }

        Console.Write("Always include these namespaces (whitelist, comma-separated): ");
        var inclusions = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(inclusions))
        {
            filters.IncludedNamespaces = inclusions.Split(',').Select(s => s.Trim()).ToList();
        }

        Console.WriteLine($"\n✅ Filters configured: {filters.GetFilterSummary()}");

        return filters;
    }

    static DependencyAnalysisOptions ConfigureCustomOptions()
    {
        var options = DependencyAnalysisOptions.None;

        Console.WriteLine("\n⚙️  Configure options (y/n):");

        Console.Write("Include method parameters? ");
        if (Console.ReadLine()?.ToLower() == "y") options |= DependencyAnalysisOptions.MethodParameters;

        Console.Write("Include method return types? ");
        if (Console.ReadLine()?.ToLower() == "y") options |= DependencyAnalysisOptions.MethodReturnTypes;

        Console.Write("Include property types? ");
        if (Console.ReadLine()?.ToLower() == "y") options |= DependencyAnalysisOptions.PropertyTypes;

        Console.Write("Include field types? ");
        if (Console.ReadLine()?.ToLower() == "y") options |= DependencyAnalysisOptions.FieldTypes;

        Console.Write("Include base type (inheritance)? ");
        if (Console.ReadLine()?.ToLower() == "y") options |= DependencyAnalysisOptions.BaseType;

        Console.Write("Include interfaces? ");
        if (Console.ReadLine()?.ToLower() == "y") options |= DependencyAnalysisOptions.Interfaces;

        return options;
    }

    static void DisplaySimpleResults(List<ClassDefinition> results)
    {
        if (!results.Any())
        {
            Console.WriteLine("❌ No classes found.");
            return;
        }

        Console.WriteLine($"\n✅ {results.Count} class(es) found:");
        for (int i = 0; i < results.Count; i++)
            Console.WriteLine($"{i + 1}. {results[i]}");

        Console.Write("\n📄 Enter number to view code: ");
        if (int.TryParse(Console.ReadLine(), out int idx) && idx > 0 && idx <= results.Count)
        {
            Console.WriteLine($"\n{'═',60}");
            Console.WriteLine(results[idx - 1].SourceCode);
            OfferToSave(results[idx - 1].SourceCode, results[idx - 1].Name);
        }
    }

    static void DisplayDependencyResults(ClassAnalysisResult result, FilterConfiguration filters)
    {
        if (result == null)
        {
            Console.WriteLine("❌ Class not found.");
            return;
        }

        Console.WriteLine($"\n✅ {result.PrimaryClass.FullName}");
        Console.WriteLine($"📦 Dependencies found: {result.Dependencies.Count}");
        Console.WriteLine($"🔗 Relationships mapped: {result.Relationships.Count}");
        Console.WriteLine($"🔍 Active filters: {filters.GetFilterSummary()}");

        if (result.Dependencies.Any())
        {
            Console.WriteLine("\n📋 Dependencies:");
            foreach (var dep in result.Dependencies.Values.OrderBy(d => d.FullName))
                Console.WriteLine($"   • {dep.FullName}");
        }

        if (result.Relationships.Any())
        {
            Console.WriteLine("\n🔗 Relationships:");
            foreach (var rel in result.Relationships)
            {
                Console.WriteLine($"   • {rel.SourceType} -> {rel.TargetType} ({rel.RelationshipKind})");
            }
        }

        Console.WriteLine("\n📄 Options:");
        Console.WriteLine("1. View full code (with dependencies)");
        Console.WriteLine("2. View primary class only");
        Console.WriteLine("3. Save to file");
        Console.WriteLine("4. View filtered out types (excluded by filters)");

        Console.Write("\nChoice: ");
        var choice = Console.ReadLine();

        switch (choice)
        {
            case "1":
                Console.WriteLine($"\n{'═',60}");
                Console.WriteLine(result.GenerateFullSourceCode(true, filters));
                OfferToSave(result.GenerateFullSourceCode(true, filters), result.PrimaryClass.Name + "_Full");
                break;
            case "2":
                Console.WriteLine($"\n{'═',60}");
                Console.WriteLine(result.PrimaryClass.SourceCode);
                OfferToSave(result.PrimaryClass.SourceCode, result.PrimaryClass.Name);
                break;
            case "3":
                var fileName = $"{result.PrimaryClass.Name}_WithDependencies.cs";
                File.WriteAllText(fileName, result.GenerateFullSourceCode(true, filters));
                Console.WriteLine($"✅ Saved to: {Path.GetFullPath(fileName)}");
                break;
            case "4":
                ShowFilteredOutTypes(result.PrimaryClass, filters);
                break;
        }
    }

    static void ShowFilteredOutTypes(ClassDefinition primary, FilterConfiguration filters)
    {
        Console.WriteLine("\n📋 Types filtered out (would be included without filters):");

        var allTypes = new HashSet<string>();
        var lines = primary.SourceCode.Split('\n');

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Contains("public") || trimmed.Contains("private") || trimmed.Contains("protected"))
            {
                var words = trimmed.Split(new[] { ' ', '(', ')', ';', '{', '}', '<', '>', ',' },
                                         StringSplitOptions.RemoveEmptyEntries);
                foreach (var word in words)
                {
                    if (!IsPrimitiveType(word) && word.Length > 2 && char.IsUpper(word[0]))
                    {
                        allTypes.Add(word);
                    }
                }
            }
        }

        var filtered = allTypes.Where(t => filters.ShouldExclude(t)).ToList();

        if (!filtered.Any())
        {
            Console.WriteLine("   (No types were filtered out)");
        }
        else
        {
            foreach (var type in filtered.OrderBy(t => t))
            {
                Console.WriteLine($"   • {type}");
            }
        }
    }

    static bool IsPrimitiveType(string typeName)
    {
        var primitives = new[] { "void", "bool", "byte", "sbyte", "char", "short", "ushort",
                                "int", "uint", "long", "ulong", "float", "double", "decimal",
                                "string", "object", "dynamic", "nint", "nuint", "public",
                                "private", "protected", "internal", "static", "readonly",
                                "abstract", "virtual", "override", "sealed", "const", "class",
                                "interface", "enum", "struct", "namespace", "get", "set" };

        return primitives.Contains(typeName.ToLower());
    }

    static void OfferToSave(string content, string name)
    {
        Console.Write("\n💾 Save to file? (y/n): ");
        if (Console.ReadLine()?.ToLower() == "y")
        {
            var fileName = $"{name}.cs";
            File.WriteAllText(fileName, content);
            Console.WriteLine($"✅ Saved to: {Path.GetFullPath(fileName)}");
        }
    }

    static void RunBatchMode(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: dotnet run <project.csproj> <ClassName> [options] [depth] [filters]");
            Console.WriteLine("Options: none, hierarchy, signature, all");
            Console.WriteLine("Filters: --include-system, --include-microsoft, --include-thirdparty");
            Console.WriteLine("         --exclude-ns:Namespace1,Namespace2");
            Console.WriteLine("         --exclude-pattern:Dto,Factory");
            Console.WriteLine("Example: dotnet run MyApp.csproj UserService all 3 --exclude-ns:Tests,Mocks");
            return;
        }

        var filters = new FilterConfiguration();
        var options = DependencyAnalysisOptions.None;
        var depth = 3;

        // Parse arguments
        for (int i = 2; i < args.Length; i++)
        {
            var arg = args[i].ToLower();

            if (arg == "none") options = DependencyAnalysisOptions.None;
            else if (arg == "hierarchy") options = DependencyAnalysisOptions.HierarchyOnly;
            else if (arg == "signature") options = DependencyAnalysisOptions.SignatureOnly;
            else if (arg == "all") options = DependencyAnalysisOptions.All;
            else if (int.TryParse(arg, out int d)) depth = d;
            else if (arg == "--include-system") filters.IncludeSystemDependencies = true;
            else if (arg == "--include-microsoft") filters.IncludeMicrosoftDependencies = true;
            else if (arg == "--include-thirdparty") filters.IncludeThirdPartyDependencies = true;
            else if (arg.StartsWith("--exclude-ns:"))
                filters.ExcludedNamespaces = arg.Substring(13).Split(',').ToList();
            else if (arg.StartsWith("--exclude-pattern:"))
                filters.ExcludedClassPatterns = arg.Substring(18).Split(',').ToList();
        }

        using var service = new TypeExplorerService();
        service.SetFilters(filters);
        service.LoadProject(args[0]);

        if (options == DependencyAnalysisOptions.None)
        {
            foreach (var result in service.FindClass(args[1]))
            {
                Console.WriteLine($"--- {result.FullName} ---");
                Console.WriteLine(result.SourceCode);
            }
        }
        else
        {
            var result = service.FindClassWithDependencies(args[1], options, depth, filters);
            if (result != null)
                Console.WriteLine(result.GenerateFullSourceCode(true, filters));
        }
    }
}
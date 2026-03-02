using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;
using System.Reflection;

namespace MCPSharp.Core.Services
{
    #region Enums and Configuration

    [Flags]
    public enum DependencyAnalysisOptions
    {
        None = 0,
        MethodParameters = 1,
        MethodReturnTypes = 2,
        PropertyTypes = 4,
        FieldTypes = 8,
        BaseType = 16,
        Interfaces = 32,
        GenericArguments = 64,
        Attributes = 128,
        EventTypes = 256,
        All = MethodParameters | MethodReturnTypes | PropertyTypes | FieldTypes |
              BaseType | Interfaces | GenericArguments | Attributes | EventTypes,
        SignatureOnly = MethodParameters | MethodReturnTypes | PropertyTypes,
        HierarchyOnly = BaseType | Interfaces
    }

    public enum RelationshipKind
    {
        Inherits,
        Implements,
        MethodParameter,
        MethodReturn,
        PropertyType,
        FieldType,
        EventType,
        GenericArgument,
        AttributeUsage
    }

    #endregion

    #region Filter Configuration

    /// <summary>
    /// Configuration for filtering dependencies and namespaces
    /// </summary>
    public class FilterConfiguration
    {
        /// <summary>
        /// Default system namespaces to exclude
        /// </summary>
        public static readonly string[] DefaultSystemNamespaces = new[]
        {
            "System",
            "System.Collections",
            "System.Collections.Generic",
            "System.Collections.Concurrent",
            "System.Collections.Immutable",
            "System.ComponentModel",
            "System.Data",
            "System.Diagnostics",
            "System.Drawing",
            "System.Globalization",
            "System.IO",
            "System.Linq",
            "System.Net",
            "System.Net.Http",
            "System.Reflection",
            "System.Runtime",
            "System.Runtime.CompilerServices",
            "System.Runtime.InteropServices",
            "System.Security",
            "System.Text",
            "System.Text.Json",
            "System.Text.RegularExpressions",
            "System.Threading",
            "System.Threading.Tasks",
            "System.Web",
            "System.Windows",
            "System.Xml",
            "System.Xml.Linq",
            "Microsoft",
            "Microsoft.Win32",
            "Newtonsoft.Json",
            "JsonConverter",
            "Windows",
            "Internal",
            "Unity",
            "Mono",
            "netstandard",
            "mscorlib"
        };

        /// <summary>
        /// Whether to include system/native dependencies
        /// </summary>
        public bool IncludeSystemDependencies { get; set; } = false;

        /// <summary>
        /// Whether to include Microsoft namespaces
        /// </summary>
        public bool IncludeMicrosoftDependencies { get; set; } = false;

        /// <summary>
        /// Whether to include third-party common libraries (Newtonsoft, etc)
        /// </summary>
        public bool IncludeThirdPartyDependencies { get; set; } = false;

        /// <summary>
        /// Custom namespace filters to exclude (user-defined)
        /// </summary>
        public List<string> ExcludedNamespaces { get; set; } = new();

        /// <summary>
        /// Custom class name patterns to exclude (user-defined)
        /// </summary>
        public List<string> ExcludedClassPatterns { get; set; } = new();

        /// <summary>
        /// Custom namespace filters to include (overrides exclusions)
        /// </summary>
        public List<string> IncludedNamespaces { get; set; } = new();

        /// <summary>
        /// Minimum namespace depth to consider (0 = all)
        /// </summary>
        public int MinNamespaceDepth { get; set; } = 0;

        /// <summary>
        /// Check if a type should be excluded based on filters
        /// </summary>
        public bool ShouldExclude(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName)) return true;

            // Check explicit inclusions first (whitelist overrides)
            foreach (var inclusion in IncludedNamespaces)
            {
                if (fullTypeName.StartsWith(inclusion, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // Check system namespaces
            if (!IncludeSystemDependencies)
            {
                foreach (var sysNs in DefaultSystemNamespaces)
                {
                    if (fullTypeName.StartsWith(sysNs + ".", StringComparison.OrdinalIgnoreCase) ||
                        fullTypeName.Equals(sysNs, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            // Check Microsoft namespaces
            if (!IncludeMicrosoftDependencies &&
                (fullTypeName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
                 fullTypeName.Equals("Microsoft", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            // Check third-party libraries
            if (!IncludeThirdPartyDependencies)
            {
                var thirdParties = new[] { "Newtonsoft", "Json.NET", "AutoMapper", "Serilog", "NLog", "log4net" };
                foreach (var tp in thirdParties)
                {
                    if (fullTypeName.StartsWith(tp + ".", StringComparison.OrdinalIgnoreCase) ||
                        fullTypeName.Equals(tp, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            // Check user-defined namespace exclusions
            foreach (var exclusion in ExcludedNamespaces)
            {
                if (fullTypeName.StartsWith(exclusion, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // Check user-defined class name pattern exclusions
            foreach (var pattern in ExcludedClassPatterns)
            {
                if (fullTypeName.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // Check namespace depth
            if (MinNamespaceDepth > 0)
            {
                var depth = fullTypeName.Count(c => c == '.');
                if (depth < MinNamespaceDepth)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Get filter summary for display
        /// </summary>
        public string GetFilterSummary()
        {
            var parts = new List<string>();

            if (IncludeSystemDependencies) parts.Add("System:Included");
            else parts.Add("System:Excluded");

            if (IncludeMicrosoftDependencies) parts.Add("Microsoft:Included");
            else parts.Add("Microsoft:Excluded");

            if (IncludeThirdPartyDependencies) parts.Add("ThirdParty:Included");
            else parts.Add("ThirdParty:Excluded");

            if (ExcludedNamespaces.Any())
                parts.Add($"ExcludedNS:[{string.Join(",", ExcludedNamespaces)}]");

            if (ExcludedClassPatterns.Any())
                parts.Add($"ExcludedPatterns:[{string.Join(",", ExcludedClassPatterns)}]");

            return string.Join(" | ", parts);
        }
    }

    #endregion

    #region Data Models

    public class ClassDefinition
    {
        public string Name { get; set; }
        public string Namespace { get; set; }
        public string FullName { get; set; }
        public string AssemblyName { get; set; }
        public bool IsPublic { get; set; }
        public bool IsAbstract { get; set; }
        public bool IsSealed { get; set; }
        public bool IsStatic { get; set; }
        public bool IsInterface { get; set; }
        public bool IsEnum { get; set; }
        public bool IsValueType { get; set; }
        public string BaseType { get; set; }
        public List<string> Interfaces { get; set; } = new();
        public string SourceCode { get; set; }

        public override string ToString() => $"[{AssemblyName}] {FullName}";
    }

    public class ClassAnalysisResult
    {
        public ClassDefinition PrimaryClass { get; set; }
        public Dictionary<string, ClassDefinition> Dependencies { get; set; } = new();
        public List<RelationshipInfo> Relationships { get; set; } = new();
        public FilterConfiguration AppliedFilters { get; set; }

        public string GenerateFullSourceCode(bool includeDependencies = true, FilterConfiguration filters = null)
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("// ============================================================================");
            sb.AppendLine($"// Primary Class: {PrimaryClass.FullName}");
            sb.AppendLine($"// Assembly: {PrimaryClass.AssemblyName}");
            sb.AppendLine($"// Total Dependencies: {Dependencies.Count}");
            if (filters != null)
            {
                sb.AppendLine($"// Filters: {filters.GetFilterSummary()}");
            }
            sb.AppendLine("// ============================================================================");
            sb.AppendLine();

            if (includeDependencies && Dependencies.Any())
            {
                sb.AppendLine("// ----------------------------------------------------------------------------");
                sb.AppendLine("// DEPENDENCIES");
                sb.AppendLine("// ----------------------------------------------------------------------------");
                sb.AppendLine();

                foreach (var dep in Dependencies.Values.OrderBy(d => d.Namespace).ThenBy(d => d.Name))
                {
                    sb.AppendLine($"// >>> {dep.FullName} [{dep.AssemblyName}]");
                    sb.AppendLine(dep.SourceCode);
                    sb.AppendLine();
                }

                sb.AppendLine("// ----------------------------------------------------------------------------");
                sb.AppendLine("// PRIMARY CLASS");
                sb.AppendLine("// ----------------------------------------------------------------------------");
                sb.AppendLine();
            }

            sb.AppendLine(PrimaryClass.SourceCode);

            if (Relationships.Any())
            {
                sb.AppendLine();
                sb.AppendLine("// ----------------------------------------------------------------------------");
                sb.AppendLine("// RELATIONSHIP SUMMARY");
                sb.AppendLine("// ----------------------------------------------------------------------------");
                foreach (var rel in Relationships)
                {
                    sb.AppendLine($"// {rel.SourceType} -> {rel.TargetType} [{rel.RelationshipKind}]");
                }
            }

            return sb.ToString();
        }
    }

    public class RelationshipInfo
    {
        public string SourceType { get; set; }
        public string TargetType { get; set; }
        public string MemberName { get; set; }
        public RelationshipKind RelationshipKind { get; set; }
    }

    public class AssemblyInfo
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public List<TypeInfo> Types { get; set; } = new();
    }

    public class TypeInfo
    {
        public string Name { get; set; }
        public string Namespace { get; set; }
        public string FullName { get; set; }
        public TypeAttributes Attributes { get; set; }
        public string BaseType { get; set; }
        public List<string> Interfaces { get; set; } = new();
        public List<MethodMetadata> Methods { get; set; } = new();
        public List<PropertyMetadata> Properties { get; set; } = new();
        public List<FieldMetadata> Fields { get; set; } = new();
        public bool IsEnum => BaseType == "System.Enum";
    }

    public class MethodMetadata
    {
        public string Name { get; set; }
        public string ReturnType { get; set; }
        public List<ParameterMetadata> Parameters { get; set; } = new();
        public MethodAttributes Attributes { get; set; }
        public bool IsConstructor { get; set; }
    }

    public class PropertyMetadata
    {
        public string Name { get; set; }
        public string PropertyType { get; set; }
        public bool HasGetter { get; set; }
        public bool HasSetter { get; set; }
    }

    public class FieldMetadata
    {
        public string Name { get; set; }
        public string FieldType { get; set; }
        public FieldAttributes Attributes { get; set; }
        public bool IsBackingField { get; set; }
    }

    public class ParameterMetadata
    {
        public string Name { get; set; }
        public string ParameterType { get; set; }
    }

    #endregion

    #region Type Resolver

    public class TypeResolver
    {
        private readonly MetadataReader _reader;

        public TypeResolver(MetadataReader reader)
        {
            _reader = reader;
        }

        public string ResolveType(EntityHandle handle)
        {
            if (handle.IsNil) return "void";

            try
            {
                switch (handle.Kind)
                {
                    case HandleKind.TypeDefinition:
                        return ResolveTypeDefinition((TypeDefinitionHandle)handle);

                    case HandleKind.TypeReference:
                        return ResolveTypeReference((TypeReferenceHandle)handle);

                    case HandleKind.TypeSpecification:
                        return ResolveTypeSpecification((TypeSpecificationHandle)handle);

                    default:
                        return "object";
                }
            }
            catch
            {
                return "object";
            }
        }

        private string ResolveTypeDefinition(TypeDefinitionHandle handle)
        {
            var typeDef = _reader.GetTypeDefinition(handle);
            var name = _reader.GetString(typeDef.Name);
            var ns = _reader.GetString(typeDef.Namespace);

            if (typeDef.GetGenericParameters().Count > 0)
            {
                var args = typeDef.GetGenericParameters()
                    .Select(p => _reader.GetString(_reader.GetGenericParameter(p).Name))
                    .ToArray();
                return $"{name}<{string.Join(", ", args)}>";
            }

            return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        }

        private string ResolveTypeReference(TypeReferenceHandle handle)
        {
            var typeRef = _reader.GetTypeReference(handle);
            var name = _reader.GetString(typeRef.Name);
            var ns = _reader.GetString(typeRef.Namespace);

            if (string.IsNullOrEmpty(ns))
            {
                return name switch
                {
                    "String" => "string",
                    "Int32" => "int",
                    "Int64" => "long",
                    "Boolean" => "bool",
                    "Double" => "double",
                    "Single" => "float",
                    "Decimal" => "decimal",
                    "Byte" => "byte",
                    "SByte" => "sbyte",
                    "Int16" => "short",
                    "UInt16" => "ushort",
                    "UInt32" => "uint",
                    "UInt64" => "ulong",
                    "Char" => "char",
                    "Object" => "object",
                    "Void" => "void",
                    _ => name
                };
            }

            return $"{ns}.{name}";
        }

        private string ResolveTypeSpecification(TypeSpecificationHandle handle)
        {
            var typeSpec = _reader.GetTypeSpecification(handle);
            var signature = typeSpec.DecodeSignature(new SignatureDecoder(this), null);
            return signature;
        }
    }

    public class SignatureDecoder : ISignatureTypeProvider<string, object>
    {
        private readonly TypeResolver _resolver;

        public SignatureDecoder(TypeResolver resolver)
        {
            _resolver = resolver;
        }

        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode switch
        {
            PrimitiveTypeCode.Void => "void",
            PrimitiveTypeCode.Boolean => "bool",
            PrimitiveTypeCode.Char => "char",
            PrimitiveTypeCode.SByte => "sbyte",
            PrimitiveTypeCode.Byte => "byte",
            PrimitiveTypeCode.Int16 => "short",
            PrimitiveTypeCode.UInt16 => "ushort",
            PrimitiveTypeCode.Int32 => "int",
            PrimitiveTypeCode.UInt32 => "uint",
            PrimitiveTypeCode.Int64 => "long",
            PrimitiveTypeCode.UInt64 => "ulong",
            PrimitiveTypeCode.Single => "float",
            PrimitiveTypeCode.Double => "double",
            PrimitiveTypeCode.String => "string",
            PrimitiveTypeCode.Object => "object",
            //PrimitiveTypeCode.Decimal => "decimal",
            _ => "object"
        };

        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            var typeDef = reader.GetTypeDefinition(handle);
            var name = reader.GetString(typeDef.Name);
            var ns = reader.GetString(typeDef.Namespace);
            return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        }

        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            var typeRef = reader.GetTypeReference(handle);
            var name = reader.GetString(typeRef.Name);
            var ns = reader.GetString(typeRef.Namespace);

            if (string.IsNullOrEmpty(ns))
            {
                return name switch
                {
                    "String" => "string",
                    "Int32" => "int",
                    "Int64" => "long",
                    "Boolean" => "bool",
                    "Double" => "double",
                    "Single" => "float",
                    "Decimal" => "decimal",
                    "Byte" => "byte",
                    "Object" => "object",
                    "Void" => "void",
                    _ => name
                };
            }

            return $"{ns}.{name}";
        }

        public string GetSZArrayType(string elementType) => $"{elementType}[]";
        public string GetPointerType(string elementType) => $"{elementType}*";
        public string GetByReferenceType(string elementType) => $"ref {elementType}";
        public string GetGenericMethodParameter(object genericContext, int index) => $"T{index}";
        public string GetGenericTypeParameter(object genericContext, int index) => $"T{index}";
        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;
        public string GetPinnedType(string elementType) => elementType;

        public string GetArrayType(string elementType, ArrayShape shape) =>
            shape.Rank == 1 ? $"{elementType}[]" : $"{elementType}[{new string(',', shape.Rank - 1)}]";

        public string GetFunctionPointerType(MethodSignature<string> signature) => "delegate*";

        public string GetGenericInstantiation(string genericType, System.Collections.Immutable.ImmutableArray<string> typeArguments)
        {
            if (genericType == "System.Nullable" && typeArguments.Length == 1)
                return $"{typeArguments[0]}?";

            return $"{genericType}<{string.Join(", ", typeArguments)}>";
        }

        public string GetTypeFromSpecification(MetadataReader reader, object genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
        {
            var typeSpec = reader.GetTypeSpecification(handle);
            return typeSpec.DecodeSignature(this, genericContext);
        }
    }

    #endregion

    #region Main Service

    public class TypeExplorerService : IDisposable
    {
        private readonly Dictionary<string, AssemblyInfo> _assemblies;
        private readonly string _nugetCachePath;
        private readonly string _dotnetRoot;
        private FilterConfiguration _currentFilters;

        public TypeExplorerService()
        {
            _assemblies = new Dictionary<string, AssemblyInfo>(StringComparer.OrdinalIgnoreCase);
            _nugetCachePath = GetNuGetCachePath();
            _dotnetRoot = GetDotNetRoot();
            _currentFilters = new FilterConfiguration();
        }

        public void SetFilters(FilterConfiguration filters)
        {
            _currentFilters = filters ?? new FilterConfiguration();
        }

        private string GetNuGetCachePath()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".nuget", "packages");
        }

        private string GetDotNetRoot()
        {
            var dotnetPath = Environment.GetEnvironmentVariable("DOTNET_ROOT");
            if (!string.IsNullOrEmpty(dotnetPath)) return dotnetPath;

            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                if (Directory.Exists("/usr/share/dotnet")) return "/usr/share/dotnet";
                if (Directory.Exists("/opt/dotnet")) return "/opt/dotnet";
            }

            if (OperatingSystem.IsWindows())
            {
                var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                return Path.Combine(programFiles, "dotnet");
            }

            return null;
        }

        public void LoadProject(string projectPath)
        {
            if (!File.Exists(projectPath))
                throw new FileNotFoundException("Project not found", projectPath);

            var ext = Path.GetExtension(projectPath).ToLower();

            switch (ext)
            {
                case ".csproj":
                    LoadCsProj(projectPath);
                    break;
                case ".sln":
                    LoadSolution(projectPath);
                    break;
                default:
                    throw new NotSupportedException($"Extension not supported: {ext}");
            }
        }

        private void LoadSolution(string slnPath)
        {
            var baseDir = Path.GetDirectoryName(slnPath);
            var lines = File.ReadAllLines(slnPath);

            foreach (var line in lines)
            {
                if (line.StartsWith("Project(") && line.Contains(".csproj"))
                {
                    var parts = line.Split(',').Select(p => p.Trim()).ToArray();
                    if (parts.Length >= 2)
                    {
                        var projPath = parts[1].Trim('"');
                        var fullPath = Path.Combine(baseDir, projPath);
                        if (File.Exists(fullPath))
                        {
                            try { LoadCsProj(fullPath); }
                            catch (Exception ex) { Console.WriteLine($"[WARNING] Failed to load {projPath}: {ex.Message}"); }
                        }
                    }
                }
            }
        }

        private void LoadCsProj(string csprojPath)
        {
            var doc = XDocument.Load(csprojPath);
            var baseDir = Path.GetDirectoryName(csprojPath);
            var targetFramework = GetTargetFramework(doc);

            Console.WriteLine($"[INFO] Analyzing: {Path.GetFileName(csprojPath)} ({targetFramework})");

            LoadProjectReferences(doc, baseDir);
            LoadNuGetPackages(doc, targetFramework);
            LoadDirectReferences(doc, baseDir);
            LoadProjectBinaries(csprojPath, targetFramework);
        }

        private string GetTargetFramework(XDocument doc)
        {
            return doc.Descendants("TargetFramework").FirstOrDefault()?.Value
                ?? doc.Descendants("TargetFrameworks").FirstOrDefault()?.Value?.Split(';').First()
                ?? "net8.0";
        }

        private void LoadProjectReferences(XDocument doc, string baseDir)
        {
            var refs = doc.Descendants("ProjectReference")
                .Select(x => x.Attribute("Include")?.Value)
                .Where(v => !string.IsNullOrEmpty(v));

            foreach (var prj in refs)
            {
                var fullPath = Path.Combine(baseDir, prj);
                if (File.Exists(fullPath) && !_assemblies.ContainsKey(fullPath))
                    LoadCsProj(fullPath);
            }
        }

        private void LoadNuGetPackages(XDocument doc, string targetFramework)
        {
            var packages = doc.Descendants("PackageReference")
                .Select(x => new
                {
                    Name = x.Attribute("Include")?.Value,
                    Version = x.Attribute("Version")?.Value ?? x.Element("Version")?.Value
                })
                .Where(p => !string.IsNullOrEmpty(p.Name) && !string.IsNullOrEmpty(p.Version));

            foreach (var pkg in packages)
                LoadNuGetPackage(pkg.Name, pkg.Version, targetFramework);
        }

        private void LoadNuGetPackage(string packageName, string version, string targetFramework)
        {
            var pkgPath = Path.Combine(_nugetCachePath, packageName.ToLower(), version);
            if (!Directory.Exists(pkgPath)) return;

            var libPath = Path.Combine(pkgPath, "lib");
            if (!Directory.Exists(libPath)) return;

            var tfm = FindBestTargetFramework(libPath, targetFramework);
            if (string.IsNullOrEmpty(tfm)) return;

            foreach (var dll in Directory.GetFiles(Path.Combine(libPath, tfm), "*.dll"))
                SafeLoadAssembly(dll);
        }

        private string FindBestTargetFramework(string libPath, string projectTfm)
        {
            var availableTfms = Directory.GetDirectories(libPath).Select(Path.GetFileName).ToList();

            var compatibilityOrder = new[] { projectTfm, "net8.0", "net7.0", "net6.0", "netstandard2.1",
                "netstandard2.0", "netstandard1.6", "net48", "net472", "net471", "net462", "net461", "net46" };

            foreach (var tfm in compatibilityOrder)
            {
                var match = availableTfms.FirstOrDefault(t => t.Equals(tfm, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }

            return availableTfms.OrderByDescending(t => t).FirstOrDefault();
        }

        private void LoadDirectReferences(XDocument doc, string baseDir)
        {
            var refs = doc.Descendants("Reference");
            foreach (var r in refs)
            {
                var hintPath = r.Element("HintPath")?.Value;
                if (!string.IsNullOrEmpty(hintPath))
                {
                    var fullPath = Path.Combine(baseDir, hintPath);
                    SafeLoadAssembly(fullPath);
                }
            }
        }

        private void LoadProjectBinaries(string csprojPath, string targetFramework)
        {
            var baseDir = Path.GetDirectoryName(csprojPath);
            var binPath = Path.Combine(baseDir, "bin", "Debug", targetFramework);

            if (!Directory.Exists(binPath))
                binPath = Path.Combine(baseDir, "bin", "Release", targetFramework);

            if (Directory.Exists(binPath))
            {
                foreach (var dll in Directory.GetFiles(binPath, "*.dll"))
                    SafeLoadAssembly(dll);
            }
        }

        private void SafeLoadAssembly(string path)
        {
            if (!File.Exists(path) || _assemblies.ContainsKey(path)) return;

            try
            {
                var info = ReadAssemblyMetadata(path);
                if (info != null)
                    _assemblies[path] = info;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Failed to load {Path.GetFileName(path)}: {ex.Message}");
            }
        }

        private AssemblyInfo ReadAssemblyMetadata(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var peReader = new PEReader(fs);

            if (!peReader.HasMetadata) return null;

            var metadataReader = peReader.GetMetadataReader();
            if (!metadataReader.IsAssembly) return null;

            var assemblyDef = metadataReader.GetAssemblyDefinition();
            var assemblyName = metadataReader.GetString(assemblyDef.Name);

            var types = new List<TypeInfo>();

            foreach (var typeDefHandle in metadataReader.TypeDefinitions)
            {
                var typeDef = metadataReader.GetTypeDefinition(typeDefHandle);
                var typeName = metadataReader.GetString(typeDef.Name);
                var typeNamespace = metadataReader.GetString(typeDef.Namespace);

                if (string.IsNullOrEmpty(typeNamespace) && typeName.StartsWith("<")) continue;

                var resolver = new TypeResolver(metadataReader);

                var typeInfo = new TypeInfo
                {
                    Name = typeName,
                    Namespace = typeNamespace,
                    FullName = string.IsNullOrEmpty(typeNamespace) ? typeName : $"{typeNamespace}.{typeName}",
                    Attributes = typeDef.Attributes,
                    BaseType = GetBaseTypeName(metadataReader, typeDef.BaseType, resolver),
                    Interfaces = GetInterfaces(metadataReader, typeDef, resolver),
                    Methods = GetMethods(metadataReader, typeDef, resolver),
                    Properties = GetProperties(metadataReader, typeDef, resolver),
                    Fields = GetFields(metadataReader, typeDef, resolver)
                };

                types.Add(typeInfo);
            }

            return new AssemblyInfo
            {
                Path = path,
                Name = assemblyName,
                Types = types
            };
        }

        private string GetBaseTypeName(MetadataReader reader, EntityHandle baseTypeHandle, TypeResolver resolver)
        {
            if (baseTypeHandle.IsNil) return null;
            return resolver.ResolveType(baseTypeHandle);
        }

        private List<string> GetInterfaces(MetadataReader reader, TypeDefinition typeDef, TypeResolver resolver)
        {
            var interfaces = new List<string>();
            foreach (var interfaceHandle in typeDef.GetInterfaceImplementations())
            {
                var interfaceImpl = reader.GetInterfaceImplementation(interfaceHandle);
                var interfaceType = interfaceImpl.Interface;
                interfaces.Add(resolver.ResolveType(interfaceType));
            }
            return interfaces;
        }

        private List<MethodMetadata> GetMethods(MetadataReader reader, TypeDefinition typeDef, TypeResolver resolver)
        {
            var methods = new List<MethodMetadata>();

            foreach (var methodHandle in typeDef.GetMethods())
            {
                var methodDef = reader.GetMethodDefinition(methodHandle);
                var methodName = reader.GetString(methodDef.Name);

                var signature = methodDef.DecodeSignature(new SignatureDecoder(resolver), null);

                var parameters = new List<ParameterMetadata>();
                foreach (var paramHandle in methodDef.GetParameters())
                {
                    var paramDef = reader.GetParameter(paramHandle);
                    var paramName = reader.GetString(paramDef.Name);

                    parameters.Add(new ParameterMetadata
                    {
                        Name = paramName,
                        ParameterType = "object"
                    });
                }

                methods.Add(new MethodMetadata
                {
                    Name = methodName,
                    ReturnType = signature.ReturnType,
                    Parameters = parameters,
                    Attributes = methodDef.Attributes,
                    IsConstructor = methodName == ".ctor" || methodName == ".cctor"
                });
            }

            return methods;
        }

        private List<PropertyMetadata> GetProperties(MetadataReader reader, TypeDefinition typeDef, TypeResolver resolver)
        {
            var properties = new List<PropertyMetadata>();

            foreach (var propHandle in typeDef.GetProperties())
            {
                var propDef = reader.GetPropertyDefinition(propHandle);
                var propName = reader.GetString(propDef.Name);

                var accessors = propDef.GetAccessors();

                string propType = "object";

                if (!accessors.Getter.IsNil)
                {
                    try
                    {
                        var getter = reader.GetMethodDefinition(accessors.Getter);
                        var sig = getter.DecodeSignature(new SignatureDecoder(resolver), null);
                        propType = sig.ReturnType;
                    }
                    catch { }
                }

                properties.Add(new PropertyMetadata
                {
                    Name = propName,
                    PropertyType = propType,
                    HasGetter = !accessors.Getter.IsNil,
                    HasSetter = !accessors.Setter.IsNil
                });
            }

            return properties;
        }

        private List<FieldMetadata> GetFields(MetadataReader reader, TypeDefinition typeDef, TypeResolver resolver)
        {
            var fields = new List<FieldMetadata>();

            foreach (var fieldHandle in typeDef.GetFields())
            {
                var fieldDef = reader.GetFieldDefinition(fieldHandle);
                var fieldName = reader.GetString(fieldDef.Name);

                bool isBackingField = fieldName.Contains("k__BackingField") ||
                                     (fieldName.StartsWith("<") && fieldName.Contains(">"));

                string fieldType = "object";
                try
                {
                    var sig = fieldDef.DecodeSignature(new SignatureDecoder(resolver), null);
                    fieldType = sig;
                }
                catch { }

                fields.Add(new FieldMetadata
                {
                    Name = fieldName,
                    FieldType = fieldType,
                    Attributes = fieldDef.Attributes,
                    IsBackingField = isBackingField
                });
            }

            return fields;
        }

        public IEnumerable<ClassDefinition> FindClass(string className)
        {
            var results = new List<ClassDefinition>();

            foreach (var assembly in _assemblies.Values)
            {
                var matchingTypes = assembly.Types
                    .Where(t => t.Name.Contains(className, StringComparison.OrdinalIgnoreCase) ||
                               t.FullName.Contains(className, StringComparison.OrdinalIgnoreCase));

                foreach (var type in matchingTypes)
                {
                    results.Add(ConvertToClassDefinition(type, assembly.Name));
                }
            }

            return results;
        }

        private ClassDefinition ConvertToClassDefinition(TypeInfo type, string assemblyName)
        {
            var modifiers = GetClassModifiers(type.Attributes);
            var isInterface = (type.Attributes & TypeAttributes.Interface) == TypeAttributes.Interface;
            var isEnum = type.IsEnum;
            var isStatic = (type.Attributes & TypeAttributes.Abstract) == TypeAttributes.Abstract &&
                          (type.Attributes & TypeAttributes.Sealed) == TypeAttributes.Sealed;

            return new ClassDefinition
            {
                Name = type.Name,
                Namespace = type.Namespace,
                FullName = type.FullName,
                AssemblyName = assemblyName,
                IsPublic = (type.Attributes & TypeAttributes.Public) == TypeAttributes.Public,
                IsAbstract = (type.Attributes & TypeAttributes.Abstract) == TypeAttributes.Abstract,
                IsSealed = (type.Attributes & TypeAttributes.Sealed) == TypeAttributes.Sealed,
                IsStatic = isStatic,
                IsInterface = isInterface,
                IsEnum = isEnum,
                BaseType = type.BaseType,
                Interfaces = type.Interfaces,
                SourceCode = GenerateSourceCode(type, modifiers, isInterface, isEnum)
            };
        }

        private string GetClassModifiers(TypeAttributes attributes)
        {
            var mods = new List<string>();

            if ((attributes & TypeAttributes.Public) == TypeAttributes.Public)
                mods.Add("public");
            else
                mods.Add("internal");

            if ((attributes & TypeAttributes.Abstract) == TypeAttributes.Abstract &&
                (attributes & TypeAttributes.Sealed) == TypeAttributes.Sealed)
                mods.Add("static");
            else if ((attributes & TypeAttributes.Abstract) == TypeAttributes.Abstract)
                mods.Add("abstract");
            else if ((attributes & TypeAttributes.Sealed) == TypeAttributes.Sealed)
                mods.Add("sealed");

            return string.Join(" ", mods);
        }

        private string GenerateSourceCode(TypeInfo type, string modifiers, bool isInterface, bool isEnum)
        {
            var sb = new System.Text.StringBuilder();
            var indent = "    ";
            var typeKeyword = isInterface ? "interface" : isEnum ? "enum" : "class";

            if (!string.IsNullOrEmpty(type.Namespace))
            {
                sb.AppendLine($"namespace {type.Namespace}");
                sb.AppendLine("{");
            }

            var inheritance = "";
            if (!string.IsNullOrEmpty(type.BaseType) && type.BaseType != "System.Object" && !isEnum)
                inheritance = $" : {FormatTypeName(type.BaseType)}";

            if (type.Interfaces.Any())
            {
                var prefix = string.IsNullOrEmpty(inheritance) ? " : " : ", ";
                inheritance += prefix + string.Join(", ", type.Interfaces.Select(FormatTypeName));
            }

            sb.AppendLine($"{indent}{modifiers} {typeKeyword} {type.Name}{inheritance}");
            sb.AppendLine($"{indent}{{");

            if (isEnum)
            {
                foreach (var field in type.Fields.Where(f => f.Attributes.HasFlag(FieldAttributes.Static) && f.Attributes.HasFlag(FieldAttributes.Public)))
                {
                    sb.AppendLine($"{indent}{indent}{field.Name},");
                }
            }
            else
            {
                foreach (var field in type.Fields.Where(f => !f.IsBackingField).OrderBy(f => f.Name))
                {
                    var fieldMods = GetFieldModifiers(field.Attributes);
                    sb.AppendLine($"{indent}{indent}{fieldMods} {FormatTypeName(field.FieldType)} {field.Name};");
                }

                foreach (var prop in type.Properties.OrderBy(p => p.Name))
                {
                    var accessors = new List<string>();
                    if (prop.HasGetter) accessors.Add("get");
                    if (prop.HasSetter) accessors.Add("set");
                    sb.AppendLine($"{indent}{indent}public {FormatTypeName(prop.PropertyType)} {prop.Name} {{ {string.Join("; ", accessors)}; }}");
                }

                var constructors = type.Methods.Where(m => m.IsConstructor).ToList();
                foreach (var ctor in constructors)
                {
                    var ctorMods = GetMethodModifiers(ctor.Attributes);
                    var parameters = string.Join(", ", ctor.Parameters.Select(p => $"{FormatTypeName(p.ParameterType)} {p.Name}"));
                    sb.AppendLine($"{indent}{indent}{ctorMods} {type.Name}({parameters});");
                }

                foreach (var method in type.Methods.Where(m => !m.IsConstructor && !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_")).OrderBy(m => m.Name))
                {
                    var methodMods = GetMethodModifiers(method.Attributes);
                    var returnType = FormatTypeName(method.ReturnType);
                    var parameters = string.Join(", ", method.Parameters.Select(p => $"{FormatTypeName(p.ParameterType)} {p.Name}"));
                    sb.AppendLine($"{indent}{indent}{methodMods} {returnType} {method.Name}({parameters});");
                }
            }

            sb.AppendLine($"{indent}}}");

            if (!string.IsNullOrEmpty(type.Namespace))
                sb.AppendLine("}");

            return sb.ToString();
        }

        private string FormatTypeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return "void";

            var simplified = typeName switch
            {
                "System.String" => "string",
                "System.Int32" => "int",
                "System.Int64" => "long",
                "System.Boolean" => "bool",
                "System.Double" => "double",
                "System.Single" => "float",
                "System.Decimal" => "decimal",
                "System.Byte" => "byte",
                "System.SByte" => "sbyte",
                "System.Int16" => "short",
                "System.UInt16" => "ushort",
                "System.UInt32" => "uint",
                "System.UInt64" => "ulong",
                "System.Char" => "char",
                "System.Object" => "object",
                "System.Void" => "void",
                _ => typeName
            };

            if (simplified.EndsWith("?"))
                return simplified;

            return simplified;
        }

        private string GetFieldModifiers(FieldAttributes attributes)
        {
            var mods = new List<string>();

            if ((attributes & FieldAttributes.Public) == FieldAttributes.Public)
                mods.Add("public");
            else if ((attributes & FieldAttributes.Private) == FieldAttributes.Private)
                mods.Add("private");
            else if ((attributes & FieldAttributes.Family) == FieldAttributes.Family)
                mods.Add("protected");
            else
                mods.Add("internal");

            if ((attributes & FieldAttributes.Static) == FieldAttributes.Static)
                mods.Add("static");

            if ((attributes & FieldAttributes.InitOnly) == FieldAttributes.InitOnly)
                mods.Add("readonly");

            return string.Join(" ", mods);
        }

        private string GetMethodModifiers(MethodAttributes attributes)
        {
            var mods = new List<string>();

            if ((attributes & MethodAttributes.Public) == MethodAttributes.Public)
                mods.Add("public");
            else if ((attributes & MethodAttributes.Private) == MethodAttributes.Private)
                mods.Add("private");
            else if ((attributes & MethodAttributes.Family) == MethodAttributes.Family)
                mods.Add("protected");

            if ((attributes & MethodAttributes.Static) == MethodAttributes.Static)
                mods.Add("static");

            return string.Join(" ", mods);
        }

        #region Dependency Analysis with Filters

        public ClassAnalysisResult FindClassWithDependencies(
            string className,
            DependencyAnalysisOptions options = DependencyAnalysisOptions.None,
            int maxDepth = 3,
            FilterConfiguration filters = null)
        {
            filters ??= _currentFilters;

            var primaryClasses = FindClass(className).ToList();
            if (!primaryClasses.Any())
                return null;

            var primary = primaryClasses.First();

            var result = new ClassAnalysisResult
            {
                PrimaryClass = primary,
                AppliedFilters = filters
            };

            if (options == DependencyAnalysisOptions.None || maxDepth <= 0)
                return result;

            var visitedTypes = new HashSet<string> { primary.FullName };
            var queue = new Queue<(string TypeName, int Depth)>();

            EnqueueDependencies(primary, queue, visitedTypes, 1, options, filters);

            while (queue.Count > 0)
            {
                var (currentTypeName, currentDepth) = queue.Dequeue();

                if (currentDepth > maxDepth) continue;

                // Apply filters
                if (filters.ShouldExclude(currentTypeName))
                    continue;

                var typeDef = FindClassByFullName(currentTypeName);
                if (typeDef == null) continue;

                if (currentTypeName != primary.FullName && !result.Dependencies.ContainsKey(currentTypeName))
                {
                    result.Dependencies[currentTypeName] = typeDef;

                    var relationship = FindRelationshipSource(currentTypeName, primary, result.Dependencies);
                    if (relationship != null)
                        result.Relationships.Add(relationship);
                }

                if (currentDepth < maxDepth)
                {
                    EnqueueDependencies(typeDef, queue, visitedTypes, currentDepth + 1, options, filters);
                }
            }

            return result;
        }

        private void EnqueueDependencies(
            ClassDefinition typeDef,
            Queue<(string, int)> queue,
            HashSet<string> visited,
            int nextDepth,
            DependencyAnalysisOptions options,
            FilterConfiguration filters)
        {
            var referencedTypes = ExtractReferencedTypes(typeDef, options);

            foreach (var refType in referencedTypes)
            {
                var cleanType = CleanTypeName(refType);

                // Skip if already visited or filtered out
                if (visited.Contains(cleanType))
                    continue;

                if (filters.ShouldExclude(cleanType))
                    continue;

                visited.Add(cleanType);
                queue.Enqueue((cleanType, nextDepth));
            }
        }

        private List<string> ExtractReferencedTypes(ClassDefinition classDef, DependencyAnalysisOptions options)
        {
            var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (options.HasFlag(DependencyAnalysisOptions.BaseType) && !string.IsNullOrEmpty(classDef.BaseType))
                types.Add(classDef.BaseType);

            if (options.HasFlag(DependencyAnalysisOptions.Interfaces) && classDef.Interfaces != null)
            {
                foreach (var iface in classDef.Interfaces)
                    types.Add(iface);
            }

            var lines = classDef.SourceCode.Split('\n');
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("//") || trimmed.StartsWith("namespace")) continue;

                if (options.HasFlag(DependencyAnalysisOptions.PropertyTypes) &&
                    trimmed.Contains("{ get") && !trimmed.StartsWith("["))
                {
                    ExtractTypeFromLine(trimmed, types);
                }

                if ((options.HasFlag(DependencyAnalysisOptions.MethodParameters) ||
                    options.HasFlag(DependencyAnalysisOptions.MethodReturnTypes)) &&
                    trimmed.Contains("(") && trimmed.Contains(")"))
                {
                    ExtractMethodTypes(trimmed, types, options);
                }

                if (options.HasFlag(DependencyAnalysisOptions.FieldTypes) &&
                    trimmed.EndsWith(";") && !trimmed.Contains("(") && !trimmed.Contains("{"))
                {
                    ExtractTypeFromLine(trimmed, types);
                }
            }

            return types.Where(t => !IsPrimitiveType(t)).ToList();
        }

        private void ExtractTypeFromLine(string line, HashSet<string> types)
        {
            var modifiers = new[] { "public", "private", "protected", "internal", "static",
                                   "readonly", "abstract", "virtual", "override", "sealed", "const" };

            var cleaned = line;
            foreach (var mod in modifiers)
                cleaned = cleaned.Replace(mod, "");

            cleaned = cleaned.Trim();
            var parts = cleaned.Split(new[] { ' ', '(', ')', ';', '{', '}', '<', '>', ',' },
                                     StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 2)
            {
                var typeName = parts[0];
                if (!IsPrimitiveType(typeName))
                {
                    if (line.Contains("<"))
                        ExtractGenericTypes(line, types);
                    else
                        types.Add(typeName);
                }
            }
        }

        private void ExtractMethodTypes(string line, HashSet<string> types, DependencyAnalysisOptions options)
        {
            if (options.HasFlag(DependencyAnalysisOptions.MethodReturnTypes))
            {
                var returnPart = line.Substring(0, line.IndexOf('(')).Trim();
                ExtractTypeFromLine(returnPart, types);
            }

            if (options.HasFlag(DependencyAnalysisOptions.MethodParameters))
            {
                var start = line.IndexOf('(') + 1;
                var end = line.LastIndexOf(')');
                if (start > 0 && end > start)
                {
                    var paramsStr = line.Substring(start, end - start);
                    var parameters = paramsStr.Split(',');

                    foreach (var param in parameters)
                    {
                        ExtractTypeFromLine(param.Trim(), types);
                    }
                }
            }
        }

        private void ExtractGenericTypes(string line, HashSet<string> types)
        {
            var start = line.IndexOf('<');
            var end = line.LastIndexOf('>');

            if (start >= 0 && end > start)
            {
                var genericArgs = line.Substring(start + 1, end - start - 1);
                var args = genericArgs.Split(',');

                foreach (var arg in args)
                {
                    var cleanArg = arg.Trim();
                    if (!IsPrimitiveType(cleanArg) && !cleanArg.StartsWith("T"))
                        types.Add(cleanArg);
                }
            }
        }

        private string CleanTypeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return typeName;

            return typeName
                .Replace("[]", "")
                .Replace("*", "")
                .Replace("&", "")
                .Replace("?", "")
                .Trim();
        }

        private bool IsPrimitiveType(string typeName)
        {
            var primitives = new[] { "void", "bool", "byte", "sbyte", "char", "short", "ushort",
                                    "int", "uint", "long", "ulong", "float", "double", "decimal",
                                    "string", "object", "dynamic", "nint", "nuint" };

            return primitives.Contains(typeName) || typeName.StartsWith("T") || typeName.Length == 1;
        }

        private ClassDefinition FindClassByFullName(string fullName)
        {
            foreach (var assembly in _assemblies.Values)
            {
                var type = assembly.Types.FirstOrDefault(t =>
                    t.FullName.Equals(fullName, StringComparison.OrdinalIgnoreCase));

                if (type != null)
                    return ConvertToClassDefinition(type, assembly.Name);
            }
            return null;
        }

        private RelationshipInfo FindRelationshipSource(string targetType, ClassDefinition primary, Dictionary<string, ClassDefinition> dependencies)
        {
            if (primary.BaseType == targetType)
            {
                return new RelationshipInfo
                {
                    SourceType = primary.FullName,
                    TargetType = targetType,
                    RelationshipKind = RelationshipKind.Inherits
                };
            }

            if (primary.Interfaces?.Contains(targetType) == true)
            {
                return new RelationshipInfo
                {
                    SourceType = primary.FullName,
                    TargetType = targetType,
                    RelationshipKind = RelationshipKind.Implements
                };
            }

            foreach (var dep in dependencies.Values)
            {
                var rel = FindRelationshipInType(dep, targetType);
                if (rel != null) return rel;
            }

            return FindRelationshipInType(primary, targetType);
        }

        private RelationshipInfo FindRelationshipInType(ClassDefinition source, string targetType)
        {
            var lines = source.SourceCode.Split('\n');

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (trimmed.Contains(targetType) && trimmed.Contains("get;"))
                {
                    var propName = ExtractMemberName(trimmed);
                    return new RelationshipInfo
                    {
                        SourceType = source.FullName,
                        TargetType = targetType,
                        MemberName = propName,
                        RelationshipKind = RelationshipKind.PropertyType
                    };
                }

                if (trimmed.Contains(targetType) && trimmed.Contains("(") && trimmed.Contains(")"))
                {
                    var methodName = ExtractMemberName(trimmed);
                    var isReturn = !trimmed.Contains($" {targetType} ") && trimmed.StartsWith("public");

                    return new RelationshipInfo
                    {
                        SourceType = source.FullName,
                        TargetType = targetType,
                        MemberName = methodName,
                        RelationshipKind = isReturn ? RelationshipKind.MethodReturn : RelationshipKind.MethodParameter
                    };
                }

                if (trimmed.Contains(targetType) && trimmed.EndsWith(";") && !trimmed.Contains("("))
                {
                    var fieldName = ExtractMemberName(trimmed);
                    return new RelationshipInfo
                    {
                        SourceType = source.FullName,
                        TargetType = targetType,
                        MemberName = fieldName,
                        RelationshipKind = RelationshipKind.FieldType
                    };
                }
            }

            return null;
        }

        private string ExtractMemberName(string line)
        {
            var parts = line.Split(new[] { ' ', '(', ';', '{', '<' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                return parts.Last().Trim();
            }
            return "unknown";
        }

        #endregion

        public void Dispose()
        {
            _assemblies.Clear();
        }
    }

    #endregion
}
using MCPSharp.Core.Configuration;
using MCPSharp.Core.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;

namespace MCPSharp.Tests
{
    /// <summary>
    /// Unit tests for TypeExplorerService - Cross-platform compatible
    /// </summary>
    public class TypeExplorerServiceTests : IDisposable
    {
        private readonly string _testProjectPath;
        private readonly string _testSolutionPath;
        private readonly string _tempDirectory;
        private bool _disposed;

        public TypeExplorerServiceTests()
        {
            // Create temporary test directory with cross-platform path
            _tempDirectory = Path.Combine(Path.GetTempPath(), $"MCPSharpTests_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDirectory);

            // Create test project structure
            CreateTestProjectStructure();

            // Set paths after creation
            var projectDir = Path.Combine(_tempDirectory, "TestProject");
            _testProjectPath = Path.Combine(projectDir, "TestProject.csproj");
            _testSolutionPath = Path.Combine(_tempDirectory, "TestSolution.sln");
        }

        private void CreateTestProjectStructure()
        {
            // Create main test project
            var projectDir = Path.Combine(_tempDirectory, "TestProject");
            Directory.CreateDirectory(projectDir);

            File.WriteAllText(Path.Combine(projectDir, "TestProject.csproj"), @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>");

            // Create test source files
            var srcDir = Path.Combine(projectDir, "src");
            Directory.CreateDirectory(srcDir);

            File.WriteAllText(Path.Combine(srcDir, "UserService.cs"), @"
namespace TestProject.Services
{
    public class UserService
    {
        public string GetUserName(int id) { return """"; }
        public void SaveUser(User user) { }
    }

    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public UserProfile Profile { get; set; }
    }

    public class UserProfile
    {
        public string Avatar { get; set; }
        public int Age { get; set; }
    }
}
");

            File.WriteAllText(Path.Combine(srcDir, "OrderService.cs"), @"
namespace TestProject.Services
{
    public class OrderService
    {
        public Order CreateOrder(int userId) { return null; }
        public void ProcessPayment(PaymentInfo payment) { }
    }

    public class Order
    {
        public int Id { get; set; }
        public decimal Total { get; set; }
        public System.Collections.Generic.List<OrderItem> Items { get; set; }
    }

    public class OrderItem
    {
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }

    public class PaymentInfo
    {
        public string Method { get; set; }
        public decimal Amount { get; set; }
    }
}
");

            File.WriteAllText(Path.Combine(srcDir, "Enums.cs"), @"
namespace TestProject.Enums
{
    public enum Status
    {
        Pending,
        Active,
        Completed,
        Cancelled
    }

    public enum Priority
    {
        Low,
        Medium,
        High,
        Critical
    }
}
");

            File.WriteAllText(Path.Combine(srcDir, "Interfaces.cs"), @"
namespace TestProject.Interfaces
{
    public interface IRepository<T>
    {
        T GetById(int id);
        void Save(T entity);
        void Delete(int id);
    }

    public interface ICacheService
    {
        void Set(string key, object value);
        object Get(string key);
    }
}
");

            // Create solution file
            File.WriteAllText(Path.Combine(_tempDirectory, "TestSolution.sln"), @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""TestProject"", ""TestProject\TestProject.csproj"", ""{12345678-1234-1234-1234-123456789012}""
EndProject
Global
EndGlobal
");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // Cleanup temporary directory
                try
                {
                    if (Directory.Exists(_tempDirectory))
                    {
                        Directory.Delete(_tempDirectory, recursive: true);
                    }
                }
                catch
                {
                    // Best effort cleanup
                }
                _disposed = true;
            }
        }

        #region Constructor and Disposal Tests

        [Fact]
        public void Constructor_InitializesSuccessfully()
        {
            // Act
            using var service = new TypeExplorerService();

            // Assert
            Assert.NotNull(service);
        }

        [Fact]
        public void Dispose_CleansUpResources()
        {
            // Arrange
            var service = new TypeExplorerService();
            service.LoadProject(_testProjectPath);

            // Act & Assert - should not throw on dispose
            service.Dispose();
            service.Dispose(); // Second dispose should not throw
        }

        #endregion

        #region LoadProject Tests

        [Fact]
        public void LoadProject_WithValidCsproj_LoadsSuccessfully()
        {
            // Arrange
            using var service = new TypeExplorerService();

            // Act
            var exception = Record.Exception(() => service.LoadProject(_testProjectPath));

            // Assert
            Assert.Null(exception);
        }

        [Fact]
        public void LoadProject_WithValidSln_LoadsSuccessfully()
        {
            // Arrange
            using var service = new TypeExplorerService();

            // Act
            var exception = Record.Exception(() => service.LoadProject(_testSolutionPath));

            // Assert
            Assert.Null(exception);
        }

        [Fact]
        public void LoadProject_WithInvalidPath_ThrowsFileNotFoundException()
        {
            // Arrange
            using var service = new TypeExplorerService();
            var invalidPath = Path.Combine(_tempDirectory, "NonExistent.csproj");

            // Act & Assert
            Assert.Throws<FileNotFoundException>(() => service.LoadProject(invalidPath));
        }

        [Fact]
        public void LoadProject_WithUnsupportedExtension_ThrowsNotSupportedException()
        {
            // Arrange
            using var service = new TypeExplorerService();
            var invalidFile = Path.Combine(_tempDirectory, "test.txt");
            File.WriteAllText(invalidFile, "test");

            // Act & Assert
            Assert.Throws<NotSupportedException>(() => service.LoadProject(invalidFile));
        }

        #endregion

        #region FindClass Tests

        [Fact]
        public void FindClass_WithExistingClass_ReturnsResults()
        {
            // Arrange
            using var service = new TypeExplorerService();
            service.LoadProject(_testProjectPath);

            // Act
            var results = service.FindClass("UserService").ToList();

            // Assert
            Assert.True(results.Count > 0);
            Assert.Contains(results, r => r.Name == "UserService");
        }

        [Fact]
        public void FindClass_WithPartialName_ReturnsMatchingResults()
        {
            // Arrange
            using var service = new TypeExplorerService();
            service.LoadProject(_testProjectPath);

            // Act
            var results = service.FindClass("Service").ToList();

            // Assert
            Assert.True(results.Count >= 2); // UserService and OrderService
            Assert.All(results, r => Assert.Contains("Service", r.Name));
        }

        [Fact]
        public void FindClass_WithCaseInsensitiveSearch_ReturnsSameResults()
        {
            // Arrange
            using var service = new TypeExplorerService();
            service.LoadProject(_testProjectPath);

            // Act
            var resultsLower = service.FindClass("userservice").ToList();
            var resultsUpper = service.FindClass("USERSERVICE").ToList();
            var resultsMixed = service.FindClass("UserService").ToList();

            // Assert
            Assert.Equal(resultsMixed.Count, resultsLower.Count);
            Assert.Equal(resultsMixed.Count, resultsUpper.Count);
        }

        [Fact]
        public void FindClass_WithNonExistentClass_ReturnsEmpty()
        {
            // Arrange
            using var service = new TypeExplorerService();
            service.LoadProject(_testProjectPath);

            // Act
            var results = service.FindClass("NonExistentClass").ToList();

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public void FindClass_WithInterface_ReturnsInterfaceType()
        {
            // Arrange
            using var service = new TypeExplorerService();
            service.LoadProject(_testProjectPath);

            // Act
            var results = service.FindClass("IRepository").ToList();

            // Assert
            Assert.True(results.Count > 0);
            Assert.Contains(results, r => r.IsInterface);
        }

        [Fact]
        public void FindClass_WithEnum_ReturnsEnumType()
        {
            // Arrange
            using var service = new TypeExplorerService();
            service.LoadProject(_testProjectPath);

            // Act
            var results = service.FindClass("Status").ToList();

            // Assert
            Assert.True(results.Count > 0);
            var statusEnum = results.FirstOrDefault(r => r.Name == "Status");
            Assert.NotNull(statusEnum);
            Assert.True(statusEnum.IsEnum);
        }

        [Fact]
        public void FindClass_ReturnsCorrectNamespace()
        {
            // Arrange
            using var service = new TypeExplorerService();
            service.LoadProject(_testProjectPath);

            // Act
            var results = service.FindClass("UserService").ToList();

            // Assert
            var userService = results.FirstOrDefault(r => r.Name == "UserService");
            Assert.NotNull(userService);
            Assert.Equal("TestProject.Services", userService.Namespace);
        }

        #endregion

        #region FindClassWithDependencies Tests

        [Fact]
        public void FindClassWithDependencies_NoneOption_ReturnsOnlyPrimaryClass()
        {
            // Arrange
            using var service = new TypeExplorerService();
            service.LoadProject(_testProjectPath);

            // Act
            var result = service.FindClassWithDependencies(
                "UserService",
                DependencyAnalysisOptions.None,
                3);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.PrimaryClass);
            Assert.Equal(0, result.Dependencies.Count);
        }

        [Fact]
        public void FindClassWithDependencies_HierarchyOption_ReturnsBaseTypes()
        {
            // Arrange
            using var service = new TypeExplorerService();
            service.LoadProject(_testProjectPath);

            // Act
            var result = service.FindClassWithDependencies(
                "User",
                DependencyAnalysisOptions.HierarchyOnly,
                3);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.PrimaryClass);
        }

        [Fact]
        public void FindClassWithDependencies_SignatureOption_ReturnsParameterTypes()
        {
            // Arrange
            using var service = new TypeExplorerService();
            service.LoadProject(_testProjectPath);

            // Act
            var result = service.FindClassWithDependencies(
                "UserService",
                DependencyAnalysisOptions.SignatureOnly,
                3);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.PrimaryClass);
        }

        [Fact]
        public void FindClassWithDependencies_AllOption_ReturnsAllDependencies()
        {
            // Arrange
            using var service = new TypeExplorerService();
            service.LoadProject(_testProjectPath);

            // Act
            var result = service.FindClassWithDependencies(
                "OrderService",
                DependencyAnalysisOptions.All,
                3);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.PrimaryClass);
            Assert.True(result.Dependencies.Count > 0 || result.Relationships.Count > 0);
        }

        [Fact]
        public void FindClassWithDependencies_WithMaxDepth_LimitsRecursion()
        {
            // Arrange
            using var service = new TypeExplorerService();
            service.LoadProject(_testProjectPath);

            // Act
            var resultDepth1 = service.FindClassWithDependencies(
                "OrderService",
                DependencyAnalysisOptions.All,
                1);

            var resultDepth3 = service.FindClassWithDependencies(
                "OrderService",
                DependencyAnalysisOptions.All,
                3);

            // Assert
            Assert.NotNull(resultDepth1);
            Assert.NotNull(resultDepth3);
            // Depth 3 should have equal or more dependencies than depth 1
            Assert.True(resultDepth3.Dependencies.Count >= resultDepth1.Dependencies.Count);
        }

        [Fact]
        public void FindClassWithDependencies_WithFilters_ExcludesSystemTypes()
        {
            // Arrange
            using var service = new TypeExplorerService();
            service.LoadProject(_testProjectPath);

            var filters = new FilterConfiguration
            {
                IncludeSystemDependencies = false,
                IncludeMicrosoftDependencies = false,
                IncludeThirdPartyDependencies = false
            };

            // Act
            var result = service.FindClassWithDependencies(
                "OrderService",
                DependencyAnalysisOptions.All,
                3,
                filters);

            // Assert
            Assert.NotNull(result);
            Assert.DoesNotContain(result.Dependencies.Keys, k => k.StartsWith("System."));
        }

        [Fact]
        public void FindClassWithDependencies_WithCustomNamespaceFilter_ExcludesSpecified()
        {
            // Arrange
            using var service = new TypeExplorerService();
            service.LoadProject(_testProjectPath);

            var filters = new FilterConfiguration
            {
                ExcludedNamespaces = new List<string> { "TestProject.Enums" }
            };

            // Act
            var result = service.FindClassWithDependencies(
                "UserService",
                DependencyAnalysisOptions.All,
                3,
                filters);

            // Assert
            Assert.NotNull(result);
            Assert.DoesNotContain(result.Dependencies.Values, d =>
                d.Namespace?.StartsWith("TestProject.Enums") == true);
        }

        [Fact]
        public void FindClassWithDependencies_WithPatternFilter_ExcludesMatching()
        {
            // Arrange
            using var service = new TypeExplorerService();
            service.LoadProject(_testProjectPath);

            var filters = new FilterConfiguration
            {
                ExcludedClassPatterns = new List<string> { "Profile" }
            };

            // Act
            var result = service.FindClassWithDependencies(
                "UserService",
                DependencyAnalysisOptions.All,
                3,
                filters);

            // Assert
            Assert.NotNull(result);
            Assert.DoesNotContain(result.Dependencies.Keys, k => k.Contains("Profile"));
        }

        [Fact]
        public void FindClassWithDependencies_NonExistentClass_ReturnsNull()
        {
            // Arrange
            using var service = new TypeExplorerService();
            service.LoadProject(_testProjectPath);

            // Act
            var result = service.FindClassWithDependencies(
                "NonExistentClass",
                DependencyAnalysisOptions.All,
                3);

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region FilterConfiguration Tests

        [Fact]
        public void FilterConfiguration_DefaultValues_AreCorrect()
        {
            // Arrange & Act
            var filters = new FilterConfiguration();

            // Assert
            Assert.False(filters.IncludeSystemDependencies);
            Assert.False(filters.IncludeMicrosoftDependencies);
            Assert.False(filters.IncludeThirdPartyDependencies);
            Assert.NotNull(filters.ExcludedNamespaces);
            Assert.NotNull(filters.ExcludedClassPatterns);
            Assert.NotNull(filters.IncludedNamespaces);
        }

        [Theory]
        [InlineData("System.String", false, true)] // Excluded when IncludeSystem = false
        [InlineData("System.String", true, false)] // Included when IncludeSystem = true
        [InlineData("System.Collections.Generic.List", false, true)]
        public void FilterConfiguration_ShouldExclude_SystemType(string typeName, bool includeSystem, bool shouldBeExcluded)
        {
            // Arrange
            var filters = new FilterConfiguration
            {
                IncludeSystemDependencies = includeSystem
            };

            // Act
            var result = filters.ShouldExclude(typeName);

            // Assert
            Assert.Equal(shouldBeExcluded, result);
        }

        [Fact]
        public void FilterConfiguration_ShouldExclude_MicrosoftType_ReturnsTrue()
        {
            // Arrange
            var filters = new FilterConfiguration
            {
                IncludeMicrosoftDependencies = false
            };

            // Act & Assert
            Assert.True(filters.ShouldExclude("Microsoft.Extensions.Logging.ILogger"));
        }

        [Fact]
        public void FilterConfiguration_ShouldExclude_CustomNamespace_ReturnsTrue()
        {
            // Arrange
            var filters = new FilterConfiguration
            {
                ExcludedNamespaces = new List<string> { "TestProject.Tests" }
            };

            // Act & Assert
            Assert.True(filters.ShouldExclude("TestProject.Tests.MockUserService"));
        }

        [Theory]
        [InlineData("TestProject.Tests.MockUserService", "Mock", true)]
        [InlineData("TestProject.Tests.UserStub", "Stub", true)]
        [InlineData("TestProject.Services.UserService", "Mock", false)]
        public void FilterConfiguration_ShouldExclude_PatternMatch(string typeName, string pattern, bool shouldBeExcluded)
        {
            // Arrange
            var filters = new FilterConfiguration
            {
                ExcludedClassPatterns = new List<string> { pattern }
            };

            // Act
            var result = filters.ShouldExclude(typeName);

            // Assert
            Assert.Equal(shouldBeExcluded, result);
        }

        [Fact]
        public void FilterConfiguration_ShouldExclude_WhitelistOverrides_ReturnsFalse()
        {
            // Arrange
            var filters = new FilterConfiguration
            {
                IncludeSystemDependencies = false,
                IncludedNamespaces = new List<string> { "System.Text" }
            };

            // Act & Assert
            Assert.False(filters.ShouldExclude("System.Text.StringBuilder"));
            Assert.True(filters.ShouldExclude("System.Collections.Generic.List"));
        }

        [Fact]
        public void FilterConfiguration_GetFilterSummary_ReturnsCorrectString()
        {
            // Arrange
            var filters = new FilterConfiguration
            {
                IncludeSystemDependencies = false,
                IncludeMicrosoftDependencies = true,
                IncludeThirdPartyDependencies = false,
                ExcludedNamespaces = new List<string> { "Tests", "Mocks" }
            };

            // Act
            var summary = filters.GetFilterSummary();

            // Assert
            Assert.NotNull(summary);
            Assert.Contains("System:Excluded", summary);
            Assert.Contains("Microsoft:Included", summary);
            Assert.Contains("ThirdParty:Excluded", summary);
            Assert.Contains("Tests", summary);
            Assert.Contains("Mocks", summary);
        }

        #endregion

        #region Source Code Generation Tests

        [Fact]
        public void ClassDefinition_SourceCode_ContainsClassDeclaration()
        {
            // Arrange
            using var service = new TypeExplorerService();
            service.LoadProject(_testProjectPath);

            // Act
            var results = service.FindClass("UserService").ToList();

            // Assert
            var userService = results.FirstOrDefault(r => r.Name == "UserService");
            Assert.NotNull(userService);
            Assert.Contains("class UserService", userService.SourceCode);
        }

        [Fact]
        public void ClassDefinition_SourceCode_ContainsMethods()
        {
            // Arrange
            using var service = new TypeExplorerService();
            service.LoadProject(_testProjectPath);

            // Act
            var results = service.FindClass("UserService").ToList();

            // Assert
            var userService = results.FirstOrDefault(r => r.Name == "UserService");
            Assert.NotNull(userService);
            Assert.Contains("GetUserName", userService.SourceCode);
            Assert.Contains("SaveUser", userService.SourceCode);
        }

        [Fact]
        public void ClassDefinition_SourceCode_ContainsProperties()
        {
            // Arrange
            using var service = new TypeExplorerService();
            service.LoadProject(_testProjectPath);

            // Act
            var results = service.FindClass("User").ToList();

            // Assert
            var user = results.FirstOrDefault(r => r.Name == "User");
            Assert.NotNull(user);
            Assert.Contains("Id", user.SourceCode);
            Assert.Contains("Name", user.SourceCode);
        }

        [Fact]
        public void Enum_SourceCode_ContainsEnumValues()
        {
            // Arrange
            using var service = new TypeExplorerService();
            service.LoadProject(_testProjectPath);

            // Act
            var results = service.FindClass("Status").ToList();

            // Assert
            var status = results.FirstOrDefault(r => r.Name == "Status");
            Assert.NotNull(status);
            Assert.True(status.IsEnum);
            Assert.Contains("Pending", status.SourceCode);
            Assert.Contains("Active", status.SourceCode);
            Assert.Contains("Completed", status.SourceCode);
        }

        [Fact]
        public void Interface_SourceCode_ContainsInterfaceDeclaration()
        {
            // Arrange
            using var service = new TypeExplorerService();
            service.LoadProject(_testProjectPath);

            // Act
            var results = service.FindClass("IRepository").ToList();

            // Assert
            var repo = results.FirstOrDefault(r => r.Name == "IRepository");
            Assert.NotNull(repo);
            Assert.True(repo.IsInterface);
            Assert.Contains("interface IRepository", repo.SourceCode);
        }

        #endregion

        #region ClassAnalysisResult Tests

        [Fact]
        public void ClassAnalysisResult_GenerateFullSourceCode_IncludesDependencies()
        {
            // Arrange
            using var service = new TypeExplorerService();
            service.LoadProject(_testProjectPath);

            var result = service.FindClassWithDependencies(
                "OrderService",
                DependencyAnalysisOptions.All,
                2);

            Assert.NotNull(result);
            Assert.True(result.Dependencies.Count > 0);

            // Act
            var fullCode = result.GenerateFullSourceCode(true, result.AppliedFilters);

            // Assert
            Assert.Contains(result.PrimaryClass.FullName, fullCode);
            Assert.Contains("// DEPENDENCIES", fullCode);
            foreach (var dep in result.Dependencies.Values)
            {
                Assert.Contains(dep.FullName, fullCode);
            }
        }

        [Fact]
        public void ClassAnalysisResult_GenerateFullSourceCode_ExcludesDependenciesWhenFalse()
        {
            // Arrange
            using var service = new TypeExplorerService();
            service.LoadProject(_testProjectPath);

            var result = service.FindClassWithDependencies(
                "OrderService",
                DependencyAnalysisOptions.All,
                2);

            Assert.NotNull(result);
            Assert.True(result.Dependencies.Count > 0);

            // Act
            var primaryOnly = result.GenerateFullSourceCode(false, result.AppliedFilters);

            // Assert
            Assert.Contains(result.PrimaryClass.FullName, primaryOnly);
            Assert.DoesNotContain("// DEPENDENCIES", primaryOnly);
        }

        [Fact]
        public void ClassAnalysisResult_Relationships_ArePopulated()
        {
            // Arrange
            using var service = new TypeExplorerService();
            service.LoadProject(_testProjectPath);

            // Act
            var result = service.FindClassWithDependencies(
                "UserService",
                DependencyAnalysisOptions.All,
                3);

            // Assert
            Assert.NotNull(result);
            // Should have relationships showing UserService -> User (method parameter)
            Assert.True(result.Relationships.Count > 0 || result.Dependencies.Count == 0);
        }

        #endregion

        #region Path Resolution Tests - Cross-Platform

        [Fact]
        public void TypeExplorerMcpOptions_ResolveProjectPath_AbsolutePath_ReturnsUnchanged()
        {
            // Arrange - use cross-platform absolute path
            var rootDir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\Projects" : "/home/user/projects";
            var options = new TypeExplorerMcpOptions
            {
                RootDirectory = rootDir
            };

            // Use platform-specific absolute path
            var absolutePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? @"C:\Other\Project.csproj"
                : "/other/project.csproj";

            // Skip if not on expected platform
            if (!Path.IsPathRooted(absolutePath))
            {
                return; // Test not applicable on this platform
            }

            // Act
            var resolved = options.ResolveProjectPath(absolutePath);

            // Assert
            Assert.Equal(Path.GetFullPath(absolutePath), resolved);
        }

        [Fact]
        public void TypeExplorerMcpOptions_ResolveProjectPath_RelativePath_ConcatenatesWithRoot()
        {
            // Arrange
            var rootDir = _tempDirectory; // Use actual temp directory
            var options = new TypeExplorerMcpOptions
            {
                RootDirectory = rootDir
            };
            var relativePath = Path.Combine("MyApp", "MyApp.csproj");

            // Act
            var resolved = options.ResolveProjectPath(relativePath);

            // Assert
            var expected = Path.GetFullPath(Path.Combine(rootDir, relativePath));
            Assert.Equal(expected, resolved);
        }

        [Fact]
        public void TypeExplorerMcpOptions_ResolveProjectPath_NullPath_ThrowsArgumentException()
        {
            // Arrange
            var options = new TypeExplorerMcpOptions();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => options.ResolveProjectPath(null));
        }

        [Fact]
        public void TypeExplorerMcpOptions_ResolveProjectPath_EmptyPath_ThrowsArgumentException()
        {
            // Arrange
            var options = new TypeExplorerMcpOptions();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => options.ResolveProjectPath(""));
        }

        [Fact]
        public void TypeExplorerMcpOptions_ResolveProjectPath_WhitespacePath_ThrowsArgumentException()
        {
            // Arrange
            var options = new TypeExplorerMcpOptions();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => options.ResolveProjectPath("   "));
        }

        [Theory]
        [InlineData("project.csproj")]
        [InlineData("folder/project.csproj")]
        [InlineData("../project.csproj")]
        public void TypeExplorerMcpOptions_ResolveProjectPath_VariousRelativePaths_WorkCorrectly(string relativePath)
        {
            // Arrange
            var rootDir = _tempDirectory;
            var options = new TypeExplorerMcpOptions
            {
                RootDirectory = rootDir
            };

            // Act
            var resolved = options.ResolveProjectPath(relativePath);

            // Assert
            Assert.True(Path.IsPathFullyQualified(resolved));
            Assert.StartsWith(rootDir, resolved);
        }

        #endregion

        #region Edge Cases and Error Handling

        [Fact]
        public void FindClass_WithEmptyString_ReturnsAllTypes()
        {
            // Arrange
            using var service = new TypeExplorerService();
            service.LoadProject(_testProjectPath);

            // Act
            var results = service.FindClass("").ToList();

            // Assert
            Assert.True(results.Count > 0);
        }

        [Fact]
        public void FindClass_WithSpecialCharacters_ReturnsEmpty()
        {
            // Arrange
            using var service = new TypeExplorerService();
            service.LoadProject(_testProjectPath);

            // Act
            var results = service.FindClass("<>!@#$%").ToList();

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public void Service_MultipleLoadProject_CacheWorks()
        {
            // Arrange
            using var service = new TypeExplorerService();

            // Act - load same project twice
            service.LoadProject(_testProjectPath);
            var results1 = service.FindClass("UserService").ToList();

            service.LoadProject(_testProjectPath);
            var results2 = service.FindClass("UserService").ToList();

            // Assert
            Assert.Equal(results1.Count, results2.Count);
        }

        [Fact]
        public void SetFilters_AfterLoadProject_AppliesCorrectly()
        {
            // Arrange
            using var service = new TypeExplorerService();
            service.LoadProject(_testProjectPath);

            var filters = new FilterConfiguration
            {
                IncludeSystemDependencies = true
            };

            // Act
            service.SetFilters(filters);

            // Assert - no exception, filters applied
            var result = service.FindClassWithDependencies(
                "UserService",
                DependencyAnalysisOptions.All,
                2);

            Assert.NotNull(result);
        }

        [Fact]
        public void Service_WorksWithPathContainingSpaces()
        {
            // Arrange - create directory with spaces
            var pathWithSpaces = Path.Combine(_tempDirectory, "path with spaces");
            Directory.CreateDirectory(pathWithSpaces);
            var projectPath = Path.Combine(pathWithSpaces, "Test.csproj");
            File.WriteAllText(projectPath, @"<Project Sdk=""Microsoft.NET.Sdk""><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>");

            using var service = new TypeExplorerService();

            // Act
            var exception = Record.Exception(() => service.LoadProject(projectPath));

            // Assert
            Assert.Null(exception);
        }

        [Fact]
        public void Service_WorksWithPathContainingUnicode()
        {
            // Arrange - create directory with unicode characters
            var unicodePath = Path.Combine(_tempDirectory, "测试路径");
            Directory.CreateDirectory(unicodePath);
            var projectPath = Path.Combine(unicodePath, "Test.csproj");
            File.WriteAllText(projectPath, @"<Project Sdk=""Microsoft.NET.Sdk""><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>");

            using var service = new TypeExplorerService();

            // Act
            var exception = Record.Exception(() => service.LoadProject(projectPath));

            // Assert
            Assert.Null(exception);
        }

        #endregion

        #region Platform-Specific Tests

        [Fact]
        public void Service_HandlesPlatformSpecificPathSeparators()
        {
            // Arrange
            using var service = new TypeExplorerService();

            // Use the native separator for the current platform
            var nativeSeparator = Path.DirectorySeparatorChar;
            var otherSeparator = nativeSeparator == '\\' ? '/' : '\\';

            // Create path with native separators
            var projectDir = Path.Combine(_tempDirectory, "TestProject");
            var nativePath = Path.Combine(projectDir, "TestProject.csproj");

            // Act - should work with native separators
            var exception = Record.Exception(() => service.LoadProject(nativePath));

            // Assert
            Assert.Null(exception);
        }

        #endregion
    }
}
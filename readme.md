<p align="center">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET 8.0" />
  <img src="https://img.shields.io/badge/MCP-Protocol-FF6B6B?style=for-the-badge" alt="MCP Protocol" />
  <img src="https://img.shields.io/badge/STDIO-Transport-4ECDC4?style=for-the-badge" alt="STDIO Transport" />
  <img src="https://img.shields.io/badge/Cross--Platform-Linux%20%7C%20macOS%20%7C%20Windows-45B7D1?style=for-the-badge" alt="Cross Platform" />
</p>

<h1 align="center">
  🔍 MCPSharp
</h1>

<p align="center">
  <b>Advanced .NET Type Explorer for AI Agents</b><br>
  <i>Inspect, analyze, and deconstruct .NET assemblies through the Model Context Protocol</i>
</p>

<p align="center">
  <a href="#features">Features</a> •
  <a href="#installation">Installation</a> •
  <a href="#usage">Usage</a> •
  <a href="#mcp-tools">MCP Tools</a> •
  <a href="#configuration">Configuration</a> •
  <a href="#examples">Examples</a> •
  <a href="#sample-output">Sample Output</a>
</p>

---

## ✨ Features

| Feature | Description |
|---------|-------------|
| 🔍 **Type Discovery** | Find classes, interfaces, enums, structs across projects and solutions |
| 🕸️ **Dependency Analysis** | Map relationships: inheritance, composition, method signatures |
| 🎯 **Smart Filtering** | Exclude System, Microsoft, or custom namespaces; pattern-based filtering |
| 📄 **Source Generation** | Generate C# code representations from compiled assemblies |
| 🌳 **Hierarchy Visualization** | View complete inheritance trees and interface implementations |
| ⚡ **MCP Native** | Full Model Context Protocol support via STDIO transport |
| 🐧 **Cross-Platform** | Linux, macOS, Windows - zero platform-specific code |

---

## 🚀 Installation

### From Source

```bash
# Clone repository
git clone https://github.com/rodrigoramosrs/mcpsharp.git
cd mcpsharp

# Build solution
dotnet build -c Release

# Run tests
dotnet test MCPSharp.Tests/MCPSharp.Tests.csproj

# Publish single-file executable
dotnet publish MCPSharp/MCPSharp.csproj -c Release -r linux-x64 --self-contained false
```

### As NuGet Package *(Coming Soon)*

```bash
dotnet add package MCPSharp.Core
```

---

## 🎮 Quick Start

### 1. Start the Server

```bash
# With root directory for relative paths
./MCPSharp --root-dir /home/user/projects

# Or via environment variable
export MCPSHARP_ROOT_DIR=/home/user/projects
./MCPSharp
```

### 2. Configure Your AI Client

**Claude Desktop** (`claude_desktop_config.json`):
```json
{
  "mcpServers": {
    "dotnet-explorer": {
      "command": "/path/to/MCPSharp",
      "args": ["--root-dir", "/home/user/projects"],
      "env": {
        "DOTNET_ENVIRONMENT": "Production"
      }
    }
  }
}
```

**Cline/VS Code** (`settings.json`):
```json
{
  "mcpServers": {
    "dotnet-explorer": {
      "command": "dotnet",
      "args": [
        "run", 
        "--project", 
        "/path/to/MCPSharp/MCPSharp.csproj",
        "--",
        "--root-dir",
        "/home/user/projects"
      ]
    }
  }
}
```

---

## 🛠️ MCP Tools

### `class_explorer_find`
Search for types by name (case-insensitive).

```json
{
  "tool": "class_explorer_find",
  "arguments": {
    "projectPath": "MyApp/MyApp.csproj",
    "className": "UserService",
    "maxResults": 10
  }
}
```

### `class_explorer_analyze`
Deep analysis with dependency exploration.

```json
{
  "tool": "class_explorer_analyze",
  "arguments": {
    "projectPath": "MyApp/MyApp.csproj",
    "className": "OrderService",
    "analysisDepth": "full",
    "maxDepth": 3,
    "includeSystem": false,
    "includeMicrosoft": false,
    "excludeNamespaces": "MyApp.Tests,MyApp.Mocks",
    "excludePatterns": "Dto,Factory"
  }
}
```

### `class_explorer_hierarchy`
View inheritance tree.

```json
{
  "tool": "class_explorer_hierarchy",
  "arguments": {
    "projectPath": "MyApp/MyApp.csproj",
    "className": "OrderService",
    "includeSystemTypes": false
  }
}
```

### `class_explorer_dependencies`
Generate dependency graph.

```json
{
  "tool": "class_explorer_dependencies",
  "arguments": {
    "projectPath": "MyApp/MyApp.csproj",
    "className": "PaymentGateway",
    "includeSystem": false,
    "maxDepth": 2
  }
}
```

### `class_explorer_export`
Export analysis to `.cs` file.

```json
{
  "tool": "class_explorer_export",
  "arguments": {
    "projectPath": "MyApp/MyApp.csproj",
    "className": "ComplexService",
    "outputDirectory": "./analysis",
    "analysisDepth": "full"
  }
}
```

### `class_explorer_clear_cache`
Clear analysis cache.

```json
{
  "tool": "class_explorer_clear_cache",
  "arguments": {
    "projectPath": "all"
  }
}
```

---

## ⚙️ Configuration

### Path Resolution

| Path Type | Example | Resolution |
|-----------|---------|------------|
| **Absolute** | `/home/user/project/MyApp.csproj` | Used as-is |
| **Relative** | `MyApp/MyApp.csproj` | Concatenated with `--root-dir` |

### Analysis Depth Levels

| Level | Description |
|-------|-------------|
| `none` | Target class only |
| `hierarchy` | Base classes + interfaces |
| `signature` | Method parameters, return types, properties |
| `full` | Complete dependency graph (recursive) |

### Filter Configuration

```csharp
// Example: Strict filtering
{
  "includeSystem": false,        // Exclude System.*
  "includeMicrosoft": false,     // Exclude Microsoft.*
  "includeThirdParty": false,    // Exclude Newtonsoft, etc.
  "excludeNamespaces": "Tests,Mocks",
  "excludePatterns": "Dto,Factory,Helper"
}
```

---

## 📊 Real-World Examples

### Understanding a Legacy Codebase

> *"Show me all dependencies of the PaymentService class, excluding test mocks and DTOs"*

```json
{
  "tool": "class_explorer_analyze",
  "arguments": {
    "projectPath": "LegacyApp/LegacyApp.csproj",
    "className": "PaymentService",
    "analysisDepth": "full",
    "maxDepth": 3,
    "excludePatterns": "Mock,Dto,Stub"
  }
}
```

### Interface Implementation Audit

> *"List all classes implementing IRepository in the domain layer"*

```json
{
  "tool": "class_explorer_list_namespace",
  "arguments": {
    "projectPath": "Domain/Domain.csproj",
    "namespaceName": "MyApp.Domain.Repositories",
    "includeNested": true
  }
}
```

### Refactoring Impact Analysis

> *"What's the inheritance hierarchy of BaseController? Include framework types."*

```json
{
  "tool": "class_explorer_hierarchy",
  "arguments": {
    "projectPath": "WebApp/WebApp.csproj",
    "className": "MyApp.Web.Controllers.BaseController",
    "includeSystemTypes": true
  }
}
```

---

## 📋 Sample Output

### Example 1: Type Discovery (`class_explorer_find`)

**Input:**
```json
{
  "tool": "class_explorer_find",
  "arguments": {
    "projectPath": "ECommerce/ECommerce.csproj",
    "className": "Order",
    "maxResults": 5
  }
}
```

**Output to LLM:**
```
Found 3 type(s) matching 'Order':

1. [class] ECommerce.Domain.Models.Order (Assembly: ECommerce.Domain)
2. [class] ECommerce.Services.OrderProcessingService (Assembly: ECommerce.Services)
3. [enum] ECommerce.Domain.Enums.OrderStatus (Assembly: ECommerce.Domain)

Use 'class_explorer_analyze' with the full type name for detailed analysis.
```

---

### Example 2: Deep Analysis with Dependencies (`class_explorer_analyze`)

**Input:**
```json
{
  "tool": "class_explorer_analyze",
  "arguments": {
    "projectPath": "ECommerce/ECommerce.csproj",
    "className": "ECommerce.Services.OrderService",
    "analysisDepth": "full",
    "maxDepth": 2,
    "includeSystem": false,
    "excludePatterns": "Dto"
  }
}
```

**Output to LLM:**
```csharp
// Class Analysis Report
// Generated: 2024-01-15 14:32:18 UTC
// Project: /home/user/projects/ECommerce/ECommerce.csproj
// Target: ECommerce.Services.OrderService
// Dependencies Found: 4
// Filters: System:Excluded | Microsoft:Excluded | ThirdParty:Excluded | ExcludedPatterns:[Dto]

// ----------------------------------------------------------------------------
// DEPENDENCIES
// ----------------------------------------------------------------------------

// >>> ECommerce.Domain.Models.Order [ECommerce.Domain]
namespace ECommerce.Domain.Models
{
    public class Order
    {
        public int Id { get; set; }
        public decimal Total { get; set; }
        public OrderStatus Status { get; set; }
        public List<OrderItem> Items { get; set; }
        public Customer Customer { get; set; }
    }
}

// >>> ECommerce.Domain.Models.OrderItem [ECommerce.Domain]
namespace ECommerce.Domain.Models
{
    public class OrderItem
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}

// >>> ECommerce.Domain.Models.Customer [ECommerce.Domain]
namespace ECommerce.Domain.Models
{
    public class Customer
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public string Name { get; set; }
    }
}

// >>> ECommerce.Domain.Enums.OrderStatus [ECommerce.Domain]
namespace ECommerce.Domain.Enums
{
    public enum OrderStatus
    {
        Pending,
        Paid,
        Shipped,
        Delivered,
        Cancelled
    }
}

// ----------------------------------------------------------------------------
// PRIMARY CLASS
// ----------------------------------------------------------------------------

namespace ECommerce.Services
{
    public class OrderService
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IPaymentGateway _paymentGateway;
        private readonly IEmailService _emailService;
        
        public OrderService(IOrderRepository orderRepository, 
                           IPaymentGateway paymentGateway, 
                           IEmailService emailService) { }
        
        public Order CreateOrder(int customerId, List<OrderItem> items) { }
        public void ProcessPayment(int orderId, PaymentInfo payment) { }
        public void ShipOrder(int orderId) { }
        public void CancelOrder(int orderId) { }
        public Order GetOrder(int orderId) { }
    }
}

// ----------------------------------------------------------------------------
// RESUMO DE RELACIONAMENTOS
// ----------------------------------------------------------------------------
// ECommerce.Services.OrderService -> ECommerce.Domain.Models.Order [MethodReturn]
// ECommerce.Services.OrderService -> ECommerce.Domain.Models.OrderItem [MethodParameter]
// ECommerce.Services.OrderService -> ECommerce.Domain.Models.Customer [PropertyType]
// ECommerce.Services.OrderService -> ECommerce.Domain.Enums.OrderStatus [PropertyType]
```

---

### Example 3: Inheritance Hierarchy (`class_explorer_hierarchy`)

**Input:**
```json
{
  "tool": "class_explorer_hierarchy",
  "arguments": {
    "projectPath": "WebApp/WebApp.csproj",
    "className": "ECommerce.Web.Controllers.ApiController",
    "includeSystemTypes": true
  }
}
```

**Output to LLM:**
```
// Inheritance Hierarchy for: ECommerce.Web.Controllers.ApiController
// Project: /home/user/projects/WebApp/WebApp.csproj

└── ApiController (class)
  └── extends ControllerBase
    └── extends Controller
      └── extends Microsoft.AspNetCore.Mvc.ControllerBase
        └── implements IActionFilter
        └── implements IAsyncActionFilter
        └── implements IFilterMetadata
        └── implements IDisposable

// Related Types:
//   [interface] ECommerce.Web.Filters.IApiExceptionFilter
//   [interface] ECommerce.Web.Filters.IRequestValidator
//   [class] ECommerce.Web.Controllers.BaseController
```

---

### Example 4: Dependency Graph (`class_explorer_dependencies`)

**Input:**
```json
{
  "tool": "class_explorer_dependencies",
  "arguments": {
    "projectPath": "PaymentService/PaymentService.csproj",
    "className": "PaymentService.Core.PaymentProcessor",
    "includeSystem": false,
    "maxDepth": 2
  }
}
```

**Output to LLM:**
```
// Dependency Report for: PaymentService.Core.PaymentProcessor
// Project: /home/user/projects/PaymentService/PaymentService.csproj
// Analysis Depth: 2
// Total Dependencies: 6
// Filtered Out: System types excluded

// Inherits:
//   PaymentProcessor ───► BasePaymentProcessor

// Implements:
//   PaymentProcessor ────► IPaymentProcessor
//   PaymentProcessor ────► IDisposable

// MethodParameter:
//   PaymentProcessor ───► PaymentRequest
//      via: ProcessPayment
//   PaymentProcessor ───► PaymentMethod
//      via: ProcessPayment
//   PaymentProcessor ───► FraudCheckResult
//      via: ValidateTransaction

// MethodReturn:
//   PaymentProcessor ───► PaymentResult
//      via: ProcessPayment
//   PaymentProcessor ───► TransactionLog
//      via: LogTransaction

// PropertyType:
//   PaymentProcessor ───► ILogger<PaymentProcessor>
//      via: _logger
//   PaymentProcessor ───► IConfiguration
//      via: _configuration

// External Dependencies:
//   [class] PaymentService.Models.PaymentRequest (PaymentService.Models)
//   [class] PaymentService.Models.PaymentResult (PaymentService.Models)
//   [class] PaymentService.Models.PaymentMethod (PaymentService.Models)
//   [class] PaymentService.Models.FraudCheckResult (PaymentService.Models)
//   [class] PaymentService.Models.TransactionLog (PaymentService.Models)
//   [interface] PaymentService.Core.IPaymentProcessor (PaymentService.Core)
```

---

### Example 5: Namespace Listing (`class_explorer_list_namespace`)

**Input:**
```json
{
  "tool": "class_explorer_list_namespace",
  "arguments": {
    "projectPath": "ECommerce/ECommerce.csproj",
    "namespaceName": "ECommerce.Domain.Models",
    "includeNested": false
  }
}
```

**Output to LLM:**
```
// Types in namespace: ECommerce.Domain.Models
// Excluding nested namespaces
// Total: 8 type(s)

namespace ECommerce.Domain.Models
{
    public class Order;
    public class OrderItem;
    public class Customer;
    public class Product;
    public class Category;
    public class ShoppingCart;
    public class CartItem;
    public class Address;
}
```

---

## 🏗️ Architecture

```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│   AI Client     │◄───►│   MCP Protocol   │◄───►│  MCPSharp       │
│  (Claude/Cline) │     │    (STDIO)       │     │   (STDIO Host)  │
└─────────────────┘     └──────────────────┘     └────────┬────────┘
                                                         │
                              ┌─────────────────────────┼─────────────────────────┐
                              │                         │                         │
                              ▼                         ▼                         ▼
                    ┌─────────────────┐       ┌─────────────────┐       ┌─────────────────┐
                    │ TypeExplorer    │       │ Filter          │       │ Metadata        │
                    │ Service         │       │ Configuration   │       │ Load Context    │
                    │                 │       │                 │       │                 │
                    │ • Assembly Scan │       │ • System.*      │       │ • PEReader      │
                    │ • Type Resolution│      │ • Microsoft.*   │       │ • TypeResolver  │
                    │ • Dependency Map│       │ • Custom NS     │       │ • Signature     │
                    │                 │       │ • Patterns      │       │   Decoder       │
                    └─────────────────┘       └─────────────────┘       └─────────────────┘
```

---

## 🧪 Testing

```bash
# Run all tests
dotnet test

# Run with verbosity
dotnet test --logger "console;verbosity=detailed"

# Filter specific tests
dotnet test --filter "FullyQualifiedName~PathResolution"
```

---

## 🤝 Contributing

1. Fork the repository
2. Create feature branch (`git checkout -b feature/amazing-feature`)
3. Commit changes (`git commit -m 'Add amazing feature'`)
4. Push to branch (`git push origin feature/amazing-feature`)
5. Open Pull Request

---

## 📜 License

MIT License - see [LICENSE](LICENSE) file

---

<p align="center">
  <sub>Built with ❤️ for the .NET and AI communities</sub>
</p>

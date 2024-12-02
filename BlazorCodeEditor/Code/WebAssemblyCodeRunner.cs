using System.Diagnostics;
using System.Reflection;
using BlazorCodeEditor.Console;
using BlazorCodeEditor.Webcil;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace BlazorCodeEditor.Code;

public class WebAssemblyCodeRunner : ICodeExecutor
{
    private readonly ConsoleOutputService _consoleOutputService;
    private readonly IResourceResolver _resourceResolver;
    private readonly IWebAssemblyHostEnvironment _env;

    public WebAssemblyCodeRunner(ConsoleOutputService consoleOutputService, IResourceResolver resourceResolver, IWebAssemblyHostEnvironment env)
    {
        _consoleOutputService = consoleOutputService;
        _resourceResolver = resourceResolver;
        _env = env;
    }

    public async Task ExecuteAsync(string code, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // Parse syntax tree
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        _consoleOutputService.AddLog("Parsed syntax tree.", ConsoleSeverity.Debug);

        // Create references
        var references = new List<MetadataReference>
        {
            await GetMetadataReferenceAsync("assemblies/System.Private.CoreLib.dll"),
            await GetConsoleMetadataReferenceAsync(),
            await GetRuntimeMetadataReferenceAsync(),
            await GetCollectionsMetadataReferenceAsync(),
        };

        // Compile code
        stopwatch.Restart();
        var compilation = CSharpCompilation.Create(
            "InMemoryAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(
                OutputKind.ConsoleApplication,
                metadataImportOptions: MetadataImportOptions.All,
                allowUnsafe: true,
                reportSuppressedDiagnostics: true
            )
        );
        _consoleOutputService.AddLog($"Compilation completed in {stopwatch.ElapsedMilliseconds} ms.", ConsoleSeverity.Debug);

        // Log diagnostics
        foreach (var diagnostic in compilation.GetDiagnostics())
        {
            var severity = diagnostic.Severity switch
            {
                DiagnosticSeverity.Error => ConsoleSeverity.Error,
                DiagnosticSeverity.Warning => ConsoleSeverity.Warning,
                _ => ConsoleSeverity.Info
            };
            _consoleOutputService.AddLog(diagnostic.GetMessage(), severity);
        }

        // Emit assembly
        stopwatch.Restart();
        using var memoryStream = new MemoryStream();
        var emitResult = compilation.Emit(memoryStream);

        _consoleOutputService.AddLog($"Emit completed in {stopwatch.ElapsedMilliseconds} ms.", ConsoleSeverity.Debug);

        if (!emitResult.Success)
        {
            foreach (var diagnostic in emitResult.Diagnostics)
            {
                var severity = diagnostic.Severity switch
                {
                    DiagnosticSeverity.Error => ConsoleSeverity.Error,
                    DiagnosticSeverity.Warning => ConsoleSeverity.Warning,
                    _ => ConsoleSeverity.Info
                };
                _consoleOutputService.AddLog(diagnostic.GetMessage(), severity);
            }
            return;
        }

        // Load assembly
        memoryStream.Seek(0, SeekOrigin.Begin);
        var assembly = Assembly.Load(memoryStream.ToArray());
        _consoleOutputService.AddLog($"Assembly loaded: {assembly.FullName}", ConsoleSeverity.Debug);

        // Find entry point
        var entryPoint = assembly.EntryPoint;
        if (entryPoint == null)
        {
            _consoleOutputService.AddLog("No entry point found in the assembly.", ConsoleSeverity.Error);
            return;
        }

        // Invoke entry point
        try
        {
            stopwatch.Restart();
            var parameters = entryPoint.GetParameters();
            var invokeArgs = parameters.Length == 1 && parameters[0].ParameterType == typeof(string[])
                ? new object?[] { Array.Empty<string>() }
                : null;

            entryPoint.Invoke(null, invokeArgs);
            _consoleOutputService.AddLog($"Execution completed in {stopwatch.ElapsedMilliseconds} ms.", ConsoleSeverity.Debug);
        }
        catch (Exception ex)
        {
            var exceptionMessage = ex.InnerException != null
                ? $"Unhandled Exception: {ex.InnerException.Message}"
                : $"Unhandled Exception: {ex.Message}";
            _consoleOutputService.AddLog(exceptionMessage, ConsoleSeverity.Error);
        }
    }

    private async Task<PortableExecutableReference> GetMetadataReferenceAsync(string assemblyPath)
    {
        var httpClient = CreateHttpClient();
        await using var stream = await httpClient.GetStreamAsync(await ResolveResourceStreamUri("System.Private.CoreLib.wasm")); //"fk089ohxy8.wasm");
        var peBytes = WebcilConverterUtil.ConvertFromWebcil(stream);

        using var peStream = new MemoryStream(peBytes);
        return MetadataReference.CreateFromStream(peStream);
    }

    private async Task<string> ResolveResourceStreamUri(string resource)
    {
        var resolved = await _resourceResolver.ResolveResource(resource);
        return $"/_framework/{resolved}";
    }

    private async Task<PortableExecutableReference> GetConsoleMetadataReferenceAsync()
    {
        var httpClient = CreateHttpClient();
        await using var stream =
            await httpClient.GetStreamAsync(await ResolveResourceStreamUri("System.Console.wasm")); // "/_framework/System.Console.wasm"); //".8gya5re9cq.wasm");
        var peBytes = WebcilConverterUtil.ConvertFromWebcil(stream);

        using var peStream = new MemoryStream(peBytes);
        return MetadataReference.CreateFromStream(peStream);
    }

    private async Task<PortableExecutableReference> GetRuntimeMetadataReferenceAsync()
    {
        var httpClient = CreateHttpClient();
        await using var stream = await httpClient.GetStreamAsync(await ResolveResourceStreamUri("System.Runtime.wasm")); //47lunemxsj.wasm");
        var peBytes = WebcilConverterUtil.ConvertFromWebcil(stream);

        using var peStream = new MemoryStream(peBytes);
        return MetadataReference.CreateFromStream(peStream);
    }

    private async Task<PortableExecutableReference> GetCollectionsMetadataReferenceAsync()
    {
        var httpClient = CreateHttpClient();
        await using var stream = await httpClient.GetStreamAsync(await ResolveResourceStreamUri("System.Collections.wasm")); //".uiz1v0ys5y.wasm");
        var peBytes = WebcilConverterUtil.ConvertFromWebcil(stream);

        using var peStream = new MemoryStream(peBytes);
        return MetadataReference.CreateFromStream(peStream);
    }
    
    private HttpClient CreateHttpClient()
    {
        var isDevelopment = _env.IsDevelopment();
        var baseAddress = isDevelopment
            ? "https://localhost:7158" // Local development
            : "https://blazor-code-editor.azurewebsites.net"; // Deployed environment

        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseAddress)
        };

        return httpClient;
    }
}

public interface ICodeExecutor
{
    public Task ExecuteAsync(string code, CancellationToken cancellationToken = default);
}
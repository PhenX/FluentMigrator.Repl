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

        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        _consoleOutputService.AddLog("Parsed syntax tree.", ConsoleSeverity.Debug);

        var references = new List<MetadataReference>
        {
            await GetMetadataReferenceAsync("System.Private.CoreLib.wasm"),
            await GetMetadataReferenceAsync("System.Runtime.wasm"),
            await GetMetadataReferenceAsync("System.Console.wasm"),
            await GetMetadataReferenceAsync("System.Collections.wasm"),
        };

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

        memoryStream.Seek(0, SeekOrigin.Begin);
        var assembly = Assembly.Load(memoryStream.ToArray());
        _consoleOutputService.AddLog($"Assembly loaded: {assembly.FullName}", ConsoleSeverity.Debug);

        var entryPoint = assembly.EntryPoint;
        if (entryPoint == null)
        {
            _consoleOutputService.AddLog("No entry point found in the assembly.", ConsoleSeverity.Error);
            return;
        }

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
    
    private async Task<string> ResolveResourceStreamUri(string resource)
    {
        var resolved = await _resourceResolver.ResolveResource(resource);
        return $"/_framework/{resolved}";
    }

    private async Task<PortableExecutableReference> GetMetadataReferenceAsync(string wasmModule)
    {
        var httpClient = CreateHttpClient();
        await using var stream = await httpClient.GetStreamAsync(await ResolveResourceStreamUri(wasmModule));
        var peBytes = WebcilConverterUtil.ConvertFromWebcil(stream);

        using var peStream = new MemoryStream(peBytes);
        return MetadataReference.CreateFromStream(peStream);
    }
    
    private HttpClient CreateHttpClient()
    {
        var isDevelopment = _env.IsDevelopment();
        var baseAddress = isDevelopment
            ? "https://localhost:7158" 
            : "https://blazor-code-editor.azurewebsites.net"; 

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
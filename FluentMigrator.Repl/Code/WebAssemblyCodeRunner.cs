using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentMigrator.Repl.Console;
using FluentMigrator.Repl.Webcil;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FluentMigrator.Repl.Code;

public class WebAssemblyCodeRunner : ICodeExecutor
{
    private readonly ConsoleOutputService _consoleOutputService;
    private readonly IResourceResolver _resourceResolver;
    private readonly IBlazorHttpClientFactory _httpClientFactory;

    public WebAssemblyCodeRunner(ConsoleOutputService consoleOutputService, IResourceResolver resourceResolver, IBlazorHttpClientFactory httpClientFactory)
    {
        _consoleOutputService = consoleOutputService;
        _resourceResolver = resourceResolver;
        _httpClientFactory = httpClientFactory;
    }

    public async Task ExecuteAsync(string code, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        _consoleOutputService.AddLog("Parsed syntax tree.", ConsoleSeverity.Debug);

        var references = new List<MetadataReference>
        {
            await GetMetadataReferenceAsync("netstandard.wasm"),
            
            await GetMetadataReferenceAsync("System.wasm"),
            await GetMetadataReferenceAsync("System.Collections.wasm"),
            await GetMetadataReferenceAsync("System.ComponentModel.Primitives.wasm"),
            await GetMetadataReferenceAsync("System.ComponentModel.TypeConverter.wasm"),
            await GetMetadataReferenceAsync("System.ComponentModel.wasm"),
            await GetMetadataReferenceAsync("System.Console.wasm"),
            await GetMetadataReferenceAsync("System.Data.Common.wasm"),
            await GetMetadataReferenceAsync("System.Linq.wasm"),
            await GetMetadataReferenceAsync("System.Private.CoreLib.wasm"),
            await GetMetadataReferenceAsync("System.Runtime.wasm"),
            
            await GetMetadataReferenceAsync("Microsoft.Data.Sqlite.wasm"),
            await GetMetadataReferenceAsync("Microsoft.Extensions.DependencyInjection.Abstractions.wasm"),
            await GetMetadataReferenceAsync("Microsoft.Extensions.DependencyInjection.wasm"),
            await GetMetadataReferenceAsync("Microsoft.Extensions.Logging.Abstractions.wasm"),
            await GetMetadataReferenceAsync("Microsoft.Extensions.Logging.wasm"),
            await GetMetadataReferenceAsync("Microsoft.Extensions.Options.wasm"),
            
            await GetMetadataReferenceAsync("FluentMigrator.Abstractions.wasm"),
            await GetMetadataReferenceAsync("FluentMigrator.Runner.Core.wasm"),
            await GetMetadataReferenceAsync("FluentMigrator.Runner.SQLite.wasm"),
            await GetMetadataReferenceAsync("FluentMigrator.wasm"),
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
                reportSuppressedDiagnostics: true,
                optimizationLevel:OptimizationLevel.Debug,
                generalDiagnosticOption:ReportDiagnostic.Error
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
                ? $"Unhandled Exception: {ex.Message} ({ex.InnerException.Message})"
                : $"Unhandled Exception: {ex.Message}";

            if (ex.InnerException is PlatformNotSupportedException pns)
            {
                _consoleOutputService.AddLog(pns.GetType().Name, ConsoleSeverity.Error);
            }
            
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
        var httpClient = await _httpClientFactory.CreateHttpClient();
        await using var stream = await httpClient.GetStreamAsync(await ResolveResourceStreamUri(wasmModule));
        var peBytes = WebcilConverterUtil.ConvertFromWebcil(stream);

        using var peStream = new MemoryStream(peBytes);
        return MetadataReference.CreateFromStream(peStream);
    }
}

public interface ICodeExecutor
{
    public Task ExecuteAsync(string code, CancellationToken cancellationToken = default);
}
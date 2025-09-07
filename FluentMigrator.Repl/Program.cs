using System;
using System.Net.Http;
using Blazor.Extensions.Logging;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using FluentMigrator.Repl;
using FluentMigrator.Repl.Code;
using FluentMigrator.Repl.Console;
using Microsoft.Extensions.DependencyInjection;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddLogging(c => c.AddBrowserConsole());

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddSingleton<ICodeExecutor, WebAssemblyCodeRunner > ();
builder.Services.AddSingleton<ConsoleOutputService>();
builder.Services.AddSingleton<IResourceResolver, ResourceResolver>();
builder.Services.AddSingleton<IBlazorHttpClientFactory, BlazorHttpClientFactory>();

await builder.Build().RunAsync();
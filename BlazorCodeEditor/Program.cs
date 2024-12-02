using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BlazorCodeEditor;
using BlazorCodeEditor.Code;
using BlazorCodeEditor.Console;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddSingleton<ICodeExecutor, WebAssemblyCodeRunner > ();
builder.Services.AddSingleton<ConsoleOutputService>();
builder.Services.AddSingleton<IResourceResolver, ResourceResolver>();

await builder.Build().RunAsync();
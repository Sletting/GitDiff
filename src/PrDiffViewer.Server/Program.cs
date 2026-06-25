using PrDiffViewer.Server.Api;
using PrDiffViewer.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<GitDiffService>();

var app = builder.Build();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.MapGitApi(includeErrorDetail: app.Environment.IsDevelopment());

app.MapFallbackToFile("index.html");

app.Run();

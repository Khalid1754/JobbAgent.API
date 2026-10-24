using JobbAgent.Api.Agents;
using JobbAgent.Api.Data;
using JobbAgent.Api.Endpoints;
using JobbAgent.Api.Models;
using JobbAgent.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<JobbAgentDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("JobbAgent"), sql => sql.EnableRetryOnFailure()));
builder.Services.ConfigureHttpJsonOptions(o => ApiJson.Configure(o.SerializerOptions));

builder.Services.AddHttpClient<GroqLlm>(c => c.Timeout = TimeSpan.FromMinutes(3));
builder.Services.AddSingleton<ILlm>(sp => sp.GetRequiredService<GroqLlm>());

const string userAgent = "JobbAgent/1.0 (https://github.com/Khalid1754/JobbAgent.API)";
builder.Services.AddHttpClient<PlatsbankenClient>(c =>
{
    c.Timeout = TimeSpan.FromSeconds(20);
    c.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
});
builder.Services.AddSingleton<IJobBoard>(sp => sp.GetRequiredService<PlatsbankenClient>());
// Vissa jobbsajter visar bara sidan för vanliga webbläsare, så vi presenterar oss som en.
builder.Services.AddHttpClient<AdReader>(c =>
{
    c.Timeout = TimeSpan.FromSeconds(20);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) " + userAgent);
    c.DefaultRequestHeaders.AcceptLanguage.ParseAdd("sv-SE,sv;q=0.9,en;q=0.8");
});

builder.Services.AddSingleton<Searcher>();
builder.Services.AddSingleton<Interpreter>();
builder.Services.AddSingleton<Matcher>();
builder.Services.AddSingleton<Writer>();
builder.Services.AddSingleton<Reviewer>();
builder.Services.AddSingleton<Coach>();
builder.Services.AddSingleton<RunEvents>();
builder.Services.AddSingleton<RunQueue>();
builder.Services.AddSingleton<RunOrchestrator>();
builder.Services.AddHostedService<RunWorker>();
builder.Services.AddSingleton<WatchScheduler>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<WatchScheduler>());

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"])
     .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

await DatabaseStartup.MigrateAsync(app.Services, app.Logger);

app.UseExceptionHandler(handler => handler.Run(async context =>
{
    context.Response.StatusCode = 500;
    await context.Response.WriteAsJsonAsync(new { error = "Något gick fel på servern." });
}));
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();

app.MapCvEndpoints();
app.MapRunEndpoints();
app.MapApplicationEndpoints();
app.MapWatchEndpoints();
app.MapFallbackToFile("index.html");

app.Run();

// Gör Program synlig för testerna (WebApplicationFactory).
public partial class Program;

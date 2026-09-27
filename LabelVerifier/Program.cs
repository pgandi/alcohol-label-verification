using LabelVerifier.Components;
using LabelVerifier.Services;

var builder = WebApplication.CreateBuilder(args);

// Blazor Web App with interactive server components.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// --- Label verification services -----------------------------------------
// Bind the "Llm" configuration section (provider, model, keys). Keys come from
// environment variables or user-secrets - never committed.
builder.Services.Configure<LlmOptions>(builder.Configuration.GetSection(LlmOptions.SectionName));

// The extractor is chosen from config at startup. Falls back to demo mode if a
// provider is selected but its key/endpoint is missing, so the app always runs.
builder.Services.AddSingleton<ILabelExtractor>(sp =>
{
    var options = new LlmOptions();
    builder.Configuration.GetSection(LlmOptions.SectionName).Bind(options);
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    return LabelExtractorFactory.Create(options, loggerFactory);
});

builder.Services.AddSingleton<VerificationService>();
builder.Services.AddScoped<LabelVerificationService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// Honor forwarded headers so HTTPS redirect / hosting behind a proxy (Render) works.
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

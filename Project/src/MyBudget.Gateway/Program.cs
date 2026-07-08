var builder = WebApplication.CreateBuilder(args);

// YARP reverse proxy — routes loaded from appsettings.json ReverseProxy section
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.MapReverseProxy();

await app.RunAsync();

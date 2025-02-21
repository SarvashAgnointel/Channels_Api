
using Serilog;
using System.Net;
using Microsoft.Extensions.Logging;
using log4net;
using log4net.Config;
using Serilog.Filters;
using DBAccess;
using Inetlab.SMPP.Logging;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(System.Net.IPAddress.Parse("0.0.0.0"), 5000);  // Replace with your IP address and port
});


builder.Services.AddSingleton<SmppClientService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IDbHandler, DbHandler>(); // If not already registered

var allowedOrigins = builder.Configuration.GetSection("CorsSettings:AllowedOrigins").Get<List<string>>();

//builder.Services.AddCors(options =>
//{
//    options.AddPolicy("AllowSpecificOrigin",
//        builder => builder.WithOrigins(allowedOrigins.ToArray())
//                          .AllowAnyHeader()
//                          .AllowAnyMethod()
//                          .AllowCredentials());
//});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin",
        builder => builder.WithOrigins("http://192.168.8.240:3000",
                                       "http://localhost:3000",
                                       "http://18.196.60.242:3000",
                                       "http://3.76.247.193:5000",
                                       "http://3.68.199.69:5000")
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials());
});


// ✅ FIX: Serilog should be configured BEFORE `builder.Host.UseSerilog();`
string baseLogDirectory = @"C:\Channel_API\Logs";
string[] controllers = { "MessageController" };

// Ensure base directories exist for all controllers
foreach (var controller in controllers)
{
    string controllerLogDirectory = Path.Combine(baseLogDirectory, controller);
    if (!Directory.Exists(controllerLogDirectory))
    {
        Directory.CreateDirectory(controllerLogDirectory);
    }
}

// ✅ FIX: Remove "Controller" from the filter
var loggerConfig = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ApplicationName", "Channels_Api")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}{Exception}");

// ✅ FIX: Add general log file (for debugging)
loggerConfig.WriteTo.File(
    @"C:\Channel_API\Logs\general.log",
    rollingInterval: RollingInterval.Day,
    retainedFileCountLimit: 90,
    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}{Exception}"
);

// ✅ FIX: Correct filtering condition
foreach (var controller in controllers)
{
    string controllerLogPath = Path.Combine(baseLogDirectory, controller, $"{controller}-.log");
    loggerConfig.WriteTo.Logger(lc => lc
        .Filter.ByIncludingOnly(Matching.WithProperty<string>("SourceContext", sc => sc.Contains(controller))) // ✅ FIXED
        .WriteTo.File(controllerLogPath,
                      rollingInterval: RollingInterval.Day,
                      retainedFileCountLimit: 90,
                      outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    );
}

// ✅ Set global Serilog instance
Log.Logger = loggerConfig.CreateLogger();

// ✅ Attach Serilog to the ASP.NET Core logging system BEFORE building the app
builder.Host.UseSerilog(Log.Logger);

// ✅ Build the app AFTER configuring logging
var app = builder.Build();



// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors("AllowSpecificOrigin");
// Use CORS middleware
app.UseCors("AllowAll");
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok("Healthy"));

app.Run();

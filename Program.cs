using DBAccess;

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

builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);

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

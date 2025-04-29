using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using SCIABackendDemo.Hubs;  
using SCIABackendDemo.Services;
using SCIABackendDemo.Configuration;
using Twilio;  


var builder = WebApplication.CreateBuilder(args);

//Configurar peticiones CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirFrontend", builder =>
    {
        builder.WithOrigins("https://scia-front.nelperecas.com")
               .AllowAnyHeader()
               .AllowAnyMethod()
               .AllowCredentials();
    });
});


// Configurar Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/callia-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Agregar servicios al contenedor.
builder.Services.AddControllers();
builder.Services.AddSignalR(); // Registrar SignalR

builder.Services.Configure<UltravoxOptions>(
    builder.Configuration.GetSection("Ultravox"));

builder.Services.AddSingleton<PromptService>();

// Registro de UltravoxService e HttpClient
builder.Services.AddHttpClient<UltravoxService>();

TwilioClient.Init(
    builder.Configuration["Twilio:AccountSid"],
    builder.Configuration["Twilio:AuthToken"]
);

var app = builder.Build();

// Después de app.UseRouting();
app.UseCors("PermitirFrontend");
// Configurar el middleware para la solicitud HTTP
app.UseSerilogRequestLogging();

app.UseRouting();
app.UseAuthorization();

// Mapear controladores y SignalR Hub
app.MapControllers();
app.MapHub<LogHub>("/loghub");


app.Run();

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using SCIABackendDemo.Hubs;  
using SCIABackendDemo.Services;
using SCIABackendDemo.Configuration;
using Twilio;  
using Microsoft.EntityFrameworkCore;
using SCIABackendDemo.Data;
using SCIABackendDemo.Models;
using Hangfire;
using Hangfire.MySql;



var builder = WebApplication.CreateBuilder(args);

//Configurar peticiones CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirFrontend", builder =>
    {
        builder.WithOrigins("https://scia-front.nelperecas.com",
            "http://localhost:5189" )// ← agrega esta línea temporalmente)
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

// Registrar DbContext con EF Core y Mysql Pomelo
builder.Services.AddDbContext<SellerCallDbContext>(options =>
    options.UseMySQL(builder.Configuration.GetConnectionString("DefaultConnection"))
);


// Registro de UltravoxService e HttpClient
builder.Services.AddHttpClient<UltravoxService>();

//Registrador de llamadas
builder.Services.AddScoped<LlamadaService>();

//Llamadas programadas
builder.Services.AddHostedService<ScheduledCallExecutor>();

//Libreria para llamadas automaticas
builder.Services.AddHangfire(config =>
    config.UseStorage(new MySqlStorage(
    builder.Configuration.GetConnectionString("DefaultConnection"),
    new MySqlStorageOptions()
)));

builder.Services.AddHangfireServer();



TwilioClient.Init(
    builder.Configuration["Twilio:AccountSid"],
    builder.Configuration["Twilio:AuthToken"]
);

var app = builder.Build();

app.UseRouting();
// Después de app.UseRouting();
app.UseCors("PermitirFrontend");
// Configurar el middleware para la solicitud HTTP
app.UseSerilogRequestLogging();


app.UseAuthorization();

// Mapear controladores y SignalR Hub
app.MapControllers();
app.MapHub<LogHub>("/loghub");
app.MapHub<CallHub>("/callhub");


app.UseHangfireDashboard(); // Opcional: acceso al panel

RecurringJob.AddOrUpdate<LlamadaService>(
    "verificar-llamadas-programadas",
    service => service.VerificarYDispararLlamadasAsync(),
    Cron.Minutely); // Ejecutar cada minuto

app.Run();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SellerCallDbContext>();

    if (!db.Users.Any())
    {
        db.Users.Add(new User
        {
            Email = "admin@sellercall.com",
            Nombre = "Admin",
            Telefono = "+13512082523"
        });
        db.SaveChanges();
    }
}

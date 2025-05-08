using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SCIABackendDemo.Data;
using SCIABackendDemo.Services;
using SCIABackendDemo.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

public class ScheduledCallExecutor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(2); // cada 2 minutos

    public ScheduledCallExecutor(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SellerCallDbContext>();
                var llamadaService = scope.ServiceProvider.GetRequiredService<LlamadaService>();
                var ultravox = scope.ServiceProvider.GetRequiredService<UltravoxService>();

                var now = DateTime.UtcNow;

                var pendientes = await db.ScheduledCalls
                    .Where(c => !c.Triggered && c.ScheduledDate <= now)
                    .ToListAsync();

                foreach (var sc in pendientes)
                {
                    try
                    {
                        var (callId, joinUrl) = await ultravox.CreateIncomingSipCallAsync();

                        // Aquí lanzarías la llamada con Twilio (como en CallTo)
                        // Puedes mejorar esta parte reutilizando lógica común

                        sc.Triggered = true;
                        db.Update(sc);
                        await db.SaveChangesAsync();
                        Log.Information("Llamada programada ejecutada: {CallId}", callId);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error ejecutando llamada programada Id={Id}", sc.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error general en ScheduledCallExecutor");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }
}

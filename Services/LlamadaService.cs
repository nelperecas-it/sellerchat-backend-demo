using SCIABackendDemo.Data;
using SCIABackendDemo.Models;
using Serilog;
using SCIABackendDemo.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;
using Microsoft.Extensions.Configuration;
using SCIABackendDemo.Hubs; 



namespace SCIABackendDemo.Services
{
    public class LlamadaService
    {
        private readonly SellerCallDbContext _db;
        private readonly UltravoxService _ultravoxService;
        private readonly IConfiguration _configuration;
        private readonly IHubContext<CallHub> _callHubContext;

        public LlamadaService(
            SellerCallDbContext db,
            UltravoxService ultravoxService,
            IConfiguration configuration,
            IHubContext<CallHub> callHubContext)
        {
            _db = db;
            _ultravoxService = ultravoxService;
            _configuration = configuration;
            _callHubContext = callHubContext;
        }


        public async Task GuardarHistorialLlamada(
            int userId,
            string callId,
            DateTime startedAt,
            DateTime? endedAt,
            string? resumen,
            string? resumenCorto,
            string? from,
            string? to,
            string? direction,
            bool isActive) 
        {
            var llamada = new CallHistory
            {
                UserId = userId,
                CallId = callId,
                StartedAt = startedAt,
                EndedAt = endedAt,
                Summary = resumen,
                ShortSummary = resumenCorto,
                From = from,
                To = to,
                Direction = direction,
                IsActive = isActive
            };

            _db.CallHistories.Add(llamada);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error guardando historial de llamada para CallId={CallId}", callId);
                throw;
            }

            
        }

         // Enviar toda la información de la llamada a SignalR
        public async Task SendCallInfoToFrontend(CallHistory callHistory)
        {
            // Crear un objeto con la información de la llamada
            var callInfo = new
            {
                CallId = callHistory.CallId,
                From = callHistory.From,
                To = callHistory.To,
                Direction = callHistory.Direction,
                StartedAt = callHistory.StartedAt,
                IsActive = callHistory.IsActive,
                Summary = callHistory.Summary,  // O cualquier otro campo que quieras mostrar
                ShortSummary = callHistory.ShortSummary
            };

            // Enviar el objeto completo a SignalR
            await _callHubContext.Clients.All.SendAsync("ReceiveCallInfo", callInfo);
        }

        public async Task VerificarYDispararLlamadasAsync()
        {
            var ahora = DateTime.UtcNow;
            var llamadas = await _db.ScheduledCalls
                .Where(c => !c.Triggered && c.ScheduledDate <= ahora)
                .ToListAsync();

            foreach (var llamada in llamadas)
            {
                try
                {
                    await EjecutarLlamadaAsync(
                        userId: llamada.UserId,
                        phone: llamada.PhoneNumber,
                        triggered: true,
                        originalCallId: llamada.CallId);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error ejecutando llamada programada Id={Id}", llamada.Id);
                }
            }
        }


        public async Task EjecutarLlamadaAsync(int userId, string phone, bool triggered = false, string? originalCallId = null)
        {
            (string callId, string joinUrl) = await _ultravoxService.CreateIncomingSipCallAsync();


            var twimlXml = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
                <Response>
                <Dial callerId=""{_configuration["Twilio:PhoneNumber"]}""
                    action=""https://scia-back.nelperecas.com/api/llamada/ended""
                    method=""POST"">
                    <Sip>{joinUrl}</Sip>
                </Dial>
                </Response>";

            var twiml = new Twilio.Types.Twiml(twimlXml);

            var call = await CallResource.CreateAsync(
                to: new PhoneNumber(phone),
                from: new PhoneNumber(_configuration["Twilio:PhoneNumber"]),
                twiml: twiml
            );

            _db.CallMappings.Add(new CallMappingEntry
            {
                CallId = callId,
                CallSid = call.Sid,
                Direction = "outbound-api"
            });

            

            await GuardarHistorialLlamada(
                userId: userId,
                callId: callId,
                startedAt: DateTime.UtcNow,
                endedAt: null,
                resumen: null,
                resumenCorto: null,
                from: _configuration["Twilio:PhoneNumber"],
                to: phone,
                direction: "outbound-api",
                isActive: true
            );

             // Enviar la información completa de la llamada a SignalR para el frontend
            var callHistory = await _db.CallHistories
                .FirstOrDefaultAsync(c => c.CallId == callId);
            if (callHistory != null)
            {
                // Enviar la información completa de la llamada al frontend
                await SendCallInfoToFrontend(callHistory);  // Aquí es donde se envía toda la información
            }

            if (triggered && originalCallId != null)
            {
                var scheduled = await _db.ScheduledCalls
                    .FirstOrDefaultAsync(s => s.CallId == originalCallId && !s.Triggered);

                if (scheduled != null)
                {
                    scheduled.Triggered = true;
                    _db.Update(scheduled);
                    await _db.SaveChangesAsync();
                }
            }
        }
        public async Task GuardarLlamadaAutomaticaSiCorresponde(int userId, string callId, string phone, string direction)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null || !user.AutoTriggerEnabled || user.AutoTriggerDays <= 0)
                return;

            // Verificamos que no haya otra ya programada
            var existe = await _db.ScheduledCalls.AnyAsync(x =>
                x.UserId == userId &&
                x.PhoneNumber == phone && 
                x.AutoGenerated &&
                !x.Triggered &&
                x.ScheduledDate > DateTime.UtcNow);

            if (existe)
                return;

            var scheduledDate = DateTime.UtcNow.AddDays(user.AutoTriggerDays);

            _db.ScheduledCalls.Add(new ScheduledCall
            {
                CallId = callId,
                PhoneNumber = phone,
                ScheduledDate = scheduledDate,
                Triggered = false,
                AutoGenerated = true,
                UserId = userId
            });

            await _db.SaveChangesAsync();
        }


    }
}

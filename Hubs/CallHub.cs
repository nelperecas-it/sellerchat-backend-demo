using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace SCIABackendDemo.Hubs
{
    public class CallHub : Hub
    {
        // Método opcional si quieres permitir que los clientes se unan a grupos
        public async Task Join(string callId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, callId);
        }

        // Método que tú vas a llamar desde el backend para notificar cambios
        public async Task BroadcastCallStatus(string callId, bool isActive)
        {
            await Clients.All.SendAsync("ReceiveCallStatus", callId, isActive);
        }
    }
}

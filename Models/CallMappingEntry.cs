//Modelo de Mapeo de llamadas Twilio+Ultravox

namespace SCIABackendDemo.Models
{
    public class CallMappingEntry
    {
        public int Id { get; set; }              // clave primaria
        public string CallId { get; set; } = null!;
        public string CallSid { get; set; } = null!;
        public string Direction { get; set; } = null!; // "inbound" o "outbound"
    }
}

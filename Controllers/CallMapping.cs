namespace SCIABackendDemo.Controllers
{
    public static class CallMapping
    {
        // callId → Twilio CallSid
        public static readonly Dictionary<string, string> InboundMap  = new();
        public static readonly Dictionary<string, string> OutboundMap = new();
    }
}

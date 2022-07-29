using System.Diagnostics;

namespace ChatSample.Telemetry
{
    public static class Common
    {
        public static readonly ActivitySource ActivitySource = new(TelemetryConstants.ServiceName);
    }
}

using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using System.Text.Json;
using System.Diagnostics;
using ChatSample.Telemetry;
using System;
using System.Collections.Generic;
using OpenTelemetry.Context.Propagation;

namespace ChatSample.Hubs
{
    public class ChatHub : Hub
    {
        // traceparent is called differently in .Net vs JS. This is the .Net name
        private string DotNetTraceparentKey = "TraceParent";
        // traceparent is called differently in .Net vs JS. This is the JS name
        private string JavaScriptTraceparentKey = "traceparent";
        private TextMapPropagator propagator = new TraceContextPropagator();

        public async Task Send(string name, string message, string rawContext)
        {
            var remoteContext = toRemoteContext(rawContext);
            using var consumerActivity = Common.ActivitySource.StartActivity("receiveMessage", ActivityKind.Consumer, remoteContext.ActivityContext);
            // Call the broadcastMessage method to update clients.

            // Here some processing would happen in real life before starting a new Activity (e.g. persisting the message)
            using (var producerActivity = Common.ActivitySource.StartActivity("broadcastMessage", ActivityKind.Producer))
            {
                var currentActivity = Activity.Current;
                var contextToPropagate = toSendableString(currentActivity);

                await Clients.All.SendAsync("broadcastMessage", name, message, contextToPropagate);
            }
        }

        private PropagationContext toRemoteContext(string rawContext)
        {
            Dictionary<string, string> dictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(rawContext);
            if(dictionary.ContainsKey(JavaScriptTraceparentKey))
            {
                dictionary.Add(DotNetTraceparentKey, dictionary.GetValueOrDefault(JavaScriptTraceparentKey));
            }
            var context = propagator.Extract(default, dictionary, (dictionary, name) =>
                {
                    if (dictionary.TryGetValue(name, out var value))
                    {
                        return new[] { value };
                    }

                    return Array.Empty<string>();
                });
            return context;
        }

        private string toSendableString(Activity activity)
        {
            Dictionary<string, string> dictionary = new Dictionary<string, string>();
            propagator.Inject(
                new PropagationContext(
                    new ActivityContext(
                        activity.TraceId,
                        activity.SpanId,
                        activity.ActivityTraceFlags,
                        activity.TraceStateString
                    ), default
                ), dictionary, (dictionary, name, value) =>
                {
                    dictionary[name] = value;
                });
            if (dictionary.ContainsKey(DotNetTraceparentKey))
            {
                dictionary[JavaScriptTraceparentKey] = dictionary[DotNetTraceparentKey];
                dictionary.Remove(DotNetTraceparentKey);
            }
            return JsonSerializer.Serialize(dictionary);
        }
    }
}
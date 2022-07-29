const signalR = require("@microsoft/signalr");
const opentelemetry = require("@opentelemetry/api");
const { Resource } = require("@opentelemetry/resources");
const { SemanticResourceAttributes } = require("@opentelemetry/semantic-conventions");
const { WebTracerProvider } = require("@opentelemetry/sdk-trace-web");
const { BatchSpanProcessor } = require("@opentelemetry/sdk-trace-base");
const { OTLPTraceExporter } = require('@opentelemetry/exporter-trace-otlp-http');
const { AlwaysOnSampler, W3CTraceContextPropagator } = require("@opentelemetry/core");
const api = require('@opentelemetry/api');

const tracer = setupOpenTelemetry();
const SpanKind = {
    INTERNAL: 0,
    SERVER: 1,
    CLIENT: 2,
    PRODUCER: 3,
    CONSUMER: 4
}

document.addEventListener('DOMContentLoaded', function () {
    var messageInput = document.getElementById('message');
    // Get the user name and store it to prepend to messages.
    var name = prompt('Enter your name:', '');
    // Set initial focus to message input box.
    messageInput.focus();
    // Start the connection.
    var connection = new signalR.HubConnectionBuilder()
                        .withUrl('/chat')
                        .build();
    // Create a function that the hub can call to broadcast messages.
    connection.on('broadcastMessage', function (name, message, propagatedTraceContext) {
        const context = api.propagation.extract(api.context.active(), JSON.parse(propagatedTraceContext));
        tracer.startActiveSpan('processBroadcastMessage', { kind: SpanKind.CONSUMER, }, context, span => {
            // Html encode display name and message.
            var encodedName = name;
            var encodedMsg = message;
            // Add the message to the page.
            var liElement = document.createElement('li');
            liElement.innerHTML = '<strong>' + encodedName + '</strong>:&nbsp;&nbsp;' + encodedMsg;
            document.getElementById('discussion').appendChild(liElement);
          
            // Be sure to end the span!
            span.end();
        });
    });
    // Transport fallback functionality is now built into start.
    connection.start()
        .then(function () {
            console.log('connection started');
            document.getElementById('sendMessage').addEventListener('click', function (event) {
                
                tracer.startActiveSpan('sendMessage', { kind: SpanKind.PRODUCER, root: true }, span => {
                    const contextToPropagate = {};
                    api.propagation.inject(api.context.active(), contextToPropagate);
                    const stringifiedContext = JSON.stringify(contextToPropagate);
                    // Call the Send method on the hub.
                    const sendMessageResult = connection.invoke('send', name, messageInput.value, stringifiedContext);
                    // Clear text box and reset focus for next comment.
                    messageInput.value = '';
                    messageInput.focus();
                    event.preventDefault();

                    // Be sure to end the span!
                    sendMessageResult.finally(_=>span.end());
                });
            });
    })
    .catch(error => {
        console.error(error.message);
    });
});

function setupOpenTelemetry() {
    const resource =
    Resource.default().merge(
        new Resource({
        [SemanticResourceAttributes.SERVICE_NAME]: "com.dynatrace.samplechat",
        [SemanticResourceAttributes.SERVICE_VERSION]: "0.1.0",
        })
    );

    const provider = new WebTracerProvider({
        resource: resource,
        sampler: new AlwaysOnSampler(),
    });
    const collectorOptions = {
        // send traces to collector
        url: "http://localhost:4317/v1/traces",
    };
    const exporter = new OTLPTraceExporter(collectorOptions);
    const processor = new BatchSpanProcessor(exporter);
    provider.addSpanProcessor(processor);

    provider.register({propagator: new W3CTraceContextPropagator()});

    const tracer = opentelemetry.trace.getTracer(
        'com.dynatrace.samplechat'
    );
    return tracer;
}

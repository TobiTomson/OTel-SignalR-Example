using ChatSample.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ChatSample.Telemetry;
using System.Collections.Generic;
using System;

namespace ChatSample
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSignalR();

            List<KeyValuePair<string, object>> dt_metadata = new List<KeyValuePair<string, object>>();
            foreach (string name in new string[] { "dt_metadata_e617c525669e072eebe3d0f08212e8f2.properties", "/var/lib/dynatrace/enrichment/dt_metadata.properties" })
            {
                try
                {
                    foreach (string line in System.IO.File.ReadAllLines(name.StartsWith("/var") ? name : System.IO.File.ReadAllText(name)))
                    {
                        var keyvalue = line.Split("=");
                        dt_metadata.Add(new KeyValuePair<string, object>(keyvalue[0], keyvalue[1]));
                    }
                }
                catch {}
            }

            services.AddControllers();
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            services.AddOpenTelemetryTracing(tracerProviderBuilder =>
            {
                tracerProviderBuilder
                .SetSampler(new AlwaysOnSampler())
                .AddSource(TelemetryConstants.ServiceName)
                .SetResourceBuilder(
                    ResourceBuilder.CreateDefault()
                        .AddService(
                            serviceName: TelemetryConstants.ServiceName,
                            serviceVersion: TelemetryConstants.ServiceVersion)
                .AddAttributes(dt_metadata));
                // the OneAgent can also pick up traces, so this is optional.
                //.AddOtlpExporter(opt =>
                //{
                //    // endpoint for collector
                //    opt.Endpoint = new System.Uri("http://localhost:4317/v1/traces");
                //    opt.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                //});
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            var fileServerOptions = new FileServerOptions();
            fileServerOptions.StaticFileOptions.OnPrepareResponse = ctx => {
                ctx.Context.Response.Headers.AccessControlAllowOrigin = "*";
            };

            app.UseFileServer(fileServerOptions);

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<ChatHub>("/chat");
            });
        }
    }
}

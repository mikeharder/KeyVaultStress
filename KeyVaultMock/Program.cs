using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace StorageMock
{
    class Secret
    {
        public string Value { get; set; }
        public string Id { get; set; }
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }
    }

    class Program
    {
        private static readonly Dictionary<string, Secret> _secrets = new Dictionary<string, Secret>();

        private static readonly DateTime _epoch = new DateTime(1970, 1, 1);

        static void Main(string[] args)
        {
            Console.WriteLine("KeyVault URL (unencrypted): \"http://<hostname>:5000\"");
            Console.WriteLine("KeyVault URL (encrypted): \"https://<hostname>:5001\"");
            Console.WriteLine();

            new WebHostBuilder()
                .UseKestrel(options =>
                {
                    options.Listen(IPAddress.Any, 5000);
                    options.Listen(IPAddress.Any, 5001, listenOptions =>
                    {
                        listenOptions.UseHttps("testCert.pfx", "testPassword");
                    });
                })
                .UseContentRoot(Directory.GetCurrentDirectory())
                .Configure(app => app.Run(async context =>
                {
                    var request = context.Request;
                    var response = context.Response;
                    if (request.Path == "/debug")
                    {
                        await Debug(request, response);
                    }
                    else if (request.Headers["authorization"].Count == 0)
                    {
                        await Unauthorized(request, response);
                    }
                    else if (request.Path.StartsWithSegments("/secrets"))
                    {
                        if (request.Method == HttpMethods.Put)
                        {
                            await Put(request, response);
                        }
                        else if (request.Method == HttpMethods.Get)
                        {
                            await Get(request, response);
                        }
                    }

                }))
                .Build()
                .Run();
        }

        private static async Task Unauthorized(HttpRequest request, HttpResponse response)
        {
            const string body = "{\"error\":{\"code\":\"Unauthorized\",\"message\":\"Request is missing a Bearer or PoP token.\"}}";

            response.StatusCode = (int)HttpStatusCode.Unauthorized;

            var headers = response.Headers;

            AddCommonHeaders(headers);

            var authUri = new UriBuilder(request.Scheme, request.Host.Host, (int)request.Host.Port, "/auth").ToString();

            headers.Add("WWW-Authenticate",
                $"Bearer authorization=\"{authUri}\", resource=\"https://vault.azure.net\"");

            await response.WriteAsync(body);
        }

        private static async Task Put(HttpRequest request, HttpResponse response)
        {
            var name = GetName(request.Path);
            
            // {"value":"TestValue"}
            var document = await JsonDocument.ParseAsync(request.Body);
            var value = document.RootElement.GetProperty("value").GetString();

            var now = DateTime.Now;
            var secret = new Secret()
            {
                Value = value,
                Id = Guid.NewGuid().ToString("N"),
                Created = now,
                Updated = now
            };
            _secrets[name] = secret;

            // PUT uses same response as GET
            await Get(request, response);
        }

        private static async Task Get(HttpRequest request, HttpResponse response)
        {
            var headers = response.Headers;
            AddCommonHeaders(headers);

            var name = GetName(request.Path);
            var secret = _secrets[name];

            var body = new
            {
                value = secret.Value,
                id = (new UriBuilder(request.Scheme, request.Host.Host, (int)request.Host.Port, request.Path.Add("/" + secret.Id))).ToString(),
                attributes = new
                {
                    enabled = true,
                    created = (secret.Created - _epoch).TotalSeconds,
                    updated = (secret.Updated - _epoch).TotalSeconds,
                    recoveryLevel = "Purgeable"
                }
            };

            await JsonSerializer.SerializeAsync(response.Body, body);
        }

        private static string GetName(PathString path)
        {
            var p = path.Value.TrimEnd('/');
            return p.Substring(p.LastIndexOf('/') + 1);
        }

        private static void AddCommonHeaders(IHeaderDictionary headers)
        {
            headers.Add("Cache-Control", "no-cache");
            headers.Add("Pragma", "no-cache");
            headers.Add("Content-Type", "application/json; charset=utf-8");
            headers.Add("Expires", "-1");
            headers.Add("Server", "Micrsoft-IIS/10.0");
            headers.Add("x-ms-keyvault-region", "westus2");
            headers.Add("x-ms-request-id", Guid.NewGuid().ToString());
            headers.Add("x-ms-keyvault-service-version", "1.1.0.876");
            headers.Add("x-ms-keyvault-network-info", "addr=127.0.0.1;act_addr_fam=InterNetwork;");
            headers.Add("X-AspNet-Version", "4.0.30319");
            headers.Add("X-Powered-By", "ASP.NET");
            headers.Add("Strict-Transport-Security", "max-age=31536000;includeSubDomains");
            headers.Add("X-Content-Type-Options", "nosniff");
            headers.Add("Connection", "close");
        }

        private static Task Debug(HttpRequest request, HttpResponse response)
        {
            response.StatusCode = (int)HttpStatusCode.OK;

            foreach (var kvp in _secrets)
            {
                Console.WriteLine($"{kvp.Key} {kvp.Value.Value}");
            }

            return Task.CompletedTask;
        }
    }
}

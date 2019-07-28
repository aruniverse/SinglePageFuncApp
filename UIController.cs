using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Collections.Generic;
using System.Reflection;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.StaticFiles;

namespace SinglePageFuncApp
{
    public static class UiController
    {
        private struct FunctionNames
        {
            internal const string Serve = nameof(UiController) + "_" + nameof(Serve);
        }

        private const string DefaultFileName = "index.html";
        private static readonly string BasePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath), @"..\SPA"));
        private static readonly string DefaultPage = Path.Combine(BasePath, DefaultFileName);

        static readonly Dictionary<string, byte[]> cache = new Dictionary<string, byte[]>();

        [FunctionName(FunctionNames.Serve)]
        public static async Task<HttpResponseMessage> Serve(ILogger log, ExecutionContext context,
        [HttpTrigger(AuthorizationLevel.Anonymous, "GET", Route = "fileserver/{*reqFile}")]HttpRequestMessage req)
        {
            byte[] content;
            var file = Uri.UnescapeDataString(req.RequestUri.AbsolutePath.Substring("/api/fileserver/".Length));
            if (cache.ContainsKey(file))
            {
                content = cache[file];
            }
            else
            {
                var path = Path.GetFullPath(Path.Combine(BasePath, file));
                if (!path.StartsWith(BasePath) || !File.Exists(path))
                {
                    path = DefaultPage;
                    file = DefaultFileName;
                }

                using (var fs = File.OpenRead(path))
                {
                    var stream = new MemoryStream();
                    await fs.CopyToAsync(stream);
                    content = stream.GetBuffer();
                    cache[file] = content;
                }
            }

            var fileProvider = new FileExtensionContentTypeProvider();
            if (!fileProvider.TryGetContentType(file, out string contentType))
            {
                throw new ArgumentOutOfRangeException($"Unable to find Content Type for file name {file}.");
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Content = new ByteArrayContent(content);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            response.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromSeconds(31556926), Public = true };
            response.Headers.Add("X-Content-Type-Options", "nosniff");

            return response;
        }
    }
}

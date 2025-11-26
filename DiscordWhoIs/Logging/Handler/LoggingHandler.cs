namespace DiscordWhoIs.Logging.Handler
{
    public class LoggingHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Console.WriteLine($"[HTTP-RAW] → {request.Method} {request.RequestUri}");

            try
            {
                var resp = await base.SendAsync(request, cancellationToken);
                Console.WriteLine($"[HTTP-RAW] ← {((int)resp.StatusCode)} after {sw.ElapsedMilliseconds}ms");
                return resp;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HTTP-RAW] !! Exception after {sw.ElapsedMilliseconds}ms: {ex.GetType().Name} - {ex.Message}");
                throw;
            }
        }
    }

}

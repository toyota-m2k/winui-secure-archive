using SecureArchive.Utils.Server.lib.model;
using System.Text;
using System.Text.RegularExpressions;

namespace SecureArchive.Utils.Server.lib.response {
    public interface IHttpResponse : IDisposable
    {
        HttpRequest Request { get; }
        void WriteResponse(Stream outputStream);
    }

    public abstract class AbstractHttpResponse : IHttpResponse
    {
        private static Regex refererForCors = new Regex(@"(?<target>http://(?:localhost|127.0.0.1)(?::\d+)?)/");
        public HttpStatusCode StatusCode { get; set; }
        //public string ReasonPhrase { get; set; }
        public HttpRequest Request { get; }


        protected AbstractHttpResponse(HttpRequest req, HttpStatusCode statusCode)
        {
            Request = req;
            StatusCode = statusCode;
            // ローカルホストからの要求に対してはCross-Origin Resource Shareingを許可する
            if (req != null && req.Headers.TryGetValue("referer", out var referer))
            {
                var m = refererForCors.Match(referer);
                if (m.Success)
                {
                    var r = m.Groups["target"]?.Value;
                    if (!string.IsNullOrEmpty(r))
                    {
                        Headers["Access-Control-Allow-Origin"] = r;
                        Headers["Access-Control-Allow-Methods"] = "GET,POST,PUT,DELETE";
                        Headers["Access-Control-Allow-Headers"] = "Content-Type,Content-Length,Accept";
                    }
                }
            }
        }

        public string GetHeaderValueOrDefault(string name, string def = "")
        {
            return Headers.TryGetValue(name, out var value) ? value : def;
        }

        public string ContentType
        {
            get => GetHeaderValueOrDefault("Content-Type");
            set => Headers["Content-Type"] = value;
        }
        public long ContentLength
        {
            get => Convert.ToInt64(GetHeaderValueOrDefault("Content-Length", "0"));
            set => Headers["Content-Length"] = $"{value}";
        }

        // Access-Control-Allow-Origin: https://127.0.0.1:3001
        // Access-Control-Max-Age:86400
        // Access-Control-Allow-Methods: GET,POST,PUT,PATCH,DELETE,HEAD,OPTIONS
        // Access-Control-Allow-Headers: Content-type,Accept,X-Custom-Header
        public Dictionary<string, string> Headers { get; } = new Dictionary<string, string>()
        {
#if DEBUG
            //{ "Access-Control-Allow-Origin", "http://localhost:5501"},
            //{ "Access-Control-Allow-Methods", "GET,POST,PUT,DELETE"},
            //{ "Access-Control-Allow-Headers", "Content-Type,Content-Length,Accept" },
#endif
        };

        protected abstract void Prepare();

        protected virtual void WriteHeaders(Stream output)
        {
            WriteText(output, $"HTTP/1.0 {(int)StatusCode} {StatusCode}\r\n");
            WriteText(output, string.Join("\r\n", Headers.Select(x => $"{x.Key}: {x.Value}")));
            WriteText(output, "\r\n");
        }
        protected abstract void WriteBody(Stream output);

        public virtual void WriteResponse(Stream output)
        {
            try {
                Prepare();
            } catch(HttpException ex) {
                ex.ErrorResponse.WriteResponse(output);
                return;
            }
            WriteHeaders(output);
            WriteText(output, "\r\n");
            WriteBody(output);
            output.Flush();
        }

        protected static void WriteText(Stream output, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            output.Write(bytes, 0, bytes.Length);
        }

        public virtual void Dispose() {
        }
    }
}

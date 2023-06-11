using SecureArchive.Utils.Server.lib.model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.Utils.Server.lib.response;

public class TextHttpResponse : AbstractHttpResponse
{
    const string CT_TEXT_PLAIN = "text/plain";
    const string CT_TEXT_HTML = "text/html";
    const string CT_JSON = "application/json";

    public string Content { get; set; }
    private byte[] Buffer = null!;

    public TextHttpResponse(HttpRequest? req, HttpStatusCode statusCode, string content, string contentType = CT_TEXT_PLAIN) : base(req, statusCode)
    {
        Content = content;
        ContentType = contentType;
    }

    protected override void Prepare()
    {
        Buffer = Content != null ? Encoding.UTF8.GetBytes(Content) : new byte[] { };
        ContentLength = Buffer.Length;
    }

    protected override void WriteBody(Stream output)
    {
        output.Write(Buffer, 0, Buffer.Length);
    }

    // informational only tostring...
    public override string ToString()
    {
        return string.Format($"TextHttpResponse status {(int)StatusCode} {StatusCode}");
    }
}

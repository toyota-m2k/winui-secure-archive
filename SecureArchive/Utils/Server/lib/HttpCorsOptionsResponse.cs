using SecureArchive.Utils.Server.lib.model;
using SecureArchive.Utils.Server.lib.response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.Utils.Server.lib;

internal class HttpCorsException : Exception {
    public HttpCorsOptionsResponse Response { get; }
    public HttpCorsException(HttpRequest req) : base("CorsException") {
        Response = new HttpCorsOptionsResponse(req);
    }
}
internal class HttpCorsOptionsResponse : AbstractHttpResponse {
    public HttpCorsOptionsResponse(HttpRequest req) : base(req, HttpStatusCode.NoContent) {
    }

    protected override void Prepare() {
        
    }

    protected override void WriteBody(Stream output) {
        
    }

    public override void WriteResponse(Stream output) {
        WriteHeaders(output);
        output.Flush();
    }

}

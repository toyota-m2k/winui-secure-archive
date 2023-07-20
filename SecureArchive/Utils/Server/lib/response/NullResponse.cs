using SecureArchive.Utils.Server.lib.model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.Utils.Server.lib.response {
    public class NullResponse : IHttpResponse {
        public HttpRequest Request { get; }

        public NullResponse(HttpRequest request) {
            Request = request;
        }

        public void WriteResponse(Stream output) {
            
        }
        public static NullResponse Get(HttpRequest req) { 
            return new NullResponse(req); 
        }

        public void Dispose() {
        }
    }
}

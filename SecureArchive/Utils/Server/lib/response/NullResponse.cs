using SecureArchive.Utils.Server.lib.model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.Utils.Server.lib.response {
    public class NullResponse : IHttpResponse {
        public NullResponse() {
        }

        public void WriteResponse(Stream output) {
            
        }
        public static NullResponse Instance { get { return new NullResponse(); } }
    }
}

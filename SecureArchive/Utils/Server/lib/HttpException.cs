using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.Utils.Server.lib {
    internal class HttpException : Exception {
        public HttpErrorResponse ErrorResponse { get; }
        public HttpException(HttpErrorResponse errorResponse):base (errorResponse.Content) { 
            ErrorResponse = errorResponse;
        }
    }
}

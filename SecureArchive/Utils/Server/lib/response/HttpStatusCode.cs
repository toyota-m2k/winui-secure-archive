using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.Utils.Server.lib.response;

public enum HttpStatusCode
{
    // for a full list of status codes, see..
    // https://en.wikipedia.org/wiki/List_of_HTTP_status_codes

    Continue = 100,

    Ok = 200,
    Created = 201,
    Accepted = 202,
    PartialContent = 206,
    MovedPermanently = 301,
    Found = 302,
    NotModified = 304,
    BadRequest = 400,
    Forbidden = 403,
    NotFound = 404,
    MethodNotAllowed = 405,
    InternalServerError = 500,
    ServiceUnavailable = 503,
}


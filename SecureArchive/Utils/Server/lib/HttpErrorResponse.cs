using SecureArchive.Utils.Server.lib.model;
using SecureArchive.Utils.Server.lib.response;

namespace SecureArchive.Utils.Server.lib;

class HttpErrorResponse
{
    public static IHttpResponse InternalServerError(HttpRequest? req)
    {
        // string content = File.ReadAllText("Resources/Pages/500.html"); 

        return new TextHttpResponse(req,
                        HttpStatusCode.InternalServerError,
                        "Internal Server Error.");
    }

    public static IHttpResponse BadRequest(HttpRequest req)
    {
        //string content = File.ReadAllText("Resources/Pages/404.html");

        return new TextHttpResponse(req,
                    HttpStatusCode.BadRequest,
                    "Bad Request.");
    }

    public static IHttpResponse NotFound(HttpRequest req)
    {
        //string content = File.ReadAllText("Resources/Pages/404.html");

        return new TextHttpResponse(req,
                    HttpStatusCode.NotFound,
                    "Not Found.");
    }

    public static IHttpResponse MethodNotAllowed(HttpRequest req)
    {
        return new TextHttpResponse(req,
                    HttpStatusCode.MethodNotAllowed,
                    "Method Not Allowed");
    }

    public static IHttpResponse ServiceUnavailable(HttpRequest req)
    {
        return new TextHttpResponse(req,
                    HttpStatusCode.ServiceUnavailable,
                    "Service Unavailable");
    }


}

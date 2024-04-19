using SecureArchive.Utils.Server.lib.model;
using SecureArchive.Utils.Server.lib.response;

namespace SecureArchive.Utils.Server.lib;

class HttpErrorResponse : TextHttpResponse
{
    public HttpErrorResponse(HttpRequest req, HttpStatusCode statusCode, string content, string contentType = "text/plain") : base(req, statusCode, content, contentType) {
    }

    public HttpException Exception => new HttpException(this);

    public static HttpErrorResponse InternalServerError(HttpRequest req)
    {
        // string content = File.ReadAllText("Resources/Pages/500.html"); 

        return new HttpErrorResponse(req,
                        HttpStatusCode.InternalServerError,
                        "Internal Server Error.");
    }

    public static HttpErrorResponse BadRequest(HttpRequest req)
    {
        //string content = File.ReadAllText("Resources/Pages/404.html");

        return new HttpErrorResponse(req,
                    HttpStatusCode.BadRequest,
                    "Bad Request.");
    }

    public static HttpErrorResponse NotFound(HttpRequest req)
    {
        //string content = File.ReadAllText("Resources/Pages/404.html");

        return new HttpErrorResponse(req,
                    HttpStatusCode.NotFound,
                    "Not Found.");
    }

    public static HttpErrorResponse Unauthorized(HttpRequest req) {
        //string content = File.ReadAllText("Resources/Pages/404.html");

        return new HttpErrorResponse(req,
                    HttpStatusCode.Unauthorized,
                    "Unauthorized.");
    }


    public static HttpErrorResponse MethodNotAllowed(HttpRequest req)
    {
        return new HttpErrorResponse(req,
                    HttpStatusCode.MethodNotAllowed,
                    "Method Not Allowed");
    }

    public static HttpErrorResponse Conflict(HttpRequest req) {
        return new HttpErrorResponse(req,
                    HttpStatusCode.Conflict,
                    "Conflict");
    }

    public static HttpErrorResponse ServiceUnavailable(HttpRequest req)
    {
        return new HttpErrorResponse(req,
                    HttpStatusCode.ServiceUnavailable,
                    "Service Unavailable");
    }
}

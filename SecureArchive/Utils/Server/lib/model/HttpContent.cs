using HttpMultipartParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace SecureArchive.Utils.Server.lib.model;

public class HttpContent {
    public long ContentLength { get; }
    public string ContentType { get; }
    public string Name { get; private set; } = "";
    public string Filename { get; private set; } = "";

    public string? TextContent { get; }
    public Stream? InputStream { get; }

    public bool HasText => !string.IsNullOrEmpty(TextContent);
    public bool HasStream => InputStream != null;

    private HttpContent(string contentType, long contentLength) {
        ContentType = contentType;
        ContentLength = contentLength;
    }
    public HttpContent(string contentText, string contentType, long contentLength)
        : this(contentType, contentLength) {
        TextContent = contentText;
        InputStream = null;
    }
    public HttpContent(Stream inputStream, string contentType, long contentLength)
        : this(contentType, contentLength) {
        InputStream = inputStream;
        TextContent = null;
    }

    public interface MultipartContentHander {
        IDictionary<string, string> Parameters { get; }
        Stream CreateFile(HttpContent multipartBody);
        void CloseFile(HttpContent multipartBody, Stream outStream);
    }

    public async Task ParseMultipartContent(MultipartContentHander multipartContentHandler, CancellationToken? cancellationToken = null, string[]? expectingBodyTypes = null) {
        if (!HasStream) {
            throw new InvalidOperationException("this is not a streamed content.");
        }
        var parser = new StreamingMultipartFormDataParser(InputStream, Encoding.UTF8, binaryMimeTypes: expectingBodyTypes, ignoreInvalidParts: true);

        parser.ParameterHandler += (ParameterPart p) => {
            multipartContentHandler.Parameters.Add(p.Name, p.Data ?? "");
        };

        Stream? outStream = null;
        HttpContent partContent = null!;
        void closeCurrentPart() {
            if (outStream != null) {
                outStream.Flush();
                multipartContentHandler.CloseFile(partContent, outStream);
                outStream = null;
            }
        }

        parser.FileHandler += (string name, string fileName, string contentType, string contentDisposition, byte[] buffer, int bytes, int partNumber, IDictionary<string, string> additionalProperties) => {
            if (partNumber == 0) {
                closeCurrentPart();
                partContent = new HttpContent(contentType, 0) { Name = name, Filename = fileName };
                outStream = multipartContentHandler.CreateFile(partContent);
            }
            outStream?.Write(buffer, 0, bytes);
        };

        if (cancellationToken == null) {
            await parser.RunAsync();
        }
        else {
            await parser.RunAsync((CancellationToken)cancellationToken!);
        }
        closeCurrentPart();
    }
}

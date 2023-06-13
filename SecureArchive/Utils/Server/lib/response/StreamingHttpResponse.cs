using SecureArchive.Utils.Server.lib.model;

namespace SecureArchive.Utils.Server.lib.response;

public class StreamingHttpResponse : AbstractHttpResponse {
    public long Start { get; set; } = 0;
    public long End { get; set; } = 0;
    private Stream InputStream;         // Seek可能なストリーム ... 無理なら SeekableInputStream でラップしよう。
    private long TotalLength = -1;
    private byte[]? Buffer = null;
    private int PartialLength = 0;

    public StreamingHttpResponse(HttpRequest req, string contentType, Stream inStream, long start, long end)
        : base(req, HttpStatusCode.Ok) {
        InputStream = inStream;
        TotalLength = inStream.Length;
        Start = start;
        End = end;
        if (TotalLength > 0) {
            ContentType = contentType;
        }
    }

    const int AUTO_BUFFER_SIZE = 32 * 1024;
    

    protected override void Prepare() {
        if (Start == 0 && End == 0) {
            StatusCode = HttpStatusCode.Ok;
            Headers["Accept-Ranges"] = "bytes";
        }
        else {
            StatusCode = HttpStatusCode.PartialContent;
            if (TotalLength>0) {
                Buffer = null;
                if (End == 0) {
                    End = TotalLength - 1;
                }
                ContentLength = End - Start + 1;
                Headers["Content-Range"] = $"bytes {Start}-{End}/{TotalLength}";
                Headers["Accept-Ranges"] = "bytes";
            } else {
                Buffer = new byte[AUTO_BUFFER_SIZE];
                InputStream.Seek(Start, SeekOrigin.Begin);
                PartialLength = InputStream.Read(Buffer, 0, Buffer.Length);
                if(End==0) {
                    End = Start + PartialLength - 1;
                }
                ContentLength = PartialLength;
                Headers["Content-Range"] = $"bytes {Start}-{End}/*";
                Headers["Accept-Ranges"] = "bytes";
            }
        }
    }

    protected override void WriteBody(Stream output) {
        if (Start == 0 && End == 0) {
            InputStream.CopyTo(output);
        }
        else {
            if(Buffer==null) {
                long chunkLength = End - Start + 1;
                long remain = chunkLength;
                byte[] buffer = new byte[Math.Min(chunkLength, AUTO_BUFFER_SIZE)];
                InputStream.Seek(Start, SeekOrigin.Begin);
                while (remain > 0) {
                    var read = InputStream.Read(buffer, 0, Math.Min(buffer.Length, (int)remain));
                    output.Write(buffer, 0, read);
                    remain -= read;
                }
            } else {
                output.Write(Buffer, 0, PartialLength);
            }
        }
    }
}

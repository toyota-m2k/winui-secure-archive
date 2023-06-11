using SecureArchive.Utils.Server.lib.model;

namespace SecureArchive.Utils.Server.lib.response;
public class StreamingHttpResponse : FileHttpResponse
{
    public long Start { get; set; } = 0;
    public long End { get; set; } = 0;

    public StreamingHttpResponse(HttpRequest req, HttpStatusCode statusCode, string contentType, string filePath, long start, long end)
        : base(req, statusCode, contentType, filePath)
    {
        Start = start;
        End = end;
    }

    protected override void Prepare()
    {
        if (Start == 0 && End == 0)
        {
            StatusCode = HttpStatusCode.Ok;
            Headers["Accept-Ranges"] = "bytes";
            base.Prepare();
        }
        else
        {
            var fileLength = FileLength;
            if (End == 0)
            {
                End = fileLength - 1;
            }
            StatusCode = HttpStatusCode.PartialContent;
            Headers["Content-Range"] = $"bytes {Start}-{End}/{fileLength}";
            Headers["Accept-Ranges"] = "bytes";
            ContentLength = End - Start + 1;
        }
    }

    protected override void WriteBody(Stream output)
    {
        if (Start == 0 && End == 0)
        {
            base.WriteBody(output);
        }
        else
        {
            long chunkLength = End - Start + 1;
            long remain = chunkLength;
            int read = 0;
            using (var input = OpenFile())
            {
                byte[] buffer = new byte[Math.Min(chunkLength, 1 * 1024 * 1024)];
                input.Seek(Start, SeekOrigin.Begin);
                while (remain > 0)
                {
                    read = input.Read(buffer, 0, Math.Min(buffer.Length, (int)remain));
                    output.Write(buffer, 0, read);
                    remain -= read;
                }
            }
        }
    }

}

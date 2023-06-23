using SecureArchive.Utils.Server.lib.model;

namespace SecureArchive.Utils.Server.lib.response;

public class StreamingHttpResponse : AbstractHttpResponse {
    public long Start { get; set; } = 0;
    public long End { get; set; } = 0;
    private Stream InputStream;         // Seek可能なストリーム ... 無理なら SeekableInputStream でラップしよう。
    private long TotalLength = -1;
    private byte[]? Buffer = null;
    private int PartialLength = 0;
    private UtLog Logger = new UtLog(typeof(StreamingHttpResponse));

    private bool SupportRange { get; } = true;
    public StreamingHttpResponse(HttpRequest req, string contentType, Stream inStream, long start, long end)
        : base(req, HttpStatusCode.Ok) {
        InputStream = inStream;
        TotalLength = inStream.Length;
        ContentType = contentType;
        Start = start;
        End = end;
        if (TotalLength > 0) {
            ContentLength = TotalLength;
        } else {
            //ContentLength = 100000;
        }
    }
    public StreamingHttpResponse(HttpRequest req, string contentType, Stream inStream)
        : base(req, HttpStatusCode.Ok) {
        InputStream = inStream;
        TotalLength = inStream.Length;
        ContentType = contentType;
        Start = 0;
        End = 0;
        SupportRange = false;
        if (TotalLength > 0) {
            ContentLength = TotalLength;
        }
        else {
            //ContentLength = 100000;
        }
    }

    // ファイル長が不明の場合、クライアントからはバッファサイズが要求されない（End=0）。
    // その場合は、サーバー側の都合でバッファサイズを決めるのだが、
    // 32KB 程度だとストリーミングに失敗し、1MBならOKだった。ある程度の大きさが必要らしい。
    // 実際に（ファイル長がわかっている場合に） Android ExoPlayer から要求されるバッファサイズが4MBだったので、
    // デフォルトのバッファサイズは 4MBとしておく。
    const int AUTO_BUFFER_SIZE = 4 * 1024 * 1024;    

    int ReadStream(Stream inStream, byte[] buffer, out bool eos) {
        eos = false;
        var remain = buffer.Length;
        while(remain> 0) {
            int read = inStream.Read(buffer, buffer.Length-remain, remain);
            if(read==0) {
                eos = true;
                break;
            }
            remain -= read;
        }
        return buffer.Length - remain;
    }

    protected override void Prepare() {
        if (Start == 0 && End == 0) {
            StatusCode = HttpStatusCode.Ok;
            if (SupportRange) {
                Headers["Accept-Ranges"] = "bytes";
            }
        }
        else {
            Logger.Debug($"Requested Range: {Start} - {End}");
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
                int buffSize = AUTO_BUFFER_SIZE;
                if (End > 0) {
                    buffSize = (int)Math.Min((long)AUTO_BUFFER_SIZE, End - Start);
                }
                Buffer = new byte[buffSize];
                InputStream.Seek(Start, SeekOrigin.Begin);
                PartialLength = ReadStream(InputStream, Buffer, out var eos);
                End = Start + PartialLength - 1;
                ContentLength = PartialLength;
                var total = eos ? $"{End+1}" :"*";
                Logger.Debug($"Actual Range: {Start} - {End} ({ContentLength})");
                Headers["Content-Range"] = $"bytes {Start}-{End}/{total}";
                Headers["Accept-Ranges"] = "bytes";
            }
        }
    }

    protected override void WriteBody(Stream output) {
        if (Start == 0 && End == 0) {
            output.Flush();
            try {
                InputStream.CopyTo(output, 2048);
            } catch (Exception e) {
                Logger.Error(e);
                throw;
            }
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

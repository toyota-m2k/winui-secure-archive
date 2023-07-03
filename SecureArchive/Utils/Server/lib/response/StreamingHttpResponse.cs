using SecureArchive.Utils.Server.lib.model;

namespace SecureArchive.Utils.Server.lib.response;

public class StreamingHttpResponse : AbstractHttpResponse {
    public long Start { get; set; } = -1;
    public long End { get; set; } = 0;
    private Stream InputStream;         // Seek可能なストリーム ... 無理なら SeekableInputStream でラップしよう。
    private long TotalLength = -1;
    private byte[]? Buffer = null;
    private int PartialLength = 0;
    private UtLog Logger = new UtLog(typeof(StreamingHttpResponse));

    private bool SupportRange { get; } = true;
    public StreamingHttpResponse(HttpRequest req, string contentType, Stream inStream, long start, long end, long totalLength=-1)
        : base(req, HttpStatusCode.Ok) {
        InputStream = inStream;
        TotalLength = (totalLength>0) ? totalLength : inStream.Length;
        ContentType = contentType;
        Start = start;
        End = end;
        if (TotalLength > 0) {
            ContentLength = TotalLength;
        } else {
            //ContentLength = 100000;
        }
    }
    public StreamingHttpResponse(HttpRequest req, string contentType, Stream inStream, long totalLength=-1)
        : base(req, HttpStatusCode.Ok) {
        InputStream = inStream;
        TotalLength = (totalLength > 0) ? totalLength : inStream.Length;
        ContentType = contentType;
        Start = -1;
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
        Headers["Content-Transfer-Encoding"] = "binary";
        if (Start == -1) {
            StatusCode = HttpStatusCode.Ok;
            if (TotalLength > 0) {
                ContentLength = TotalLength;
            }
            if (SupportRange) {
                Headers["Accept-Ranges"] = "bytes";
            }
            Logger.Debug("No Range Requested");
        }
        else {
            Logger.Debug($"Requested Range: {Start} - {End} ({string.Format("{0:#,0}", End - Start + 1)} bytes)");
            StatusCode = HttpStatusCode.PartialContent;
            Buffer = null;
            var total = "*";
            if (TotalLength>0) {
                total = $"{TotalLength}";
                ContentLength = TotalLength;
                if (End <= 0) {
                    End = TotalLength - 1;
                }
            }
            int buffSize = AUTO_BUFFER_SIZE;
            if (End > 0) {
                buffSize = (int)Math.Min((long)AUTO_BUFFER_SIZE, End - Start + 1);
            }
            Buffer = new byte[buffSize];
            InputStream.Seek(Start, SeekOrigin.Begin);
            PartialLength = ReadStream(InputStream, Buffer, out var eos);
            End = Start + PartialLength - 1;
            if(TotalLength<=0) { 
                ContentLength = PartialLength;
                total = eos ? $"{End+1}" :"*";
            }
            Logger.Debug($"Actual Range: {Start}-{End}/{total} ({string.Format("{0:#,0}", PartialLength)} Bytes)");
            Headers["Content-Range"] = $"bytes {Start}-{End}/{total}";
            Headers["Accept-Ranges"] = "bytes";
        }
    }

    protected override void WriteBody(Stream output) {
        if (Start == -1) {
            output.Flush();
            try {
                InputStream.CopyTo(output, AUTO_BUFFER_SIZE);
                output.Flush();
            } catch (Exception e) {
                Logger.Error(e);
                throw;
            }
        }
        else {
            if(Buffer==null) {
                throw new Exception("Internal error: Buffer is null");
            }
            output.Write(Buffer, 0, PartialLength);
        }
    }
}

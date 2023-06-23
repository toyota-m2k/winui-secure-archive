namespace SecureArchive.Utils.Server.lib;

/**
 * HTTP 1.0 の時代と違って、データを送り終えてもソケットは閉じないので、
 * タイムアウトしてコネクションが切れるまで、ゼロバイトリードは発生しない。
 * （なぜかアップロードに時間がかかりすぎて呼び出し側でタイムアウトする現象に悩んだぞ。）
 * そのため、Content-Length が指定されている場合は、その長さだけ読み込んで、
 * 人工的にゼロバイトリードを発生させるためのクラスを作った。
 */
internal class HttpInputStream : Stream {
    public Stream SocketStream { get; }
    public long ContentLength { get; }

    public override bool CanRead => SocketStream.CanRead;

    public override bool CanSeek => SocketStream.CanSeek;

    public override bool CanWrite => SocketStream.CanWrite;

    public override long Length => SocketStream.Length;

    public override long Position {
        get => SocketStream.Position;
        set => SocketStream.Position = value;
    }


    HttpInputStream(Stream socketStream, long contentLength) {
        SocketStream = socketStream;
        ContentLength = contentLength;
    }

    public static Stream Create(Stream socketStream, long contentLength) { 
        if(contentLength > 0) {
            return new HttpInputStream(socketStream, contentLength);
        }
        else {
            return socketStream;
        }
    }


    public override void Flush() {
        SocketStream.Flush();
    }

    private long _readLength = 0L;



    public override int Read(byte[] buffer, int offset, int count) {
        if(ContentLength>0 && _readLength >= ContentLength) {
            return 0;
        }
        var len = SocketStream.Read(buffer, offset, count);
        _readLength += len;
        return len;
    }

    public override long Seek(long offset, SeekOrigin origin) {
        return SocketStream.Seek(offset, origin);
    }

    public override void SetLength(long value) {
        SocketStream.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count) {
        SocketStream.Write(buffer, offset, count);
    }
}

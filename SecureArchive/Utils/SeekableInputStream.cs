using System.Diagnostics;

namespace SecureArchive.Utils;

public class SeekableInputStream : Stream
{
    private const int BUFFER_SIZE = 8192;
    private Stream _internalStream;
    public delegate Stream ReopenStreamProc(Stream currentStream);
    private ReopenStreamProc? _reopenStream;

    public SeekableInputStream(Stream inStream, ReopenStreamProc? reopenStream)
    {
        Debug.Assert(inStream.CanRead);
        _internalStream = inStream;
        _reopenStream = reopenStream;
    }


    public override long Length
    {
        get
        {
            try
            {
                return _internalStream.Length;
            }
            catch (Exception e)
            {
                return -1;
            }
        }
    }

    private long _position = 0L;
    public override long Position
    {
        get => _position;
        set => Seek(value, SeekOrigin.Begin);
    }

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override void Flush()
    {

    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return _internalStream.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        if (_internalStream.CanSeek)
        {
            return _internalStream.Seek(offset, origin);
        }

        long seekTo = 0;
        switch (origin)
        {
            case SeekOrigin.Begin:
                seekTo = offset;
                break;
            case SeekOrigin.Current:
                seekTo = _position + offset;
                break;
            case SeekOrigin.End:
                var length = Length;
                if (length < 0)
                {
                    throw new InvalidOperationException("End position is not undefined.");
                }
                seekTo = length;
                break;
        }
        if (seekTo == _position) return seekTo;
        if (seekTo < _position)
        {
            if (_reopenStream == null)
            {
                throw new InvalidOperationException("cannot seek backword.");
            }
            _internalStream = _reopenStream(_internalStream);
            _position = 0;
        }
        Skip(seekTo - _position);
        return seekTo;
    }

    private void Skip(long offset)
    {
        if (offset == 0) return;
        if (offset < 0) throw new InvalidOperationException("offset must be positive.");
        var buffer = new byte[Math.Min(offset, BUFFER_SIZE)];
        long remain = offset;
        while (remain > 0)
        {
            remain -= Read(buffer, 0, (int)Math.Min(remain, buffer.Length));
        }
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException("cannot set length to readonly stream.");
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException("cannot set length to readonly stream.");
    }

    protected override void Dispose(bool disposing) {
        base.Dispose(disposing);
        _internalStream.Dispose();
    }
}

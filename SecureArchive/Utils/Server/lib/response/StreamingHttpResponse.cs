﻿using SecureArchive.Utils.Server.lib.model;
using System.Diagnostics;

namespace SecureArchive.Utils.Server.lib.response;

public class StreamingHttpResponse : AbstractHttpResponse {
    private long Start { get; set; }
    private long End { get; set; }
    private Stream InputStream { get; }         // Seek可能なストリーム ... 無理なら SeekableInputStream でラップしよう。
    private long TotalLength { get; }
    
    //private byte[]? Buffer = null;
    //private int PartialLength = 0;
    private UtLog Logger = new UtLog(typeof(StreamingHttpResponse));

    /**
     * Range指定をサポートするか？
     */
    private bool SupportRange { get; } = true;

    public event Action? OnComplete;

    private string F(long size) {
        return string.Format("{0:#,0}",size);
    }
    /**
     * コンストラクタ
     * @param totalLength   不明の場合は、-1を指定する。
     */
    public StreamingHttpResponse(HttpRequest req, string contentType, Stream inStream, bool supportRange, long start, long end, long totalLength/*=-1*/, Action? onComplete)
        : base(req, HttpStatusCode.Ok) {
        //Logger.Debug($"Start={F(start)} End={F(end)} TotalLength={F(totalLength)} Stream.Length={F(inStream.Length)}");
        InputStream = inStream;
        TotalLength = (totalLength>0) ? totalLength : inStream.Length;
        ContentType = contentType;
        Start = start;
        End = end;
        SupportRange = supportRange;
        if(onComplete!=null) {
            OnComplete += onComplete;
        }
        //Logger.Debug($"[{F(req.Id)}] Start={F(start)} End={F(end)} Total={F(totalLength)}");
    }

    /**
     * Rangeをサポートするストリーミングレスポンス（初回用）を生成する。
     */
    public static StreamingHttpResponse CreateForRangedInitial(HttpRequest req, string contentType, Stream inStream, long totalLength/*=-1*/, Action? onComplete) {
        return new StreamingHttpResponse(req, contentType, inStream, true, -1, 0, totalLength, onComplete);
    }
    /**
     * Range指定付き要求に対するストリーミングレスポンス。
     */
    public static StreamingHttpResponse CreateForRanged(HttpRequest req, string contentType, Stream inStream, long start, long end, long totalLength/*=-1*/, Action? onComplete) {
        return new StreamingHttpResponse(req, contentType, inStream, true, start, end, totalLength, onComplete);
    }
    /**
     * Rangeをサポートしないストリーミングレスポンスを生成する。
     */
    public static StreamingHttpResponse CreateForNoRanged(HttpRequest req, string contentType, Stream inStream, long totalLength/*=-1*/, Action? onComplete) {
        return new StreamingHttpResponse(req, contentType, inStream, false, -1, 0, totalLength, onComplete);
    }

    // ファイル長が不明の場合、クライアントからはバッファサイズが要求されない（End=0）。
    // その場合は、サーバー側の都合でバッファサイズを決めるのだが、
    // 32KB 程度だとストリーミングに失敗し、1MBならOKだった。ある程度の大きさが必要らしい。
    // 実際に（ファイル長がわかっている場合に） Android ExoPlayer から要求されるバッファサイズが4MBだったので、
    // デフォルトのバッファサイズは 4MBとしておく。
    const int AUTO_BUFFER_SIZE = 4 * 1024 * 1024;    

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
            //Logger.Debug($"[{Request.Id}] No Range Requested");
        }
        else {
            //Logger.Debug($"[{Request.Id}] Requested Range: {F(Start)} - {F(End)} ({F(End - Start + 1)} bytes in {F(TotalLength)})");
            StatusCode = HttpStatusCode.PartialContent;

            var total = "*";
            if (TotalLength>0) {
                total = $"{TotalLength}";
                // ContentLength = TotalLength;  Range指定の場合も Content-Length には全体のサイズを入れるのかと思っていたが、返すデータサイズを指定するらしい。
                if (End >= TotalLength) {
                    End = TotalLength - 1;
                }
                else if (End <= 0) {
                    End = Math.Min(Start+AUTO_BUFFER_SIZE, TotalLength) - 1;
                }
                if(Start>End) {
                    End = Start;
                }
                Debug.Assert(Start < TotalLength);
            }

            //Logger.Debug($"[{Request.Id}] Actual Range: {F(Start)}-{F(End)}/{F(TotalLength)} ({F(PartialLength)} Bytes)");
            Headers["Content-Range"] = End>0 ? $"bytes {Start}-{End}/{total}" : $"bytes {Start}-/{total}";
            Headers["Accept-Ranges"] = "bytes";
            // Headers["Content-Length"] = $"{End-Start+1}";
            ContentLength = End-Start+1;    // Range指定の場合の Content-Length は実際に返すデータの長さ(End-Start+1)にする
        }
    }

    private void ExecuteWithLog(string msg, Action action) {
        try {
            Logger.Debug($"[{Request.Id}] {msg}: Start Action.");
            action();
        }
        catch (IOException e) {
            if ((uint)e.HResult == 0x80131620) {
                Logger.Debug($"[{Request.Id}] {msg}: Maybe cancelled by the client.");
            }
            else {
                Logger.Error(e, $"[{Request.Id}] {msg}: Unexpected IOException.");
            }
        }
        catch (Exception e) {
            Logger.Error(e, $"[{Request.Id}] {msg}: Error");
            throw;
        }
    }   


    private void CopyStream(Stream input, Stream output) {
        var buffer = new byte[AUTO_BUFFER_SIZE];
        long length = 0L;
        while (true) {
            int read = input.Read(buffer, 0, AUTO_BUFFER_SIZE);
            if (read == 0) {
                output.Flush();
                return;
            }
            output.Write(buffer, 0, read);
            length += read;
            Logger.Debug($"[{Request.Id}] CopyStream: {F(read)} bytes (total={F(length)})");
        }
    }

    private void CopyStream(Stream input, Stream output, long length) {
        var buffer = new byte[AUTO_BUFFER_SIZE];
        var remain = length;
        while (remain > 0) {
            int read = input.Read(buffer, 0, (int)Math.Min(remain, (long)AUTO_BUFFER_SIZE));
            if (read == 0) {
                break;
            }
            output.Write(buffer, 0, read);
            remain -= read;
            Logger.Debug($"[{Request.Id}] CopyStream: {F(length-remain)} bytes (total={F(length)})");
        }
        output.Flush();
        return;
    }

    protected override void WriteBody(Stream output) {
        if (Start == -1) {
            ExecuteWithLog("No-Range (Stream Copy)", () => {
                //InputStream.CopyTo(output, AUTO_BUFFER_SIZE);
                InputStream.Seek(0, SeekOrigin.Begin);
                CopyStream(InputStream, output);
                output.Flush();
            });
        }
        else {
            ExecuteWithLog($"Range ({F(Start)}-{F(End)})", () => {
                InputStream.Seek(Start, SeekOrigin.Begin);
                CopyStream(InputStream, output, ContentLength);
            });
        }
    }

    override public void Dispose() {
        OnComplete?.Invoke();
        OnComplete = null;
    }
}

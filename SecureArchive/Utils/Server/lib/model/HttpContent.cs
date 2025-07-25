﻿using HttpMultipartParser;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

    private UtLog Logger = new UtLog(typeof(HttpContent));

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

    public interface IMultipartContentHander {
        IDictionary<string, string> Parameters { get; }
        Stream CreateFile(int slot, HttpContent multipartBody);
        void CloseFile(HttpContent multipartBody, Stream outStream);
        void Progress(long current, long total);
    }

    //public void receiveToStream() {
    //    FileUtils.SafeDelete("c:\\temp\\x.dat");
    //    using (var fileStream = File.OpenWrite("c:\\temp\\x.dat")) {
    //        byte[] buff = new byte[1024 * 128];
    //        int len;
    //        Logger.Debug($"Content-Length: {ContentLength}");
    //        while ((len = InputStream!.Read(buff, 0, buff.Length)) > 0) {
    //            fileStream.Write(buff, 0, len);
    //            Logger.Debug($"len={len}/total={fileStream.Length}");
    //        }
    //        Logger.Debug($"len={len}/total={fileStream.Length}");
    //        fileStream.Flush();
    //        fileStream.Position = 0;
    //    }
    //}

    public void ParseMultipartContent(int slot, IMultipartContentHander multipartContentHandler, string[]? expectingBodyTypes = null) {
        if (!HasStream) {
            throw new InvalidOperationException("this is not a streamed content.");
        }

        var parser = new StreamingMultipartFormDataParser(HttpInputStream.Create(InputStream!, ContentLength), Encoding.UTF8, binaryMimeTypes: expectingBodyTypes, ignoreInvalidParts: true);

        parser.ParameterHandler += (ParameterPart p) => {
            multipartContentHandler.Parameters.Add(p.Name, p.Data ?? "");
        };

        Stream? outStream = null;
        HttpContent partContent = null!;
        void closeCurrentPart() {
            if (outStream != null) {
                Debug.WriteLine($"{DateTime.Now.ToLocalTime()} closeCurrentPart");
                multipartContentHandler.CloseFile(partContent, outStream);
                outStream = null;
                Debug.WriteLine($"{DateTime.Now.ToLocalTime()} closeCurrentPart: Done");
            }
        }

        long contentLength = 0;
        long receivedLength = 0;
        parser.FileHandler += (string name, string fileName, string contentType, string contentDisposition, byte[] buffer, int bytes, int partNumber, IDictionary<string, string> additionalProperties) => {
            if (partNumber == 0) {
                closeCurrentPart();
                contentLength = 0;
                receivedLength = 0;
                if(additionalProperties.TryGetValue("content-length", out var contentLengthText)) {
                    contentLength = Convert.ToInt64(contentLengthText);
                }
                partContent = new HttpContent(contentType, contentLength) { Name = name, Filename = fileName };
                outStream = multipartContentHandler.CreateFile(slot, partContent);
            }
            outStream?.Write(buffer, 0, bytes);
            receivedLength += bytes;
            multipartContentHandler.Progress(receivedLength, contentLength);
            var percent = contentLength > 0 ? receivedLength * 100 / contentLength : -1;
            Debug.WriteLine($"{DateTime.Now.ToLocalTime()}: {receivedLength}/{contentLength} ({percent} %)");
        };

        parser.Run();
        closeCurrentPart();
    }
}

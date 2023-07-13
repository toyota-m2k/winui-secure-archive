using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.Utils;
public class ProgressableStreamContent : HttpContent {
    // The default buffer size is 4096 bytes
    private const int defaultBufferSize = 4096;

    // The inner content that actually provides the data
    private readonly HttpContent content;

    // The buffer size to use for reading and writing
    private readonly int bufferSize;

    // The action to invoke when progress is reported
    private readonly Action<long, long?> progress;

    // The total number of bytes that have been sent
    private long totalBytes;

    public ProgressableStreamContent(HttpContent content, int bufferSize, Action<long, long?> progress) {
        this.content = content ?? throw new ArgumentNullException(nameof(content));
        this.bufferSize = bufferSize > 0 ? bufferSize : throw new ArgumentOutOfRangeException(nameof(bufferSize));
        this.progress = progress ?? throw new ArgumentNullException(nameof(progress));

        // Copy the headers from the inner content
        foreach (var header in content.Headers) {
            Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
    }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context) {
        var buffer = new byte[bufferSize];
        var size = content.Headers.ContentLength;

        using var sinput = await content.ReadAsStreamAsync();

        while (true) {
            var length = await sinput.ReadAsync(buffer, 0, buffer.Length);
            if (length <= 0) break;

            await stream.WriteAsync(buffer, 0, length);

            totalBytes += length;
            progress(totalBytes, size);
        }
    }

    protected override bool TryComputeLength(out long length) {
        length = content.Headers.ContentLength.GetValueOrDefault();
        return true;
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            content.Dispose();
        }
        base.Dispose(disposing);
    }
}
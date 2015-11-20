namespace OwinCompression
{
    using Microsoft.Owin;
    using System.IO.Compression;
    using System.Threading.Tasks;
    using System;
    using System.IO;
    using System.Linq;
    using System.Collections.Generic;
    using System.Threading;

    public class ResponseCompressionMiddleware : OwinMiddleware
    {
        private const string AcceptEncodingHeader = "Accept-Encoding";
        private const string ContentEncodingHeader = "Content-Encoding";
        private static readonly Dictionary<string, ResponseCompressions> _supportedCompressions;

        static ResponseCompressionMiddleware()
        {
            Interlocked.Exchange(ref _supportedCompressions, GetSupportedCompressions());
        }

        public ResponseCompressionMiddleware(OwinMiddleware next)
            : base(next)
        {
        }

        public async override Task Invoke(IOwinContext context)
        {
            var compression = GetCompression(context.Request.Headers);

            var compressionStream = GetCompressionStreamBuilder(context, compression);

            if (compression == ResponseCompressions.None || compressionStream == null)
            {
                // Unsupported or no compression
                //  Invoke next & quick exit
                await Next.Invoke(context);
                return;
            }

            await InvokeRequestAndCompressStream(context, compressionStream);
        }

        private async Task InvokeRequestAndCompressStream(IOwinContext context, Func<Stream, Stream> compression)
        {
            // ref the response stream for use copying later
            var stream = context.Response.Body;
            using (var buffer = new MemoryStream())
            {
                // replace the current response body for the duration of the request
                //  so we can use the memory stream buffer
                context.Response.Body = buffer;
                await Next.Invoke(context);
                context.Response.Body = stream;

                using (var compressedBuffer = new MemoryStream())
                {
                    // get a compression buffer & copy the "fake" response stream
                    //  to another "fake", using the compression stream
                    using (var compressionStream = compression(compressedBuffer))
                    {
                        buffer.Seek(0, SeekOrigin.Begin);
                        await buffer.CopyToAsync(compressionStream);
                    }

                    // we need the new "fake" stream to update the contentLength,
                    //  because we cannot query the HttpResponse for a length (NotSupportedException)
                    context.Response.ContentLength = compressedBuffer.Length;
                    compressedBuffer.Seek(0, SeekOrigin.Begin);
                    await compressedBuffer.CopyToAsync(stream);
                }
            }
        }

        private static Func<Stream, Stream> GetCompressionStreamBuilder(IOwinContext context, ResponseCompressions compression)
        {
            var compressionStream = default(Func<Stream, Stream>);

            switch (compression)
            {
                // Get a compression stream, but we require the underlying stream
                //  to remain open after compression
                case ResponseCompressions.Gzip:
                    compressionStream = s => new GZipStream(s, CompressionMode.Compress, true);
                    break;

                case ResponseCompressions.Deflate:
                    compressionStream = s => new DeflateStream(s, CompressionMode.Compress, true);
                    break;

                default:
                    return null;
            }

            var compressionHeader = compression.ToString().ToLower();
            context.Response.Headers.AppendValues(ContentEncodingHeader, compressionHeader);

            return compressionStream;
        }

        private static ResponseCompressions GetCompression(IHeaderDictionary headers)
        {
            if (!headers.ContainsKey(AcceptEncodingHeader))
                return ResponseCompressions.None;

            var encodingsAccepted = headers[AcceptEncodingHeader]
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim());

            var firstEncoding = encodingsAccepted
                .FirstOrDefault(e => _supportedCompressions.ContainsKey(e)); ;

            return _supportedCompressions[firstEncoding ?? string.Empty];
        }

        private static Dictionary<string, ResponseCompressions> GetSupportedCompressions()
        {
            var dict = Enum.GetValues(typeof(ResponseCompressions)).OfType<ResponseCompressions>()
                .ToDictionary(s => s == ResponseCompressions.None ? string.Empty : s.ToString().ToLower(), s => s);

            return dict;
        }
    }
}

namespace OwinCompression
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal enum ResponseCompressions
    {
        None = 0x00,
        Gzip = 0x01,
        Deflate = 0x02
    }
}

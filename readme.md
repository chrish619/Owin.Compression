# Owin.Compression

IIS compression is broken / removed by the Microsoft.Owin.Host.SystemWeb. In short, this middle ware is to replace & reimplement a GZip'ed / Deflate'd response back to the client.

## Usage

To use compression on the built-in mime types:
```
    appBuilder.Use<ResponseCompressionMiddleware>() // To use compression on default mime types
```

To use compression on your preferred mime types:
```
    IEnumerable<string> mimeTypesToCompress = ..
    appBuilder.Use<ResponseCompressionMiddleware>(mimeTypesToCompress);
```

Any others will be ignored / returned as uncompressed

### Default mime / types

Several default mimetypes that support gzip compression have been added:

* text/*
* message/*
* application/javascript
* application/json

## TODO

* Use a configuration for mimetypes: either from web.config, or other configuration
* Disable compression if Server capacity is near limit ( ?? )
* Instrumentation ( how long does it actually take for a server to compress a stream? )
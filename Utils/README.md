# Utils Library

The **Utils** library is a large collection of helper namespaces covering many common programming needs.
It targets **.NET 8** and is the base dependency for the other utility packages contained in this repository.

## Features

- **Arrays** – helpers for comparing arrays, working with multi-dimensional data and specialized comparers
- **Collections** – indexed lists, skip lists, LRU caches and dictionary extensions
- **Expressions** – creation and transformation of expression trees and lambda utilities
- **Files** – filesystem helpers to manipulate paths and temporary files
- **Mathematics** – base classes for expression transformation and math functions
- **Net** – advanced URI builder and network helpers
- **Objects** – data conversion routines and an advanced string formatter
- **Reflection** – additional reflection primitives such as `PropertyOrFieldInfo`
- **Resources** – utilities for working with embedded resources
- **Security** – Google Authenticator helpers
- **Streams** – base16/base32/base64 converters and binary serialization
- **XML** – helpers for XML processing

The design separates data structures from processing logic wherever possible and exposes extensibility points through interfaces.

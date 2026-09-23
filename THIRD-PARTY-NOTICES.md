# Third-party notices

MsdfAtlasGen.Native and MsdfAtlasGen redistribute native binaries built from
the following third-party software. Each binary (the `msdf-atlas-gen`
executable and the `msdf_atlas_c` shared library) is statically linked and
therefore embeds the listed libraries.

## msdf-atlas-gen

- Source: https://github.com/Chlumsky/msdf-atlas-gen (pinned at tag `v1.4`)
- Author: Viktor Chlumský
- License: MIT

```
MIT License

Copyright (c) 2020 Viktor Chlumský

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED. See the upstream LICENSE for the full text.
```

## msdfgen

- Source: https://github.com/Chlumsky/msdfgen (submodule of msdf-atlas-gen)
- Author: Viktor Chlumský
- License: MIT

The core distance-field generation library, built from the pinned submodule.
Skia-based geometry preprocessing is **disabled** in these builds
(`MSDF_ATLAS_USE_SKIA=OFF`), so Skia is neither linked nor redistributed.

## FreeType

- Source: https://freetype.org
- License: FreeType License (BSD-style) or GPLv2, at the user's option

Linked statically for font loading. Provided via vcpkg at native build time.

## libpng / zlib

- Sources: http://www.libpng.org, https://zlib.net
- Licenses: libpng (PNG Reference Library License / zlib-style), zlib (zlib)

Linked statically for PNG atlas output. Provided via vcpkg at native build time.

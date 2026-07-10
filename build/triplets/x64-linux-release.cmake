set(VCPKG_TARGET_ARCHITECTURE x64)
set(VCPKG_CRT_LINKAGE dynamic)
set(VCPKG_LIBRARY_LINKAGE static)
set(VCPKG_CMAKE_SYSTEM_NAME Linux)

# The package ships Release binaries only; skip building duplicate Debug
# variants of freetype/libpng and their transitive dependencies in CI.
set(VCPKG_BUILD_TYPE release)

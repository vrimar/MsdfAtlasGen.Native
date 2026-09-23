cmake_minimum_required(VERSION 3.15)

set(MSDF_ATLAS_VCPKG_FEATURES_SET ON)
set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")

project(msdf-atlas-gen-native LANGUAGES CXX)

get_property(_msdf_multi_config GLOBAL PROPERTY GENERATOR_IS_MULTI_CONFIG)
if(NOT _msdf_multi_config AND NOT CMAKE_BUILD_TYPE)
    set(CMAKE_BUILD_TYPE Release CACHE STRING "" FORCE)
endif()

set(MSDF_ATLAS_BUILD_STANDALONE ON  CACHE BOOL "")
set(MSDF_ATLAS_USE_VCPKG        ON  CACHE BOOL "")
set(MSDF_ATLAS_USE_SKIA         OFF CACHE BOOL "")
set(MSDF_ATLAS_NO_ARTERY_FONT   ON  CACHE BOOL "")
set(MSDF_ATLAS_MSDFGEN_EXTERNAL OFF CACHE BOOL "")
set(MSDF_ATLAS_INSTALL          OFF CACHE BOOL "")
set(MSDF_ATLAS_DYNAMIC_RUNTIME  OFF CACHE BOOL "")
set(BUILD_SHARED_LIBS           OFF CACHE BOOL "")

set(CMAKE_POSITION_INDEPENDENT_CODE ON)
set(CMAKE_CXX_VISIBILITY_PRESET hidden)
set(CMAKE_VISIBILITY_INLINES_HIDDEN ON)

add_subdirectory("${CMAKE_CURRENT_SOURCE_DIR}/../external/msdf-atlas-gen" "${CMAKE_BINARY_DIR}/msdf-atlas-gen")

add_library(msdf_atlas_c SHARED src/msdf_atlas_c.cpp include/msdf_atlas_c.h)
target_include_directories(msdf_atlas_c PUBLIC "${CMAKE_CURRENT_SOURCE_DIR}/include")
target_compile_features(msdf_atlas_c PRIVATE cxx_std_11)
target_compile_definitions(msdf_atlas_c PRIVATE MSDF_ATLAS_C_BUILDING)
target_link_libraries(msdf_atlas_c PRIVATE msdf-atlas-gen::msdf-atlas-gen)
set_target_properties(msdf_atlas_c PROPERTIES
    OUTPUT_NAME msdf_atlas_c
    LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
    ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/lib"
)

if(APPLE)
    set_target_properties(msdf_atlas_c PROPERTIES
        MACOSX_RPATH ON
        INSTALL_NAME_DIR "@rpath"
        BUILD_WITH_INSTALL_NAME_DIR ON
    )
    target_link_options(msdf_atlas_c PRIVATE
        "-Wl,-exported_symbols_list,${CMAKE_CURRENT_SOURCE_DIR}/exports.txt"
        "-Wl,-dead_strip"
    )
    set_property(TARGET msdf_atlas_c APPEND PROPERTY LINK_DEPENDS "${CMAKE_CURRENT_SOURCE_DIR}/exports.txt")
elseif(UNIX)
    target_link_options(msdf_atlas_c PRIVATE
        "-Wl,--version-script=${CMAKE_CURRENT_SOURCE_DIR}/exports.map"
        "-Wl,--exclude-libs,ALL"
        "-Wl,--no-undefined"
        "-Wl,--as-needed"
    )
    set_property(TARGET msdf_atlas_c APPEND PROPERTY LINK_DEPENDS "${CMAKE_CURRENT_SOURCE_DIR}/exports.map")
elseif(MSVC)
    target_compile_definitions(msdf_atlas_c PRIVATE _CRT_SECURE_NO_WARNINGS)
endif()

#ifndef MSDF_ATLAS_C_H
#define MSDF_ATLAS_C_H

#include <stddef.h>
#include <stdint.h>

#if defined(_WIN32)
  #if defined(MSDF_ATLAS_C_BUILDING)
    #define MSDF_ATLAS_C_API __declspec(dllexport)
  #else
    #define MSDF_ATLAS_C_API __declspec(dllimport)
  #endif
#elif defined(__GNUC__) || defined(__clang__)
  #define MSDF_ATLAS_C_API __attribute__((visibility("default")))
#else
  #define MSDF_ATLAS_C_API
#endif

#define MSDF_ATLAS_C_ABI_VERSION 1u

/** msdf_atlas_options.spacing value selecting the CLI behaviour: 0 for msdf/mtsdf, -1 otherwise. */
#define MSDF_ATLAS_SPACING_DEFAULT INT32_MIN

#ifdef __cplusplus
extern "C" {
#endif

typedef enum msdf_atlas_image_type {
    MSDF_ATLAS_IMAGE_HARD_MASK = 0,
    MSDF_ATLAS_IMAGE_SOFT_MASK = 1,
    MSDF_ATLAS_IMAGE_SDF = 2,
    MSDF_ATLAS_IMAGE_PSDF = 3,
    MSDF_ATLAS_IMAGE_MSDF = 4,
    MSDF_ATLAS_IMAGE_MTSDF = 5
} msdf_atlas_image_type;

typedef enum msdf_atlas_glyph_selection {
    /** Printable ASCII 0x20..0x7E; ids ignored. */
    MSDF_ATLAS_SELECT_ASCII = 0,
    /** ids are Unicode codepoints. */
    MSDF_ATLAS_SELECT_CODEPOINTS = 1,
    /** ids are glyph indices. */
    MSDF_ATLAS_SELECT_GLYPH_INDICES = 2,
    /** Every glyph in the font; ids ignored. */
    MSDF_ATLAS_SELECT_ALL_GLYPHS = 3
} msdf_atlas_glyph_selection;

typedef enum msdf_atlas_dimensions_constraint {
    /** Resolved to MULTIPLE_OF_FOUR_SQUARE, like the CLI. */
    MSDF_ATLAS_DIM_NONE = 0,
    MSDF_ATLAS_DIM_SQUARE = 1,
    MSDF_ATLAS_DIM_EVEN_SQUARE = 2,
    MSDF_ATLAS_DIM_MULTIPLE_OF_FOUR_SQUARE = 3,
    MSDF_ATLAS_DIM_POWER_OF_TWO_RECTANGLE = 4,
    MSDF_ATLAS_DIM_POWER_OF_TWO_SQUARE = 5
} msdf_atlas_dimensions_constraint;

typedef enum msdf_atlas_units {
    MSDF_ATLAS_UNITS_PIXELS = 0,
    MSDF_ATLAS_UNITS_EMS = 1
} msdf_atlas_units;

typedef enum msdf_atlas_edge_coloring {
    MSDF_ATLAS_COLORING_SIMPLE = 0,
    MSDF_ATLAS_COLORING_INKTRAP = 1,
    MSDF_ATLAS_COLORING_BY_DISTANCE = 2
} msdf_atlas_edge_coloring;

/** The CLI -errorcorrection presets. */
typedef enum msdf_atlas_error_correction {
    MSDF_ATLAS_EC_AUTO_MIXED = 0,
    MSDF_ATLAS_EC_AUTO_FAST = 1,
    MSDF_ATLAS_EC_AUTO_FULL = 2,
    MSDF_ATLAS_EC_DISABLED = 3,
    MSDF_ATLAS_EC_DISTANCE_FAST = 4,
    MSDF_ATLAS_EC_DISTANCE_FULL = 5,
    MSDF_ATLAS_EC_EDGE_FAST = 6,
    MSDF_ATLAS_EC_EDGE_FULL = 7
} msdf_atlas_error_correction;

typedef enum msdf_atlas_y_origin {
    /** Y up; pixel row 0 is the bottom row. */
    MSDF_ATLAS_Y_ORIGIN_BOTTOM = 0,
    /** Y down; pixel row 0 is the top row. */
    MSDF_ATLAS_Y_ORIGIN_TOP = 1
} msdf_atlas_y_origin;

typedef enum msdf_atlas_status {
    MSDF_ATLAS_OK = 0,
    MSDF_ATLAS_E_INVALID_ARGUMENT = 1,
    MSDF_ATLAS_E_STRUCT_SIZE = 2,
    MSDF_ATLAS_E_ABI_MISMATCH = 3,
    MSDF_ATLAS_E_FONT_LOAD = 4,
    MSDF_ATLAS_E_NO_GLYPHS = 5,
    MSDF_ATLAS_E_PACK_FAILED = 6,
    /** msdf_atlas_last_error_detail() returns the number of glyphs that did not fit. */
    MSDF_ATLAS_E_PACK_DID_NOT_FIT = 7,
    MSDF_ATLAS_E_ATLAS_SIZE = 8,
    MSDF_ATLAS_E_ENCODE_FAILED = 9,
    MSDF_ATLAS_E_IO = 10,
    MSDF_ATLAS_E_OUT_OF_MEMORY = 11,
    MSDF_ATLAS_E_INTERNAL = 12
} msdf_atlas_status;

/**
 * Generation options. Blittable with no implicit padding on 64-bit targets.
 * Fill with msdf_atlas_options_init, then override fields. Enums travel as int32_t, booleans as 0/1.
 */
typedef struct msdf_atlas_options {
    uint32_t struct_size;
    uint32_t abi_version;

    int32_t glyph_selection;
    uint32_t id_count;
    /** Not retained after msdf_atlas_generate returns. */
    const uint32_t *ids;

    int32_t image_type;
    int32_t y_origin;

    /** Pixels per em. Greater than 0 fixes the scale. */
    double em_size;
    /** Minimum pixels per em when em_size is 0; the packer then maximises the scale. */
    double min_em_size;

    int32_t range_units;
    int32_t reserved0;
    /** Distance range bounds. Equal bounds select 2 px; mask image types always use 1 px. */
    double range_lower;
    double range_upper;

    int32_t dimensions_constraint;
    /** width and height both greater than 0 fix the atlas dimensions. */
    int32_t width;
    int32_t height;
    int32_t spacing;

    /** Forced to 0 unless the image type is psdf, msdf or mtsdf. */
    double miter_limit;
    int32_t px_align_origin_x;
    int32_t px_align_origin_y;

    int32_t inner_padding_units;
    int32_t outer_padding_units;
    double inner_padding_left;
    double inner_padding_bottom;
    double inner_padding_right;
    double inner_padding_top;
    double outer_padding_left;
    double outer_padding_bottom;
    double outer_padding_right;
    double outer_padding_top;

    /** Applies to msdf and mtsdf only. */
    int32_t edge_coloring;
    int32_t reserved1;
    /** Radians. */
    double angle_threshold;
    uint64_t coloring_seed;

    int32_t error_correction;
    int32_t overlap_support;
    /** When set, the error correction distance check is disabled, like the CLI. */
    int32_t scanline_pass;
    int32_t kerning;
    /** 0 selects the hardware concurrency. */
    int32_t thread_count;
    int32_t reserved2;
} msdf_atlas_options;

/** Em units, y-signed per y_origin exactly like the CLI JSON. */
typedef struct msdf_atlas_font_metrics {
    double em_size;
    double ascender_y;
    double descender_y;
    double line_height;
    double underline_y;
    double underline_thickness;
} msdf_atlas_font_metrics;

/** Bounds match the CLI JSON planeBounds (em units) and atlasBounds (pixels) for the selected y_origin. */
typedef struct msdf_atlas_glyph {
    int32_t glyph_index;
    /** 0 when selected by glyph index. */
    uint32_t codepoint;
    int32_t is_whitespace;
    int32_t has_plane_bounds;
    int32_t has_atlas_bounds;
    int32_t reserved0;
    double advance;
    double plane_left;
    double plane_bottom;
    double plane_right;
    double plane_top;
    double atlas_left;
    double atlas_bottom;
    double atlas_right;
    double atlas_top;
} msdf_atlas_glyph;

/** Kerning by glyph index, em units. A codepoint is 0 when that glyph has none. */
typedef struct msdf_atlas_kerning {
    int32_t glyph_index1;
    int32_t glyph_index2;
    uint32_t codepoint1;
    uint32_t codepoint2;
    double advance;
} msdf_atlas_kerning;

typedef struct msdf_atlas_atlas_metrics {
    /** Final pixels per em. */
    double scale;
    /** Final distance range in pixels. */
    double px_range_lower;
    double px_range_upper;
    int32_t width;
    int32_t height;
    int32_t channels;
    int32_t image_type;
    int32_t y_origin;
    int32_t glyphs_loaded;
    int32_t glyphs_requested;
    int32_t reserved0;
} msdf_atlas_atlas_metrics;

typedef struct msdf_atlas_result msdf_atlas_result;

MSDF_ATLAS_C_API uint32_t msdf_atlas_abi_version(void);
MSDF_ATLAS_C_API const char *msdf_atlas_upstream_version(void);
MSDF_ATLAS_C_API const char *msdf_atlas_msdfgen_version(void);
MSDF_ATLAS_C_API uint32_t msdf_atlas_options_size(void);
MSDF_ATLAS_C_API void msdf_atlas_options_init(msdf_atlas_options *options);
/** Static string, never NULL. */
MSDF_ATLAS_C_API const char *msdf_atlas_error_string(int32_t status);
/** Thread-local detail of the last failed call on this thread, 0 when none. */
MSDF_ATLAS_C_API int32_t msdf_atlas_last_error_detail(void);

/** One-shot generation. font_data only needs to stay valid for the duration of the call. Reentrant. */
MSDF_ATLAS_C_API int32_t msdf_atlas_generate(const msdf_atlas_options *options,
                                             const uint8_t *font_data, size_t font_size,
                                             msdf_atlas_result **out_result);
/** Accepts NULL. */
MSDF_ATLAS_C_API void msdf_atlas_result_free(msdf_atlas_result *result);

/** The getters return memory owned by the result, valid until msdf_atlas_result_free. */
MSDF_ATLAS_C_API const uint8_t *msdf_atlas_result_image(const msdf_atlas_result *result,
                                                        int32_t *width, int32_t *height, int32_t *channels);
MSDF_ATLAS_C_API const msdf_atlas_atlas_metrics *msdf_atlas_result_atlas_metrics(const msdf_atlas_result *result);
MSDF_ATLAS_C_API const msdf_atlas_font_metrics *msdf_atlas_result_font_metrics(const msdf_atlas_result *result);
MSDF_ATLAS_C_API const msdf_atlas_glyph *msdf_atlas_result_glyphs(const msdf_atlas_result *result, uint32_t *count);
MSDF_ATLAS_C_API const msdf_atlas_kerning *msdf_atlas_result_kerning(const msdf_atlas_result *result, uint32_t *count);
/** Upstream JSON exporter. The path is passed to fopen, so non-ANSI paths are unsupported on Windows. */
MSDF_ATLAS_C_API int32_t msdf_atlas_result_export_json(const msdf_atlas_result *result, const char *utf8_path);

/**
 * Encodes caller-owned pixels (1, 3 or 4 channels, rows ordered per y_origin) as PNG.
 * The PNG is always top-down. Release *out_data with msdf_atlas_buffer_free.
 */
MSDF_ATLAS_C_API int32_t msdf_atlas_encode_png(const uint8_t *pixels, int32_t width, int32_t height, int32_t channels,
                                               int32_t y_origin, uint8_t **out_data, size_t *out_size);
/** Accepts NULL. */
MSDF_ATLAS_C_API void msdf_atlas_buffer_free(void *buffer);

#ifdef __cplusplus
}
#endif

#endif

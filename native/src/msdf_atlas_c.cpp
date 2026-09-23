#include "msdf_atlas_c.h"

#include <algorithm>
#include <cmath>
#include <cstddef>
#include <cstdlib>
#include <cstring>
#include <initializer_list>
#include <limits>
#include <map>
#include <memory>
#include <new>
#include <thread>
#include <utility>
#include <vector>

#include <msdf-atlas-gen/msdf-atlas-gen.h>

#define MSDF_ATLAS_C_STRINGIZE_(x) #x
#define MSDF_ATLAS_C_STRINGIZE(x) MSDF_ATLAS_C_STRINGIZE_(x)

static_assert(sizeof(msdf_atlas_options) == 224, "msdf_atlas_options layout");
static_assert(offsetof(msdf_atlas_options, ids) == 16, "msdf_atlas_options layout");
static_assert(offsetof(msdf_atlas_options, em_size) == 32, "msdf_atlas_options layout");
static_assert(offsetof(msdf_atlas_options, miter_limit) == 88, "msdf_atlas_options layout");
static_assert(offsetof(msdf_atlas_options, coloring_seed) == 192, "msdf_atlas_options layout");
static_assert(offsetof(msdf_atlas_options, reserved2) == 220, "msdf_atlas_options layout");
static_assert(sizeof(msdf_atlas_font_metrics) == 48, "msdf_atlas_font_metrics layout");
static_assert(sizeof(msdf_atlas_glyph) == 96, "msdf_atlas_glyph layout");
static_assert(offsetof(msdf_atlas_glyph, advance) == 24, "msdf_atlas_glyph layout");
static_assert(sizeof(msdf_atlas_kerning) == 24, "msdf_atlas_kerning layout");
static_assert(sizeof(msdf_atlas_atlas_metrics) == 56, "msdf_atlas_atlas_metrics layout");

static_assert((int) msdf_atlas::ImageType::HARD_MASK == MSDF_ATLAS_IMAGE_HARD_MASK, "ImageType mapping");
static_assert((int) msdf_atlas::ImageType::SOFT_MASK == MSDF_ATLAS_IMAGE_SOFT_MASK, "ImageType mapping");
static_assert((int) msdf_atlas::ImageType::SDF == MSDF_ATLAS_IMAGE_SDF, "ImageType mapping");
static_assert((int) msdf_atlas::ImageType::PSDF == MSDF_ATLAS_IMAGE_PSDF, "ImageType mapping");
static_assert((int) msdf_atlas::ImageType::MSDF == MSDF_ATLAS_IMAGE_MSDF, "ImageType mapping");
static_assert((int) msdf_atlas::ImageType::MTSDF == MSDF_ATLAS_IMAGE_MTSDF, "ImageType mapping");
static_assert((int) msdf_atlas::DimensionsConstraint::NONE == MSDF_ATLAS_DIM_NONE, "DimensionsConstraint mapping");
static_assert((int) msdf_atlas::DimensionsConstraint::SQUARE == MSDF_ATLAS_DIM_SQUARE, "DimensionsConstraint mapping");
static_assert((int) msdf_atlas::DimensionsConstraint::EVEN_SQUARE == MSDF_ATLAS_DIM_EVEN_SQUARE, "DimensionsConstraint mapping");
static_assert((int) msdf_atlas::DimensionsConstraint::MULTIPLE_OF_FOUR_SQUARE == MSDF_ATLAS_DIM_MULTIPLE_OF_FOUR_SQUARE, "DimensionsConstraint mapping");
static_assert((int) msdf_atlas::DimensionsConstraint::POWER_OF_TWO_RECTANGLE == MSDF_ATLAS_DIM_POWER_OF_TWO_RECTANGLE, "DimensionsConstraint mapping");
static_assert((int) msdf_atlas::DimensionsConstraint::POWER_OF_TWO_SQUARE == MSDF_ATLAS_DIM_POWER_OF_TWO_SQUARE, "DimensionsConstraint mapping");

struct msdf_atlas_result {
    // FontGeometry keeps a pointer to glyphs: glyphs must be declared first and the result never moved.
    std::vector<msdf_atlas::GlyphGeometry> glyphs;
    msdf_atlas::FontGeometry font;
    msdf_atlas::ImageType imageType;
    msdfgen::YAxisOrientation yDirection;
    bool kerning;
    msdfgen::Range pxRange;
    msdf_atlas_atlas_metrics atlas;
    msdf_atlas_font_metrics metrics;
    std::vector<uint8_t> pixels;
    std::vector<msdf_atlas_glyph> glyphInfos;
    std::vector<msdf_atlas_kerning> kerningPairs;

    msdf_atlas_result() :
        glyphs(), font(&glyphs), imageType(msdf_atlas::ImageType::MTSDF), yDirection(msdfgen::Y_UPWARD),
        kerning(false), pxRange(), atlas(), metrics() { }

    msdf_atlas_result(const msdf_atlas_result &) = delete;
    msdf_atlas_result &operator=(const msdf_atlas_result &) = delete;
};

namespace {

using namespace msdf_atlas;

typedef void (*EdgeColoringFunction)(msdfgen::Shape &, double, unsigned long long);

const unsigned long long LCG_MULTIPLIER = 6364136223846793005ull;
const unsigned long long LCG_INCREMENT = 1442695040888963407ull;
const double DEFAULT_EM_SIZE = 32.0;
const double DEFAULT_ANGLE_THRESHOLD = 3.0;
const double DEFAULT_MITER_LIMIT = 1.0;
const double DEFAULT_PIXEL_RANGE = 2.0;

thread_local int32_t lastErrorDetail = 0;

class FreetypeSession {
public:
    FreetypeSession() : library(msdfgen::initializeFreetype()), font(nullptr) { }
    ~FreetypeSession() {
        if (font)
            msdfgen::destroyFont(font);
        if (library)
            msdfgen::deinitializeFreetype(library);
    }
    FreetypeSession(const FreetypeSession &) = delete;
    FreetypeSession &operator=(const FreetypeSession &) = delete;

    msdfgen::FreetypeHandle *library;
    msdfgen::FontHandle *font;
};

bool inRange(int32_t value, int32_t min, int32_t max) {
    return value >= min && value <= max;
}

bool allFinite(std::initializer_list<double> values) {
    for (double value : values)
        if (!std::isfinite(value))
            return false;
    return true;
}

int32_t validateOptions(const msdf_atlas_options &o) {
    if (o.struct_size != sizeof(msdf_atlas_options))
        return MSDF_ATLAS_E_STRUCT_SIZE;
    if (o.abi_version != MSDF_ATLAS_C_ABI_VERSION)
        return MSDF_ATLAS_E_ABI_MISMATCH;
    if (o.reserved0 || o.reserved1 || o.reserved2)
        return MSDF_ATLAS_E_INVALID_ARGUMENT;
    if (!(
        inRange(o.glyph_selection, MSDF_ATLAS_SELECT_ASCII, MSDF_ATLAS_SELECT_ALL_GLYPHS) &&
        inRange(o.image_type, MSDF_ATLAS_IMAGE_HARD_MASK, MSDF_ATLAS_IMAGE_MTSDF) &&
        inRange(o.y_origin, MSDF_ATLAS_Y_ORIGIN_BOTTOM, MSDF_ATLAS_Y_ORIGIN_TOP) &&
        inRange(o.range_units, MSDF_ATLAS_UNITS_PIXELS, MSDF_ATLAS_UNITS_EMS) &&
        inRange(o.dimensions_constraint, MSDF_ATLAS_DIM_NONE, MSDF_ATLAS_DIM_POWER_OF_TWO_SQUARE) &&
        inRange(o.inner_padding_units, MSDF_ATLAS_UNITS_PIXELS, MSDF_ATLAS_UNITS_EMS) &&
        inRange(o.outer_padding_units, MSDF_ATLAS_UNITS_PIXELS, MSDF_ATLAS_UNITS_EMS) &&
        inRange(o.edge_coloring, MSDF_ATLAS_COLORING_SIMPLE, MSDF_ATLAS_COLORING_BY_DISTANCE) &&
        inRange(o.error_correction, MSDF_ATLAS_EC_AUTO_MIXED, MSDF_ATLAS_EC_EDGE_FULL)
    ))
        return MSDF_ATLAS_E_INVALID_ARGUMENT;
    if ((o.glyph_selection == MSDF_ATLAS_SELECT_CODEPOINTS || o.glyph_selection == MSDF_ATLAS_SELECT_GLYPH_INDICES) && !(o.ids && o.id_count > 0))
        return MSDF_ATLAS_E_INVALID_ARGUMENT;
    if ((o.width > 0) != (o.height > 0))
        return MSDF_ATLAS_E_INVALID_ARGUMENT;
    if (!allFinite({
        o.em_size, o.min_em_size, o.range_lower, o.range_upper, o.miter_limit, o.angle_threshold,
        o.inner_padding_left, o.inner_padding_bottom, o.inner_padding_right, o.inner_padding_top,
        o.outer_padding_left, o.outer_padding_bottom, o.outer_padding_right, o.outer_padding_top
    }))
        return MSDF_ATLAS_E_INVALID_ARGUMENT;
    if (o.em_size < 0 || o.min_em_size < 0 || o.miter_limit < 0 || o.thread_count < 0 || o.range_lower > o.range_upper)
        return MSDF_ATLAS_E_INVALID_ARGUMENT;
    if (!(o.spacing == MSDF_ATLAS_SPACING_DEFAULT || o.spacing >= -1))
        return MSDF_ATLAS_E_INVALID_ARGUMENT;
    return MSDF_ATLAS_OK;
}

void applyErrorCorrection(msdfgen::ErrorCorrectionConfig &ec, int32_t preset) {
    typedef msdfgen::ErrorCorrectionConfig EC;
    switch (preset) {
        case MSDF_ATLAS_EC_AUTO_MIXED:
            ec.mode = EC::EDGE_PRIORITY, ec.distanceCheckMode = EC::CHECK_DISTANCE_AT_EDGE;
            break;
        case MSDF_ATLAS_EC_AUTO_FAST:
            ec.mode = EC::EDGE_PRIORITY, ec.distanceCheckMode = EC::DO_NOT_CHECK_DISTANCE;
            break;
        case MSDF_ATLAS_EC_AUTO_FULL:
            ec.mode = EC::EDGE_PRIORITY, ec.distanceCheckMode = EC::ALWAYS_CHECK_DISTANCE;
            break;
        case MSDF_ATLAS_EC_DISABLED:
            ec.mode = EC::DISABLED, ec.distanceCheckMode = EC::DO_NOT_CHECK_DISTANCE;
            break;
        case MSDF_ATLAS_EC_DISTANCE_FAST:
            ec.mode = EC::INDISCRIMINATE, ec.distanceCheckMode = EC::DO_NOT_CHECK_DISTANCE;
            break;
        case MSDF_ATLAS_EC_DISTANCE_FULL:
            ec.mode = EC::INDISCRIMINATE, ec.distanceCheckMode = EC::ALWAYS_CHECK_DISTANCE;
            break;
        case MSDF_ATLAS_EC_EDGE_FAST:
            ec.mode = EC::EDGE_ONLY, ec.distanceCheckMode = EC::DO_NOT_CHECK_DISTANCE;
            break;
        case MSDF_ATLAS_EC_EDGE_FULL:
            ec.mode = EC::EDGE_ONLY, ec.distanceCheckMode = EC::ALWAYS_CHECK_DISTANCE;
            break;
    }
}

int loadGlyphs(msdf_atlas_result &result, msdfgen::FontHandle *font, const msdf_atlas_options &o, int &requested) {
    const bool kerning = o.kerning != 0;
    switch (o.glyph_selection) {
        case MSDF_ATLAS_SELECT_CODEPOINTS:
        case MSDF_ATLAS_SELECT_GLYPH_INDICES: {
            Charset ids;
            for (uint32_t i = 0; i < o.id_count; ++i)
                ids.add(o.ids[i]);
            requested = (int) std::min(ids.size(), (size_t) std::numeric_limits<int>::max());
            if (o.glyph_selection == MSDF_ATLAS_SELECT_CODEPOINTS)
                return result.font.loadCharset(font, 1, ids, false, kerning);
            return result.font.loadGlyphset(font, 1, ids, false, kerning);
        }
        case MSDF_ATLAS_SELECT_ALL_GLYPHS: {
            unsigned count = 0;
            if (!msdfgen::getGlyphCount(count, font))
                return -1;
            requested = (int) std::min(count, (unsigned) std::numeric_limits<int>::max());
            return result.font.loadGlyphRange(font, 1, 0, count, false, kerning);
        }
        default:
            requested = (int) Charset::ASCII.size();
            return result.font.loadCharset(font, 1, Charset::ASCII, false, kerning);
    }
}

void colorEdges(std::vector<GlyphGeometry> &glyphs, const msdf_atlas_options &o, int threadCount) {
    const double angleThreshold = o.angle_threshold > 0 ? o.angle_threshold : DEFAULT_ANGLE_THRESHOLD;
    const unsigned long long seed = o.coloring_seed;
    if (o.edge_coloring == MSDF_ATLAS_COLORING_BY_DISTANCE) {
        Workload([&glyphs, angleThreshold, seed](int i, int) -> bool {
            unsigned long long glyphSeed = (LCG_MULTIPLIER*(seed^i)+LCG_INCREMENT)*!!seed;
            glyphs[i].edgeColoring(&msdfgen::edgeColoringByDistance, angleThreshold, glyphSeed);
            return true;
        }, (int) glyphs.size()).finish(threadCount);
    } else {
        EdgeColoringFunction coloring = o.edge_coloring == MSDF_ATLAS_COLORING_SIMPLE ? &msdfgen::edgeColoringSimple : &msdfgen::edgeColoringInkTrap;
        unsigned long long glyphSeed = seed;
        for (GlyphGeometry &glyph : glyphs) {
            glyphSeed *= LCG_MULTIPLIER;
            glyph.edgeColoring(coloring, angleThreshold, glyphSeed);
        }
    }
}

template <int N, GeneratorFunction<float, N> GEN_FN>
void generateAtlas(msdf_atlas_result &result, const GeneratorAttributes &attributes, int threadCount) {
    ImmediateAtlasGenerator<float, N, GEN_FN, BitmapAtlasStorage<byte, N> > generator(result.atlas.width, result.atlas.height);
    generator.setAttributes(attributes);
    generator.setThreadCount(threadCount);
    generator.generate(result.glyphs.data(), (int) result.glyphs.size());
    msdfgen::BitmapConstSection<byte, N> bitmap = generator.atlasStorage().operator msdfgen::BitmapConstSection<byte, N>();
    bitmap.reorient(result.yDirection);
    const size_t rowSize = (size_t) N*(size_t) bitmap.width;
    result.pixels.resize(rowSize*(size_t) bitmap.height);
    for (int y = 0; y < bitmap.height; ++y)
        memcpy(result.pixels.data()+rowSize*(size_t) y, bitmap(0, y), rowSize);
    result.atlas.channels = N;
}

void fillLayout(msdf_atlas_result &result) {
    const bool downward = result.yDirection == msdfgen::Y_DOWNWARD;
    const double yFactor = downward ? -1 : 1;
    const double height = result.atlas.height;

    const msdfgen::FontMetrics &fontMetrics = result.font.getMetrics();
    result.metrics.em_size = fontMetrics.emSize;
    result.metrics.ascender_y = yFactor*fontMetrics.ascenderY;
    result.metrics.descender_y = yFactor*fontMetrics.descenderY;
    result.metrics.line_height = fontMetrics.lineHeight;
    result.metrics.underline_y = yFactor*fontMetrics.underlineY;
    result.metrics.underline_thickness = fontMetrics.underlineThickness;

    result.glyphInfos.reserve(result.glyphs.size());
    for (const GlyphGeometry &glyph : result.font.getGlyphs()) {
        msdf_atlas_glyph info = msdf_atlas_glyph();
        info.glyph_index = glyph.getIndex();
        info.codepoint = glyph.getCodepoint();
        info.is_whitespace = glyph.isWhitespace() ? 1 : 0;
        info.advance = glyph.getAdvance();
        double l, b, r, t;
        glyph.getQuadPlaneBounds(l, b, r, t);
        if (l || b || r || t) {
            info.has_plane_bounds = 1;
            info.plane_left = l;
            info.plane_right = r;
            info.plane_bottom = downward ? -b : b;
            info.plane_top = downward ? -t : t;
        }
        glyph.getQuadAtlasBounds(l, b, r, t);
        if (l || b || r || t) {
            info.has_atlas_bounds = 1;
            info.atlas_left = l;
            info.atlas_right = r;
            info.atlas_bottom = downward ? height-b : b;
            info.atlas_top = downward ? height-t : t;
        }
        result.glyphInfos.push_back(info);
    }

    const std::map<std::pair<int, int>, double> &kerning = result.font.getKerning();
    result.kerningPairs.reserve(kerning.size());
    for (const std::pair<const std::pair<int, int>, double> &entry : kerning) {
        msdf_atlas_kerning pair = msdf_atlas_kerning();
        pair.glyph_index1 = entry.first.first;
        pair.glyph_index2 = entry.first.second;
        const GlyphGeometry *glyph1 = result.font.getGlyph(msdfgen::GlyphIndex((unsigned) entry.first.first));
        const GlyphGeometry *glyph2 = result.font.getGlyph(msdfgen::GlyphIndex((unsigned) entry.first.second));
        pair.codepoint1 = glyph1 ? glyph1->getCodepoint() : 0;
        pair.codepoint2 = glyph2 ? glyph2->getCodepoint() : 0;
        pair.advance = entry.second;
        result.kerningPairs.push_back(pair);
    }
}

int32_t generate(const msdf_atlas_options &o, const uint8_t *fontData, size_t fontSize, msdf_atlas_result **outResult) {
    if (int32_t status = validateOptions(o))
        return status;
    if (fontSize == 0 || fontSize > (size_t) std::numeric_limits<int>::max())
        return MSDF_ATLAS_E_INVALID_ARGUMENT;

    const ImageType imageType = (ImageType) o.image_type;
    const bool isMultiChannel = imageType == ImageType::MSDF || imageType == ImageType::MTSDF;
    const bool isMask = imageType == ImageType::HARD_MASK || imageType == ImageType::SOFT_MASK;
    const bool fixedDimensions = o.width > 0 && o.height > 0;
    const int threadCount = o.thread_count > 0 ? o.thread_count : std::max((int) std::thread::hardware_concurrency(), 1);

    FreetypeSession freetype;
    if (!freetype.library)
        return MSDF_ATLAS_E_INTERNAL;
    freetype.font = msdfgen::loadFontData(freetype.library, fontData, (int) fontSize);
    if (!freetype.font)
        return MSDF_ATLAS_E_FONT_LOAD;

    std::unique_ptr<msdf_atlas_result> result(new msdf_atlas_result());
    result->imageType = imageType;
    result->yDirection = o.y_origin == MSDF_ATLAS_Y_ORIGIN_TOP ? msdfgen::Y_DOWNWARD : msdfgen::Y_UPWARD;
    result->kerning = o.kerning != 0;

    int requested = 0;
    if (loadGlyphs(*result, freetype.font, o, requested) < 0)
        return MSDF_ATLAS_E_FONT_LOAD;
    if (result->glyphs.empty())
        return MSDF_ATLAS_E_NO_GLYPHS;

    msdfgen::Range pxRange = 0, emRange = 0;
    if (isMask)
        pxRange = 1;
    else if (o.range_lower == o.range_upper)
        pxRange = DEFAULT_PIXEL_RANGE;
    else if (o.range_units == MSDF_ATLAS_UNITS_EMS)
        emRange = msdfgen::Range(o.range_lower, o.range_upper);
    else
        pxRange = msdfgen::Range(o.range_lower, o.range_upper);

    double minEmSize = std::max(o.em_size, o.min_em_size);
    if (!fixedDimensions && !(minEmSize > 0))
        minEmSize = DEFAULT_EM_SIZE;

    const Padding innerPadding(o.inner_padding_left, o.inner_padding_bottom, o.inner_padding_right, o.inner_padding_top);
    const Padding outerPadding(o.outer_padding_left, o.outer_padding_bottom, o.outer_padding_right, o.outer_padding_top);

    TightAtlasPacker packer;
    if (fixedDimensions)
        packer.setDimensions(o.width, o.height);
    else if (o.dimensions_constraint == MSDF_ATLAS_DIM_NONE)
        packer.setDimensionsConstraint(DimensionsConstraint::MULTIPLE_OF_FOUR_SQUARE);
    else
        packer.setDimensionsConstraint((DimensionsConstraint) o.dimensions_constraint);
    packer.setSpacing(o.spacing != MSDF_ATLAS_SPACING_DEFAULT ? o.spacing : isMultiChannel ? 0 : -1);
    if (o.em_size > 0)
        packer.setScale(o.em_size);
    else
        packer.setMinimumScale(minEmSize);
    packer.setPixelRange(pxRange);
    packer.setUnitRange(emRange);
    packer.setMiterLimit(imageType == ImageType::PSDF || isMultiChannel ? o.miter_limit : 0);
    packer.setOriginPixelAlignment(o.px_align_origin_x != 0, o.px_align_origin_y != 0);
    if (o.inner_padding_units == MSDF_ATLAS_UNITS_EMS)
        packer.setInnerUnitPadding(innerPadding);
    else
        packer.setInnerPixelPadding(innerPadding);
    if (o.outer_padding_units == MSDF_ATLAS_UNITS_EMS)
        packer.setOuterUnitPadding(outerPadding);
    else
        packer.setOuterPixelPadding(outerPadding);

    const int remaining = packer.pack(result->glyphs.data(), (int) result->glyphs.size());
    if (remaining < 0)
        return MSDF_ATLAS_E_PACK_FAILED;
    if (remaining > 0) {
        lastErrorDetail = remaining;
        return MSDF_ATLAS_E_PACK_DID_NOT_FIT;
    }

    int width = 0, height = 0;
    packer.getDimensions(width, height);
    if (!(width > 0 && height > 0))
        return MSDF_ATLAS_E_ATLAS_SIZE;
    result->pxRange = packer.getPixelRange();
    result->atlas.scale = packer.getScale();
    result->atlas.px_range_lower = result->pxRange.lower;
    result->atlas.px_range_upper = result->pxRange.upper;
    result->atlas.width = width;
    result->atlas.height = height;
    result->atlas.image_type = o.image_type;
    result->atlas.y_origin = o.y_origin;
    result->atlas.glyphs_loaded = (int32_t) result->glyphs.size();
    result->atlas.glyphs_requested = requested;

    if (isMultiChannel)
        colorEdges(result->glyphs, o, threadCount);

    GeneratorAttributes attributes = GeneratorAttributes();
    attributes.config.overlapSupport = o.overlap_support != 0;
    attributes.scanlinePass = o.scanline_pass != 0;
    applyErrorCorrection(attributes.config.errorCorrection, o.error_correction);
    if (attributes.scanlinePass)
        attributes.config.errorCorrection.distanceCheckMode = msdfgen::ErrorCorrectionConfig::DO_NOT_CHECK_DISTANCE;

    switch (imageType) {
        case ImageType::HARD_MASK:
            generateAtlas<1, scanlineGenerator>(*result, attributes, threadCount);
            break;
        case ImageType::SOFT_MASK:
        case ImageType::SDF:
            generateAtlas<1, sdfGenerator>(*result, attributes, threadCount);
            break;
        case ImageType::PSDF:
            generateAtlas<1, psdfGenerator>(*result, attributes, threadCount);
            break;
        case ImageType::MSDF:
            generateAtlas<3, msdfGenerator>(*result, attributes, threadCount);
            break;
        case ImageType::MTSDF:
            generateAtlas<4, mtsdfGenerator>(*result, attributes, threadCount);
            break;
    }

    fillLayout(*result);
    *outResult = result.release();
    return MSDF_ATLAS_OK;
}

template <int N>
bool encodePngBuffer(std::vector<byte> &output, const uint8_t *pixels, int width, int height, msdfgen::YAxisOrientation yDirection) {
    return encodePng(output, msdfgen::BitmapConstSection<byte, N>(pixels, width, height, yDirection));
}

}

uint32_t msdf_atlas_abi_version(void) {
    return MSDF_ATLAS_C_ABI_VERSION;
}

const char *msdf_atlas_upstream_version(void) {
    return MSDF_ATLAS_C_STRINGIZE(MSDF_ATLAS_VERSION);
}

const char *msdf_atlas_msdfgen_version(void) {
    return MSDF_ATLAS_C_STRINGIZE(MSDFGEN_VERSION);
}

uint32_t msdf_atlas_options_size(void) {
    return sizeof(msdf_atlas_options);
}

void msdf_atlas_options_init(msdf_atlas_options *options) {
    if (!options)
        return;
    memset(options, 0, sizeof(msdf_atlas_options));
    options->struct_size = sizeof(msdf_atlas_options);
    options->abi_version = MSDF_ATLAS_C_ABI_VERSION;
    options->glyph_selection = MSDF_ATLAS_SELECT_ASCII;
    options->image_type = MSDF_ATLAS_IMAGE_MTSDF;
    options->y_origin = MSDF_ATLAS_Y_ORIGIN_BOTTOM;
    options->em_size = DEFAULT_EM_SIZE;
    options->range_units = MSDF_ATLAS_UNITS_PIXELS;
    options->range_lower = -.5*DEFAULT_PIXEL_RANGE;
    options->range_upper = .5*DEFAULT_PIXEL_RANGE;
    options->dimensions_constraint = MSDF_ATLAS_DIM_MULTIPLE_OF_FOUR_SQUARE;
    options->spacing = MSDF_ATLAS_SPACING_DEFAULT;
    options->miter_limit = DEFAULT_MITER_LIMIT;
    options->px_align_origin_y = 1;
    options->inner_padding_units = MSDF_ATLAS_UNITS_EMS;
    options->outer_padding_units = MSDF_ATLAS_UNITS_EMS;
    options->edge_coloring = MSDF_ATLAS_COLORING_INKTRAP;
    options->angle_threshold = DEFAULT_ANGLE_THRESHOLD;
    options->error_correction = MSDF_ATLAS_EC_AUTO_MIXED;
    options->overlap_support = 1;
    options->scanline_pass = 1;
    options->kerning = 1;
}

const char *msdf_atlas_error_string(int32_t status) {
    switch (status) {
        case MSDF_ATLAS_OK: return "Success.";
        case MSDF_ATLAS_E_INVALID_ARGUMENT: return "An argument or option value is invalid.";
        case MSDF_ATLAS_E_STRUCT_SIZE: return "The options structure size does not match this library.";
        case MSDF_ATLAS_E_ABI_MISMATCH: return "The options ABI version does not match this library.";
        case MSDF_ATLAS_E_FONT_LOAD: return "The font could not be loaded.";
        case MSDF_ATLAS_E_NO_GLYPHS: return "None of the selected glyphs exist in the font.";
        case MSDF_ATLAS_E_PACK_FAILED: return "The glyphs could not be packed into an atlas.";
        case MSDF_ATLAS_E_PACK_DID_NOT_FIT: return "Some glyphs did not fit into the atlas.";
        case MSDF_ATLAS_E_ATLAS_SIZE: return "The atlas dimensions could not be determined.";
        case MSDF_ATLAS_E_ENCODE_FAILED: return "The image could not be encoded.";
        case MSDF_ATLAS_E_IO: return "The output file could not be written.";
        case MSDF_ATLAS_E_OUT_OF_MEMORY: return "Out of memory.";
        case MSDF_ATLAS_E_INTERNAL: return "Internal error.";
    }
    return "Unknown status.";
}

int32_t msdf_atlas_last_error_detail(void) {
    return lastErrorDetail;
}

int32_t msdf_atlas_generate(const msdf_atlas_options *options, const uint8_t *font_data, size_t font_size, msdf_atlas_result **out_result) {
    lastErrorDetail = 0;
    if (!out_result)
        return MSDF_ATLAS_E_INVALID_ARGUMENT;
    *out_result = nullptr;
    if (!options || !font_data)
        return MSDF_ATLAS_E_INVALID_ARGUMENT;
    try {
        return generate(*options, font_data, font_size, out_result);
    } catch (const std::bad_alloc &) {
        return MSDF_ATLAS_E_OUT_OF_MEMORY;
    } catch (...) {
        return MSDF_ATLAS_E_INTERNAL;
    }
}

void msdf_atlas_result_free(msdf_atlas_result *result) {
    delete result;
}

const uint8_t *msdf_atlas_result_image(const msdf_atlas_result *result, int32_t *width, int32_t *height, int32_t *channels) {
    if (!result)
        return nullptr;
    if (width)
        *width = result->atlas.width;
    if (height)
        *height = result->atlas.height;
    if (channels)
        *channels = result->atlas.channels;
    return result->pixels.data();
}

const msdf_atlas_atlas_metrics *msdf_atlas_result_atlas_metrics(const msdf_atlas_result *result) {
    return result ? &result->atlas : nullptr;
}

const msdf_atlas_font_metrics *msdf_atlas_result_font_metrics(const msdf_atlas_result *result) {
    return result ? &result->metrics : nullptr;
}

const msdf_atlas_glyph *msdf_atlas_result_glyphs(const msdf_atlas_result *result, uint32_t *count) {
    if (count)
        *count = result ? (uint32_t) result->glyphInfos.size() : 0;
    return result ? result->glyphInfos.data() : nullptr;
}

const msdf_atlas_kerning *msdf_atlas_result_kerning(const msdf_atlas_result *result, uint32_t *count) {
    if (count)
        *count = result ? (uint32_t) result->kerningPairs.size() : 0;
    return result ? result->kerningPairs.data() : nullptr;
}

int32_t msdf_atlas_result_export_json(const msdf_atlas_result *result, const char *utf8_path) {
    if (!result || !utf8_path)
        return MSDF_ATLAS_E_INVALID_ARGUMENT;
    try {
        msdf_atlas::JsonAtlasMetrics metrics = msdf_atlas::JsonAtlasMetrics();
        metrics.distanceRange = result->pxRange;
        metrics.size = result->atlas.scale;
        metrics.width = result->atlas.width;
        metrics.height = result->atlas.height;
        metrics.yDirection = result->yDirection;
        metrics.grid = nullptr;
        return msdf_atlas::exportJSON(&result->font, 1, result->imageType, metrics, utf8_path, result->kerning) ? MSDF_ATLAS_OK : MSDF_ATLAS_E_IO;
    } catch (const std::bad_alloc &) {
        return MSDF_ATLAS_E_OUT_OF_MEMORY;
    } catch (...) {
        return MSDF_ATLAS_E_INTERNAL;
    }
}

int32_t msdf_atlas_encode_png(const uint8_t *pixels, int32_t width, int32_t height, int32_t channels, int32_t y_origin, uint8_t **out_data, size_t *out_size) {
    if (!out_data || !out_size)
        return MSDF_ATLAS_E_INVALID_ARGUMENT;
    *out_data = nullptr;
    *out_size = 0;
    if (!pixels || width <= 0 || height <= 0 || !(channels == 1 || channels == 3 || channels == 4) || !inRange(y_origin, MSDF_ATLAS_Y_ORIGIN_BOTTOM, MSDF_ATLAS_Y_ORIGIN_TOP))
        return MSDF_ATLAS_E_INVALID_ARGUMENT;
    if ((size_t) channels*(size_t) width*(size_t) height > (size_t) std::numeric_limits<int>::max())
        return MSDF_ATLAS_E_INVALID_ARGUMENT;
    try {
        const msdfgen::YAxisOrientation yDirection = y_origin == MSDF_ATLAS_Y_ORIGIN_TOP ? msdfgen::Y_DOWNWARD : msdfgen::Y_UPWARD;
        std::vector<msdf_atlas::byte> png;
        bool encoded = false;
        switch (channels) {
            case 1:
                encoded = encodePngBuffer<1>(png, pixels, width, height, yDirection);
                break;
            case 3:
                encoded = encodePngBuffer<3>(png, pixels, width, height, yDirection);
                break;
            case 4:
                encoded = encodePngBuffer<4>(png, pixels, width, height, yDirection);
                break;
        }
        if (!encoded || png.empty())
            return MSDF_ATLAS_E_ENCODE_FAILED;
        uint8_t *buffer = static_cast<uint8_t *>(malloc(png.size()));
        if (!buffer)
            return MSDF_ATLAS_E_OUT_OF_MEMORY;
        memcpy(buffer, png.data(), png.size());
        *out_data = buffer;
        *out_size = png.size();
        return MSDF_ATLAS_OK;
    } catch (const std::bad_alloc &) {
        return MSDF_ATLAS_E_OUT_OF_MEMORY;
    } catch (...) {
        return MSDF_ATLAS_E_INTERNAL;
    }
}

void msdf_atlas_buffer_free(void *buffer) {
    free(buffer);
}

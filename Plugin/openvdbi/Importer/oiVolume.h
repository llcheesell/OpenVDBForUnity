#pragma once

class oiContext;
struct oiVolumeData;
struct oiVolumeSummary;

class oiVolume
{
public:
    oiVolume(const openvdb::FloatGrid& grid, const openvdb::Coord& extents);
    virtual ~oiVolume();

    void reset();
    void setScaleFactor(float scaleFactor);
    void setNormalizationRange(float minVal, float maxVal);
    void fillTextureBuffer(oiVolumeData& data) const;
    const oiVolumeSummary& getSummary() const;
private:
    oiVolumeSummary* m_summary = nullptr;
    float m_scaleFactor = 1.0f;
    float m_fixedMin = 0.0f;
    float m_fixedMax = 0.0f;
    const openvdb::FloatGrid& m_grid;
    openvdb::Coord m_extents;
};
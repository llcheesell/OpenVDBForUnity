using UnityEngine;

namespace OpenVDB.Realtime
{
    /// <summary>
    /// Utility class for converting and optimizing volume data for real-time rendering.
    /// Provides methods to analyze volumes, compute statistics, and prepare data
    /// for the GPU pipeline.
    /// </summary>
    public static class VolumeDataConverter
    {
        /// <summary>
        /// Statistics about a volume texture, useful for auto-configuring rendering parameters.
        /// </summary>
        public struct VolumeStats
        {
            public float minValue;
            public float maxValue;
            public float meanValue;
            public float occupancyRatio; // Fraction of non-zero voxels
            public int totalVoxels;
            public int occupiedVoxels;
            public Vector3Int dimensions;
        }

        /// <summary>
        /// Analyze a volume texture to compute statistics.
        /// Runs on the CPU, so best called during import or initialization.
        /// </summary>
        public static VolumeStats AnalyzeVolume(Texture3D volume)
        {
            var stats = new VolumeStats();
            stats.dimensions = new Vector3Int(volume.width, volume.height, volume.depth);
            stats.totalVoxels = volume.width * volume.height * volume.depth;

            var pixels = volume.GetPixels();
            float minVal = float.MaxValue;
            float maxVal = float.MinValue;
            double sum = 0;
            int occupied = 0;
            float threshold = 0.001f;

            for (int i = 0; i < pixels.Length; i++)
            {
                float val = pixels[i].r;
                if (val < minVal) minVal = val;
                if (val > maxVal) maxVal = val;
                sum += val;
                if (val > threshold) occupied++;
            }

            stats.minValue = minVal;
            stats.maxValue = maxVal;
            stats.meanValue = pixels.Length > 0 ? (float)(sum / pixels.Length) : 0f;
            stats.occupiedVoxels = occupied;
            stats.occupancyRatio = pixels.Length > 0 ? (float)occupied / pixels.Length : 0f;

            return stats;
        }

        /// <summary>
        /// Suggests quality parameters based on volume statistics and target frame time.
        /// </summary>
        public static VolumeQualityPreset SuggestQuality(VolumeStats stats, float targetFPS = 60f)
        {
            var preset = ScriptableObject.CreateInstance<VolumeQualityPreset>();

            // Larger volumes need bigger steps
            int maxDim = Mathf.Max(stats.dimensions.x, Mathf.Max(stats.dimensions.y, stats.dimensions.z));

            if (maxDim <= 64)
            {
                // Small volume - can afford high quality
                preset.stepDistance = 0.005f;
                preset.maxSteps = 256;
                preset.shadowSteps = 8;
            }
            else if (maxDim <= 128)
            {
                preset.stepDistance = 0.008f;
                preset.maxSteps = 200;
                preset.shadowSteps = 6;
            }
            else if (maxDim <= 256)
            {
                preset.stepDistance = 0.012f;
                preset.maxSteps = 150;
                preset.shadowSteps = 4;
            }
            else
            {
                // Large volume - prioritize performance
                preset.stepDistance = 0.02f;
                preset.maxSteps = 100;
                preset.shadowSteps = 3;
            }

            // More sparse volumes benefit more from empty space skipping
            preset.emptySpaceSkipping = stats.occupancyRatio < 0.5f;
            preset.temporalJitter = true;
            preset.adaptiveStepping = true;
            preset.henyeyGreensteinPhase = maxDim <= 128;
            preset.multiScatterApprox = false;
            preset.temporalReprojection = targetFPS <= 30f;

            // Occupancy grid resolution
            preset.occupancyGridDivisor = maxDim <= 128 ? 4 : 8;

            return preset;
        }

        /// <summary>
        /// Creates a normalized version of the volume texture (values mapped to 0-1 range).
        /// </summary>
        public static Texture3D NormalizeVolume(Texture3D source)
        {
            var stats = AnalyzeVolume(source);

            if (stats.maxValue <= 0 || Mathf.Approximately(stats.minValue, stats.maxValue))
                return source;

            var pixels = source.GetPixels();
            float range = stats.maxValue - stats.minValue;

            for (int i = 0; i < pixels.Length; i++)
            {
                float normalized = (pixels[i].r - stats.minValue) / range;
                pixels[i] = new Color(normalized, normalized, normalized, normalized);
            }

            var result = new Texture3D(source.width, source.height, source.depth, source.format, false);
            result.SetPixels(pixels);
            result.Apply();
            result.name = source.name + "_normalized";

            return result;
        }

        /// <summary>
        /// Downsamples a volume texture by the given factor.
        /// Useful for LOD generation.
        /// </summary>
        public static Texture3D DownsampleVolume(Texture3D source, int factor = 2)
        {
            int newW = Mathf.Max(1, source.width / factor);
            int newH = Mathf.Max(1, source.height / factor);
            int newD = Mathf.Max(1, source.depth / factor);

            var srcPixels = source.GetPixels();
            var dstPixels = new Color[newW * newH * newD];

            for (int z = 0; z < newD; z++)
            {
                for (int y = 0; y < newH; y++)
                {
                    for (int x = 0; x < newW; x++)
                    {
                        // Average the block of voxels
                        float sum = 0;
                        int count = 0;

                        for (int dz = 0; dz < factor; dz++)
                        {
                            for (int dy = 0; dy < factor; dy++)
                            {
                                for (int dx = 0; dx < factor; dx++)
                                {
                                    int sx = Mathf.Min(x * factor + dx, source.width - 1);
                                    int sy = Mathf.Min(y * factor + dy, source.height - 1);
                                    int sz = Mathf.Min(z * factor + dz, source.depth - 1);
                                    int srcIdx = sx + sy * source.width + sz * source.width * source.height;
                                    sum += srcPixels[srcIdx].r;
                                    count++;
                                }
                            }
                        }

                        float avg = sum / count;
                        int dstIdx = x + y * newW + z * newW * newH;
                        dstPixels[dstIdx] = new Color(avg, avg, avg, avg);
                    }
                }
            }

            var result = new Texture3D(newW, newH, newD, source.format, false);
            result.SetPixels(dstPixels);
            result.Apply();
            result.name = source.name + $"_lod{factor}";

            return result;
        }
    }
}

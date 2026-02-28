using UnityEngine;

namespace OpenVDB.Realtime
{
    /// <summary>
    /// Generates an occupancy grid from a dense volume Texture3D using a compute shader.
    /// The occupancy grid marks which regions of the volume contain data,
    /// enabling empty space skipping during ray marching.
    /// </summary>
    public class OccupancyGridGenerator
    {
        ComputeShader m_computeShader;
        int m_buildKernel;
        int m_mipKernel;

        static readonly int s_sourceVolumeId = Shader.PropertyToID("_SourceVolume");
        static readonly int s_occupancyGridId = Shader.PropertyToID("_OccupancyGrid");
        static readonly int s_sourceSizeId = Shader.PropertyToID("_SourceSize");
        static readonly int s_occupancySizeId = Shader.PropertyToID("_OccupancySize");
        static readonly int s_densityThresholdId = Shader.PropertyToID("_DensityThreshold");
        static readonly int s_mipSourceId = Shader.PropertyToID("_OccupancyMipSource");
        static readonly int s_mipDestId = Shader.PropertyToID("_OccupancyMipDest");
        static readonly int s_mipSourceSizeId = Shader.PropertyToID("_MipSourceSize");

        const int ThreadGroupSize = 4; // Must match [numthreads(4,4,4)] in compute shader

        public OccupancyGridGenerator(ComputeShader computeShader)
        {
            m_computeShader = computeShader;
            m_buildKernel = m_computeShader.FindKernel("BuildOccupancyGrid");
            m_mipKernel = m_computeShader.FindKernel("BuildMipChain");
        }

        /// <summary>
        /// Builds an occupancy grid from the given volume texture.
        /// </summary>
        public RenderTexture Generate(Texture3D sourceVolume, int divisor = 8, float densityThreshold = 0.001f)
        {
            return GenerateInternal(sourceVolume, sourceVolume.width, sourceVolume.height, sourceVolume.depth, divisor, densityThreshold);
        }

        /// <summary>
        /// Builds an occupancy grid from a RenderTexture source (for runtime updates).
        /// </summary>
        public RenderTexture Generate(RenderTexture sourceVolume, int srcW, int srcH, int srcD, int divisor = 8, float densityThreshold = 0.001f)
        {
            return GenerateInternal(sourceVolume, srcW, srcH, srcD, divisor, densityThreshold);
        }

        RenderTexture GenerateInternal(Texture sourceVolume, int srcW, int srcH, int srcD, int divisor, float densityThreshold)
        {
            int occW = Mathf.Max(1, srcW / divisor);
            int occH = Mathf.Max(1, srcH / divisor);
            int occD = Mathf.Max(1, srcD / divisor);

            var occupancyGrid = CreateVolumeRT(occW, occH, occD, RenderTextureFormat.RFloat);

            m_computeShader.SetTexture(m_buildKernel, s_sourceVolumeId, sourceVolume);
            m_computeShader.SetTexture(m_buildKernel, s_occupancyGridId, occupancyGrid);
            m_computeShader.SetInts(s_sourceSizeId, srcW, srcH, srcD);
            m_computeShader.SetInts(s_occupancySizeId, occW, occH, occD);
            m_computeShader.SetFloat(s_densityThresholdId, densityThreshold);

            int groupsX = Mathf.CeilToInt((float)occW / ThreadGroupSize);
            int groupsY = Mathf.CeilToInt((float)occH / ThreadGroupSize);
            int groupsZ = Mathf.CeilToInt((float)occD / ThreadGroupSize);

            m_computeShader.Dispatch(m_buildKernel, groupsX, groupsY, groupsZ);

            return occupancyGrid;
        }

        static RenderTexture CreateVolumeRT(int width, int height, int depth, RenderTextureFormat format)
        {
            var rt = new RenderTexture(width, height, 0, format);
            rt.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            rt.volumeDepth = depth;
            rt.enableRandomWrite = true;
            rt.filterMode = FilterMode.Point;
            rt.wrapMode = TextureWrapMode.Clamp;
            rt.Create();
            return rt;
        }
    }
}

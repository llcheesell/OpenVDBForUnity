using UnityEngine;
using UnityEngine.Rendering;

namespace OpenVDB.Realtime
{
    /// <summary>
    /// Generates a sparse brick atlas from a dense volume texture.
    /// Bricks that contain no data are excluded, reducing GPU memory usage.
    /// An indirection texture maps from volume space to atlas coordinates.
    /// </summary>
    public class BrickMapGenerator : System.IDisposable
    {
        ComputeShader m_computeShader;
        int m_countKernel;
        int m_buildAtlasKernel;
        int m_buildIndirectionKernel;

        static readonly int s_sourceVolumeId = Shader.PropertyToID("_SourceVolume");
        static readonly int s_brickAtlasId = Shader.PropertyToID("_BrickAtlas");
        static readonly int s_indirectionId = Shader.PropertyToID("_IndirectionTexture");
        static readonly int s_occupancyGridId = Shader.PropertyToID("_OccupancyGrid");
        static readonly int s_activeBrickListId = Shader.PropertyToID("_ActiveBrickList");
        static readonly int s_activeBrickCountId = Shader.PropertyToID("_ActiveBrickCount");
        static readonly int s_sourceSizeId = Shader.PropertyToID("_SourceSize");
        static readonly int s_brickGridSizeId = Shader.PropertyToID("_BrickGridSize");
        static readonly int s_brickSizeId = Shader.PropertyToID("_BrickSize");
        static readonly int s_atlasSizeId = Shader.PropertyToID("_AtlasSize");
        static readonly int s_atlasBricksPerRowId = Shader.PropertyToID("_AtlasBricksPerRow");
        static readonly int s_atlasBricksPerSliceId = Shader.PropertyToID("_AtlasBricksPerSlice");
        static readonly int s_densityThresholdId = Shader.PropertyToID("_DensityThreshold");

        public BrickMapGenerator(ComputeShader computeShader)
        {
            m_computeShader = computeShader;
            m_countKernel = m_computeShader.FindKernel("CountActiveBricks");
            m_buildAtlasKernel = m_computeShader.FindKernel("BuildBrickAtlas");
            m_buildIndirectionKernel = m_computeShader.FindKernel("BuildIndirectionTable");
        }

        public struct BrickMapResult
        {
            public RenderTexture brickAtlas;
            public RenderTexture indirectionTexture;
            public int activeBrickCount;
            public Vector3Int brickGridSize;
            public int brickSize;
        }

        /// <summary>
        /// Generates a brick map from a dense volume texture.
        /// </summary>
        public BrickMapResult Generate(
            Texture3D sourceVolume,
            RenderTexture occupancyGrid,
            int brickSize = 8,
            float densityThreshold = 0.001f)
        {
            int srcW = sourceVolume.width;
            int srcH = sourceVolume.height;
            int srcD = sourceVolume.depth;

            int gridW = Mathf.Max(1, srcW / brickSize);
            int gridH = Mathf.Max(1, srcH / brickSize);
            int gridD = Mathf.Max(1, srcD / brickSize);

            int maxBricks = gridW * gridH * gridD;

            // Step 1: Count active bricks
            var activeBrickList = new ComputeBuffer(Mathf.Max(1, maxBricks), sizeof(uint) * 3);
            var activeBrickCount = new ComputeBuffer(1, sizeof(uint));
            activeBrickCount.SetData(new uint[] { 0 });

            m_computeShader.SetTexture(m_countKernel, s_occupancyGridId, occupancyGrid);
            m_computeShader.SetBuffer(m_countKernel, s_activeBrickListId, activeBrickList);
            m_computeShader.SetBuffer(m_countKernel, s_activeBrickCountId, activeBrickCount);
            m_computeShader.SetInts(s_brickGridSizeId, gridW, gridH, gridD);
            m_computeShader.SetFloat(s_densityThresholdId, densityThreshold);

            int groupsX = Mathf.CeilToInt(gridW / 4f);
            int groupsY = Mathf.CeilToInt(gridH / 4f);
            int groupsZ = Mathf.CeilToInt(gridD / 4f);
            m_computeShader.Dispatch(m_countKernel, groupsX, groupsY, groupsZ);

            // Read back count
            uint[] countData = new uint[1];
            activeBrickCount.GetData(countData);
            int numActive = Mathf.Max(1, (int)countData[0]);

            // Step 2: Compute atlas layout
            int bricksPerRow = Mathf.CeilToInt(Mathf.Pow(numActive, 1f / 3f));
            int bricksPerSlice = bricksPerRow;
            int atlasSlices = Mathf.CeilToInt((float)numActive / (bricksPerRow * bricksPerSlice));

            int atlasW = bricksPerRow * brickSize;
            int atlasH = bricksPerSlice * brickSize;
            int atlasD = atlasSlices * brickSize;

            // Step 3: Build atlas
            var brickAtlas = CreateVolumeRT(atlasW, atlasH, atlasD, RenderTextureFormat.RFloat);

            m_computeShader.SetTexture(m_buildAtlasKernel, s_sourceVolumeId, sourceVolume);
            m_computeShader.SetTexture(m_buildAtlasKernel, s_brickAtlasId, brickAtlas);
            m_computeShader.SetBuffer(m_buildAtlasKernel, s_activeBrickListId, activeBrickList);
            m_computeShader.SetBuffer(m_buildAtlasKernel, s_activeBrickCountId, activeBrickCount);
            m_computeShader.SetInts(s_sourceSizeId, srcW, srcH, srcD);
            m_computeShader.SetInt(s_brickSizeId, brickSize);
            m_computeShader.SetInts(s_atlasSizeId, atlasW, atlasH, atlasD);
            m_computeShader.SetInt(s_atlasBricksPerRowId, bricksPerRow);
            m_computeShader.SetInt(s_atlasBricksPerSliceId, bricksPerSlice);

            int atlasGroupsZ = Mathf.CeilToInt(brickSize * numActive / 4f);
            m_computeShader.Dispatch(m_buildAtlasKernel,
                Mathf.CeilToInt(brickSize / 4f),
                Mathf.CeilToInt(brickSize / 4f),
                Mathf.Max(1, atlasGroupsZ));

            // Step 4: Build indirection table
            var indirectionTexture = CreateVolumeRT(gridW, gridH, gridD, RenderTextureFormat.ARGBHalf);

            m_computeShader.SetTexture(m_buildIndirectionKernel, s_indirectionId, indirectionTexture);
            m_computeShader.SetBuffer(m_buildIndirectionKernel, s_activeBrickListId, activeBrickList);
            m_computeShader.SetBuffer(m_buildIndirectionKernel, s_activeBrickCountId, activeBrickCount);
            m_computeShader.SetInts(s_brickGridSizeId, gridW, gridH, gridD);
            m_computeShader.SetInt(s_atlasBricksPerRowId, bricksPerRow);
            m_computeShader.SetInt(s_atlasBricksPerSliceId, bricksPerSlice);

            m_computeShader.Dispatch(m_buildIndirectionKernel, groupsX, groupsY, groupsZ);

            // Cleanup
            activeBrickList.Release();
            activeBrickCount.Release();

            return new BrickMapResult
            {
                brickAtlas = brickAtlas,
                indirectionTexture = indirectionTexture,
                activeBrickCount = numActive,
                brickGridSize = new Vector3Int(gridW, gridH, gridD),
                brickSize = brickSize
            };
        }

        public void Dispose() { }

        static RenderTexture CreateVolumeRT(int width, int height, int depth, RenderTextureFormat format)
        {
            var rt = new RenderTexture(width, height, 0, format);
            rt.dimension = TextureDimension.Tex3D;
            rt.volumeDepth = depth;
            rt.enableRandomWrite = true;
            rt.filterMode = FilterMode.Bilinear;
            rt.wrapMode = TextureWrapMode.Clamp;
            rt.Create();
            return rt;
        }
    }
}

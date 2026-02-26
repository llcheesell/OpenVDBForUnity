using UnityEngine;

namespace OpenVDB.Realtime
{
    /// <summary>
    /// Manages temporal reprojection buffers for amortized volume rendering.
    /// Keeps history of previous frames and blends with current frame to
    /// improve quality without increasing per-frame cost.
    /// </summary>
    public class TemporalVolumeRenderer : System.IDisposable
    {
        ComputeShader m_computeShader;
        int m_reprojectKernel;

        RenderTexture m_historyColor;
        RenderTexture m_historyDepth;
        RenderTexture m_outputColor;
        RenderTexture m_outputDepth;

        int m_width;
        int m_height;
        bool m_initialized;

        static readonly int s_currentColorId = Shader.PropertyToID("_CurrentColor");
        static readonly int s_currentDepthId = Shader.PropertyToID("_CurrentDepth");
        static readonly int s_historyColorId = Shader.PropertyToID("_HistoryColor");
        static readonly int s_historyDepthId = Shader.PropertyToID("_HistoryDepth");
        static readonly int s_motionVectorsId = Shader.PropertyToID("_MotionVectors");
        static readonly int s_outputColorId = Shader.PropertyToID("_OutputColor");
        static readonly int s_outputDepthId = Shader.PropertyToID("_OutputDepth");
        static readonly int s_resolutionId = Shader.PropertyToID("_Resolution");
        static readonly int s_blendFactorId = Shader.PropertyToID("_BlendFactor");
        static readonly int s_depthRejectId = Shader.PropertyToID("_DepthRejectThreshold");
        static readonly int s_colorBoxScaleId = Shader.PropertyToID("_ColorBoxScale");

        public RenderTexture outputColor => m_outputColor;
        public RenderTexture outputDepth => m_outputDepth;

        public TemporalVolumeRenderer(ComputeShader computeShader)
        {
            m_computeShader = computeShader;
            m_reprojectKernel = m_computeShader.FindKernel("TemporalReproject");
        }

        public void EnsureBuffers(int width, int height)
        {
            if (m_initialized && m_width == width && m_height == height)
                return;

            Release();

            m_width = width;
            m_height = height;

            m_historyColor = CreateRT(width, height, RenderTextureFormat.ARGBHalf);
            m_historyDepth = CreateRT(width, height, RenderTextureFormat.RFloat);
            m_outputColor = CreateRT(width, height, RenderTextureFormat.ARGBHalf);
            m_outputDepth = CreateRT(width, height, RenderTextureFormat.RFloat);

            // Clear history
            var prev = RenderTexture.active;
            RenderTexture.active = m_historyColor;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = m_historyDepth;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = prev;

            m_initialized = true;
        }

        /// <summary>
        /// Perform temporal reprojection, blending current frame with history.
        /// </summary>
        public void Reproject(
            RenderTexture currentColor,
            RenderTexture currentDepth,
            RenderTexture motionVectors,
            float blendFactor = 0.05f,
            float depthRejectThreshold = 0.1f,
            float colorBoxScale = 1.25f)
        {
            if (!m_initialized)
                return;

            m_computeShader.SetTexture(m_reprojectKernel, s_currentColorId, currentColor);
            m_computeShader.SetTexture(m_reprojectKernel, s_currentDepthId, currentDepth);
            m_computeShader.SetTexture(m_reprojectKernel, s_historyColorId, m_historyColor);
            m_computeShader.SetTexture(m_reprojectKernel, s_historyDepthId, m_historyDepth);
            m_computeShader.SetTexture(m_reprojectKernel, s_motionVectorsId, motionVectors);
            m_computeShader.SetTexture(m_reprojectKernel, s_outputColorId, m_outputColor);
            m_computeShader.SetTexture(m_reprojectKernel, s_outputDepthId, m_outputDepth);
            m_computeShader.SetVector(s_resolutionId, new Vector4(m_width, m_height, 0, 0));
            m_computeShader.SetFloat(s_blendFactorId, blendFactor);
            m_computeShader.SetFloat(s_depthRejectId, depthRejectThreshold);
            m_computeShader.SetFloat(s_colorBoxScaleId, colorBoxScale);

            int groupsX = Mathf.CeilToInt(m_width / 8f);
            int groupsY = Mathf.CeilToInt(m_height / 8f);
            m_computeShader.Dispatch(m_reprojectKernel, groupsX, groupsY, 1);

            // Swap: output becomes history for next frame
            SwapBuffers();
        }

        void SwapBuffers()
        {
            (m_historyColor, m_outputColor) = (m_outputColor, m_historyColor);
            (m_historyDepth, m_outputDepth) = (m_outputDepth, m_historyDepth);
        }

        public void InvalidateHistory()
        {
            if (!m_initialized)
                return;

            var prev = RenderTexture.active;
            RenderTexture.active = m_historyColor;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = m_historyDepth;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = prev;
        }

        void Release()
        {
            if (m_historyColor != null) { m_historyColor.Release(); Object.Destroy(m_historyColor); }
            if (m_historyDepth != null) { m_historyDepth.Release(); Object.Destroy(m_historyDepth); }
            if (m_outputColor != null) { m_outputColor.Release(); Object.Destroy(m_outputColor); }
            if (m_outputDepth != null) { m_outputDepth.Release(); Object.Destroy(m_outputDepth); }
            m_initialized = false;
        }

        public void Dispose()
        {
            Release();
        }

        static RenderTexture CreateRT(int width, int height, RenderTextureFormat format)
        {
            var rt = new RenderTexture(width, height, 0, format);
            rt.enableRandomWrite = true;
            rt.filterMode = FilterMode.Bilinear;
            rt.wrapMode = TextureWrapMode.Clamp;
            rt.Create();
            return rt;
        }
    }
}

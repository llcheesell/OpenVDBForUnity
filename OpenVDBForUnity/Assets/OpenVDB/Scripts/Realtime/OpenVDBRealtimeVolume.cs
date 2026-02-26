using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenVDB.Realtime
{
    /// <summary>
    /// Main component for real-time OpenVDB volume rendering.
    /// Manages the GPU-accelerated rendering pipeline including:
    /// - Sparse occupancy grid for empty space skipping
    /// - Enhanced ray marching with adaptive stepping
    /// - Temporal reprojection for amortized quality
    /// - Henyey-Greenstein phase function for realistic scattering
    /// </summary>
    [Obsolete("Use OpenVDB.OpenVDBVolume instead. This component will be removed in a future version.")]
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class OpenVDBRealtimeVolume : MonoBehaviour
    {
        [Header("Volume Data")]
        [SerializeField]
        Texture3D m_volumeTexture;

        [Header("Quality")]
        [SerializeField]
        QualityLevel m_qualityLevel = QualityLevel.Medium;

        [SerializeField]
        VolumeQualityPreset m_customPreset;

        [Header("Rendering")]
        [SerializeField, Range(0.1f, 5.0f)]
        float m_intensity = 0.5f;

        [SerializeField, Range(0.002f, 0.05f)]
        float m_stepDistance = 0.008f;

        [SerializeField, Range(32, 512)]
        int m_maxSteps = 200;

        [Header("Lighting")]
        [SerializeField]
        bool m_enableDirectionalLight = true;

        [SerializeField, Range(1, 16)]
        int m_shadowSteps = 6;

        [SerializeField]
        Color m_shadowDensity = new Color(0.4f, 0.4f, 0.4f, 1f);

        [SerializeField, Range(0.001f, 0.1f)]
        float m_shadowThreshold = 0.01f;

        [SerializeField, Range(-0.9f, 0.9f)]
        float m_phaseAnisotropy = 0.3f;

        [Header("Ambient")]
        [SerializeField]
        bool m_enableAmbientLight = true;

        [SerializeField]
        Color m_ambientColor = new Color(0.4f, 0.4f, 0.5f, 1f);

        [SerializeField, Range(0f, 1f)]
        float m_ambientDensity = 0.2f;

        [Header("Empty Space Skipping")]
        [SerializeField]
        bool m_enableEmptySpaceSkipping = true;

        [SerializeField, Range(4, 32)]
        int m_occupancyDivisor = 8;

        [SerializeField, Range(0.0001f, 0.01f)]
        float m_occupancyThreshold = 0.001f;

        [Header("Temporal")]
        [SerializeField]
        bool m_enableTemporalJitter = true;

        [Header("Adaptive Stepping")]
        [SerializeField]
        bool m_enableAdaptiveStepping = true;

        [SerializeField, Range(0f, 2f)]
        float m_adaptiveDistanceScale = 0.5f;

        [SerializeField, Range(0.001f, 0.01f)]
        float m_minStepDistance = 0.003f;

        [SerializeField, Range(0.01f, 0.1f)]
        float m_maxStepDistance = 0.05f;

        [Header("Advanced")]
        [SerializeField]
        bool m_enableHGPhase = true;

        [SerializeField]
        bool m_enableMultiScatter = false;

        [Header("Compute Shaders")]
        [SerializeField]
        ComputeShader m_occupancyComputeShader;

        // Runtime state
        MeshRenderer m_renderer;
        MaterialPropertyBlock m_propertyBlock;
        OccupancyGridGenerator m_occupancyGenerator;
        RenderTexture m_occupancyGrid;
        Texture3D m_lastVolumeTexture;
        int m_frameIndex;

        // Shader property IDs
        static readonly int s_volumeId = Shader.PropertyToID("_Volume");
        static readonly int s_occupancyGridId = Shader.PropertyToID("_OccupancyGrid");
        static readonly int s_occupancyGridSizeId = Shader.PropertyToID("_OccupancyGridSize");
        static readonly int s_intensityId = Shader.PropertyToID("_Intensity");
        static readonly int s_stepDistanceId = Shader.PropertyToID("_StepDistance");
        static readonly int s_maxStepsId = Shader.PropertyToID("_MaxSteps");
        static readonly int s_shadowStepsId = Shader.PropertyToID("_ShadowSteps");
        static readonly int s_shadowDensityId = Shader.PropertyToID("_ShadowDensity");
        static readonly int s_shadowThresholdId = Shader.PropertyToID("_ShadowThreshold");
        static readonly int s_ambientColorId = Shader.PropertyToID("_AmbientColor");
        static readonly int s_ambientDensityId = Shader.PropertyToID("_AmbientDensity");
        static readonly int s_phaseGId = Shader.PropertyToID("_PhaseG");
        static readonly int s_adaptiveDistScaleId = Shader.PropertyToID("_AdaptiveDistScale");
        static readonly int s_minStepDistId = Shader.PropertyToID("_MinStepDistance");
        static readonly int s_maxStepDistId = Shader.PropertyToID("_MaxStepDistance");
        static readonly int s_mainLightDirId = Shader.PropertyToID("_MainLightDir");
        static readonly int s_mainLightColorId = Shader.PropertyToID("_MainLightColor");
        static readonly int s_frameIndexId = Shader.PropertyToID("_FrameIndex");

        void OnEnable()
        {
            m_renderer = GetComponent<MeshRenderer>();
            m_propertyBlock = new MaterialPropertyBlock();

            if (m_occupancyComputeShader != null)
            {
                m_occupancyGenerator = new OccupancyGridGenerator(m_occupancyComputeShader);
            }

            ApplyQualityPreset();
            RebuildOccupancyGrid();
        }

        void OnDisable()
        {
            ReleaseOccupancyGrid();
        }

        void OnDestroy()
        {
            ReleaseOccupancyGrid();
        }

        void Update()
        {
            if (m_renderer == null) return;

            // Rebuild occupancy grid if volume texture changed
            if (m_volumeTexture != m_lastVolumeTexture)
            {
                RebuildOccupancyGrid();
                m_lastVolumeTexture = m_volumeTexture;
            }

            UpdateMaterialProperties();
            m_frameIndex++;
        }

        /// <summary>
        /// Apply quality preset to material properties.
        /// </summary>
        public void ApplyQualityPreset()
        {
            if (m_qualityLevel == QualityLevel.Custom && m_customPreset != null)
            {
                ApplyPreset(m_customPreset);
                return;
            }

            if (m_qualityLevel == QualityLevel.Custom)
                return;

            var preset = VolumeQualityPreset.CreateDefault(m_qualityLevel);
            ApplyPreset(preset);

            if (Application.isPlaying)
                Destroy(preset);
            else
                DestroyImmediate(preset);
        }

        void ApplyPreset(VolumeQualityPreset preset)
        {
            m_stepDistance = preset.stepDistance;
            m_maxSteps = preset.maxSteps;
            m_shadowSteps = preset.shadowSteps;
            m_enableEmptySpaceSkipping = preset.emptySpaceSkipping;
            m_enableTemporalJitter = preset.temporalJitter;
            m_enableAdaptiveStepping = preset.adaptiveStepping;
            m_enableHGPhase = preset.henyeyGreensteinPhase;
            m_enableMultiScatter = preset.multiScatterApprox;
            m_adaptiveDistanceScale = preset.adaptiveDistanceScale;
            m_minStepDistance = preset.minStepDistance;
            m_maxStepDistance = preset.maxStepDistance;
            m_occupancyDivisor = preset.occupancyGridDivisor;
        }

        void UpdateMaterialProperties()
        {
            m_renderer.GetPropertyBlock(m_propertyBlock);

            // Volume texture
            if (m_volumeTexture != null)
                m_propertyBlock.SetTexture(s_volumeId, m_volumeTexture);

            // Occupancy grid
            if (m_occupancyGrid != null && m_enableEmptySpaceSkipping)
            {
                m_propertyBlock.SetTexture(s_occupancyGridId, m_occupancyGrid);
                m_propertyBlock.SetVector(s_occupancyGridSizeId, new Vector4(
                    m_occupancyGrid.width, m_occupancyGrid.height, m_occupancyGrid.volumeDepth, 0));
            }

            // Rendering params
            m_propertyBlock.SetFloat(s_intensityId, m_intensity);
            m_propertyBlock.SetFloat(s_stepDistanceId, m_stepDistance);
            m_propertyBlock.SetInt(s_maxStepsId, m_maxSteps);
            m_propertyBlock.SetFloat(s_shadowStepsId, m_shadowSteps);
            m_propertyBlock.SetColor(s_shadowDensityId, m_shadowDensity);
            m_propertyBlock.SetFloat(s_shadowThresholdId, m_shadowThreshold);
            m_propertyBlock.SetColor(s_ambientColorId, m_ambientColor);
            m_propertyBlock.SetFloat(s_ambientDensityId, m_ambientDensity);
            m_propertyBlock.SetFloat(s_phaseGId, m_phaseAnisotropy);
            m_propertyBlock.SetFloat(s_adaptiveDistScaleId, m_adaptiveDistanceScale);
            m_propertyBlock.SetFloat(s_minStepDistId, m_minStepDistance);
            m_propertyBlock.SetFloat(s_maxStepDistId, m_maxStepDistance);
            m_propertyBlock.SetFloat(s_frameIndexId, m_frameIndex);

            // Sync main directional light
            SyncMainLight();

            // Shader keywords
            var mat = m_renderer.sharedMaterial;
            if (mat != null)
            {
                SetKeyword(mat, "ENABLE_OCCUPANCY_SKIP", m_enableEmptySpaceSkipping && m_occupancyGrid != null);
                SetKeyword(mat, "ENABLE_TEMPORAL_JITTER", m_enableTemporalJitter);
                SetKeyword(mat, "ENABLE_ADAPTIVE_STEPPING", m_enableAdaptiveStepping);
                SetKeyword(mat, "ENABLE_HG_PHASE", m_enableHGPhase);
                SetKeyword(mat, "ENABLE_MULTI_SCATTER", m_enableMultiScatter);
                SetKeyword(mat, "ENABLE_DIRECTIONAL_LIGHT", m_enableDirectionalLight);
                SetKeyword(mat, "ENABLE_AMBIENT_LIGHT", m_enableAmbientLight);
            }

            m_renderer.SetPropertyBlock(m_propertyBlock);
        }

        void SyncMainLight()
        {
            var sun = RenderSettings.sun;
            if (sun != null)
            {
                m_propertyBlock.SetVector(s_mainLightDirId, -sun.transform.forward);
                m_propertyBlock.SetColor(s_mainLightColorId, sun.color * sun.intensity);
                return;
            }

            Light brightest = null;
            float maxIntensity = 0f;
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional && light.intensity > maxIntensity)
                {
                    brightest = light;
                    maxIntensity = light.intensity;
                }
            }
            if (brightest != null)
            {
                m_propertyBlock.SetVector(s_mainLightDirId, -brightest.transform.forward);
                m_propertyBlock.SetColor(s_mainLightColorId, brightest.color * brightest.intensity);
            }
        }

        /// <summary>
        /// Rebuild the occupancy grid from the current volume texture.
        /// Call this when the volume data changes.
        /// </summary>
        public void RebuildOccupancyGrid()
        {
            ReleaseOccupancyGrid();

            if (m_volumeTexture == null || m_occupancyGenerator == null)
                return;

            if (!m_enableEmptySpaceSkipping)
                return;

            m_occupancyGrid = m_occupancyGenerator.Generate(
                m_volumeTexture, m_occupancyDivisor, m_occupancyThreshold);
        }

        void ReleaseOccupancyGrid()
        {
            if (m_occupancyGrid != null)
            {
                m_occupancyGrid.Release();
                if (Application.isPlaying)
                    Destroy(m_occupancyGrid);
                else
                    DestroyImmediate(m_occupancyGrid);
                m_occupancyGrid = null;
            }
        }

        static void SetKeyword(Material mat, string keyword, bool enabled)
        {
            if (enabled)
                mat.EnableKeyword(keyword);
            else
                mat.DisableKeyword(keyword);
        }

        // Public API

        public Texture3D volumeTexture
        {
            get => m_volumeTexture;
            set
            {
                m_volumeTexture = value;
                RebuildOccupancyGrid();
            }
        }

        public QualityLevel qualityLevel
        {
            get => m_qualityLevel;
            set
            {
                m_qualityLevel = value;
                ApplyQualityPreset();
            }
        }

        public float intensity
        {
            get => m_intensity;
            set => m_intensity = Mathf.Clamp(value, 0.1f, 5.0f);
        }

        public float stepDistance
        {
            get => m_stepDistance;
            set => m_stepDistance = Mathf.Clamp(value, 0.002f, 0.05f);
        }

        public int maxSteps
        {
            get => m_maxSteps;
            set => m_maxSteps = Mathf.Clamp(value, 32, 512);
        }

        public int shadowSteps
        {
            get => m_shadowSteps;
            set => m_shadowSteps = Mathf.Clamp(value, 1, 16);
        }

        public float phaseAnisotropy
        {
            get => m_phaseAnisotropy;
            set => m_phaseAnisotropy = Mathf.Clamp(value, -0.9f, 0.9f);
        }

        public bool enableEmptySpaceSkipping
        {
            get => m_enableEmptySpaceSkipping;
            set
            {
                m_enableEmptySpaceSkipping = value;
                if (value && m_occupancyGrid == null)
                    RebuildOccupancyGrid();
            }
        }

        public bool enableTemporalJitter
        {
            get => m_enableTemporalJitter;
            set => m_enableTemporalJitter = value;
        }

        public bool enableAdaptiveStepping
        {
            get => m_enableAdaptiveStepping;
            set => m_enableAdaptiveStepping = value;
        }
    }
}

using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenVDB
{
    /// <summary>
    /// Unified OpenVDB volume component supporting both Classic and Realtime rendering modes.
    /// Switch between modes via the RenderMode property. Each mode uses a different shader
    /// with appropriate features. All features are keyword-gated for zero GPU cost when disabled.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class OpenVDBVolume : MonoBehaviour
    {
        // ====================================================================
        // Mode Selection
        // ====================================================================

        [Header("Render Mode")]
        [SerializeField]
        VolumeRenderMode m_renderMode = VolumeRenderMode.Realtime;

        // ====================================================================
        // Shared Fields (both modes)
        // ====================================================================

        [Header("Volume Data")]
        [SerializeField]
        Texture3D m_volumeTexture;

        [Header("Rendering")]
        [SerializeField, Range(0.1f, 5.0f)]
        float m_intensity = 0.5f;

        [SerializeField, Range(0.002f, 0.1f)]
        float m_stepDistance = 0.008f;

        [Header("Lighting")]
        [SerializeField]
        bool m_enableDirectionalLight = true;

        [SerializeField, Range(1, 64)]
        int m_shadowSteps = 6;

        [SerializeField]
        Color m_shadowDensity = new Color(0.4f, 0.4f, 0.4f, 1f);

        [SerializeField, Range(0.001f, 0.1f)]
        float m_shadowThreshold = 0.01f;

        [Header("Ambient")]
        [SerializeField]
        bool m_enableAmbientLight = true;

        [SerializeField]
        Color m_ambientColor = new Color(0.4f, 0.4f, 0.5f, 1f);

        [SerializeField, Range(0f, 1f)]
        float m_ambientDensity = 0.2f;

        [Header("Light Influence")]
        [SerializeField, Range(0f, 5f)]
        float m_lightInfluence = 1.0f;

        [SerializeField, Range(0f, 5f)]
        float m_ambientInfluence = 1.0f;

        [Header("Color Ramp")]
        [SerializeField]
        bool m_enableColorRamp = false;

        [SerializeField]
        Gradient m_colorRampGradient = new Gradient();

        [SerializeField, Range(0f, 2f)]
        float m_colorRampIntensity = 1.0f;

        [Header("Spot Lights")]
        [SerializeField]
        bool m_enableSpotLights = false;

        [SerializeField]
        Light[] m_spotLights = new Light[0];

        [SerializeField, Range(0f, 5f)]
        float m_spotLightInfluence = 1.0f;

        [Header("Shadow Casting")]
        [SerializeField]
        bool m_enableShadowCasting = false;

        [SerializeField, Range(-0.1f, 0.1f)]
        float m_shadowExtraBias = 0.0f;

        [SerializeField, Range(0.001f, 0.1f)]
        float m_shadowDensityThreshold = 0.01f;

        [Header("Debug")]
        [SerializeField, Range(0, 5)]
        int m_debugMode = 0;

        [Header("Performance")]
        [SerializeField]
        bool m_autoSyncLight = true;

        // ====================================================================
        // Realtime-Only Fields
        // ====================================================================

        [Header("Quality Preset (Realtime)")]
        [SerializeField]
        Realtime.QualityLevel m_qualityLevel = Realtime.QualityLevel.Medium;

        [SerializeField]
        Realtime.VolumeQualityPreset m_customPreset;

        [SerializeField, Range(32, 512)]
        int m_maxSteps = 200;

        [Header("Empty Space Skipping (Realtime)")]
        [SerializeField]
        bool m_enableEmptySpaceSkipping = true;

        [SerializeField, Range(4, 32)]
        int m_occupancyDivisor = 8;

        [SerializeField, Range(0.0001f, 0.01f)]
        float m_occupancyThreshold = 0.001f;

        [Header("Temporal (Realtime)")]
        [SerializeField]
        bool m_enableTemporalJitter = true;

        [Header("Adaptive Stepping (Realtime)")]
        [SerializeField]
        bool m_enableAdaptiveStepping = true;

        [SerializeField, Range(0f, 2f)]
        float m_adaptiveDistanceScale = 0.5f;

        [SerializeField, Range(0.001f, 0.01f)]
        float m_minStepDistance = 0.003f;

        [SerializeField, Range(0.01f, 0.1f)]
        float m_maxStepDistance = 0.05f;

        [Header("Advanced (Realtime)")]
        [SerializeField]
        bool m_enableHGPhase = true;

        [SerializeField]
        bool m_enableMultiScatter = false;

        [SerializeField, Range(-0.9f, 0.9f)]
        float m_phaseAnisotropy = 0.3f;

        [Header("Compute Shaders (Realtime)")]
        [SerializeField]
        ComputeShader m_occupancyComputeShader;

        // ====================================================================
        // Runtime State
        // ====================================================================

        MeshRenderer m_renderer;
        MaterialPropertyBlock m_propertyBlock;
        Texture2D m_bakedColorRamp;
        bool m_gradientDirty = true;

        // Cached light lookup
        Light m_cachedMainLight;
        float m_lightCacheTime;
        const float LightCacheInterval = 0.5f;

        // Realtime-specific runtime state
        Realtime.OccupancyGridGenerator m_occupancyGenerator;
        RenderTexture m_occupancyGrid;
        Texture3D m_lastVolumeTexture;
        int m_frameIndex;

        // Shader property IDs
        static readonly int s_volumeId = Shader.PropertyToID("_Volume");
        static readonly int s_intensityId = Shader.PropertyToID("_Intensity");
        static readonly int s_stepDistanceId = Shader.PropertyToID("_StepDistance");
        static readonly int s_shadowStepsId = Shader.PropertyToID("_ShadowSteps");
        static readonly int s_shadowDensityId = Shader.PropertyToID("_ShadowDensity");
        static readonly int s_shadowThresholdId = Shader.PropertyToID("_ShadowThreshold");
        static readonly int s_ambientColorId = Shader.PropertyToID("_AmbientColor");
        static readonly int s_ambientDensityId = Shader.PropertyToID("_AmbientDensity");
        static readonly int s_mainLightDirId = Shader.PropertyToID("_MainLightDir");
        static readonly int s_mainLightColorId = Shader.PropertyToID("_MainLightColor");
        static readonly int s_lightInfluenceId = Shader.PropertyToID("_LightInfluence");
        static readonly int s_ambientInfluenceId = Shader.PropertyToID("_AmbientInfluence");
        static readonly int s_colorRampId = Shader.PropertyToID("_ColorRamp");
        static readonly int s_colorRampIntensityId = Shader.PropertyToID("_ColorRampIntensity");
        static readonly int s_shadowExtraBiasId = Shader.PropertyToID("_ShadowExtraBias");
        static readonly int s_shadowDensityThresholdId = Shader.PropertyToID("_ShadowDensityThreshold");

        // Spot light IDs
        static readonly int s_spotLight0PosId = Shader.PropertyToID("_SpotLight0_Position");
        static readonly int s_spotLight0DirId = Shader.PropertyToID("_SpotLight0_Direction");
        static readonly int s_spotLight0ColorId = Shader.PropertyToID("_SpotLight0_Color");
        static readonly int s_spotLight0ParamsId = Shader.PropertyToID("_SpotLight0_Params");
        static readonly int s_spotLight1PosId = Shader.PropertyToID("_SpotLight1_Position");
        static readonly int s_spotLight1DirId = Shader.PropertyToID("_SpotLight1_Direction");
        static readonly int s_spotLight1ColorId = Shader.PropertyToID("_SpotLight1_Color");
        static readonly int s_spotLight1ParamsId = Shader.PropertyToID("_SpotLight1_Params");
        static readonly int s_spotLightCountId = Shader.PropertyToID("_SpotLightCount");
        static readonly int s_spotLightInfluenceId = Shader.PropertyToID("_SpotLightInfluence");

        // Debug
        static readonly int s_debugModeId = Shader.PropertyToID("_DebugMode");

        // Realtime-specific IDs
        static readonly int s_occupancyGridId = Shader.PropertyToID("_OccupancyGrid");
        static readonly int s_occupancyGridSizeId = Shader.PropertyToID("_OccupancyGridSize");
        static readonly int s_maxStepsId = Shader.PropertyToID("_MaxSteps");
        static readonly int s_phaseGId = Shader.PropertyToID("_PhaseG");
        static readonly int s_adaptiveDistScaleId = Shader.PropertyToID("_AdaptiveDistScale");
        static readonly int s_minStepDistId = Shader.PropertyToID("_MinStepDistance");
        static readonly int s_maxStepDistId = Shader.PropertyToID("_MaxStepDistance");
        static readonly int s_frameIndexId = Shader.PropertyToID("_FrameIndex");

        // ====================================================================
        // Lifecycle
        // ====================================================================

        void OnEnable()
        {
            m_renderer = GetComponent<MeshRenderer>();
            m_propertyBlock = new MaterialPropertyBlock();
            m_gradientDirty = true;

            if (m_occupancyComputeShader != null)
            {
                m_occupancyGenerator = new Realtime.OccupancyGridGenerator(m_occupancyComputeShader);
            }

            if (m_renderMode == VolumeRenderMode.Realtime)
            {
                ApplyQualityPreset();
                RebuildOccupancyGrid();
            }
        }

        void OnDisable()
        {
            ReleaseColorRamp();
            ReleaseOccupancyGrid();
        }

        void OnDestroy()
        {
            ReleaseColorRamp();
            ReleaseOccupancyGrid();
        }

        void OnValidate()
        {
            m_gradientDirty = true;
        }

        void Update()
        {
            if (m_renderer == null) return;

            // Rebuild occupancy grid if volume texture changed (Realtime mode)
            if (m_renderMode == VolumeRenderMode.Realtime && m_volumeTexture != m_lastVolumeTexture)
            {
                RebuildOccupancyGrid();
                m_lastVolumeTexture = m_volumeTexture;
            }

            UpdateMaterialProperties();

            if (m_renderMode == VolumeRenderMode.Realtime)
                m_frameIndex++;
        }

        // ====================================================================
        // Mode Switching
        // ====================================================================

        public static bool IsHDRP()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline == null) return false;
            return pipeline.GetType().Name.Contains("HDRenderPipelineAsset");
        }

        string GetShaderName()
        {
            if (m_renderMode == VolumeRenderMode.Realtime)
                return IsHDRP() ? "OpenVDB/Realtime/HDRP" : "OpenVDB/Realtime/Standard";
            else
                return IsHDRP() ? "OpenVDB/HDRP/Standard" : "OpenVDB/Standard";
        }

        /// <summary>
        /// Applies the appropriate shader to the material based on the current render mode.
        /// </summary>
        public void ApplyShader()
        {
            if (m_renderer == null)
                m_renderer = GetComponent<MeshRenderer>();
            if (m_renderer == null) return;

            var mat = m_renderer.sharedMaterial;
            if (mat == null) return;

            string shaderName = GetShaderName();
            var shader = Shader.Find(shaderName);
            if (shader != null)
            {
                mat.shader = shader;
            }
            else
            {
                Debug.LogWarning($"[OpenVDB] Shader '{shaderName}' not found.");
            }
        }

        // ====================================================================
        // Material Property Update
        // ====================================================================

        void UpdateMaterialProperties()
        {
            m_renderer.GetPropertyBlock(m_propertyBlock);

            // Volume texture
            if (m_volumeTexture != null)
                m_propertyBlock.SetTexture(s_volumeId, m_volumeTexture);

            // Shared params
            m_propertyBlock.SetFloat(s_intensityId, m_intensity);
            m_propertyBlock.SetFloat(s_stepDistanceId, m_stepDistance);
            m_propertyBlock.SetFloat(s_shadowStepsId, m_shadowSteps);
            m_propertyBlock.SetColor(s_shadowDensityId, m_shadowDensity);
            m_propertyBlock.SetFloat(s_shadowThresholdId, m_shadowThreshold);
            m_propertyBlock.SetColor(s_ambientColorId, m_ambientColor);
            m_propertyBlock.SetFloat(s_ambientDensityId, m_ambientDensity);

            // Light influence
            m_propertyBlock.SetFloat(s_lightInfluenceId, m_lightInfluence);
            m_propertyBlock.SetFloat(s_ambientInfluenceId, m_ambientInfluence);

            // Shadow casting
            m_propertyBlock.SetFloat(s_shadowExtraBiasId, m_shadowExtraBias);
            m_propertyBlock.SetFloat(s_shadowDensityThresholdId, m_shadowDensityThreshold);
            m_renderer.shadowCastingMode = m_enableShadowCasting
                ? ShadowCastingMode.On
                : ShadowCastingMode.Off;

            // Auto sync light
            if (m_autoSyncLight)
            {
                SyncMainLight();
            }

            // Color ramp
            if (m_enableColorRamp)
            {
                BakeGradientTexture();
                if (m_bakedColorRamp != null)
                {
                    m_propertyBlock.SetTexture(s_colorRampId, m_bakedColorRamp);
                }
                m_propertyBlock.SetFloat(s_colorRampIntensityId, m_colorRampIntensity);
            }

            // Spot lights
            if (m_enableSpotLights)
            {
                SyncSpotLights();
                m_propertyBlock.SetFloat(s_spotLightInfluenceId, m_spotLightInfluence);
            }

            // Debug mode
            m_propertyBlock.SetInt(s_debugModeId, m_debugMode);

            // Mode-specific params
            if (m_renderMode == VolumeRenderMode.Realtime)
            {
                UpdateRealtimeProperties();
            }

            // Shader keywords
            SyncKeywords();

            m_renderer.SetPropertyBlock(m_propertyBlock);
        }

        void UpdateRealtimeProperties()
        {
            // Occupancy grid
            if (m_occupancyGrid != null && m_enableEmptySpaceSkipping)
            {
                m_propertyBlock.SetTexture(s_occupancyGridId, m_occupancyGrid);
                m_propertyBlock.SetVector(s_occupancyGridSizeId, new Vector4(
                    m_occupancyGrid.width, m_occupancyGrid.height, m_occupancyGrid.volumeDepth, 0));
            }

            m_propertyBlock.SetInt(s_maxStepsId, m_maxSteps);
            m_propertyBlock.SetFloat(s_phaseGId, m_phaseAnisotropy);
            m_propertyBlock.SetFloat(s_adaptiveDistScaleId, m_adaptiveDistanceScale);
            m_propertyBlock.SetFloat(s_minStepDistId, m_minStepDistance);
            m_propertyBlock.SetFloat(s_maxStepDistId, m_maxStepDistance);
            m_propertyBlock.SetFloat(s_frameIndexId, m_frameIndex);
        }

        void SyncKeywords()
        {
            var mat = m_renderer.sharedMaterial;
            if (mat == null) return;

            // Shared keywords
            SetKeyword(mat, "ENABLE_DIRECTIONAL_LIGHT", m_enableDirectionalLight);
            SetKeyword(mat, "ENABLE_AMBIENT_LIGHT", m_enableAmbientLight);
            SetKeyword(mat, "ENABLE_COLOR_RAMP", m_enableColorRamp);
            SetKeyword(mat, "ENABLE_SPOT_LIGHTS", m_enableSpotLights);

            // Realtime-only keywords
            if (m_renderMode == VolumeRenderMode.Realtime)
            {
                SetKeyword(mat, "ENABLE_OCCUPANCY_SKIP", m_enableEmptySpaceSkipping && m_occupancyGrid != null);
                SetKeyword(mat, "ENABLE_TEMPORAL_JITTER", m_enableTemporalJitter);
                SetKeyword(mat, "ENABLE_ADAPTIVE_STEPPING", m_enableAdaptiveStepping);
                SetKeyword(mat, "ENABLE_HG_PHASE", m_enableHGPhase);
                SetKeyword(mat, "ENABLE_MULTI_SCATTER", m_enableMultiScatter);
            }
        }

        // ====================================================================
        // Color Ramp
        // ====================================================================

        void BakeGradientTexture()
        {
            if (!m_gradientDirty && m_bakedColorRamp != null) return;

            if (m_bakedColorRamp == null)
            {
                m_bakedColorRamp = new Texture2D(256, 1, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
            }

            var pixels = new Color[256];
            for (int i = 0; i < 256; i++)
            {
                pixels[i] = m_colorRampGradient.Evaluate(i / 255f);
            }
            m_bakedColorRamp.SetPixels(pixels);
            m_bakedColorRamp.Apply();
            m_gradientDirty = false;
        }

        void ReleaseColorRamp()
        {
            if (m_bakedColorRamp != null)
            {
                if (Application.isPlaying)
                    Destroy(m_bakedColorRamp);
                else
                    DestroyImmediate(m_bakedColorRamp);
                m_bakedColorRamp = null;
            }
        }

        // ====================================================================
        // Spot Lights
        // ====================================================================

        const int MaxSpotLights = 2;

        static readonly int[][] s_spotLightPropertyIds = {
            new[] { Shader.PropertyToID("_SpotLight0_Position"), Shader.PropertyToID("_SpotLight0_Direction"),
                    Shader.PropertyToID("_SpotLight0_Color"), Shader.PropertyToID("_SpotLight0_Params") },
            new[] { Shader.PropertyToID("_SpotLight1_Position"), Shader.PropertyToID("_SpotLight1_Direction"),
                    Shader.PropertyToID("_SpotLight1_Color"), Shader.PropertyToID("_SpotLight1_Params") },
        };

        void SyncSpotLights()
        {
            int count = 0;
            if (m_spotLights != null)
            {
                for (int i = 0; i < Mathf.Min(m_spotLights.Length, MaxSpotLights); i++)
                {
                    var light = m_spotLights[i];
                    if (light == null || light.type != LightType.Spot || !light.enabled) continue;
                    SetSpotLightProperties(count, light);
                    count++;
                }
            }
            m_propertyBlock.SetFloat(s_spotLightCountId, count);
        }

        void SetSpotLightProperties(int index, Light light)
        {
            var ids = s_spotLightPropertyIds[index];
            var color = light.color;

            float outerRad = light.spotAngle * 0.5f * Mathf.Deg2Rad;
            float innerRad = light.innerSpotAngle * 0.5f * Mathf.Deg2Rad;
            float cosOuter = Mathf.Cos(outerRad);
            float cosInner = Mathf.Cos(innerRad);
            float angleScale = 1.0f / Mathf.Max(cosInner - cosOuter, 0.001f);
            float angleOffset = -cosOuter * angleScale;

            m_propertyBlock.SetVector(ids[0], light.transform.position);
            m_propertyBlock.SetVector(ids[1], light.transform.forward);
            m_propertyBlock.SetVector(ids[2], new Vector4(color.r, color.g, color.b, 1));
            m_propertyBlock.SetVector(ids[3], new Vector4(light.range, angleScale, angleOffset, light.intensity));
        }

        // ====================================================================
        // Main Light Sync
        // ====================================================================

        void SyncMainLight()
        {
            var sun = RenderSettings.sun;
            if (sun != null)
            {
                m_propertyBlock.SetVector(s_mainLightDirId, -sun.transform.forward);
                m_propertyBlock.SetColor(s_mainLightColorId, sun.color * sun.intensity);
                return;
            }

            // Refresh cached light periodically instead of every frame
            if (m_cachedMainLight == null || Time.unscaledTime - m_lightCacheTime > LightCacheInterval)
            {
                m_cachedMainLight = FindBrightestDirectionalLight();
                m_lightCacheTime = Time.unscaledTime;
            }

            if (m_cachedMainLight != null)
            {
                m_propertyBlock.SetVector(s_mainLightDirId, -m_cachedMainLight.transform.forward);
                m_propertyBlock.SetColor(s_mainLightColorId, m_cachedMainLight.color * m_cachedMainLight.intensity);
            }
        }

        static Light FindBrightestDirectionalLight()
        {
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
            return brightest;
        }

        // ====================================================================
        // Occupancy Grid (Realtime)
        // ====================================================================

        /// <summary>
        /// Rebuild the occupancy grid from the current volume texture.
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

        // ====================================================================
        // Quality Presets (Realtime)
        // ====================================================================

        /// <summary>
        /// Apply quality preset to rendering parameters (Realtime mode only).
        /// </summary>
        public void ApplyQualityPreset()
        {
            if (m_qualityLevel == Realtime.QualityLevel.Custom && m_customPreset != null)
            {
                ApplyPreset(m_customPreset);
                return;
            }
            if (m_qualityLevel == Realtime.QualityLevel.Custom)
                return;

            var preset = Realtime.VolumeQualityPreset.CreateDefault(m_qualityLevel);
            ApplyPreset(preset);

            if (Application.isPlaying)
                Destroy(preset);
            else
                DestroyImmediate(preset);
        }

        void ApplyPreset(Realtime.VolumeQualityPreset preset)
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

        // ====================================================================
        // Gizmos
        // ====================================================================

        void OnDrawGizmosSelected()
        {
            if (!m_enableSpotLights || m_spotLights == null) return;

            foreach (var light in m_spotLights)
            {
                if (light == null || !light.enabled) continue;

                // Light position marker
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(light.transform.position, 0.1f);

                // Light direction ray (length = range)
                Gizmos.color = Color.red;
                Gizmos.DrawRay(light.transform.position, light.transform.forward * light.range);

                // Range sphere
                Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
                Gizmos.DrawWireSphere(light.transform.position, light.range);

                // Cone visualization at range distance
                float outerAngleRad = light.spotAngle * 0.5f * Mathf.Deg2Rad;
                float coneRadius = Mathf.Tan(outerAngleRad) * light.range;
                Vector3 coneEnd = light.transform.position + light.transform.forward * light.range;

                Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
                Vector3 right = light.transform.right * coneRadius;
                Vector3 up = light.transform.up * coneRadius;
                // Draw cone edges
                Gizmos.DrawLine(light.transform.position, coneEnd + right);
                Gizmos.DrawLine(light.transform.position, coneEnd - right);
                Gizmos.DrawLine(light.transform.position, coneEnd + up);
                Gizmos.DrawLine(light.transform.position, coneEnd - up);
            }
        }

        // ====================================================================
        // Utility
        // ====================================================================

        static void SetKeyword(Material mat, string keyword, bool enabled)
        {
            if (enabled)
                mat.EnableKeyword(keyword);
            else
                mat.DisableKeyword(keyword);
        }

        /// <summary>
        /// Sets the volume texture (Texture3D from OpenVDB data).
        /// </summary>
        public void SetVolumeTexture(Texture3D texture)
        {
            m_volumeTexture = texture;
            if (m_renderMode == VolumeRenderMode.Realtime)
                RebuildOccupancyGrid();
        }

        // ====================================================================
        // Public Properties
        // ====================================================================

        public VolumeRenderMode renderMode
        {
            get => m_renderMode;
            set
            {
                if (m_renderMode != value)
                {
                    m_renderMode = value;
                    ApplyShader();

                    if (value == VolumeRenderMode.Realtime)
                    {
                        ApplyQualityPreset();
                        RebuildOccupancyGrid();
                    }
                }
            }
        }

        public Texture3D volumeTexture
        {
            get => m_volumeTexture;
            set
            {
                m_volumeTexture = value;
                if (m_renderMode == VolumeRenderMode.Realtime)
                    RebuildOccupancyGrid();
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
            set => m_stepDistance = Mathf.Clamp(value, 0.002f, 0.1f);
        }

        public int maxSteps
        {
            get => m_maxSteps;
            set => m_maxSteps = Mathf.Clamp(value, 32, 512);
        }

        public int shadowSteps
        {
            get => m_shadowSteps;
            set => m_shadowSteps = Mathf.Clamp(value, 1, 64);
        }

        public float phaseAnisotropy
        {
            get => m_phaseAnisotropy;
            set => m_phaseAnisotropy = Mathf.Clamp(value, -0.9f, 0.9f);
        }

        public Realtime.QualityLevel qualityLevel
        {
            get => m_qualityLevel;
            set
            {
                m_qualityLevel = value;
                ApplyQualityPreset();
            }
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

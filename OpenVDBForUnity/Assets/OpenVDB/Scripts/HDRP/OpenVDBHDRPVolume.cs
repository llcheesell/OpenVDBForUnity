using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenVDB
{
    /// <summary>
    /// Manages an OpenVDB volume for HDRP rendering using the custom ray marching shader.
    /// Automatically syncs the main directional light direction and color to the material.
    /// </summary>
    [Obsolete("Use OpenVDBVolume instead. This component will be removed in a future version.")]
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class OpenVDBHDRPVolume : MonoBehaviour
    {
        [Header("Quality")]
        [SerializeField, Range(16, 200)]
        int m_maxIterations = 100;

        [SerializeField, Range(0.005f, 0.1f)]
        float m_stepDistance = 0.01f;

        [SerializeField, Range(0.1f, 2.0f)]
        float m_intensity = 0.3f;

        [Header("Shadows")]
        [SerializeField]
        bool m_enableDirectionalLight = true;

        [SerializeField, Range(1, 64)]
        int m_shadowSteps = 32;

        [SerializeField]
        Color m_shadowDensity = new Color(0.4f, 0.4f, 0.4f, 1f);

        [SerializeField, Range(0.001f, 0.1f)]
        float m_shadowThreshold = 0.001f;

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

        [Header("Shadow Casting")]
        [SerializeField]
        bool m_enableShadowCasting = false;

        [SerializeField, Range(-0.1f, 0.1f)]
        float m_shadowExtraBias = 0.0f;

        [SerializeField, Range(0.001f, 0.1f)]
        float m_shadowDensityThreshold = 0.01f;

        [Header("Performance")]
        [SerializeField]
        bool m_autoSyncLight = true;

        MeshRenderer m_renderer;
        MaterialPropertyBlock m_propertyBlock;
        Texture2D m_bakedColorRamp;
        bool m_gradientDirty = true;

        static readonly int s_intensityId = Shader.PropertyToID("_Intensity");
        static readonly int s_stepDistanceId = Shader.PropertyToID("_StepDistance");
        static readonly int s_shadowStepsId = Shader.PropertyToID("_ShadowSteps");
        static readonly int s_shadowDensityId = Shader.PropertyToID("_ShadowDensity");
        static readonly int s_shadowThresholdId = Shader.PropertyToID("_ShadowThreshold");
        static readonly int s_ambientColorId = Shader.PropertyToID("_AmbientColor");
        static readonly int s_ambientDensityId = Shader.PropertyToID("_AmbientDensity");
        static readonly int s_mainLightDirId = Shader.PropertyToID("_MainLightDir");
        static readonly int s_mainLightColorId = Shader.PropertyToID("_MainLightColor");
        static readonly int s_volumeId = Shader.PropertyToID("_Volume");
        static readonly int s_lightInfluenceId = Shader.PropertyToID("_LightInfluence");
        static readonly int s_ambientInfluenceId = Shader.PropertyToID("_AmbientInfluence");
        static readonly int s_colorRampId = Shader.PropertyToID("_ColorRamp");
        static readonly int s_colorRampIntensityId = Shader.PropertyToID("_ColorRampIntensity");
        static readonly int s_shadowExtraBiasId = Shader.PropertyToID("_ShadowExtraBias");
        static readonly int s_shadowDensityThresholdId = Shader.PropertyToID("_ShadowDensityThreshold");

        // Spot light shader property IDs
        static readonly int s_spotLight0PosId = Shader.PropertyToID("_SpotLight0_Position");
        static readonly int s_spotLight0DirId = Shader.PropertyToID("_SpotLight0_Direction");
        static readonly int s_spotLight0ColorId = Shader.PropertyToID("_SpotLight0_Color");
        static readonly int s_spotLight0ParamsId = Shader.PropertyToID("_SpotLight0_Params");
        static readonly int s_spotLight1PosId = Shader.PropertyToID("_SpotLight1_Position");
        static readonly int s_spotLight1DirId = Shader.PropertyToID("_SpotLight1_Direction");
        static readonly int s_spotLight1ColorId = Shader.PropertyToID("_SpotLight1_Color");
        static readonly int s_spotLight1ParamsId = Shader.PropertyToID("_SpotLight1_Params");
        static readonly int s_spotLightCountId = Shader.PropertyToID("_SpotLightCount");

        void OnEnable()
        {
            m_renderer = GetComponent<MeshRenderer>();
            m_propertyBlock = new MaterialPropertyBlock();
            m_gradientDirty = true;
        }

        void OnDisable()
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

        void OnValidate()
        {
            m_gradientDirty = true;
        }

        void Update()
        {
            if (m_renderer == null) return;

            m_renderer.GetPropertyBlock(m_propertyBlock);

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
            }

            // Sync shader keywords
            var mat = m_renderer.sharedMaterial;
            if (mat != null)
            {
                SetKeyword(mat, "ENABLE_DIRECTIONAL_LIGHT", m_enableDirectionalLight);
                SetKeyword(mat, "ENABLE_AMBIENT_LIGHT", m_enableAmbientLight);
                SetKeyword(mat, "ENABLE_COLOR_RAMP", m_enableColorRamp);
                SetKeyword(mat, "ENABLE_SPOT_LIGHTS", m_enableSpotLights);
            }

            m_renderer.SetPropertyBlock(m_propertyBlock);
        }

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

        void SyncSpotLights()
        {
            int count = 0;
            if (m_spotLights != null)
            {
                for (int i = 0; i < Mathf.Min(m_spotLights.Length, 2); i++)
                {
                    var light = m_spotLights[i];
                    if (light == null || light.type != LightType.Spot || !light.enabled) continue;

                    var pos = light.transform.position;
                    var dir = light.transform.forward;
                    var color = light.color * light.intensity;
                    var range = light.range;

                    // Compute angleScale and angleOffset from spotAngle
                    float outerRad = light.spotAngle * 0.5f * Mathf.Deg2Rad;
                    float innerRad = light.innerSpotAngle * 0.5f * Mathf.Deg2Rad;
                    float cosOuter = Mathf.Cos(outerRad);
                    float cosInner = Mathf.Cos(innerRad);
                    float angleScale = 1.0f / Mathf.Max(cosInner - cosOuter, 0.001f);
                    float angleOffset = -cosOuter * angleScale;

                    if (count == 0)
                    {
                        m_propertyBlock.SetVector(s_spotLight0PosId, pos);
                        m_propertyBlock.SetVector(s_spotLight0DirId, dir);
                        m_propertyBlock.SetVector(s_spotLight0ColorId, new Vector4(color.r, color.g, color.b, 1));
                        m_propertyBlock.SetVector(s_spotLight0ParamsId, new Vector4(range, angleScale, angleOffset, light.intensity));
                    }
                    else if (count == 1)
                    {
                        m_propertyBlock.SetVector(s_spotLight1PosId, pos);
                        m_propertyBlock.SetVector(s_spotLight1DirId, dir);
                        m_propertyBlock.SetVector(s_spotLight1ColorId, new Vector4(color.r, color.g, color.b, 1));
                        m_propertyBlock.SetVector(s_spotLight1ParamsId, new Vector4(range, angleScale, angleOffset, light.intensity));
                    }
                    count++;
                }
            }
            m_propertyBlock.SetFloat(s_spotLightCountId, count);
        }

        void SyncMainLight()
        {
            var sun = RenderSettings.sun;
            if (sun != null)
            {
                m_propertyBlock.SetVector(s_mainLightDirId, -sun.transform.forward);
                m_propertyBlock.SetColor(s_mainLightColorId, sun.color * sun.intensity);
            }
            else
            {
                // Fallback: find brightest directional light
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
        }

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
            if (m_renderer == null) return;
            if (m_propertyBlock == null) m_propertyBlock = new MaterialPropertyBlock();

            m_renderer.GetPropertyBlock(m_propertyBlock);
            m_propertyBlock.SetTexture(s_volumeId, texture);
            m_renderer.SetPropertyBlock(m_propertyBlock);
        }

        public float stepDistance
        {
            get => m_stepDistance;
            set => m_stepDistance = Mathf.Clamp(value, 0.005f, 0.1f);
        }

        public float intensity
        {
            get => m_intensity;
            set => m_intensity = Mathf.Clamp(value, 0.1f, 2.0f);
        }

        public int shadowSteps
        {
            get => m_shadowSteps;
            set => m_shadowSteps = Mathf.Clamp(value, 1, 64);
        }

        public int maxIterations
        {
            get => m_maxIterations;
            set => m_maxIterations = Mathf.Clamp(value, 16, 200);
        }
    }
}

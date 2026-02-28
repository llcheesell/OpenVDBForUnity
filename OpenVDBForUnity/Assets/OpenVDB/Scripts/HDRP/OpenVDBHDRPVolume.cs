using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenVDB
{
    /// <summary>
    /// Manages an OpenVDB volume for HDRP rendering using the custom ray marching shader.
    /// Automatically syncs the main directional light direction and color to the material.
    /// </summary>
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

        [Header("Performance")]
        [SerializeField]
        bool m_autoSyncLight = true;

        MeshRenderer m_renderer;
        MaterialPropertyBlock m_propertyBlock;

        static readonly int s_maxIterationsId = Shader.PropertyToID("_MaxIterations");
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

        void OnEnable()
        {
            m_renderer = GetComponent<MeshRenderer>();
            m_propertyBlock = new MaterialPropertyBlock();
        }

        void Update()
        {
            if (m_renderer == null) return;

            m_renderer.GetPropertyBlock(m_propertyBlock);

            m_propertyBlock.SetInt(s_maxIterationsId, m_maxIterations);
            m_propertyBlock.SetFloat(s_intensityId, m_intensity);
            m_propertyBlock.SetFloat(s_stepDistanceId, m_stepDistance);
            m_propertyBlock.SetFloat(s_shadowStepsId, m_shadowSteps);
            m_propertyBlock.SetColor(s_shadowDensityId, m_shadowDensity);
            m_propertyBlock.SetFloat(s_shadowThresholdId, m_shadowThreshold);
            m_propertyBlock.SetColor(s_ambientColorId, m_ambientColor);
            m_propertyBlock.SetFloat(s_ambientDensityId, m_ambientDensity);

            if (m_autoSyncLight)
            {
                SyncMainLight();
            }

            // Sync shader keywords
            var mat = m_renderer.sharedMaterial;
            if (mat != null)
            {
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

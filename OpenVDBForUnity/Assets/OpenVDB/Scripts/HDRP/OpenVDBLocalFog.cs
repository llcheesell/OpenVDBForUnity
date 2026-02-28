using System;
using UnityEngine;

namespace OpenVDB
{
    /// <summary>
    /// Integrates OpenVDB volume data with HDRP's LocalVolumetricFog system.
    /// This provides native HDRP volumetric lighting integration at the cost of
    /// lower resolution (limited by HDRP's volumetric buffer: 64-128 slices).
    ///
    /// Requires:
    /// - HDRP package installed
    /// - Volumetric Fog enabled in HDRP Asset (Lighting > Volumetrics)
    /// - Fog Volume Override enabled in scene Volume profile
    ///
    /// Usage:
    /// 1. Add this component alongside a LocalVolumetricFog component
    /// 2. Assign a Texture3D from OpenVDB data
    /// 3. The component will sync the texture and parameters to LocalVolumetricFog
    ///
    /// For higher quality rendering, use OpenVDBVolume instead.
    /// </summary>
    [ExecuteAlways]
    public class OpenVDBLocalFog : MonoBehaviour
    {
        [SerializeField]
        Texture3D m_volumeTexture;

        [SerializeField]
        float m_fogDistance = 5.0f;

        [SerializeField]
        Color m_albedo = Color.white;

        [SerializeField]
        Vector3 m_size = new Vector3(10f, 10f, 10f);

        [SerializeField]
        float m_blendDistance = 1.0f;

        [Header("Distance Fade")]
        [SerializeField]
        float m_distanceFadeStart = 50f;

        [SerializeField]
        float m_distanceFadeEnd = 100f;

        Component m_localVolumetricFog;
        Type m_fogType;
        bool m_hdrpAvailable;

        void OnEnable()
        {
            TryInitializeHDRP();
        }

        void Update()
        {
            if (!m_hdrpAvailable || m_localVolumetricFog == null) return;
            SyncParameters();
        }

        void TryInitializeHDRP()
        {
            m_hdrpAvailable = false;

            // Dynamically find HDRP types to avoid compile errors when HDRP is not installed
            m_fogType = Type.GetType(
                "UnityEngine.Rendering.HighDefinition.LocalVolumetricFog, Unity.RenderPipelines.HighDefinition.Runtime");

            if (m_fogType == null) return;

            m_localVolumetricFog = GetComponent(m_fogType);
            if (m_localVolumetricFog == null)
            {
                m_localVolumetricFog = gameObject.AddComponent(m_fogType);
            }

            m_hdrpAvailable = m_localVolumetricFog != null;
        }

        void SyncParameters()
        {
            if (m_fogType == null || m_localVolumetricFog == null) return;

            var parametersField = m_fogType.GetProperty("parameters");
            if (parametersField == null) return;

            var parameters = parametersField.GetValue(m_localVolumetricFog);
            if (parameters == null) return;

            var paramType = parameters.GetType();

            SetFieldValue(paramType, parameters, "volumeMask", m_volumeTexture);
            SetFieldValue(paramType, parameters, "meanFreePath", m_fogDistance);
            SetFieldValue(paramType, parameters, "albedo", m_albedo);
            SetFieldValue(paramType, parameters, "size", m_size);
            SetFieldValue(paramType, parameters, "blendDistance", m_blendDistance);
            SetFieldValue(paramType, parameters, "distanceFadeStart", m_distanceFadeStart);
            SetFieldValue(paramType, parameters, "distanceFadeEnd", m_distanceFadeEnd);

            parametersField.SetValue(m_localVolumetricFog, parameters);
        }

        static void SetFieldValue(Type type, object obj, string name, object value)
        {
            var field = type.GetField(name);
            if (field != null)
            {
                field.SetValue(obj, value);
            }
        }

        public Texture3D volumeTexture
        {
            get => m_volumeTexture;
            set
            {
                m_volumeTexture = value;
                if (m_hdrpAvailable) SyncParameters();
            }
        }

        public float fogDistance
        {
            get => m_fogDistance;
            set => m_fogDistance = value;
        }

        public Vector3 size
        {
            get => m_size;
            set => m_size = value;
        }
    }
}

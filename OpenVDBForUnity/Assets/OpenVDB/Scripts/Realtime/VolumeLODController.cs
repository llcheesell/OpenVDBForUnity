using UnityEngine;

namespace OpenVDB.Realtime
{
    /// <summary>
    /// Dynamically adjusts volume rendering quality based on camera distance
    /// and frame budget. This component modifies the OpenVDBVolume
    /// parameters at runtime for optimal performance.
    /// </summary>
    [RequireComponent(typeof(OpenVDBVolume))]
    public class VolumeLODController : MonoBehaviour
    {
        [Header("Distance-based LOD")]
        [SerializeField]
        bool m_enableDistanceLOD = true;

        [SerializeField, Range(1f, 50f)]
        float m_nearDistance = 5f;

        [SerializeField, Range(10f, 200f)]
        float m_farDistance = 50f;

        [Header("Quality at Near Distance")]
        [SerializeField, Range(0.002f, 0.02f)]
        float m_nearStepDistance = 0.005f;

        [SerializeField, Range(64, 512)]
        int m_nearMaxSteps = 256;

        [SerializeField, Range(1, 16)]
        int m_nearShadowSteps = 8;

        [Header("Quality at Far Distance")]
        [SerializeField, Range(0.01f, 0.05f)]
        float m_farStepDistance = 0.025f;

        [SerializeField, Range(32, 256)]
        int m_farMaxSteps = 64;

        [SerializeField, Range(1, 8)]
        int m_farShadowSteps = 2;

        [Header("Frame Budget")]
        [SerializeField]
        bool m_enableFrameBudget = false;

        [SerializeField, Range(8f, 33f)]
        float m_targetFrameTimeMs = 16.6f;

        [SerializeField, Range(0.5f, 2f)]
        float m_qualityAdjustSpeed = 1f;

        // Runtime
        OpenVDBVolume m_volume;
        Camera m_mainCamera;
        float m_currentQualityScale = 1f;
        float m_smoothedFrameTime;

        void OnEnable()
        {
            m_volume = GetComponent<OpenVDBVolume>();
        }

        void Update()
        {
            if (m_volume == null) return;

            // Cache camera reference
            if (m_mainCamera == null)
                m_mainCamera = Camera.main;
            if (m_mainCamera == null)
                return;

            float qualityT = 1f;

            // Distance-based LOD
            if (m_enableDistanceLOD)
            {
                float dist = Vector3.Distance(m_mainCamera.transform.position, transform.position);
                float distT = Mathf.InverseLerp(m_nearDistance, m_farDistance, dist);
                qualityT = 1f - distT;
            }

            // Frame budget adjustment
            if (m_enableFrameBudget)
            {
                float frameTime = Time.unscaledDeltaTime * 1000f;
                m_smoothedFrameTime = Mathf.Lerp(m_smoothedFrameTime, frameTime, 0.1f);

                float budgetRatio = m_smoothedFrameTime / m_targetFrameTimeMs;
                if (budgetRatio > 1.1f)
                {
                    // Over budget - reduce quality
                    m_currentQualityScale -= m_qualityAdjustSpeed * Time.unscaledDeltaTime;
                }
                else if (budgetRatio < 0.9f)
                {
                    // Under budget - increase quality
                    m_currentQualityScale += m_qualityAdjustSpeed * Time.unscaledDeltaTime * 0.5f;
                }
                m_currentQualityScale = Mathf.Clamp(m_currentQualityScale, 0.2f, 1f);
                qualityT *= m_currentQualityScale;
            }

            // Apply interpolated quality settings
            m_volume.stepDistance = Mathf.Lerp(m_farStepDistance, m_nearStepDistance, qualityT);
            m_volume.maxSteps = Mathf.RoundToInt(Mathf.Lerp(m_farMaxSteps, m_nearMaxSteps, qualityT));
            m_volume.shadowSteps = Mathf.RoundToInt(Mathf.Lerp(m_farShadowSteps, m_nearShadowSteps, qualityT));
        }

        public float currentQualityScale => m_currentQualityScale;
    }
}

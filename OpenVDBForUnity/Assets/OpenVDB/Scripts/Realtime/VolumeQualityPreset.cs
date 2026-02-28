using UnityEngine;

namespace OpenVDB.Realtime
{
    public enum QualityLevel
    {
        Low,
        Medium,
        High,
        Ultra,
        Custom
    }

    [CreateAssetMenu(fileName = "VolumeQualityPreset", menuName = "OpenVDB/Volume Quality Preset")]
    public class VolumeQualityPreset : ScriptableObject
    {
        [Header("Ray Marching")]
        [Range(0.002f, 0.05f)]
        public float stepDistance = 0.008f;

        [Range(32, 512)]
        public int maxSteps = 200;

        [Header("Shadows")]
        [Range(1, 16)]
        public int shadowSteps = 6;

        [Header("Features")]
        public bool emptySpaceSkipping = true;
        public bool temporalJitter = true;
        public bool adaptiveStepping = true;
        public bool henyeyGreensteinPhase = true;
        public bool multiScatterApprox = false;

        [Header("Adaptive Stepping")]
        [Range(0f, 2f)]
        public float adaptiveDistanceScale = 0.5f;

        [Range(0.001f, 0.01f)]
        public float minStepDistance = 0.003f;

        [Range(0.01f, 0.1f)]
        public float maxStepDistance = 0.05f;

        [Header("Occupancy Grid")]
        [Range(8, 64)]
        public int occupancyGridDivisor = 8;

        public static VolumeQualityPreset CreateDefault(QualityLevel level)
        {
            var preset = CreateInstance<VolumeQualityPreset>();

            switch (level)
            {
                case QualityLevel.Low:
                    preset.stepDistance = 0.02f;
                    preset.maxSteps = 64;
                    preset.shadowSteps = 2;
                    preset.emptySpaceSkipping = true;
                    preset.temporalJitter = true;
                    preset.adaptiveStepping = true;
                    preset.henyeyGreensteinPhase = false;
                    preset.multiScatterApprox = false;
                    break;

                case QualityLevel.Medium:
                    preset.stepDistance = 0.01f;
                    preset.maxSteps = 128;
                    preset.shadowSteps = 4;
                    preset.emptySpaceSkipping = true;
                    preset.temporalJitter = true;
                    preset.adaptiveStepping = true;
                    preset.henyeyGreensteinPhase = true;
                    preset.multiScatterApprox = false;
                    break;

                case QualityLevel.High:
                    preset.stepDistance = 0.005f;
                    preset.maxSteps = 256;
                    preset.shadowSteps = 8;
                    preset.emptySpaceSkipping = true;
                    preset.temporalJitter = true;
                    preset.adaptiveStepping = true;
                    preset.henyeyGreensteinPhase = true;
                    preset.multiScatterApprox = true;
                    break;

                case QualityLevel.Ultra:
                    preset.stepDistance = 0.003f;
                    preset.maxSteps = 512;
                    preset.shadowSteps = 16;
                    preset.emptySpaceSkipping = true;
                    preset.temporalJitter = true;
                    preset.adaptiveStepping = false;
                    preset.henyeyGreensteinPhase = true;
                    preset.multiScatterApprox = true;
                    break;
            }

            return preset;
        }
    }
}

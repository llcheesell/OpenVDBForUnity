#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using OpenVDB.Realtime;
using QualityLevel = OpenVDB.Realtime.QualityLevel;

namespace OpenVDB.Editor
{
    [CustomEditor(typeof(OpenVDBRealtimeVolume))]
    public class OpenVDBRealtimeVolumeEditor : UnityEditor.Editor
    {
        SerializedProperty m_volumeTexture;
        SerializedProperty m_qualityLevel;
        SerializedProperty m_customPreset;
        SerializedProperty m_intensity;
        SerializedProperty m_stepDistance;
        SerializedProperty m_maxSteps;
        SerializedProperty m_enableDirectionalLight;
        SerializedProperty m_shadowSteps;
        SerializedProperty m_shadowDensity;
        SerializedProperty m_shadowThreshold;
        SerializedProperty m_phaseAnisotropy;
        SerializedProperty m_enableAmbientLight;
        SerializedProperty m_ambientColor;
        SerializedProperty m_ambientDensity;
        SerializedProperty m_enableEmptySpaceSkipping;
        SerializedProperty m_occupancyDivisor;
        SerializedProperty m_occupancyThreshold;
        SerializedProperty m_enableTemporalJitter;
        SerializedProperty m_enableAdaptiveStepping;
        SerializedProperty m_adaptiveDistanceScale;
        SerializedProperty m_minStepDistance;
        SerializedProperty m_maxStepDistance;
        SerializedProperty m_enableHGPhase;
        SerializedProperty m_enableMultiScatter;
        SerializedProperty m_occupancyComputeShader;

        bool m_showRendering = true;
        bool m_showLighting = true;
        bool m_showEmptySpace = true;
        bool m_showTemporal = true;
        bool m_showAdaptive = true;
        bool m_showAdvanced;

        void OnEnable()
        {
            m_volumeTexture = serializedObject.FindProperty("m_volumeTexture");
            m_qualityLevel = serializedObject.FindProperty("m_qualityLevel");
            m_customPreset = serializedObject.FindProperty("m_customPreset");
            m_intensity = serializedObject.FindProperty("m_intensity");
            m_stepDistance = serializedObject.FindProperty("m_stepDistance");
            m_maxSteps = serializedObject.FindProperty("m_maxSteps");
            m_enableDirectionalLight = serializedObject.FindProperty("m_enableDirectionalLight");
            m_shadowSteps = serializedObject.FindProperty("m_shadowSteps");
            m_shadowDensity = serializedObject.FindProperty("m_shadowDensity");
            m_shadowThreshold = serializedObject.FindProperty("m_shadowThreshold");
            m_phaseAnisotropy = serializedObject.FindProperty("m_phaseAnisotropy");
            m_enableAmbientLight = serializedObject.FindProperty("m_enableAmbientLight");
            m_ambientColor = serializedObject.FindProperty("m_ambientColor");
            m_ambientDensity = serializedObject.FindProperty("m_ambientDensity");
            m_enableEmptySpaceSkipping = serializedObject.FindProperty("m_enableEmptySpaceSkipping");
            m_occupancyDivisor = serializedObject.FindProperty("m_occupancyDivisor");
            m_occupancyThreshold = serializedObject.FindProperty("m_occupancyThreshold");
            m_enableTemporalJitter = serializedObject.FindProperty("m_enableTemporalJitter");
            m_enableAdaptiveStepping = serializedObject.FindProperty("m_enableAdaptiveStepping");
            m_adaptiveDistanceScale = serializedObject.FindProperty("m_adaptiveDistanceScale");
            m_minStepDistance = serializedObject.FindProperty("m_minStepDistance");
            m_maxStepDistance = serializedObject.FindProperty("m_maxStepDistance");
            m_enableHGPhase = serializedObject.FindProperty("m_enableHGPhase");
            m_enableMultiScatter = serializedObject.FindProperty("m_enableMultiScatter");
            m_occupancyComputeShader = serializedObject.FindProperty("m_occupancyComputeShader");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Volume Data
            EditorGUILayout.LabelField("Volume Data", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_volumeTexture);
            EditorGUILayout.PropertyField(m_occupancyComputeShader, new GUIContent("Occupancy Compute Shader"));
            EditorGUILayout.Space();

            // Quality Preset
            EditorGUILayout.LabelField("Quality Preset", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_qualityLevel);
            if (m_qualityLevel.enumValueIndex == (int)QualityLevel.Custom)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_customPreset);
                EditorGUI.indentLevel--;
            }

            if (GUILayout.Button("Apply Preset"))
            {
                var vol = (OpenVDBRealtimeVolume)target;
                vol.ApplyQualityPreset();
                EditorUtility.SetDirty(target);
            }
            EditorGUILayout.Space();

            // Rendering
            m_showRendering = EditorGUILayout.Foldout(m_showRendering, "Rendering", true, EditorStyles.foldoutHeader);
            if (m_showRendering)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_intensity);
                EditorGUILayout.PropertyField(m_stepDistance);
                EditorGUILayout.PropertyField(m_maxSteps);
                EditorGUI.indentLevel--;
            }

            // Lighting
            m_showLighting = EditorGUILayout.Foldout(m_showLighting, "Lighting", true, EditorStyles.foldoutHeader);
            if (m_showLighting)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_enableDirectionalLight);
                if (m_enableDirectionalLight.boolValue)
                {
                    EditorGUILayout.PropertyField(m_shadowSteps);
                    EditorGUILayout.PropertyField(m_shadowDensity);
                    EditorGUILayout.PropertyField(m_shadowThreshold);
                }
                EditorGUILayout.PropertyField(m_enableAmbientLight);
                if (m_enableAmbientLight.boolValue)
                {
                    EditorGUILayout.PropertyField(m_ambientColor);
                    EditorGUILayout.PropertyField(m_ambientDensity);
                }
                EditorGUI.indentLevel--;
            }

            // Empty Space Skipping
            m_showEmptySpace = EditorGUILayout.Foldout(m_showEmptySpace, "Empty Space Skipping", true, EditorStyles.foldoutHeader);
            if (m_showEmptySpace)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_enableEmptySpaceSkipping);
                if (m_enableEmptySpaceSkipping.boolValue)
                {
                    EditorGUILayout.PropertyField(m_occupancyDivisor, new GUIContent("Grid Divisor"));
                    EditorGUILayout.PropertyField(m_occupancyThreshold, new GUIContent("Threshold"));

                    if (GUILayout.Button("Rebuild Occupancy Grid"))
                    {
                        ((OpenVDBRealtimeVolume)target).RebuildOccupancyGrid();
                    }
                }
                EditorGUI.indentLevel--;
            }

            // Temporal
            m_showTemporal = EditorGUILayout.Foldout(m_showTemporal, "Temporal Jitter", true, EditorStyles.foldoutHeader);
            if (m_showTemporal)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_enableTemporalJitter);
                EditorGUILayout.HelpBox(
                    "Temporal jitter adds per-pixel noise to ray start positions. " +
                    "Combined with TAA, this eliminates banding without additional ray steps.",
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }

            // Adaptive Stepping
            m_showAdaptive = EditorGUILayout.Foldout(m_showAdaptive, "Adaptive Stepping", true, EditorStyles.foldoutHeader);
            if (m_showAdaptive)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_enableAdaptiveStepping);
                if (m_enableAdaptiveStepping.boolValue)
                {
                    EditorGUILayout.PropertyField(m_adaptiveDistanceScale, new GUIContent("Distance Scale"));
                    EditorGUILayout.PropertyField(m_minStepDistance, new GUIContent("Min Step"));
                    EditorGUILayout.PropertyField(m_maxStepDistance, new GUIContent("Max Step"));
                }
                EditorGUI.indentLevel--;
            }

            // Advanced
            m_showAdvanced = EditorGUILayout.Foldout(m_showAdvanced, "Advanced", true, EditorStyles.foldoutHeader);
            if (m_showAdvanced)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_enableHGPhase, new GUIContent("Henyey-Greenstein Phase"));
                if (m_enableHGPhase.boolValue)
                {
                    EditorGUILayout.PropertyField(m_phaseAnisotropy, new GUIContent("Anisotropy (g)"));
                    EditorGUILayout.HelpBox(
                        "g > 0: Forward scattering (bright halo around light)\n" +
                        "g = 0: Isotropic scattering\n" +
                        "g < 0: Back scattering",
                        MessageType.None);
                }
                EditorGUILayout.PropertyField(m_enableMultiScatter, new GUIContent("Multi-Scattering Approx"));
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif

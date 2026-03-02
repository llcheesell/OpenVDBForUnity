#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using OpenVDB.Realtime;
using QualityLevel = OpenVDB.Realtime.QualityLevel;

namespace OpenVDB.Editor
{
    [CustomEditor(typeof(OpenVDBVolume))]
    public class OpenVDBVolumeEditor : UnityEditor.Editor
    {
        // Mode
        SerializedProperty m_renderMode;

        // Shared
        SerializedProperty m_volumeTexture;
        SerializedProperty m_intensity;
        SerializedProperty m_stepDistance;
        SerializedProperty m_enableDirectionalLight;
        SerializedProperty m_shadowSteps;
        SerializedProperty m_shadowDensity;
        SerializedProperty m_shadowThreshold;
        SerializedProperty m_enableAmbientLight;
        SerializedProperty m_ambientColor;
        SerializedProperty m_ambientDensity;
        SerializedProperty m_lightInfluence;
        SerializedProperty m_ambientInfluence;
        SerializedProperty m_enableColorRamp;
        SerializedProperty m_colorRampGradient;
        SerializedProperty m_colorRampIntensity;
        SerializedProperty m_enableSpotLights;
        SerializedProperty m_spotLights;
        SerializedProperty m_spotLightInfluence;
        SerializedProperty m_enableShadowCasting;
        SerializedProperty m_shadowExtraBias;
        SerializedProperty m_shadowDensityThreshold;
        SerializedProperty m_autoSyncLight;

        // Realtime-only
        SerializedProperty m_qualityLevel;
        SerializedProperty m_customPreset;
        SerializedProperty m_maxSteps;
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
        SerializedProperty m_phaseAnisotropy;
        SerializedProperty m_occupancyComputeShader;

        // Debug
        SerializedProperty m_debugMode;

        // Foldout states
        bool m_showRendering = true;
        bool m_showLighting = true;
        bool m_showAmbient = true;
        bool m_showLightInfluence = true;
        bool m_showColorRamp;
        bool m_showSpotLights;
        bool m_showShadowCasting;
        bool m_showEmptySpace = true;
        bool m_showTemporal = true;
        bool m_showAdaptive = true;
        bool m_showAdvanced;
        bool m_showQuality = true;

        VolumeRenderMode m_lastRenderMode;

        void OnEnable()
        {
            m_renderMode = serializedObject.FindProperty("m_renderMode");

            // Shared
            m_volumeTexture = serializedObject.FindProperty("m_volumeTexture");
            m_intensity = serializedObject.FindProperty("m_intensity");
            m_stepDistance = serializedObject.FindProperty("m_stepDistance");
            m_enableDirectionalLight = serializedObject.FindProperty("m_enableDirectionalLight");
            m_shadowSteps = serializedObject.FindProperty("m_shadowSteps");
            m_shadowDensity = serializedObject.FindProperty("m_shadowDensity");
            m_shadowThreshold = serializedObject.FindProperty("m_shadowThreshold");
            m_enableAmbientLight = serializedObject.FindProperty("m_enableAmbientLight");
            m_ambientColor = serializedObject.FindProperty("m_ambientColor");
            m_ambientDensity = serializedObject.FindProperty("m_ambientDensity");
            m_lightInfluence = serializedObject.FindProperty("m_lightInfluence");
            m_ambientInfluence = serializedObject.FindProperty("m_ambientInfluence");
            m_enableColorRamp = serializedObject.FindProperty("m_enableColorRamp");
            m_colorRampGradient = serializedObject.FindProperty("m_colorRampGradient");
            m_colorRampIntensity = serializedObject.FindProperty("m_colorRampIntensity");
            m_enableSpotLights = serializedObject.FindProperty("m_enableSpotLights");
            m_spotLights = serializedObject.FindProperty("m_spotLights");
            m_spotLightInfluence = serializedObject.FindProperty("m_spotLightInfluence");
            m_enableShadowCasting = serializedObject.FindProperty("m_enableShadowCasting");
            m_shadowExtraBias = serializedObject.FindProperty("m_shadowExtraBias");
            m_shadowDensityThreshold = serializedObject.FindProperty("m_shadowDensityThreshold");
            m_autoSyncLight = serializedObject.FindProperty("m_autoSyncLight");

            // Realtime-only
            m_qualityLevel = serializedObject.FindProperty("m_qualityLevel");
            m_customPreset = serializedObject.FindProperty("m_customPreset");
            m_maxSteps = serializedObject.FindProperty("m_maxSteps");
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
            m_phaseAnisotropy = serializedObject.FindProperty("m_phaseAnisotropy");
            m_occupancyComputeShader = serializedObject.FindProperty("m_occupancyComputeShader");

            // Debug
            m_debugMode = serializedObject.FindProperty("m_debugMode");

            m_lastRenderMode = (VolumeRenderMode)m_renderMode.enumValueIndex;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            bool isRealtime = m_renderMode.enumValueIndex == (int)VolumeRenderMode.Realtime;

            // ---- Render Mode ----
            EditorGUILayout.LabelField("OpenVDB Volume", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(m_renderMode, new GUIContent("Render Mode"));
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                var vol = (OpenVDBVolume)target;
                vol.ApplyShader();
                if (vol.renderMode == VolumeRenderMode.Realtime)
                {
                    vol.ApplyQualityPreset();
                    vol.RebuildOccupancyGrid();
                }
                EditorUtility.SetDirty(target);
                serializedObject.Update();
                isRealtime = m_renderMode.enumValueIndex == (int)VolumeRenderMode.Realtime;
            }

            EditorGUILayout.HelpBox(
                isRealtime
                    ? "Realtime: Optimized ray marching with occupancy grid, adaptive stepping, temporal jitter, and HG phase function."
                    : "Classic: Traditional ray marching with HDRP light buffer integration.",
                MessageType.Info);

            EditorGUILayout.Space();

            // ---- Volume Data ----
            EditorGUILayout.LabelField("Volume Data", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_volumeTexture);
            if (isRealtime)
            {
                EditorGUILayout.PropertyField(m_occupancyComputeShader, new GUIContent("Occupancy Compute Shader"));
            }
            EditorGUILayout.PropertyField(m_autoSyncLight, new GUIContent("Auto Sync Light"));
            EditorGUILayout.Space();

            // ---- Quality Preset (Realtime only) ----
            if (isRealtime)
            {
                m_showQuality = EditorGUILayout.Foldout(m_showQuality, "Quality Preset", true, EditorStyles.foldoutHeader);
                if (m_showQuality)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(m_qualityLevel);
                    if (m_qualityLevel.enumValueIndex == (int)QualityLevel.Custom)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(m_customPreset);
                        EditorGUI.indentLevel--;
                    }
                    if (GUILayout.Button("Apply Preset"))
                    {
                        ((OpenVDBVolume)target).ApplyQualityPreset();
                        EditorUtility.SetDirty(target);
                    }
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.Space();
            }

            // ---- Rendering ----
            m_showRendering = EditorGUILayout.Foldout(m_showRendering, "Rendering", true, EditorStyles.foldoutHeader);
            if (m_showRendering)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_intensity);
                EditorGUILayout.PropertyField(m_stepDistance);
                if (isRealtime)
                {
                    EditorGUILayout.PropertyField(m_maxSteps);
                }
                EditorGUI.indentLevel--;
            }

            // ---- Lighting ----
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
                if (isRealtime && m_enableHGPhase != null)
                {
                    EditorGUILayout.PropertyField(m_enableHGPhase, new GUIContent("Henyey-Greenstein Phase"));
                    if (m_enableHGPhase.boolValue)
                    {
                        EditorGUILayout.PropertyField(m_phaseAnisotropy, new GUIContent("Anisotropy (g)"));
                        EditorGUILayout.HelpBox(
                            "g > 0: Forward scattering\ng = 0: Isotropic\ng < 0: Back scattering",
                            MessageType.None);
                    }
                }
                EditorGUI.indentLevel--;
            }

            // ---- Ambient ----
            m_showAmbient = EditorGUILayout.Foldout(m_showAmbient, "Ambient", true, EditorStyles.foldoutHeader);
            if (m_showAmbient)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_enableAmbientLight);
                if (m_enableAmbientLight.boolValue)
                {
                    EditorGUILayout.PropertyField(m_ambientColor);
                    EditorGUILayout.PropertyField(m_ambientDensity);
                }
                EditorGUI.indentLevel--;
            }

            // ---- Light Influence ----
            m_showLightInfluence = EditorGUILayout.Foldout(m_showLightInfluence, "Light Influence", true, EditorStyles.foldoutHeader);
            if (m_showLightInfluence)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_lightInfluence);
                EditorGUILayout.PropertyField(m_ambientInfluence);
                EditorGUILayout.HelpBox(
                    "Controls how much scene lights affect the volume.\n" +
                    "1.0 = Normal, <1 = Dimmer, >1 = Brighter.",
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }

            // ---- Color Ramp ----
            m_showColorRamp = EditorGUILayout.Foldout(m_showColorRamp, "Color Ramp", true, EditorStyles.foldoutHeader);
            if (m_showColorRamp)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_enableColorRamp);
                if (m_enableColorRamp.boolValue)
                {
                    EditorGUILayout.PropertyField(m_colorRampGradient, new GUIContent("Gradient"));
                    EditorGUILayout.PropertyField(m_colorRampIntensity, new GUIContent("Intensity"));
                    EditorGUILayout.HelpBox(
                        "Maps density (0=transparent, 1=dense) to color and opacity via gradient.",
                        MessageType.Info);
                }
                EditorGUI.indentLevel--;
            }

            // ---- Spot Lights ----
            m_showSpotLights = EditorGUILayout.Foldout(m_showSpotLights, "Spot Lights", true, EditorStyles.foldoutHeader);
            if (m_showSpotLights)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_enableSpotLights);
                if (m_enableSpotLights.boolValue)
                {
                    EditorGUILayout.PropertyField(m_spotLights, new GUIContent("Lights (max 2)"), true);
                    EditorGUILayout.PropertyField(m_spotLightInfluence, new GUIContent("Spot Light Influence"));
                    EditorGUILayout.HelpBox(
                        "Assign up to 2 Unity Spot Lights. Parameters are synced automatically.\n" +
                        "Spot Light Influence controls brightness independently from directional Light Influence.",
                        MessageType.Info);
                }
                EditorGUI.indentLevel--;
            }

            // ---- Shadow Casting ----
            m_showShadowCasting = EditorGUILayout.Foldout(m_showShadowCasting, "Shadow Casting", true, EditorStyles.foldoutHeader);
            if (m_showShadowCasting)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_enableShadowCasting);
                if (m_enableShadowCasting.boolValue)
                {
                    EditorGUILayout.PropertyField(m_shadowExtraBias, new GUIContent("Extra Bias"));
                    EditorGUILayout.PropertyField(m_shadowDensityThreshold, new GUIContent("Density Threshold"));
                    EditorGUILayout.HelpBox(
                        "When enabled, the volume casts shadows onto other meshes.\nThis is GPU-expensive - use only when needed.",
                        MessageType.Warning);
                }
                EditorGUI.indentLevel--;
            }

            // ---- Realtime-Only Sections ----
            if (isRealtime)
            {
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
                            ((OpenVDBVolume)target).RebuildOccupancyGrid();
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
                        "Adds per-pixel noise to ray start positions. Combined with TAA, eliminates banding.",
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
                    EditorGUILayout.PropertyField(m_enableMultiScatter, new GUIContent("Multi-Scattering Approx"));
                    EditorGUI.indentLevel--;
                }
            }

            // ---- Debug ----
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_debugMode, new GUIContent("Debug Mode"));
            EditorGUILayout.HelpBox(
                "0: Normal  1: World Position  2: Spot Distance\n" +
                "3: Dist Atten  4: Cone Atten  5: Combined Atten",
                MessageType.None);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif

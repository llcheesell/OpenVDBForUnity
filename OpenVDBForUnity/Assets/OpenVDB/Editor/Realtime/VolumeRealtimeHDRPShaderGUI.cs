#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace OpenVDB.Editor
{
    public class VolumeRealtimeHDRPShaderGUI : ShaderGUI
    {
        bool m_showQuality = true;
        bool m_showLighting = true;
        bool m_showAmbient = true;
        bool m_showLightInfluence = true;
        bool m_showAdaptive = true;
        bool m_showColorRamp;
        bool m_showSpotLights;
        bool m_showShadowCasting;
        bool m_showFeatures = true;
        bool m_showDepth;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            var material = materialEditor.target as Material;
            if (material == null) return;

            MaterialProperty FindProp(string name) =>
                FindProperty(name, properties, false);

            EditorGUILayout.LabelField("OpenVDB Realtime HDRP Volume", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Volume
            materialEditor.TexturePropertySingleLine(
                new GUIContent("Volume Texture"), FindProp("_Volume"));
            var occGrid = FindProp("_OccupancyGrid");
            if (occGrid != null)
                materialEditor.TexturePropertySingleLine(
                    new GUIContent("Occupancy Grid"), occGrid);

            EditorGUILayout.Space();

            // Quality
            m_showQuality = EditorGUILayout.Foldout(m_showQuality, "Quality", true, EditorStyles.foldoutHeader);
            if (m_showQuality)
            {
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(FindProp("_Intensity"), "Intensity");
                materialEditor.ShaderProperty(FindProp("_StepDistance"), "Step Distance");
                materialEditor.ShaderProperty(FindProp("_MaxSteps"), "Max Steps");
                EditorGUI.indentLevel--;
            }

            // Light Influence
            m_showLightInfluence = EditorGUILayout.Foldout(m_showLightInfluence, "Light Influence", true, EditorStyles.foldoutHeader);
            if (m_showLightInfluence)
            {
                EditorGUI.indentLevel++;
                var lightInf = FindProp("_LightInfluence");
                var ambInf = FindProp("_AmbientInfluence");
                if (lightInf != null)
                    materialEditor.ShaderProperty(lightInf, "Light Influence");
                if (ambInf != null)
                    materialEditor.ShaderProperty(ambInf, "Ambient Influence");
                EditorGUILayout.HelpBox(
                    "Controls how much scene lights affect the volume.\n" +
                    "1.0 = Normal, <1 = Dimmer, >1 = Brighter.",
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }

            // Lighting
            m_showLighting = EditorGUILayout.Foldout(m_showLighting, "Lighting", true, EditorStyles.foldoutHeader);
            if (m_showLighting)
            {
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(FindProp("_EnableDirectionalLight"), "Directional Light");
                if (material.IsKeywordEnabled("ENABLE_DIRECTIONAL_LIGHT"))
                {
                    materialEditor.ShaderProperty(FindProp("_ShadowSteps"), "Shadow Steps");
                    materialEditor.ShaderProperty(FindProp("_ShadowDensity"), "Shadow Density");
                    materialEditor.ShaderProperty(FindProp("_ShadowThreshold"), "Shadow Threshold");
                    materialEditor.ShaderProperty(FindProp("_PhaseG"), "Phase Anisotropy (HG)");

                    var hdrpLight = FindProp("_EnableHDRPLightData");
                    if (hdrpLight != null)
                    {
                        materialEditor.ShaderProperty(hdrpLight, "Auto HDRP Light");
                        if (!material.IsKeywordEnabled("ENABLE_HDRP_LIGHT_DATA"))
                        {
                            materialEditor.ShaderProperty(FindProp("_MainLightDir"), "Light Direction");
                            materialEditor.ShaderProperty(FindProp("_MainLightColor"), "Light Color");
                        }
                    }
                }
                EditorGUI.indentLevel--;
            }

            // Ambient
            m_showAmbient = EditorGUILayout.Foldout(m_showAmbient, "Ambient", true, EditorStyles.foldoutHeader);
            if (m_showAmbient)
            {
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(FindProp("_EnableAmbientLight"), "Ambient Light");
                if (material.IsKeywordEnabled("ENABLE_AMBIENT_LIGHT"))
                {
                    materialEditor.ShaderProperty(FindProp("_AmbientColor"), "Ambient Color");
                    materialEditor.ShaderProperty(FindProp("_AmbientDensity"), "Ambient Density");
                }
                EditorGUI.indentLevel--;
            }

            // Color Ramp
            m_showColorRamp = EditorGUILayout.Foldout(m_showColorRamp, "Color Ramp", true, EditorStyles.foldoutHeader);
            if (m_showColorRamp)
            {
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(FindProp("_EnableColorRamp"), "Enable");
                if (material.IsKeywordEnabled("ENABLE_COLOR_RAMP"))
                {
                    materialEditor.ShaderProperty(FindProp("_ColorRampIntensity"), "Ramp Intensity");
                    EditorGUILayout.HelpBox(
                        "Color Ramp is set via the OpenVDBVolume component's Gradient field.\n" +
                        "The gradient maps density (0=transparent, 1=dense) to color and opacity.",
                        MessageType.Info);
                }
                EditorGUI.indentLevel--;
            }

            // Spot Lights
            m_showSpotLights = EditorGUILayout.Foldout(m_showSpotLights, "Spot Lights", true, EditorStyles.foldoutHeader);
            if (m_showSpotLights)
            {
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(FindProp("_EnableSpotLights"), "Enable");
                if (material.IsKeywordEnabled("ENABLE_SPOT_LIGHTS"))
                {
                    EditorGUILayout.HelpBox(
                        "Spot lights are configured via the OpenVDBVolume component.\n" +
                        "Assign up to 2 Unity Spot Lights in the component's Spot Lights array.",
                        MessageType.Info);
                }
                EditorGUI.indentLevel--;
            }

            // Shadow Casting
            m_showShadowCasting = EditorGUILayout.Foldout(m_showShadowCasting, "Shadow Casting", true, EditorStyles.foldoutHeader);
            if (m_showShadowCasting)
            {
                EditorGUI.indentLevel++;
                var extraBias = FindProp("_ShadowExtraBias");
                var densThreshold = FindProp("_ShadowDensityThreshold");
                if (extraBias != null)
                    materialEditor.ShaderProperty(extraBias, "Extra Bias");
                if (densThreshold != null)
                    materialEditor.ShaderProperty(densThreshold, "Density Threshold");
                EditorGUILayout.HelpBox(
                    "Shadow casting is toggled via the OpenVDBVolume component.\n" +
                    "When enabled, the volume casts shadows onto other meshes.\n" +
                    "This is GPU-expensive \u2014 use only when needed.",
                    MessageType.Warning);
                EditorGUI.indentLevel--;
            }

            // Adaptive Stepping
            m_showAdaptive = EditorGUILayout.Foldout(m_showAdaptive, "Adaptive Stepping", true, EditorStyles.foldoutHeader);
            if (m_showAdaptive)
            {
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(FindProp("_AdaptiveDistScale"), "Distance Scale");
                materialEditor.ShaderProperty(FindProp("_MinStepDistance"), "Min Step");
                materialEditor.ShaderProperty(FindProp("_MaxStepDistance"), "Max Step");
                EditorGUI.indentLevel--;
            }

            // Features (keyword toggles)
            m_showFeatures = EditorGUILayout.Foldout(m_showFeatures, "Features", true, EditorStyles.foldoutHeader);
            if (m_showFeatures)
            {
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(FindProp("_EnableOccupancySkip"), "Empty Space Skipping");
                materialEditor.ShaderProperty(FindProp("_EnableTemporalJitter"), "Temporal Jitter");
                materialEditor.ShaderProperty(FindProp("_EnableAdaptiveStepping"), "Adaptive Stepping");
                materialEditor.ShaderProperty(FindProp("_EnableHGPhase"), "HG Phase Function");
                materialEditor.ShaderProperty(FindProp("_EnableMultiScatter"), "Multi-Scatter Approx");
                EditorGUI.indentLevel--;
            }

            // Depth Options
            m_showDepth = EditorGUILayout.Foldout(m_showDepth, "Depth Options", true, EditorStyles.foldoutHeader);
            if (m_showDepth)
            {
                EditorGUI.indentLevel++;
                var depthWrite = FindProp("_EnableDepthWrite");
                var sceneClip = FindProp("_EnableSceneDepthClip");
                if (depthWrite != null)
                    materialEditor.ShaderProperty(depthWrite, "Write Depth");
                if (sceneClip != null)
                    materialEditor.ShaderProperty(sceneClip, "Scene Depth Clip");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            materialEditor.ShaderProperty(FindProp("_Cull"), "Culling");

            EditorGUILayout.Space();
            materialEditor.RenderQueueField();
        }
    }
}
#endif

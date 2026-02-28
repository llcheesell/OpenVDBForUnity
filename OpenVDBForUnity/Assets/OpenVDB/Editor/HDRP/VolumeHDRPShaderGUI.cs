using UnityEditor;
using UnityEngine;

namespace OpenVDB.Editor
{
    public class VolumeHDRPShaderGUI : ShaderGUI
    {
        bool m_showQuality = true;
        bool m_showLighting = true;
        bool m_showAmbient = true;
        bool m_showLightInfluence = true;
        bool m_showColorRamp = false;
        bool m_showSpotLights = false;
        bool m_showShadowCasting = false;
        bool m_showDepth = false;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            var material = materialEditor.target as Material;
            if (material == null) return;

            EditorGUILayout.LabelField("OpenVDB HDRP Volume", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Volume texture
            var volumeProp = FindProperty("_Volume", properties);
            materialEditor.TexturePropertySingleLine(new GUIContent("Volume Texture"), volumeProp);

            EditorGUILayout.Space();

            // Quality settings
            m_showQuality = EditorGUILayout.Foldout(m_showQuality, "Quality", true);
            if (m_showQuality)
            {
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(FindProperty("_Intensity", properties), "Intensity");
                materialEditor.ShaderProperty(FindProperty("_StepDistance", properties), "Step Distance");

                EditorGUILayout.HelpBox(
                    "Lower Step Distance = Higher quality but more GPU cost.\n" +
                    "0.01 = High quality, 0.03 = Medium, 0.05 = Fast",
                    MessageType.Info);

                materialEditor.ShaderProperty(FindProperty("_Cull", properties), "Culling");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // Light Influence
            m_showLightInfluence = EditorGUILayout.Foldout(m_showLightInfluence, "Light Influence", true);
            if (m_showLightInfluence)
            {
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(FindProperty("_LightInfluence", properties), "Light Influence");
                materialEditor.ShaderProperty(FindProperty("_AmbientInfluence", properties), "Ambient Influence");
                EditorGUILayout.HelpBox(
                    "Controls how much scene lights affect the volume.\n" +
                    "1.0 = Normal, <1 = Dimmer, >1 = Brighter.\n" +
                    "Independent of Intensity (which controls density).",
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // Directional light
            m_showLighting = EditorGUILayout.Foldout(m_showLighting, "Directional Light", true);
            if (m_showLighting)
            {
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(FindProperty("_EnableDirectionalLight", properties), "Enable");
                if (material.IsKeywordEnabled("ENABLE_DIRECTIONAL_LIGHT"))
                {
                    materialEditor.ShaderProperty(FindProperty("_ShadowSteps", properties), "Shadow Steps");
                    materialEditor.ShaderProperty(FindProperty("_ShadowDensity", properties), "Shadow Density");
                    materialEditor.ShaderProperty(FindProperty("_ShadowThreshold", properties), "Shadow Threshold");
                    materialEditor.ShaderProperty(FindProperty("_EnableHDRPLightData", properties), "Auto HDRP Light");
                    if (!material.IsKeywordEnabled("ENABLE_HDRP_LIGHT_DATA"))
                    {
                        materialEditor.ShaderProperty(FindProperty("_MainLightDir", properties), "Light Direction");
                        materialEditor.ShaderProperty(FindProperty("_MainLightColor", properties), "Light Color");
                    }
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // Ambient light
            m_showAmbient = EditorGUILayout.Foldout(m_showAmbient, "Ambient Light", true);
            if (m_showAmbient)
            {
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(FindProperty("_EnableAmbientLight", properties), "Enable");
                if (material.IsKeywordEnabled("ENABLE_AMBIENT_LIGHT"))
                {
                    materialEditor.ShaderProperty(FindProperty("_AmbientColor", properties), "Color");
                    materialEditor.ShaderProperty(FindProperty("_AmbientDensity", properties), "Density");
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // Color Ramp
            m_showColorRamp = EditorGUILayout.Foldout(m_showColorRamp, "Color Ramp", true);
            if (m_showColorRamp)
            {
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(FindProperty("_EnableColorRamp", properties), "Enable");
                if (material.IsKeywordEnabled("ENABLE_COLOR_RAMP"))
                {
                    materialEditor.ShaderProperty(FindProperty("_ColorRampIntensity", properties), "Ramp Intensity");
                    EditorGUILayout.HelpBox(
                        "Color Ramp is set via the OpenVDBVolume component's Gradient field.\n" +
                        "The gradient maps density (0=transparent, 1=dense) to color and opacity.",
                        MessageType.Info);
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // Spot Lights
            m_showSpotLights = EditorGUILayout.Foldout(m_showSpotLights, "Spot Lights", true);
            if (m_showSpotLights)
            {
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(FindProperty("_EnableSpotLights", properties), "Enable");
                if (material.IsKeywordEnabled("ENABLE_SPOT_LIGHTS"))
                {
                    EditorGUILayout.HelpBox(
                        "Spot lights are configured via the OpenVDBVolume component.\n" +
                        "Assign up to 2 Unity Spot Lights in the component's Spot Lights array.",
                        MessageType.Info);
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // Shadow Casting
            m_showShadowCasting = EditorGUILayout.Foldout(m_showShadowCasting, "Shadow Casting", true);
            if (m_showShadowCasting)
            {
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(FindProperty("_ShadowExtraBias", properties), "Extra Bias");
                materialEditor.ShaderProperty(FindProperty("_ShadowDensityThreshold", properties), "Density Threshold");
                EditorGUILayout.HelpBox(
                    "Shadow casting is toggled via the OpenVDBVolume component.\n" +
                    "When enabled, the volume casts shadows onto other meshes.\n" +
                    "This is GPU-expensive — use only when needed.",
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // Depth options
            m_showDepth = EditorGUILayout.Foldout(m_showDepth, "Depth Options", true);
            if (m_showDepth)
            {
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(FindProperty("_EnableDepthWrite", properties), "Write Depth");
                materialEditor.ShaderProperty(FindProperty("_EnableSceneDepthClip", properties), "Clip Against Scene Depth");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            materialEditor.RenderQueueField();
        }
    }
}

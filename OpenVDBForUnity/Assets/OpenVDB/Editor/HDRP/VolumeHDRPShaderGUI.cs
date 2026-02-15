using UnityEditor;
using UnityEngine;

namespace OpenVDB.Editor
{
    public class VolumeHDRPShaderGUI : ShaderGUI
    {
        bool m_showQuality = true;
        bool m_showLighting = true;
        bool m_showAmbient = true;

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
            materialEditor.RenderQueueField();
        }
    }
}

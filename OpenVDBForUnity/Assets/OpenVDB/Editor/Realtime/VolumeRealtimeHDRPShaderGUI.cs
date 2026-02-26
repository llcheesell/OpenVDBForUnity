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
        bool m_showAdaptive = true;
        bool m_showFeatures = true;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            MaterialProperty FindProp(string name) =>
                FindProperty(name, properties, false);

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

            // Lighting
            m_showLighting = EditorGUILayout.Foldout(m_showLighting, "Lighting", true, EditorStyles.foldoutHeader);
            if (m_showLighting)
            {
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(FindProp("_ShadowSteps"), "Shadow Steps");
                materialEditor.ShaderProperty(FindProp("_ShadowDensity"), "Shadow Density");
                materialEditor.ShaderProperty(FindProp("_ShadowThreshold"), "Shadow Threshold");
                materialEditor.ShaderProperty(FindProp("_PhaseG"), "Phase Anisotropy");
                materialEditor.ShaderProperty(FindProp("_MainLightDir"), "Light Direction");
                materialEditor.ShaderProperty(FindProp("_MainLightColor"), "Light Color");
                EditorGUI.indentLevel--;
            }

            // Ambient
            m_showAmbient = EditorGUILayout.Foldout(m_showAmbient, "Ambient", true, EditorStyles.foldoutHeader);
            if (m_showAmbient)
            {
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(FindProp("_AmbientColor"), "Ambient Color");
                materialEditor.ShaderProperty(FindProp("_AmbientDensity"), "Ambient Density");
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

            // Features
            m_showFeatures = EditorGUILayout.Foldout(m_showFeatures, "Features", true, EditorStyles.foldoutHeader);
            if (m_showFeatures)
            {
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(FindProp("_EnableOccupancySkip"), "Empty Space Skipping");
                materialEditor.ShaderProperty(FindProp("_EnableTemporalJitter"), "Temporal Jitter");
                materialEditor.ShaderProperty(FindProp("_EnableAdaptiveStepping"), "Adaptive Stepping");
                materialEditor.ShaderProperty(FindProp("_EnableHGPhase"), "HG Phase Function");
                materialEditor.ShaderProperty(FindProp("_EnableMultiScatter"), "Multi-Scatter Approx");
                materialEditor.ShaderProperty(FindProp("_EnableDirectionalLight"), "Directional Light");
                materialEditor.ShaderProperty(FindProp("_EnableAmbientLight"), "Ambient Light");
                materialEditor.ShaderProperty(FindProp("_EnableHDRPLightData"), "Auto HDRP Light");
                materialEditor.ShaderProperty(FindProp("_EnableDepthWrite"), "Write Depth");
                materialEditor.ShaderProperty(FindProp("_EnableSceneDepthClip"), "Scene Depth Clip");
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

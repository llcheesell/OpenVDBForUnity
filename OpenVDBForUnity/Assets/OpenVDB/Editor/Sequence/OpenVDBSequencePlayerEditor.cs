using UnityEditor;
using UnityEngine;

namespace OpenVDB.Editor
{
    [CustomEditor(typeof(OpenVDBSequencePlayer))]
    public class OpenVDBSequencePlayerEditor : UnityEditor.Editor
    {
        SerializedProperty m_fileSource;
        SerializedProperty m_directory;
        SerializedProperty m_filePattern;
        SerializedProperty m_filePaths;
        SerializedProperty m_frameRate;
        SerializedProperty m_playbackMode;
        SerializedProperty m_playOnAwake;
        SerializedProperty m_cacheSize;
        SerializedProperty m_preloadCount;
        SerializedProperty m_textureMaxSize;
        SerializedProperty m_scaleFactor;
        SerializedProperty m_useFixedDensityRange;
        SerializedProperty m_fixedMinDensity;
        SerializedProperty m_fixedMaxDensity;

        void OnEnable()
        {
            m_fileSource = serializedObject.FindProperty("m_fileSource");
            m_directory = serializedObject.FindProperty("m_directory");
            m_filePattern = serializedObject.FindProperty("m_filePattern");
            m_filePaths = serializedObject.FindProperty("m_filePaths");
            m_frameRate = serializedObject.FindProperty("m_frameRate");
            m_playbackMode = serializedObject.FindProperty("m_playbackMode");
            m_playOnAwake = serializedObject.FindProperty("m_playOnAwake");
            m_cacheSize = serializedObject.FindProperty("m_cacheSize");
            m_preloadCount = serializedObject.FindProperty("m_preloadCount");
            m_textureMaxSize = serializedObject.FindProperty("m_textureMaxSize");
            m_scaleFactor = serializedObject.FindProperty("m_scaleFactor");
            m_useFixedDensityRange = serializedObject.FindProperty("m_useFixedDensityRange");
            m_fixedMinDensity = serializedObject.FindProperty("m_fixedMinDensity");
            m_fixedMaxDensity = serializedObject.FindProperty("m_fixedMaxDensity");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var player = target as OpenVDBSequencePlayer;

            EditorGUILayout.LabelField("OpenVDB Sequence Player", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // File source
            EditorGUILayout.LabelField("File Source", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_fileSource);

            var source = (OpenVDBSequencePlayer.FileSource)m_fileSource.enumValueIndex;
            if (source == OpenVDBSequencePlayer.FileSource.Directory)
            {
                EditorGUILayout.PropertyField(m_directory, new GUIContent("Directory (StreamingAssets)"));
                EditorGUILayout.PropertyField(m_filePattern);
                EditorGUILayout.HelpBox(
                    "Files will be sorted alphabetically.\n" +
                    "Use naming like: smoke_001.vdb, smoke_002.vdb, ...",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.PropertyField(m_filePaths, new GUIContent("File Paths (StreamingAssets)"));
            }

            EditorGUILayout.Space();

            // Playback
            EditorGUILayout.LabelField("Playback", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_frameRate);
            EditorGUILayout.PropertyField(m_playbackMode);
            EditorGUILayout.PropertyField(m_playOnAwake);

            EditorGUILayout.Space();

            // Performance
            EditorGUILayout.LabelField("Performance", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_cacheSize, new GUIContent("Cache Size (frames)"));
            EditorGUILayout.PropertyField(m_preloadCount, new GUIContent("Preload Ahead"));
            EditorGUILayout.PropertyField(m_textureMaxSize, new GUIContent("Texture Max Size"));
            EditorGUILayout.PropertyField(m_scaleFactor);

            var cacheMemoryMB = EstimateCacheMemoryMB(m_cacheSize.intValue, m_textureMaxSize.intValue);
            EditorGUILayout.HelpBox(
                $"Estimated max cache memory: ~{cacheMemoryMB:F0} MB\n" +
                $"({m_cacheSize.intValue} frames x {m_textureMaxSize.intValue}^3 x 16 bytes)",
                MessageType.Info);

            EditorGUILayout.Space();

            // Density Normalization
            EditorGUILayout.LabelField("Density Normalization", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_useFixedDensityRange, new GUIContent("Use Fixed Range"));
            if (m_useFixedDensityRange.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_fixedMinDensity, new GUIContent("Min Density"));
                EditorGUILayout.PropertyField(m_fixedMaxDensity, new GUIContent("Max Density"));
                EditorGUI.indentLevel--;

                if (player != null && GUILayout.Button("Auto-Detect Range (Scan All Frames)"))
                {
                    serializedObject.ApplyModifiedProperties();
                    player.ScanGlobalDensityRange();
                    serializedObject.Update();
                }

                EditorGUILayout.HelpBox(
                    "Fixed range ensures consistent brightness across all frames.\n" +
                    "Use 'Auto-Detect' to scan all frames and find the global min/max.",
                    MessageType.Info);
            }

            EditorGUILayout.Space();

            // Preview & Runtime controls
            if (player != null)
            {
                EditorGUILayout.LabelField("Preview / Runtime", EditorStyles.boldLabel);

                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.IntField("Total Frames", player.frameCount);
                EditorGUILayout.IntField("Current Frame", player.currentFrame);
                if (Application.isPlaying)
                    EditorGUILayout.Toggle("Playing", player.isPlaying);
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.Space();

                if (Application.isPlaying)
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button(player.isPlaying ? "Pause" : "Play"))
                    {
                        if (player.isPlaying)
                            player.Pause();
                        else
                            player.Play();
                    }
                    if (GUILayout.Button("Stop"))
                    {
                        player.Stop();
                    }
                    EditorGUILayout.EndHorizontal();
                }

                // Frame scrubber (works in both Edit and Play mode)
                if (player.frameCount > 0)
                {
                    var newFrame = EditorGUILayout.IntSlider("Scrub Frame", player.currentFrame, 0,
                        Mathf.Max(0, player.frameCount - 1));
                    if (newFrame != player.currentFrame)
                    {
                        player.currentFrame = newFrame;
                    }
                }

                EditorGUILayout.Space();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Reload"))
                {
                    player.RefreshFiles();
                }
                if (GUILayout.Button("Clear Cache"))
                {
                    player.ClearCache();
                }
                EditorGUILayout.EndHorizontal();

                if (player.frameCount == 0)
                {
                    EditorGUILayout.HelpBox(
                        "No VDB files found. Check that:\n" +
                        "- Files exist in StreamingAssets/<directory>\n" +
                        "- File pattern matches (default: *.vdb)\n" +
                        "Click 'Reload' after changing settings.",
                        MessageType.Warning);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        static float EstimateCacheMemoryMB(int cacheSize, int textureSize)
        {
            if (textureSize <= 0) textureSize = 256;
            long bytesPerFrame = (long)textureSize * textureSize * textureSize * 16; // RGBA float
            long totalBytes = bytesPerFrame * cacheSize;
            return totalBytes / (1024f * 1024f);
        }
    }
}

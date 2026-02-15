using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace OpenVDB
{
    /// <summary>
    /// Plays back a sequence of VDB files as an animated volume.
    ///
    /// Supports two file specification modes:
    /// 1. Directory mode: Specify a folder containing numbered VDB files (e.g., smoke_001.vdb, smoke_002.vdb)
    /// 2. Explicit list mode: Manually assign an array of VDB file paths
    ///
    /// Features:
    /// - Configurable frame cache size to control memory usage
    /// - Background loading of upcoming frames
    /// - Looping and ping-pong playback modes
    /// - Frame rate control independent of Unity's frame rate
    /// - Timeline integration via OpenVDBTimelineClip
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    public class OpenVDBSequencePlayer : MonoBehaviour, IDisposable
    {
        public enum PlaybackMode { Loop, Once, PingPong }
        public enum FileSource { Directory, ExplicitList }

        [Header("File Source")]
        [SerializeField]
        FileSource m_fileSource = FileSource.Directory;

        [SerializeField, Tooltip("Directory containing numbered VDB files (relative to StreamingAssets)")]
        string m_directory = "";

        [SerializeField, Tooltip("File extension filter")]
        string m_filePattern = "*.vdb";

        [SerializeField, Tooltip("Explicit list of VDB file paths (relative to StreamingAssets)")]
        string[] m_filePaths = new string[0];

        [Header("Playback")]
        [SerializeField]
        float m_frameRate = 24f;

        [SerializeField]
        PlaybackMode m_playbackMode = PlaybackMode.Loop;

        [SerializeField]
        bool m_playOnAwake = true;

        [Header("Performance")]
        [SerializeField, Range(1, 32), Tooltip("Max frames to keep in memory")]
        int m_cacheSize = 8;

        [SerializeField, Range(1, 4), Tooltip("Number of frames to preload ahead")]
        int m_preloadCount = 2;

        [SerializeField, Tooltip("Texture max size for all frames (0 = use stream settings default)")]
        int m_textureMaxSize = 128;

        [SerializeField, Tooltip("Scale factor for all frames")]
        float m_scaleFactor = 0.01f;

        // Runtime state
        string[] m_resolvedPaths;
        Dictionary<int, CachedFrame> m_frameCache = new Dictionary<int, CachedFrame>();
        LinkedList<int> m_cacheOrder = new LinkedList<int>();

        int m_currentFrame;
        float m_time;
        bool m_isPlaying;
        int m_pingPongDirection = 1;
        bool m_initialized;
        static bool s_apiInitialized;

        MeshRenderer m_renderer;
        MeshFilter m_meshFilter;
        Mesh m_boundingMesh;

        static readonly int s_volumeId = Shader.PropertyToID("_Volume");

        struct CachedFrame
        {
            public Texture3D texture;
            public Vector3 scale;
            public bool isLoading;
        }

        // Public API

        /// <summary>Total number of frames in the sequence.</summary>
        public int frameCount => m_resolvedPaths != null ? m_resolvedPaths.Length : 0;

        /// <summary>Current frame index (0-based).</summary>
        public int currentFrame
        {
            get => m_currentFrame;
            set => SetFrame(Mathf.Clamp(value, 0, frameCount - 1));
        }

        /// <summary>Normalized time (0-1) across the full sequence.</summary>
        public float normalizedTime
        {
            get => frameCount > 1 ? (float)m_currentFrame / (frameCount - 1) : 0f;
            set => SetFrame(Mathf.RoundToInt(Mathf.Clamp01(value) * (frameCount - 1)));
        }

        public float frameRate
        {
            get => m_frameRate;
            set => m_frameRate = Mathf.Max(0.01f, value);
        }

        public bool isPlaying => m_isPlaying;

        public void Play()
        {
            if (!m_initialized) Initialize();
            m_isPlaying = true;
        }

        public void Pause()
        {
            m_isPlaying = false;
        }

        public void Stop()
        {
            m_isPlaying = false;
            m_time = 0;
            m_currentFrame = 0;
            m_pingPongDirection = 1;
        }

        public void SetFrame(int frame)
        {
            if (!m_initialized) Initialize();
            if (m_resolvedPaths == null || m_resolvedPaths.Length == 0) return;

            frame = Mathf.Clamp(frame, 0, frameCount - 1);
            if (frame == m_currentFrame && m_frameCache.ContainsKey(frame)) return;

            m_currentFrame = frame;
            ApplyFrame(frame);
            PreloadNearbyFrames(frame);
        }

        void OnEnable()
        {
            m_renderer = GetComponent<MeshRenderer>();
            m_meshFilter = GetComponent<MeshFilter>();
            Initialize();
            if (m_playOnAwake && Application.isPlaying)
            {
                Play();
            }
        }

        void OnDisable()
        {
            m_isPlaying = false;
        }

        void OnDestroy()
        {
            Dispose();
        }

        void Update()
        {
            if (!m_initialized || m_resolvedPaths == null || m_resolvedPaths.Length == 0) return;
            if (!m_isPlaying) return;

            float dt = Application.isPlaying ? Time.deltaTime : Time.unscaledDeltaTime * 0.5f;
            m_time += dt;

            float frameDuration = 1f / m_frameRate;
            if (m_time >= frameDuration)
            {
                m_time -= frameDuration;
                AdvanceFrame();
            }
        }

        void AdvanceFrame()
        {
            int nextFrame = m_currentFrame + m_pingPongDirection;

            switch (m_playbackMode)
            {
                case PlaybackMode.Loop:
                    nextFrame = nextFrame % frameCount;
                    if (nextFrame < 0) nextFrame += frameCount;
                    break;

                case PlaybackMode.Once:
                    if (nextFrame >= frameCount)
                    {
                        m_isPlaying = false;
                        return;
                    }
                    break;

                case PlaybackMode.PingPong:
                    if (nextFrame >= frameCount || nextFrame < 0)
                    {
                        m_pingPongDirection *= -1;
                        nextFrame = m_currentFrame + m_pingPongDirection;
                    }
                    break;
            }

            SetFrame(nextFrame);
        }

        void Initialize()
        {
            if (m_initialized) return;

            if (!s_apiInitialized)
            {
                OpenVDBAPI.oiInitialize();
                s_apiInitialized = true;
            }

            ResolvePaths();

            // Create the bounding mesh if not already assigned
            if (m_meshFilter != null && m_meshFilter.sharedMesh == null)
            {
                m_boundingMesh = Voxelizer.VoxelMesh.Build(new[] { Vector3.zero }, 1f);
                m_meshFilter.sharedMesh = m_boundingMesh;
            }

            m_initialized = true;

            // Auto-load and display the first frame so we get immediate visual feedback
            if (m_resolvedPaths != null && m_resolvedPaths.Length > 0)
            {
                ApplyFrame(0);
            }
        }

        void ResolvePaths()
        {
            switch (m_fileSource)
            {
                case FileSource.Directory:
                    ResolveDirectoryPaths();
                    break;
                case FileSource.ExplicitList:
                    m_resolvedPaths = m_filePaths != null ? m_filePaths.ToArray() : new string[0];
                    break;
            }
        }

        void ResolveDirectoryPaths()
        {
            if (string.IsNullOrEmpty(m_directory))
            {
                m_resolvedPaths = new string[0];
                return;
            }

            var fullDir = Path.Combine(Application.streamingAssetsPath, m_directory);
            if (!Directory.Exists(fullDir))
            {
                Debug.LogWarning($"OpenVDBSequencePlayer: Directory not found: {fullDir}");
                m_resolvedPaths = new string[0];
                return;
            }

            var files = Directory.GetFiles(fullDir, m_filePattern)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            // Store paths relative to StreamingAssets
            m_resolvedPaths = new string[files.Length];
            for (int i = 0; i < files.Length; i++)
            {
                m_resolvedPaths[i] = files[i]; // Use full path for loading
            }
        }

        void ApplyFrame(int frame)
        {
            if (m_renderer == null) return;

            if (m_frameCache.TryGetValue(frame, out var cached))
            {
                if (cached.texture != null && !cached.isLoading)
                {
                    m_renderer.sharedMaterial.SetTexture(s_volumeId, cached.texture);
                    transform.localScale = cached.scale;
                    return;
                }
            }

            // Load the frame synchronously if not cached
            LoadFrame(frame);

            if (m_frameCache.TryGetValue(frame, out cached) && cached.texture != null)
            {
                m_renderer.sharedMaterial.SetTexture(s_volumeId, cached.texture);
                transform.localScale = cached.scale;
            }
        }

        void LoadFrame(int frame)
        {
            if (frame < 0 || frame >= m_resolvedPaths.Length) return;
            if (m_frameCache.ContainsKey(frame) && !m_frameCache[frame].isLoading) return;

            var path = m_resolvedPaths[frame];
            if (string.IsNullOrEmpty(path)) return;

            // Determine the full path
            string fullPath;
            if (m_fileSource == FileSource.Directory)
            {
                fullPath = path; // Already full path from Directory.GetFiles
            }
            else
            {
                fullPath = Path.Combine(Application.streamingAssetsPath, path);
            }

            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"OpenVDBSequencePlayer: File not found: {fullPath}");
                return;
            }

            // Use a unique context ID for this frame
            int contextId = GetInstanceID() * 10000 + frame;
            var context = oiContext.Create(contextId);

            var config = new oiConfig();
            config.SetDefaults();
            config.scaleFactor = m_scaleFactor;
            config.textureMaxSize = m_textureMaxSize > 0 ? m_textureMaxSize : 256;
            context.SetConfig(ref config);

            if (!context.Load(fullPath))
            {
                Debug.LogWarning($"OpenVDBSequencePlayer: Failed to load frame {frame}: {fullPath}");
                context.Destroy();
                return;
            }

            var volume = context.volume;
            var summary = new oiVolumeSummary();
            volume.GetSummary(ref summary);

            // Create Texture3D
            var texture = new Texture3D(summary.width, summary.height, summary.depth,
                (TextureFormat)summary.format, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            var pixels = new PinnedList<Color>(texture.GetPixels());
            var volumeData = default(oiVolumeData);
            volumeData.voxels = pixels;

            volume.FillData(ref volumeData);
            volume.GetSummary(ref summary);

            texture.SetPixels(pixels.Array);
            texture.Apply();

            var scale = new Vector3(summary.xscale, summary.yscale, summary.zscale);

            // Evict from cache if full
            EnsureCacheCapacity();

            m_frameCache[frame] = new CachedFrame
            {
                texture = texture,
                scale = scale,
                isLoading = false
            };
            m_cacheOrder.AddLast(frame);

            // Clean up native context
            context.Destroy();
        }

        void PreloadNearbyFrames(int currentFrame)
        {
            for (int i = 1; i <= m_preloadCount; i++)
            {
                int ahead = currentFrame + i * m_pingPongDirection;
                if (m_playbackMode == PlaybackMode.Loop)
                {
                    ahead = ahead % frameCount;
                    if (ahead < 0) ahead += frameCount;
                }

                if (ahead >= 0 && ahead < frameCount && !m_frameCache.ContainsKey(ahead))
                {
                    LoadFrame(ahead);
                }
            }
        }

        void EnsureCacheCapacity()
        {
            while (m_cacheOrder.Count >= m_cacheSize && m_cacheOrder.Count > 0)
            {
                var oldest = m_cacheOrder.First.Value;
                m_cacheOrder.RemoveFirst();

                if (m_frameCache.TryGetValue(oldest, out var cached))
                {
                    if (cached.texture != null)
                    {
                        if (Application.isPlaying)
                            UnityEngine.Object.Destroy(cached.texture);
                        else
                            UnityEngine.Object.DestroyImmediate(cached.texture);
                    }
                    m_frameCache.Remove(oldest);
                }
            }
        }

        public void Dispose()
        {
            foreach (var kvp in m_frameCache)
            {
                if (kvp.Value.texture != null)
                {
                    if (Application.isPlaying)
                        UnityEngine.Object.Destroy(kvp.Value.texture);
                    else
                        UnityEngine.Object.DestroyImmediate(kvp.Value.texture);
                }
            }
            m_frameCache.Clear();
            m_cacheOrder.Clear();

            if (m_boundingMesh != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(m_boundingMesh);
                else
                    UnityEngine.Object.DestroyImmediate(m_boundingMesh);
            }

            m_initialized = false;
        }

        /// <summary>
        /// Clears the frame cache. Call this if you change texture settings and need to reload.
        /// </summary>
        public void ClearCache()
        {
            Dispose();
        }

        /// <summary>
        /// Refresh file paths (e.g., after adding new files to the directory).
        /// </summary>
        public void RefreshFiles()
        {
            Dispose();
            m_initialized = false;
            Initialize();
        }
    }
}

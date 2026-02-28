using UnityEngine;

namespace OpenVDB.Realtime
{
    /// <summary>
    /// Plays back a sequence of VDB volume textures in real-time.
    /// Manages loading, caching, and transitioning between frames
    /// with occupancy grid updates.
    /// </summary>
    [RequireComponent(typeof(OpenVDBVolume))]
    public class OpenVDBRealtimeSequencePlayer : MonoBehaviour
    {
        [Header("Sequence")]
        [SerializeField]
        Texture3D[] m_volumeFrames;

        [SerializeField]
        float m_framesPerSecond = 24f;

        [SerializeField]
        bool m_loop = true;

        [SerializeField]
        bool m_playOnStart = true;

        [Header("Frame Cache")]
        [SerializeField, Range(1, 16)]
        int m_preloadFrameCount = 4;

        // Runtime state
        OpenVDBVolume m_volume;
        float m_time;
        int m_currentFrame = -1;
        bool m_isPlaying;

        void Awake()
        {
            m_volume = GetComponent<OpenVDBVolume>();
        }

        void Start()
        {
            if (m_playOnStart)
                Play();
        }

        void Update()
        {
            if (!m_isPlaying || m_volumeFrames == null || m_volumeFrames.Length == 0)
                return;

            m_time += Time.deltaTime;

            float frameDuration = 1f / Mathf.Max(m_framesPerSecond, 0.001f);
            int targetFrame = Mathf.FloorToInt(m_time / frameDuration);

            if (m_loop)
            {
                targetFrame = targetFrame % m_volumeFrames.Length;
            }
            else
            {
                targetFrame = Mathf.Min(targetFrame, m_volumeFrames.Length - 1);
                if (targetFrame >= m_volumeFrames.Length - 1 && m_time >= m_volumeFrames.Length * frameDuration)
                {
                    m_isPlaying = false;
                }
            }

            if (targetFrame != m_currentFrame)
            {
                SetFrame(targetFrame);
            }
        }

        public void SetFrame(int frame)
        {
            if (m_volumeFrames == null || frame < 0 || frame >= m_volumeFrames.Length)
                return;

            m_currentFrame = frame;
            var texture = m_volumeFrames[frame];

            if (texture != null && m_volume != null)
            {
                m_volume.volumeTexture = texture;
            }
        }

        public void Play()
        {
            m_isPlaying = true;
            if (m_currentFrame < 0)
                SetFrame(0);
        }

        public void Pause()
        {
            m_isPlaying = false;
        }

        public void Stop()
        {
            m_isPlaying = false;
            m_time = 0f;
            m_currentFrame = -1;
        }

        public void SetFrames(Texture3D[] frames)
        {
            m_volumeFrames = frames;
            Stop();
        }

        public bool isPlaying => m_isPlaying;
        public int currentFrame => m_currentFrame;
        public int frameCount => m_volumeFrames != null ? m_volumeFrames.Length : 0;

        public float framesPerSecond
        {
            get => m_framesPerSecond;
            set => m_framesPerSecond = Mathf.Max(0.001f, value);
        }
    }
}

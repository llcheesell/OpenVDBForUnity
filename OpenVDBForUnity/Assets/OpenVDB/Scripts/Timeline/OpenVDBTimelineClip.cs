using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace OpenVDB
{
    /// <summary>
    /// Timeline clip asset for OpenVDB sequence playback.
    /// Place on an OpenVDBTimelineTrack to control VDB animation via Timeline.
    ///
    /// The clip maps its duration to the full frame range of the sequence.
    /// Scrubbing and looping work automatically.
    /// </summary>
    [Serializable]
    public class OpenVDBTimelineClip : PlayableAsset, ITimelineClipAsset
    {
        [SerializeField, Tooltip("Override frame rate (0 = use player's frame rate)")]
        float m_frameRateOverride;

        [SerializeField, Tooltip("Frame offset within the sequence")]
        int m_frameOffset;

        public ClipCaps clipCaps => ClipCaps.Looping | ClipCaps.Extrapolation | ClipCaps.ClipIn;

        public float frameRateOverride
        {
            get => m_frameRateOverride;
            set => m_frameRateOverride = value;
        }

        public int frameOffset
        {
            get => m_frameOffset;
            set => m_frameOffset = value;
        }

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<OpenVDBTimelineBehaviour>.Create(graph);
            return playable;
        }
    }
}

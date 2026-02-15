using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace OpenVDB
{
    /// <summary>
    /// Timeline track for OpenVDB sequence playback.
    ///
    /// Usage:
    /// 1. Add an "OpenVDB Sequence Track" to your Timeline
    /// 2. Bind an OpenVDBSequencePlayer component to the track
    /// 3. Add OpenVDB Sequence clips to control frame playback
    /// 4. The clip duration maps to the full sequence range
    ///
    /// Scrubbing in the Timeline editor will update the VDB frame in real time.
    /// </summary>
    [TrackColor(0.4f, 0.6f, 1.0f)]
    [TrackClipType(typeof(OpenVDBTimelineClip))]
    [TrackBindingType(typeof(OpenVDBSequencePlayer))]
    public class OpenVDBTimelineTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<OpenVDBTimelineMixer>.Create(graph, inputCount);
        }
    }
}

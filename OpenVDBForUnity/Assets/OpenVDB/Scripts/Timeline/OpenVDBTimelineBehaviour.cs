using System;
using UnityEngine;
using UnityEngine.Playables;

namespace OpenVDB
{
    /// <summary>
    /// PlayableBehaviour that controls an OpenVDBSequencePlayer during Timeline playback.
    /// Sets the current frame based on the clip's normalized time.
    /// </summary>
    [Serializable]
    public class OpenVDBTimelineBehaviour : PlayableBehaviour
    {
        [NonSerialized]
        public OpenVDBSequencePlayer sequencePlayer;
    }
}

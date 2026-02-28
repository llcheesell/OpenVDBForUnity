using System;
using UnityEngine.Playables;

namespace OpenVDB
{
    [Serializable]
    public class OpenVDBTimelineBehaviour : PlayableBehaviour
    {
        public float frameRateOverride;
        public int frameOffset;
    }
}

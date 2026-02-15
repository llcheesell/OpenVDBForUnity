using UnityEngine;
using UnityEngine.Playables;

namespace OpenVDB
{
    /// <summary>
    /// Mixer for OpenVDB Timeline track.
    /// Handles mapping the Timeline playback position to VDB sequence frames.
    /// </summary>
    public class OpenVDBTimelineMixer : PlayableBehaviour
    {
        OpenVDBSequencePlayer m_sequencePlayer;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            m_sequencePlayer = playerData as OpenVDBSequencePlayer;
            if (m_sequencePlayer == null || m_sequencePlayer.frameCount == 0) return;

            int inputCount = playable.GetInputCount();
            for (int i = 0; i < inputCount; i++)
            {
                float weight = playable.GetInputWeight(i);
                if (weight <= 0f) continue;

                var inputPlayable = (ScriptPlayable<OpenVDBTimelineBehaviour>)playable.GetInput(i);
                var behaviour = inputPlayable.GetBehaviour();

                // Get normalized time within this clip (0-1)
                double clipDuration = inputPlayable.GetDuration();
                double clipTime = inputPlayable.GetTime();

                if (clipDuration <= 0) continue;

                float normalizedTime = (float)(clipTime / clipDuration);

                // Determine frame offset from the clip asset
                int frameOffset = 0;
                // We pass the frame offset through the behaviour if needed

                int totalFrames = m_sequencePlayer.frameCount;
                int frame = Mathf.FloorToInt(normalizedTime * totalFrames) + frameOffset;
                frame = Mathf.Clamp(frame, 0, totalFrames - 1);

                m_sequencePlayer.currentFrame = frame;
                break; // Use the first active clip
            }
        }
    }
}

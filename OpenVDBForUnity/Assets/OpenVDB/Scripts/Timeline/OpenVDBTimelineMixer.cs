using UnityEngine;
using UnityEngine.Playables;

namespace OpenVDB
{
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

                double clipDuration = inputPlayable.GetDuration();
                double clipTime = inputPlayable.GetTime();

                if (clipDuration <= 0) continue;

                float normalizedTime = (float)(clipTime / clipDuration);

                if (behaviour.frameRateOverride > 0f)
                {
                    m_sequencePlayer.frameRate = behaviour.frameRateOverride;
                }

                int totalFrames = m_sequencePlayer.frameCount;
                int frame = Mathf.FloorToInt(normalizedTime * totalFrames) + behaviour.frameOffset;
                frame = Mathf.Clamp(frame, 0, totalFrames - 1);

                m_sequencePlayer.currentFrame = frame;
                break;
            }
        }
    }
}

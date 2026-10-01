using UnityEngine;

namespace MakeupSniper
{
    public enum SfxKind { Kiss, Pop, Miss, Tada, Tick }

    /// <summary>Звуки, сгенерированные кодом (без аудиофайлов): «чмок», хлопок базуки, промах, фанфары.</summary>
    public class Sfx : MonoBehaviour
    {
        AudioSource source;
        AudioClip[] clips;
        const int Rate = 44100;

        void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            clips = new AudioClip[5];
            clips[(int)SfxKind.Kiss] = Sweep("kiss", 0.16f, 1100f, 220f, 0.5f, 0);
            clips[(int)SfxKind.Pop] = Sweep("pop", 0.30f, 300f, 60f, 0.7f, 1);
            clips[(int)SfxKind.Miss] = Sweep("miss", 0.08f, 180f, 160f, 0.25f, 2);
            clips[(int)SfxKind.Tick] = Sweep("tick", 0.10f, 880f, 880f, 0.3f, 0);
            clips[(int)SfxKind.Tada] = Arpeggio("tada", new[] { 523f, 659f, 784f, 1047f }, 0.12f, 0.5f);
        }

        public void Play(SfxKind kind)
        {
            if (source == null || clips == null) return;
            var clip = clips[(int)kind];
            if (clip != null) source.PlayOneShot(clip);
        }

        // wave: 0 синус, 1 треугольник, 2 меандр
        static float Wave(float phase, int wave)
        {
            float p = phase - Mathf.Floor(phase);
            if (wave == 1) return 4f * Mathf.Abs(p - 0.5f) - 1f;
            if (wave == 2) return p < 0.5f ? 1f : -1f;
            return Mathf.Sin(p * Mathf.PI * 2f);
        }

        static AudioClip Sweep(string name, float seconds, float f0, float f1, float volume, int wave)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float f = f0 * Mathf.Pow(f1 / f0, t);
                phase += f / Rate;
                data[i] = Wave(phase, wave) * volume * Mathf.Pow(1f - t, 2f);
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip Arpeggio(string name, float[] notes, float step, float noteLength)
        {
            int n = Mathf.CeilToInt((step * (notes.Length - 1) + noteLength) * Rate);
            var data = new float[n];
            for (int k = 0; k < notes.Length; k++)
            {
                int start = Mathf.RoundToInt(k * step * Rate), len = Mathf.RoundToInt(noteLength * Rate);
                for (int i = 0; i < len && start + i < n; i++)
                {
                    float t = (float)i / len;
                    data[start + i] += Wave(notes[k] * i / Rate, 1) * 0.22f * Mathf.Pow(1f - t, 2f);
                }
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;

namespace OddRhythms
{
    class InstrumentManager
    {
        protected Note[] notes;
        protected Instrument i;
        protected RhythmTracker r;
        
        private bool isSorted = false;

        internal InstrumentManager(Instrument ins, RhythmTracker rh)
        {
            i = ins;
            r = rh;
            notes = new Note[0];
            isSorted = true;
        }

        internal virtual void AddNote(Note n)
        {
            n.time = (int) (r.GetTime(n.measure, n.beat) * Constants.freq);
            n.secDur = (int) (r.GetDuration(n.measure, n.beat, n.duration) * Constants.freq);
            notes = notes.Append(n).ToArray();
            isSorted = false;
        }

        internal void Sort()
        {
            if (!isSorted)
            {
                Array.Sort(notes);
                isSorted = true;
            }
        }

        internal virtual float[] GetStream()
        {
            if (notes.Length > 0)
            {
                Sort();
                float last_time = r.GetTime(notes.Last().measure, notes.Last().beat + notes.Last().duration) * i.sampleFrequency;
     //         Debug.Log("Last Time: " + last_time / i.sampleFrequency + " " + last_time);
                float[] retval = new float[(int)last_time];
     //         Debug.Log("Last Note: " + notes.Last().measure + " " + notes.Last().beat + " " + notes.Last().duration);
                for (int i = 0; i < notes.Length; ++i)
                {
                    //Debug.Log(notes[i].measure + " " + notes[i].beat + " " + notes[i].duration);
                    this.i.InsertNote(ref retval, r.GetTime(notes[i].measure, notes[i].beat), notes[i].pitch,
                        r.GetDuration(notes[i].measure, notes[i].beat, notes[i].duration));
                }
                return (retval);
            }
            else {
                return (new float[0]);
            }
        }

        /*
         *  Fills given array with the stream; returns offset (in samples) to when the notes start sounding, or -1 if no notes.
         *  Will resize array to hold enough space, but will not otherwise change array, besides adding the notes. So garbage
         *  can be left over.
         */
        internal virtual int GetStream(ref float[] retval)
        {
            if (notes.Length > 0)
            {
                Sort();
                float first_time = r.GetTime(notes.First().measure, notes.First().beat) * i.sampleFrequency;
                float offset = first_time / i.sampleFrequency;
                float last_time = r.GetTime(notes.Last().measure, notes.Last().beat + notes.Last().duration) * i.sampleFrequency;
                //         Debug.Log("Last Time: " + last_time / i.sampleFrequency + " " + last_time);
                if (retval.Length < last_time - first_time)
                {
                    retval = retval.Concat(new float[(int)(retval.Length - (last_time - first_time))]).ToArray();
                }
     //         Debug.Log("Last Note: " + notes.Last().measure + " " + notes.Last().beat + " " + notes.Last().duration);
                for (int i = 0; i < notes.Length; ++i)
                {
                    //Debug.Log(notes[i].measure + " " + notes[i].beat + " " + notes[i].duration);
                    this.i.InsertNote(ref retval, r.GetTime(notes[i].measure, notes[i].beat) - offset, notes[i].pitch,
                        r.GetDuration(notes[i].measure, notes[i].beat, notes[i].duration));
                }
                return ((int)first_time);
            }
            else {
                return (-1);
            }
        }

        internal virtual float[] GetStream(int pos, int len)
        {
            float[] retval = new float[len];
            Sort();
            foreach (Note n in notes)
            {
                int start = n.time ;
                int end = start + n.secDur;

                if ((start >= pos && start < pos + len) ||
                    (start < pos && end > pos))
                {
                   this.i.InsertNote(ref retval, start, n.pitch, end - start, pos, len);
                }
            }

            return (retval);
        }

        internal void SetRhythmTracker(RhythmTracker rhy)
        {
            r = rhy;
        }

        internal void SetInstrument(Instrument ins)
        {
            i = ins;
        }

        internal void SetVolume(float v)
        {
            i.SetVolume(v);
        }

        internal virtual void IncrementMeasures(int inc)
        {
            foreach (Note n in notes)
            {
                n.measure += inc;
            }
        }
    }

    class DrumManager : InstrumentManager
    {
        internal DrumManager(Instrument ins, RhythmTracker r) : base(ins, r)
        {
        }

        internal override void AddNote(Note n)
        {
            n.pitch = Notes.D1;
            base.AddNote(n);
        }

        internal override float[] GetStream()
        {
            if (notes.Length > 0)
            {
                Array.Sort(notes);
                DrumInstrument b = i as DrumInstrument;
                float last_time = (r.GetTime(notes.Last().measure, notes.Last().beat) + b.Dropoff) * i.sampleFrequency;
     //         Debug.Log("Last Time: " + last_time / i.sampleFrequency + " " + last_time);
                float[] retval = new float[(int)last_time];
     //         Debug.Log("Last Note: " + notes.Last().measure + " " + notes.Last().beat + " " + b.Dropoff);
                for (int i = 0; i < notes.Length; ++i)
                {
                    this.i.InsertNote(ref retval, r.GetTime(notes[i].measure, notes[i].beat), notes[i].pitch, b.Dropoff);
                }
                return (retval);
            }
            else
            {
                return (new float[0]);
            }
        }

        /*
         *  Fills given array with the stream; returns offset (in samples) to when the notes start sounding, or -1 if no notes.
         *  Will resize array to hold enough space, but will not otherwise change array, besides adding the notes. So garbage
         *  can be left over.
         */
        internal override int GetStream(ref float[] retval)
        {
            if (notes.Length > 0)
            {
                Sort();
                DrumInstrument b = i as DrumInstrument;
                float first_time = r.GetTime(notes.First().measure, notes.First().beat) * i.sampleFrequency;
                float offset = first_time / i.sampleFrequency;
                float last_time = r.GetTime(notes.Last().measure, notes.Last().beat + notes.Last().duration) * i.sampleFrequency;
                //         Debug.Log("Last Time: " + last_time / i.sampleFrequency + " " + last_time);
                if (retval.Length < last_time - first_time)
                {
                    retval = retval.Concat(new float[(int)(retval.Length - (last_time - first_time))]).ToArray();
                }
                //         Debug.Log("Last Note: " + notes.Last().measure + " " + notes.Last().beat + " " + notes.Last().duration);
                for (int i = 0; i < notes.Length; ++i)
                {
                    //Debug.Log(notes[i].measure + " " + notes[i].beat + " " + notes[i].duration);
                    this.i.InsertNote(ref retval, r.GetTime(notes[i].measure, notes[i].beat) - offset, notes[i].pitch,
                        b.Dropoff);
                }
                return ((int)first_time);
            }
            else
            {
                return (-1);
            }
        }

        internal override float[] GetStream(int pos, int len)
        {
            float[] retval = new float[len];
            DrumInstrument b = i as DrumInstrument;
            Sort();
            foreach (Note n in notes)
            {
                int start = n.time;
                int end = start + (int) (b.Dropoff * Constants.freq);

                if ((start >= pos && start < pos + len) ||
                    (start < pos && end > pos))
                {
                    this.i.InsertNote(ref retval, n.time, n.pitch, b.Dropoff * Constants.freq, pos, len);
                }
            }

            return (retval);
        }


    }

    class NullManager : InstrumentManager
    {
        internal NullManager(Instrument ins = null, RhythmTracker r = null) : base(ins, r)
        {
        }

        internal override void AddNote(Note n)
        {
        }

        internal override float[] GetStream()
        {
               return (new float[0]);
        }

        internal override int GetStream(ref float[] retval)
        {
            return (-1);
        }
        internal override float[] GetStream(int pos, int len)
        {
            float[] retval = new float[len];
            return (retval);
        }

    }

    class NullDrumManager : DrumManager
    {
        internal NullDrumManager(Instrument ins = null, RhythmTracker r = null) : base(ins, r)
        {
        }

        internal override void AddNote(Note n)
        {
        }

        internal override float[] GetStream()
        {
            return (new float[0]);
        }

        internal override int GetStream(ref float[] retval)
        {
            return (-1);
        }
        internal override float[] GetStream(int pos, int len)
        {
            float[] retval = new float[len];
            return (retval);
        }


    }


    class DrumManagerManager
    {
        DrumManager bass, snare, highhat;
        List<Note> bassNotes, snareNotes, highHatNotes;

        internal DrumManagerManager()
        {
            bassNotes = new List<Note>();
            snareNotes = new List<Note>();
            highHatNotes = new List<Note>();
        }
        internal DrumManagerManager(DrumManager b, DrumManager s, DrumManager h)
        {
            bass = b;
            snare = s;
            highhat = h;
            bassNotes = new List<Note>();
            snareNotes = new List<Note>();
            highHatNotes = new List<Note>();
        }
        internal void SnareHit(int measure, float beat)
        {
            snareNotes.Add(new Note(measure, beat, Notes.REST, 0));
            if (!(snare is null))
            {
                snare.AddNote(snareNotes.Last());
            }
        }

        internal void BassHit(int measure, float beat)
        {
            bassNotes.Add(new Note(measure, beat, Notes.REST, 0));
            if (!(bass is null))
            {
                bass.AddNote(bassNotes.Last());
            }
        }

        internal void HighHatHit(int measure, float beat)
        {
            highHatNotes.Add(new Note(measure, beat, Notes.REST, 0));
            if (!(highhat is null))
            {
                highhat.AddNote(highHatNotes.Last());
            }
        }

        internal float[] GetStream()
        {
            float[] a, b, c;
//            Debug.Log("Getting Bass");
            a = bass.GetStream();
//            Debug.Log("Bass done; getting snare");
            b = snare.GetStream();
//            Debug.Log("Snare done");
            c = highhat.GetStream();
            return (BasicMixer.Mix(a, b, c));
            
        }

        internal void SetSnare(DrumManager s)
        {
            snare = s;
            if (!(snare is null))
            {
                foreach (Note e in snareNotes)
                {
                    snare.AddNote(e);
                }
            }
        }
        internal void SetBass(DrumManager b)
        {
            bass = b;
            if (!(bass is null))
            {
                foreach (Note e in bassNotes)
                {
                    bass.AddNote(e);
                }
            }
        }

        internal void SetHighHat(DrumManager h)
        {
            highhat = h;
            if (!(highhat is null))
            {
                foreach (Note e in snareNotes)
                {
                    highhat.AddNote(e);
                }
            }
        }

        internal void IncrementMeasures(int inc)
        {
            if (!(bass is null))
            {
                bass.IncrementMeasures(inc);
            }
            if (!(highhat is null))
            {
                highhat.IncrementMeasures(inc);
            }
            if (!(snare is null))
            {
                snare.IncrementMeasures(inc);
            }
            foreach (Note n in bassNotes)
            {
                n.measure += inc;
            }
            foreach (Note n in snareNotes)
            {
                n.measure += inc;
            }
            foreach (Note n in highHatNotes)
            {
                n.measure += inc;
            }
        }
    }

    abstract class Instrument
    {
        internal int sampleFrequency;
        protected float volume;

        internal Instrument(int sf = 48000, float volume = 1.0f)
        {
            this.sampleFrequency = sf;
            this.volume = volume;
        }
        internal abstract float[] GetNote(float freq, float duration); // duration should be in seconds
        internal abstract void InsertNote(ref float[] stream, float time, float freq, float duration); // duration should be in seconds

        internal abstract void InsertNote(ref float[] stream, float time, float freq, float duration, int start, int dur);
        internal float CalcSineWave(float freq, int i)
        {
            return (Mathf.Sin(i * freq * 2.0f * Mathf.PI / sampleFrequency));
        }

        internal float GetVolume()
        {
            return volume;
        }

        internal void SetVolume(float v)
        {
            volume = v;
        }
    }

    class SquareWaveInstrument : Instrument
    {
        internal SquareWaveInstrument(int fre = 48000, float vol = 1.0f) : base(fre, vol)
        {
        }
        internal override float[] GetNote(float freq, float duration)
        {
            int len = (int)(duration * sampleFrequency);
            float[] retVal = new float[len];

            for (int i = 0; i < len; ++i)
            {
                retVal[i] = 0;
                float amp = .5f;
                float totamp = 0;
                for (int j = 1; totamp < .99f; ++j, totamp += amp, amp /= 2)
                {
                    retVal[i] += volume * CalcSineWave(freq * j, i) * amp;
                }
            }
            return (retVal);
        }
        internal override void InsertNote(ref float[] stream, float time, float freq, float duration)
        {
            int len = (int)(duration * sampleFrequency);
            int start = (int)(time * sampleFrequency);
   //       Debug.Log("Size of Stream: " + stream.Length + "\nstart: " + start);
            for (int i = 0; i < len; ++i)
            {
                stream[start + i] = 0;
                float amp = .5f;
                float totamp = 0;
                for (int j = 1; totamp < .999f; ++j, totamp += amp, amp /= 2)
                {
                    stream[start + i] += volume * CalcSineWave(freq * j, i) * amp;
                }
            }
        }
        internal override void InsertNote(ref float[] stream, float time, float freq, float duration, int start, int dur)
        {
            int len = (int)(duration);
            int noteStart = (int)(time);

            int streamPos;
            int notePos;
            if (start > noteStart)
            {
                streamPos = 0;
                notePos = start - noteStart;
            }
            else
            {
                streamPos = noteStart - start;
                notePos = 0;
            }

            for ( ; notePos < len && streamPos < dur; ++notePos, ++streamPos)
            {
                stream[streamPos] = 0;
                float amp = .5f;
                float totamp = 0;
                for (int j = 1; totamp < .999f; ++j, totamp += amp, amp /= 2)
                {
                    stream[streamPos] += volume * CalcSineWave(freq * j, notePos) * amp;
                }
            }
        }


    }

    class SineWaveInstrument : Instrument
    {
        internal SineWaveInstrument(int fre = 48000, float vol = 1.0f) : base(fre, vol)
        {
        }
        internal override float[] GetNote(float freq, float duration)
        {
            int len = (int)(duration * sampleFrequency);
            float[] retVal = new float[len];

            for (int i = 0; i < len; ++i)
            {
                retVal[i] = volume * CalcSineWave(freq, i) * .25f;
            }
            return (retVal);
        }
        internal override void InsertNote(ref float[] stream, float time, float freq, float duration)
        {
            int len = (int)(duration * sampleFrequency);
            int start = (int)(time * sampleFrequency);
   //       Debug.Log("Size of Stream: " + stream.Length + "\nstart: " + start);
            for (int i = 0; i < len && start+i < stream.Length; ++i)
            {
                stream[start+i] += volume * CalcSineWave(freq, i) * .25f;
            }
        }
        internal override void InsertNote(ref float[] stream, float time, float freq, float duration, int start, int dur)
        {
            int len = (int)(duration);
            int noteStart = (int)(time);

            int streamPos;
            int notePos;
            if (start > noteStart)
            {
                streamPos = 0;
                notePos = start - noteStart;
            }
            else
            {
                streamPos = noteStart - start;
                notePos = 0;
            }

            for (; notePos < len && streamPos < dur && streamPos < stream.Length; ++notePos, ++streamPos)
            {
                stream[streamPos] += volume * CalcSineWave(freq, notePos) * .25f;
            }
      
        }
    }
    abstract class DrumInstrument : Instrument
    {
        protected float dropoff;
        public float Dropoff { get => dropoff; protected internal set => dropoff = value; }

        internal DrumInstrument(int fre = 48000, float vol = 1.0f) : base(fre, vol)
        {
        }

    }

    class SnareDrumInstrument : DrumInstrument
    {
        private float triangFreq, sineFreq, ampTriang, ampSine, lenTriang, lenSine, lenNoise, noiseFreq, noiseFMFreq, ampNoise;

        internal SnareDrumInstrument(int fre = 48000, float vol = 1.0f) : base(fre, vol)
        {
            Dropoff = .2f;
            triangFreq = 111f;
            sineFreq = 255f;
            ampTriang = .2f;
            ampSine = .2f;
            lenTriang = .2f;
            lenSine = .2f;
            lenNoise = .17f;
            noiseFreq = 440f;
            noiseFMFreq = 500000f;
            ampNoise = 500000f;
        }
        internal override float[] GetNote(float freq, float duration)
        {
            int len = (int)(Dropoff * sampleFrequency);
            float[] retVal = new float[len];

            for (int i = 0; i < len; ++i)
            {
                retVal[i] = ampTriang * Mathf.Clamp01(1 - i / lenTriang / sampleFrequency) *
                (CalcSineWave(triangFreq + 175f, i) + CalcSineWave(triangFreq + 224f, i)) / 2 +
                ampSine * Mathf.Clamp01(1 - i / lenSine / sampleFrequency) * (CalcSineWave(sineFreq - 75f, i) + 
                CalcSineWave(sineFreq + 75f, i)) / 2
                + Mathf.Clamp01(1 - i / lenNoise / sampleFrequency) * Mathf.Sin(CalcSineWave(noiseFreq, i) + ampNoise * CalcSineWave(noiseFMFreq, i));
                retVal[i] *= volume;
            }
            return (retVal);
        }
        internal override void InsertNote(ref float[] stream, float time, float freq, float duration)
        {
            int len = (int)(Dropoff * sampleFrequency);
            int start = (int)(time * sampleFrequency);

   //       Debug.Log("Size of Stream: " + stream.Length + "\nstart: " + start + "\nLen: " + len);
            for (int i = 0; i < len; ++i)
            {///*
                stream[start + i] = ampTriang * Mathf.Clamp01(1 - i / lenTriang / sampleFrequency) *
                (CalcSineWave(triangFreq + 175f, i) + CalcSineWave(triangFreq + 224f, i)) / 2 +
                ampSine * Mathf.Clamp01(1 - i / lenSine / sampleFrequency) * (CalcSineWave(sineFreq - 75f, i) +
                CalcSineWave(sineFreq + 75f, i)) / 2
                + Mathf.Clamp01(1 - i / lenNoise / sampleFrequency) * Mathf.Sin(CalcSineWave(noiseFreq, i) + ampNoise * CalcSineWave(noiseFMFreq, i));
                stream[start + i] *= volume;
            }
        }
        internal override void InsertNote(ref float[] stream, float time, float freq, float duration, int start, int dur)
        {
            int len = (int)(Dropoff * sampleFrequency);
            int noteStart = (int)(time);

            int streamPos;
            int notePos;
            if (start > noteStart)
            {
                streamPos = 0;
                notePos = start - noteStart;
            }
            else
            {
                streamPos = noteStart - start;
                notePos = 0;
            }
         //   Debug.Log("Snare: " + streamPos + " " + notePos + " " + len + " " + dur);
            for (; notePos < len && streamPos < dur; ++notePos, ++streamPos)
            {
                stream[streamPos] = ampTriang * Mathf.Clamp01(1 - notePos / lenTriang / sampleFrequency) *
                    (CalcSineWave(triangFreq + 175f, notePos) + CalcSineWave(triangFreq + 224f, notePos)) / 2 +
                    ampSine * Mathf.Clamp01(1 - notePos / lenSine / sampleFrequency) * (CalcSineWave(sineFreq - 75f, notePos) +
                    CalcSineWave(sineFreq + 75f, notePos)) / 2
                    + Mathf.Clamp01(1 - notePos / lenNoise / sampleFrequency) * Mathf.Sin(CalcSineWave(noiseFreq, notePos)
                    + ampNoise * CalcSineWave(noiseFMFreq, notePos));
                stream[streamPos] *= volume;

            }

        }
    }


    class BassDrumInstrument : DrumInstrument
    {
        internal BassDrumInstrument(int fre = 48000, float vol = 1.0f) : base(fre, vol)
        {
            Dropoff = .3f;
        }
        internal override float[] GetNote(float freq, float duration)
        {
            int len = (int)(Dropoff * sampleFrequency);
            float[] retVal = new float[len];
            float subFreq = .5f * freq;

            for (int i = 0; i < len; ++i)
            {
                retVal[i] = (CalcSineWave(freq - i * subFreq / len, i)
                    + CalcSineWave((freq - i * subFreq / len) * 2, i) * .25f) * (1f - i / sampleFrequency / Dropoff);
                retVal[i] *= volume;
            }
            return (retVal);
        }
        internal override void InsertNote(ref float[] stream, float time, float freq, float duration)
        {
            int len = (int)(Dropoff * sampleFrequency);
            int start = (int)(time * sampleFrequency);
            float subFreq = .5f * freq;
            //         Debug.Log("Size of Stream: " + stream.Length + "\nstart: " + start + "\nLen: " + len);
            for (int i = 0; i < len; ++i)
            {
                stream[start + i] = (CalcSineWave(freq - i * subFreq / len, i)
                    + CalcSineWave((freq - i * subFreq / len) * 2, i) * .25f) * (1f - i / sampleFrequency / Dropoff);
                stream[start + i] *= volume;
            }

        }
        internal override void InsertNote(ref float[] stream, float time, float freq, float duration, int start, int dur)
        {
            int len = (int)(Dropoff * sampleFrequency);
            int noteStart = (int)(time);
            float subFreq = .5f * freq;

            int streamPos;
            int notePos;
            if (start > noteStart)
            {
                streamPos = 0;
                notePos = start - noteStart;
            }
            else
            {
                streamPos = noteStart - start;
                notePos = 0;
            }

            for (; notePos < len && streamPos < dur; ++notePos, ++streamPos)
            {
                stream[streamPos] = (CalcSineWave(freq - notePos * subFreq / len, notePos)
                    + CalcSineWave((freq - notePos * subFreq / len) * 2, notePos) * .25f) * (1f - notePos / sampleFrequency / Dropoff);
                stream[streamPos] *= volume;
            }

        }
    }

    internal struct Oscillator
    {
        internal float freq, carr, len, amp;

        internal Oscillator(float frequency, float carrier, float len, float amp)
        {
            freq = frequency;
            carr = carrier;
            this.len = len;
            this.amp = amp;
        }
    }

    abstract class AbstractCymbalInstrument : DrumInstrument
    {
        protected Oscillator[] oscillators;

        internal AbstractCymbalInstrument(int fre = 48000, float vol = 1.0f) : base(fre, vol)
        { }

    }
    class CymbalInstrument : DrumInstrument
    {
        protected Oscillator [] oscillators;
        internal CymbalInstrument(int fre = 48000, float vol = 1.0f) : base(fre, vol)
        {
            oscillators = new Oscillator[3];
            oscillators[0] = new Oscillator(1047, 1481, .15f, .333f);
            oscillators[1] = new Oscillator(1109, 1049, .15f, .333f);
            oscillators[2] = new Oscillator(1175, 1480, .15f, .333f);
            Dropoff = 0.333f;
        }

        internal CymbalInstrument(Oscillator[] oscs, int fre = 480000, float vol = 1.0f) : base(fre, vol)
        {
            if (oscs is null)
            {
                oscs = new Oscillator[3];
                oscs[0] = new Oscillator(1047, 1481, .15f, .333f);
                oscs[1] = new Oscillator(1109, 1049, .15f, .333f);
                oscs[2] = new Oscillator(1175, 1480, .15f, .333f);
            }
            oscillators = oscs;
            Dropoff = 0.0f;
            foreach (Oscillator o in oscs)
            {
                if (o.len > Dropoff)
                    Dropoff = o.len;
            }
        }

        internal override float[] GetNote(float freq, float duration)
        {
            int len = (int)(Dropoff * sampleFrequency);
            float[] retVal = new float[len];
            float subFreq = .5f * freq;

            for (int i = 0; i < len; ++i)
            {
                retVal[i] = 0.0f;
                foreach (Oscillator o in oscillators)
                {
                    retVal[i] += o.amp * Mathf.Clamp01(1 - i / o.len / sampleFrequency) * CalcSineWave(CalcSineWave(o.freq, i) + o.freq * CalcSineWave(o.carr, i), i);
                }
                retVal[i] *= volume;
            }
            return (retVal);
        }
        internal override void InsertNote(ref float[] stream, float time, float freq, float duration)
        {
            int len = (int)(Dropoff * sampleFrequency);
            int start = (int)(time * sampleFrequency);
            float subFreq = .5f * freq;
 //         Debug.Log("Size of Stream: " + stream.Length + "\nstart: " + start + "\nLen: " + len);
            for (int i = 0; i < len; ++i)
            {
                stream[start + i] = 0.0f;
                foreach (Oscillator o in oscillators)
                {
                    stream[start + i] += o.amp * Mathf.Clamp01(1 - i / o.len / sampleFrequency) * CalcSineWave(CalcSineWave(o.freq, i) + o.freq * CalcSineWave(o.carr, i), i);
                }
                stream[start + i] *= volume;
            }

        }
        internal override void InsertNote(ref float[] stream, float time, float freq, float duration, int start, int dur)
        {
            int len = (int)(Dropoff * sampleFrequency);
            int noteStart = (int)(time);
            float subFreq = .5f * freq;

            int streamPos;
            int notePos;
            if (start > noteStart)
            {
                streamPos = 0;
                notePos = start - noteStart;
            }
            else
            {
                streamPos = noteStart - start;
                notePos = 0;
            }

            for (; notePos < len && streamPos < dur; ++notePos, ++streamPos)
            {
                stream[streamPos] = 0.0f;
                foreach (Oscillator o in oscillators)
                {
                    stream[streamPos] += o.amp * Mathf.Clamp01(1 - notePos / o.len / sampleFrequency) * CalcSineWave(CalcSineWave(o.freq, notePos) + o.freq * CalcSineWave(o.carr, notePos), notePos);
                }
                stream[streamPos] *= volume;
            }

        }
    }

    class RideCymbal : CymbalInstrument
    {
        RideCymbal(int fre = 48000, float vol = 1.0f) : base(fre, vol)
        {
            oscillators = new Oscillator[6];
            oscillators[0] = new Oscillator(2499, 2500, .3f, .16f);
            oscillators[1] = new Oscillator(1300, 1700, .4f, .16f);
            oscillators[2] = new Oscillator(2000, 3900, .4f, .16f);
            oscillators[3] = new Oscillator(3000, 5000, .4f, .6f);
            oscillators[4] = new Oscillator(7000, 10000, .9f, .16f);
            oscillators[5] = new Oscillator(10000, 18500, .8f, .16f);
        }


    }

        class NullInstrument : Instrument
    {
        internal NullInstrument(int fre = 48000, float vol = 1.0f) : base(fre, vol)
        {
        }
        internal override float[] GetNote(float freq, float duration)
        {
            return (new float[0]);
        }
        internal override void InsertNote(ref float[] stream, float time, float freq, float duration)
        {
        }
        internal override void InsertNote(ref float[] stream, float time, float freq, float duration, int start, int dur)
        {
        }

    }

        class BasicMixer
    {
        internal static float[] Mix(params float[][] stuff)
        {
            int numStreams = stuff.Length;
            if (numStreams == 0)
            {
                return (new float[0]);
            }

            if (numStreams == 1)
            {
                return stuff[0];
            }

            int maxSize = stuff[0].Length;

            foreach (float[] f in stuff)
            {
//              Debug.Log("Size: " + f.Length);

                if (f.Length > maxSize)
                {
                    maxSize = f.Length;
                }
            }

  //        Debug.Log("Max Size: " + maxSize);

            float[] retval = new float[maxSize];

            for (int i = 0; i < maxSize; ++i)
            {

                float temp = 0.0f;
                foreach (float[] f in stuff)
                {
                    if (i < f.Length)
                    {
                        temp += f[i];
                    }
                }

                retval[i] = temp / numStreams;
            }

            return (retval);
        }
    }

    class PreVolumeMixer
    {
        internal static float[] MixWithVolume(float[] volume, params float[][] stuff)
        {
            int numStreams = stuff.Length;
            if (numStreams == 0)
            {
                return (new float[0]);
            }
            
            int maxSize = stuff[0].Length;
            if (maxSize > volume.Length)
            {
                Debug.Log("Volume badly defined");
                for (int j = volume.Length; j < maxSize; ++j)
                {
                    volume = volume.Append(1.0f).ToArray();
                }
            }

            float volumeDivisor = 0.0f;

            foreach (float f in volume)
            {
                volumeDivisor += f;
            }

            foreach (float[] f in stuff)
            {
//              Debug.Log("Size: " + f.Length);

                if (f.Length > maxSize)
                {
                    maxSize = f.Length;
                }
            }

//          Debug.Log("Max Size: " + maxSize);

            float[] retval = new float[maxSize];

            for (int i = 0; i < maxSize; ++i)
            {

                float temp = 0.0f;
                for (int j = 0; j < stuff.Length; ++j)
                { 
                    if (i < stuff[j].Length)
                    {
                        temp += stuff[j][i] * volume[j];
                    }
                }

                if (volumeDivisor == 0.0f)
                {
                    temp = 0;
                }
                else
                {
                    temp /= volumeDivisor;
                }
                retval[i] = temp;
            }

            return (retval);
        }

        internal static float[] Mix(params float[][] stuff)
        {
            int numStreams = stuff.Length;
            if (numStreams == 0)
            {
                return (new float[0]);
            }

            if (numStreams == 1)
            {
                return stuff[0];
            }

            int maxSize = stuff[0].Length;

            foreach (float[] f in stuff)
            {
 //             Debug.Log("Size: " + f.Length);

                if (f.Length > maxSize)
                {
                    maxSize = f.Length;
                }
            }

 //         Debug.Log("Max Size: " + maxSize);

            float[] retval = new float[maxSize];

            for (int i = 0; i < maxSize; ++i)
            {

                float temp = 0.0f;
                for (int j = 0; j < stuff.Length; ++j)
                {
                    if (i < stuff[j].Length)
                    {
                        temp += stuff[j][i];
                    }
                }
                retval[i] = temp;
            }
            return (retval);
        }
        internal static void Mix(int start, ref float[] output, params float[][] stuff)
        {
            int numStreams = stuff.Length;
            if (numStreams == 0)
            {
                return;
            }

            if (numStreams == 1)
            {
                stuff[0].CopyTo(output, start);
            }

            int maxSize = stuff[0].Length;

            foreach (float[] f in stuff)
            {
                //             Debug.Log("Size: " + f.Length);

                if (f.Length > maxSize)
                {
                    maxSize = f.Length;
                }
            }

            //         Debug.Log("Max Size: " + maxSize);

            for (int i = 0; i < maxSize; ++i)
            {

                float temp = 0.0f;
                for (int j = 0; j < stuff.Length; ++j)
                {
                    if (i < stuff[j].Length)
                    {
                        temp += stuff[j][i];
                    }
                }
                output[start + i] = temp;
            }
        }
    }


    class Note : IComparable
    {
        internal float pitch;
        internal int measure;
        internal float beat;
        internal float duration; // in beats

        internal int time, secDur;
        internal Note(int m, float b, float p, float d)
        {
            measure = m;
            beat = b;
            pitch = p;
            duration = d;
        }
        // Sorts by temporal placement (measure, then beat)
        public int CompareTo(object obj)
        {
            if (obj == null) return 1;

            Note otherNote = obj as Note;
            if (otherNote != null)
            {
                int retval = this.measure.CompareTo(otherNote.measure);
                if (retval == 0)
                {
                    retval = this.beat.CompareTo(otherNote.beat);
                }
                return (retval);
            }
            else
            {
                throw new ArgumentException("Object is not a Note");
            }
        }
        public override string ToString()
        {
            return "Measure: " + measure + " Beat: " + beat + " Pitch: " + pitch + " Duration: " + duration;
        }
    }
    
    internal struct Notes 
    {
        internal const float A0 = 27.5f, Bb0 = 29.1352f, B0 = 30.8677f, C0 = 32.7032f, Db0 = 34.6478f, D0 = 36.7081f, Eb0 = 38.8909f, E0 = 41.2034f, F0 = 43.6535f, Gb0 = 46.2493f, G0 = 48.9994f, Ab0 = 51.9131f,
                           A1 = 55, Bb1 = 58.2705f, B1 = 61.7354f, C1 = 65.4064f, Db1 = 69.2957f, D1 = 73.4162f, Eb1 = 77.7817f, E1 = 82.4069f, F1 = 87.3071f, Gb1 = 92.4986f, G1 = 97.9989f, Ab1 = 103.8261f,
                           A2 = 110, Bb2 = 116.5409f, B2 = 123.4708f, C2 = 130.8128f, Db2 = 138.5913f, D2 = 146.8324f, Eb2 = 155.5635f, E2 = 164.8138f, F2 = 174.6141f, Gb2 = 184.9972f, G2 = 195.9977f, Ab2 = 207.6523f,
                           A3 = 220, Bb3 = 233.0819f, B3 = 246.9417f, C3 = 261.6256f, Db3 = 277.1826f, D3 = 293.6648f, Eb3 = 311.1270f, E3 = 329.6276f, F3 = 349.2282f, Gb3 = 369.9944f, G3 = 391.9954f, Ab3 = 415.3047f,
                           A4 = 440, Bb4 = 466.1637f, B4 = 493.8833f, C4 = 523.2511f, Db4 = 554.3653f, D4 = 587.3295f, Eb4 = 622.2540f, E4 = 659.2551f, F4 = 698.4564f, Gb4 = 739.9888f, G4 = 783.9909f, Ab4 = 830.6094f,
                           A5 = 880, Bb5 = 932.3275f, B5 = 987.7666f, C5 = 1046.5023f, Db5 = 1108.7305f, D5 = 1174.6591f, Eb5 = 1244.5079f, E5 = 1318.5102f, F5 = 1396.9129f, Gb5 = 1479.9777f, G5 = 1567.9817f, Ab5 = 1661.2188f,
                           A6 = 1760, Bb6 = 1864.6550f, B6 = 1975.5332f, C6 = 2093.0045f, Db6 = 2217.4610f, D6 = 2349.3181f, Eb6 = 2489.0159f, E6 = 2637.0205f, F6 = 2793.8259f, Gb6 = 2959.9554f, G6 = 3135.9635f, Ab6 = 3322.4376f,
                           A7 = 3520, Bb7 = 3729.3101f, B7 = 3951.0664f, C7 = 4186.0090f, Db7 = 4434.9221f, D7 = 4698.6363f, Eb7 = 4978.0317f, E7 = 5274.0409f, F7 = 5587.6517f, Gb7 = 5919.9108f, G7 = 6271.9270f, Ab7 = 6644.8752f,
                           A8 = 7040, Bb8 = 7458.6202f, B8 = 7902.1328f, C8 = 8372.0181f, Db8 = 8869.8442f, D8 = 9397.2726f, Eb8 = 9956.0635f, E8 = 10548.0818f, F8 = 11175.3034f, Gb8 = 11839.8215f, G8 = 12543.8540f, Ab8 = 13289.7503f;
        internal const float As0 = Bb0, Cs0 = Db0, Ds0 = Eb0, Fs0 = Gb0, Gs0 = Ab0, Bs0 = C0, Es0 = F0, Cb0 = B0, Fb0 = E0,
                           As1 = Bb1, Cs1 = Db1, Ds1 = Eb1, Fs1 = Gb1, Gs1 = Ab1, Bs1 = C1, Es1 = F1, Cb1 = B1, Fb1 = E1,
                           As2 = Bb2, Cs2 = Db2, Ds2 = Eb2, Fs2 = Gb2, Gs2 = Ab2, Bs2 = C2, Es2 = F2, Cb2 = B2, Fb2 = E2,
                           As3 = Bb3, Cs3 = Db3, Ds3 = Eb3, Fs3 = Gb3, Gs3 = Ab3, Bs3 = C3, Es3 = F3, Cb3 = B3, Fb3 = E3,
                           As4 = Bb4, Cs4 = Db4, Ds4 = Eb4, Fs4 = Gb4, Gs4 = Ab4, Bs4 = C4, Es4 = F4, Cb4 = B4, Fb4 = E4,
                           As5 = Bb5, Cs5 = Db5, Ds5 = Eb5, Fs5 = Gb5, Gs5 = Ab5, Bs5 = C5, Es5 = F5, Cb5 = B5, Fb5 = E5,
                           As6 = Bb6, Cs6 = Db6, Ds6 = Eb6, Fs6 = Gb6, Gs6 = Ab6, Bs6 = C6, Es6 = F6, Cb6 = B6, Fb6 = E6,
                           As7 = Bb7, Cs7 = Db7, Ds7 = Eb7, Fs7 = Gb7, Gs7 = Ab7, Bs7 = C7, Es7 = F7, Cb7 = B7, Fb7 = E7,
                           As8 = Bb8, Cs8 = Db8, Ds8 = Eb8, Fs8 = Gb8, Gs8 = Ab8, Bs8 = C8, Es8 = F8, Cb8 = B8, Fb8 = E8;
        internal const float REST = 0f;

        internal const int totalNotes = 109;

        internal static float Enumerate(int i)
        {
            switch (i)
            {
                case 0:
                default:
                    return REST;

                case 1:
                    return A0;

                case 2:
                    return Bb0;

                case 3:
                    return B0;

                case 4:
                    return C0;

                case 5:
                    return Db0;

                case 6:
                    return D0;

                case 7:
                    return Eb0;

                case 8:
                    return E0;

                case 9:
                    return F0;

                case 11:
                    return Gb0;

                case 12:
                    return G0;

                case 13:
                    return Ab0;

                case 14:
                    return A1;

                case 15:
                    return Bb1;

                case 16:
                    return B1;

                case 17:
                    return C1;

                case 18:
                    return Db1;

                case 19:
                    return D1;

                case 20:
                    return Eb1;

                case 21:
                    return E1;

                case 22:
                    return F1;

                case 23:
                    return Gb1;

                case 24:
                    return G1;

                case 25:
                    return Ab1;

                case 26:
                    return A2;

                case 27:
                    return Bb2;

                case 28:
                    return B2;

                case 29:
                    return C2;

                case 30:
                    return Db2;

                case 31:
                    return D2;

                case 32:
                    return Eb2;

                case 33:
                    return E2;

                case 34:
                    return F2;

                case 35:
                    return Gb2;

                case 36:
                    return G2;

                case 37:
                    return Ab2;

                case 38:
                    return A3;

                case 39:
                    return Bb3;

                case 40:
                    return B3;

                case 41:
                    return C3;

                case 42:
                    return Db3;

                case 43:
                    return D3;

                case 44:
                    return Eb3;

                case 45:
                    return E3;

                case 46:
                    return F3;

                case 47:
                    return Gb3;

                case 48:
                    return G3;

                case 49:
                    return Ab3;

                case 50:
                    return A4;

                case 51:
                    return Bb4;

                case 52:
                    return B4;

                case 53:
                    return C4;

                case 54:
                    return Db4;

                case 55:
                    return D4;

                case 56:
                    return Eb4;

                case 57:
                    return E4;

                case 58:
                    return F4;

                case 59:
                    return Gb4;

                case 60:
                    return G4;

                case 61:
                    return Ab4;

                case 62:
                    return A5;

                case 63:
                    return Bb5;

                case 64:
                    return B5;

                case 65:
                    return C5;

                case 66:
                    return Db5;

                case 67:
                    return D5;

                case 68:
                    return Eb5;

                case 69:
                    return E5;

                case 70:
                    return F5;

                case 71:
                    return Gb5;

                case 72:
                    return G5;

                case 73:
                    return Ab5;

                case 74:
                    return A6;

                case 75:
                    return Bb6;

                case 76:
                    return B6;

                case 77:
                    return C6;

                case 78:
                    return Db6;

                case 79:
                    return D6;

                case 80:
                    return Eb6;

                case 81:
                    return E6;

                case 82:
                    return F6;

                case 83:
                    return Gb6;

                case 84:
                    return G6;

                case 85:
                    return Ab6;

                case 86:
                    return A7;

                case 87:
                    return Bb7;

                case 88:
                    return B7;

                case 89:
                    return C7;

                case 90:
                    return Db7;

                case 91:
                    return D7;

                case 92:
                    return Eb7;

                case 93:
                    return E7;

                case 94:
                    return F7;

                case 95:
                    return Gb7;

                case 96:
                    return G7;

                case 97:
                    return Ab7;

                case 98:
                    return A8;

                case 99:
                    return Bb8;

                case 100:
                    return B8;

                case 101:
                    return C8;

                case 102:
                    return Db8;

                case 103:
                    return D8;

                case 104:
                    return Eb8;

                case 105:
                    return E8;

                case 106:
                    return F8;

                case 107:
                    return Gb8;

                case 108:
                    return G8;

                case 109:
                    return Ab8;

            }
        }

        internal int TotalNotes()
        {
            return 109;
        }
     }
    /*
    internal enum NoteNames
    {
        A0, Bb0, B0, C0 = 32.7032f, Db0 = 34.6478f, D0 = 36.7081f, Eb0 = 38.8909f, E0 = 41.2034f, F0 = 43.6535f, Gb0 = 46.2493f, G0 = 48.9994f, Ab0 = 51.9131f,
        A1, Bb1, B1, C1 = 65.4064f, Db1 = 69.2957f, D1 = 73.4162f, Eb1 = 77.7817f, E1 = 82.4069f, F1 = 87.3071f, Gb1 = 92.4986f, G1 = 97.9989f, Ab1 = 103.8261f,
        A2, Bb2, B2, C2 = 130.8128f, Db2 = 138.5913f, D2 = 146.8324f, Eb2 = 155.5635f, E2 = 164.8138f, F2 = 174.6141f, Gb2 = 184.9972f, G2 = 195.9977f, Ab2 = 207.6523f,
        A3, Bb3, B3, C3 = 261.6256f, Db3 = 277.1826f, D3 = 293.6648f, Eb3 = 311.1270f, E3 = 329.6276f, F3 = 349.2282f, Gb3 = 369.9944f, G3 = 391.9954f, Ab3 = 415.3047f,
        A4, Bb4, B4 = 493.8833f, C4 = 523.2511f, Db4 = 554.3653f, D4 = 587.3295f, Eb4 = 622.2540f, E4 = 659.2551f, F4 = 698.4564f, Gb4 = 739.9888f, G4 = 783.9909f, Ab4 = 830.6094f,
        A5, Bb5, B5 = 987.7666f, C5 = 1046.5023f, Db5 = 1108.7305f, D5 = 1174.6591f, Eb5 = 1244.5079f, E5 = 1318.5102f, F5 = 1396.9129f, Gb5 = 1479.9777f, G5 = 1567.9817f, Ab5 = 1661.2188f,
        A6, Bb6, B6 = 1975.5332f, C6 = 2093.0045f, Db6 = 2217.4610f, D6 = 2349.3181f, Eb6 = 2489.0159f, E6 = 2637.0205f, F6 = 2793.8259f, Gb6 = 2959.9554f, G6 = 3135.9635f, Ab6 = 3322.4376f,
        A7, Bb7, B7 = 3951.0664f, C7 = 4186.0090f, Db7 = 4434.9221f, D7 = 4698.6363f, Eb7 = 4978.0317f, E7 = 5274.0409f, F7 = 5587.6517f, Gb7 = 5919.9108f, G7 = 6271.9270f, Ab7 = 6644.8752f,
        A8, Bb8, B8 = 7902.1328f, C8 = 8372.0181f, Db8 = 8869.8442f, D8 = 9397.2726f, Eb8 = 9956.0635f, E8 = 10548.0818f, F8 = 11175.3034f, Gb8 = 11839.8215f, G8 = 12543.8540f, Ab8 = 13289.7503f;
        internal const float As0 = Bb0, Cs0 = Db0, Ds0 = Eb0, Fs0 = Gb0, Gs0 = Ab0, Bs0 = C0, Es0 = F0, Cb0 = B0, Fb0 = E0,
                           As1 = Bb1, Cs1 = Db1, Ds1 = Eb1, Fs1 = Gb1, Gs1 = Ab1, Bs1 = C1, Es1 = F1, Cb1 = B1, Fb1 = E1,
                           As2 = Bb2, Cs2 = Db2, Ds2 = Eb2, Fs2 = Gb2, Gs2 = Ab2, Bs2 = C2, Es2 = F2, Cb2 = B2, Fb2 = E2,
                           As3 = Bb3, Cs3 = Db3, Ds3 = Eb3, Fs3 = Gb3, Gs3 = Ab3, Bs3 = C3, Es3 = F3, Cb3 = B3, Fb3 = E3,
                           As4 = Bb4, Cs4 = Db4, Ds4 = Eb4, Fs4 = Gb4, Gs4 = Ab4, Bs4 = C4, Es4 = F4, Cb4 = B4, Fb4 = E4,
                           As5 = Bb5, Cs5 = Db5, Ds5 = Eb5, Fs5 = Gb5, Gs5 = Ab5, Bs5 = C5, Es5 = F5, Cb5 = B5, Fb5 = E5,
                           As6 = Bb6, Cs6 = Db6, Ds6 = Eb6, Fs6 = Gb6, Gs6 = Ab6, Bs6 = C6, Es6 = F6, Cb6 = B6, Fb6 = E6,
                           As7 = Bb7, Cs7 = Db7, Ds7 = Eb7, Fs7 = Gb7, Gs7 = Ab7, Bs7 = C7, Es7 = F7, Cb7 = B7, Fb7 = E7,
                           As8 = Bb8, Cs8 = Db8, Ds8 = Eb8, Fs8 = Gb8, Gs8 = Ab8, Bs8 = C8, Es8 = F8, Cb8 = B8, Fb8 = E8;
    internal const float REST = 0f;
}*/
    internal enum Harmony
    {
        None = -2, NoChord, C, G, D, A, E, B, Fs, Cs, Gs, Ds, As, F, Cm, Gm, Dm, Am, Em, Bm, Fsm, Csm, Gsm, Dsm, Asm, Fm, 
            Cb = B, Db = Cs, Eb = Ds, Fb = E, Gb = Fs, Ab = Gs, Bb = As, Cbm = Bm, Dbm = Csm, Ebm = Dsm, Fbm = Em, Gbm = Fsm, Abm = Gsm, Bbm = Asm,
            Es = F, Bs = C, Esm = Fm, Bsm = Cm
    }

    internal enum Interval
    {
        Unison = 0, MinorSecond, MajorSecond, MinorThird, MajorThird, PerfectFourth, Tritone, PerfectFifth, MinorSixth, MajorSixth,
            MinorSeventh, MajorSeventh, Octave
    }
}

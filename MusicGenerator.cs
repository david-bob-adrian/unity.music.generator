using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace OddRhythms
{
    static class Constants
    {
        public const int freq = 48000;
    }

    class MusicGenerator
    {
        public static AudioClip Generate(int seed, float valence, float energy, Version version = Version.V1_0_0)
        {
            Score score = CreateScore(seed, valence, energy, version);
            return CompileMusic(score);
        }

        public static Score CreateScore(int seed, float energy, float valence, Version version = Version.V1_0_0)
        {
            Score retval;
            switch (version)
            {
                case Version.V1_0_1:
                    retval = new Score2(seed, energy, valence);
                    break;

                default:
                    //Debug.Log("Creating score: " + seed + " " + energy + " " + valence);
                    retval = new Score(seed, energy, valence);
                    break;
            }

            return (retval);
        }

        private static AudioClip CompileMusic(Score s)
        {
            return (s.getClip());
        }

    }

    public class ScoreManager
    {
        private Score score;
        private AudioClip clip;
        private int position;

        public void ChangeScore(Score newScore, ref AudioSource source)
        {
            AudioClip scoreClip = newScore.getClip();
            if (scoreClip is null)
            {
                //Debug.Log("ScoreClip is Null!");
            }

            if (clip)
            {
                //newScore.initRunningScore(score, source);
//                newScore.addSoundingNotes(score.getSoundingNotes());
            }
            clip = AudioClip.Create("whatever", 1, 1, Constants.freq, true, OnAudioRead, OnAudioSetPosition);
            source.clip = clip;
            source.timeSamples = newScore.getPosition();
            
            score = newScore;
        }

        public void ChangeEnergyOrValence(float valence, float energy)
        {
            if (!(score is null))
            {
                score.valence = valence;
                score.energy = energy;
                score.Compile2();
            }
        }

        public void Stop()
        {
            clip = null;
        }

        void OnAudioRead(float[] data)
        {
            if (score is null)
            {
                //Debug.Log("Score is Null in On Audio Read... score is " + score);
            }
            else
            {
                score.OnAudioReadNew(data);
                //Debug.Log("Score is not null in On Audio Read... score is " + score);
            }
        }

        public void OnAudioSetPosition(int newPosition)
        {
            if (score is null)
            {
                //Debug.Log("Score is Null in On Audio Set Position...");
            }
            else
            {
                score.OnAudioSetPosition(newPosition);
                //Debug.Log("Score is not null in On Audio Set Position...");
            }
        }

    }

    public class Score
    {
        private protected Section2[] sections;
        private protected float[] data;
        private protected bool compiled;
        private protected int position;
        private protected AudioClip clip;
        private protected OldSeed[] oldSeeds = new OldSeed[0];
        private protected int seed;
        private protected int currSection;
        private protected float _energy;
        private protected float _valence;
        private protected float _oldAro, _oldVal;
        private protected bool firstTime;
        private protected int t;

        public float energy
        {
            get => _energy;
            set
            {
                if (value != _energy)
                {
                    oldSeeds = oldSeeds.Append(new OldSeed(position, _energy, valence)).ToArray();
                    _oldAro = _energy;
                    compiled = false;
                }
                _energy = value;
            }
        }
        public float valence
        {
            get => _valence;
            set
            {
                if (value != _valence)
                {
                    oldSeeds = oldSeeds.Append(new OldSeed(position, energy, _valence)).ToArray();
                    _oldVal = _valence;
                    compiled = false;
                }
                _valence = value;
            }
        }

        private protected Score()
        {
            seed = 0;
            energy = 3;
            valence = 3;
        }

        public Score(int seed, float energy, float valence)
        {
            this.seed = seed;
            this.energy = energy;
            this.valence = valence;
            this.compiled = false;
            this._oldAro = -1;
            this._oldVal = -1;
            firstTime = true;
            position = 0;

            t = -1;

            currSection = 0;
            if (seed == 0)
            {
                RhythmTracker r = new RhythmTracker(Constants.freq, "120 D W S W W 1");
                InstrumentManager man1 = new InstrumentManager(new SquareWaveInstrument(Constants.freq), r);
                InstrumentManager man2 = new InstrumentManager(new SineWaveInstrument(Constants.freq), r);
                DrumManager bassDrum = new DrumManager(new BassDrumInstrument(Constants.freq), r);
                DrumManager snareDrum = new DrumManager(new SnareDrumInstrument(Constants.freq), r);

                DrumManagerManager drummer = new DrumManagerManager(bassDrum, snareDrum, new NullDrumManager());

                man1.AddNote(new Note(1, 1, Notes.A4, 1));
                man1.AddNote(new Note(1, 2, Notes.E4, 1));
                man1.AddNote(new Note(1, 3, Notes.A4, 1));
                man1.AddNote(new Note(1, 4, Notes.A5, 1));
                man1.AddNote(new Note(1, 5, Notes.REST, 1));

                man2.AddNote(new Note(1, 4, Notes.E4, 1));

                drummer.BassHit(1, 1);
                drummer.BassHit(1, 3);
                drummer.BassHit(1, 5);
                drummer.BassHit(1, 5.5f);

                drummer.SnareHit(1, 2);
                drummer.SnareHit(1, 4);

                data = BasicMixer.Mix(man1.GetStream(), man2.GetStream(), drummer.GetStream());
                clip = AudioClip.Create("whatever", data.Length, 1, Constants.freq, true, OnAudioRead, OnAudioSetPosition);
            }
            else
            {
                DetermineForm(seed);    // doesn't depend on valence/energy
                AssignSeeds(seed);      // .
                foreach (Section2 section in sections)
                {
                    section.DeriveSection(energy, valence);    // does depend on valence and energy
                }
            }
            compiled = false;
        }

        public int getPosition()
        {
            return position;
        }

        private protected void OnAudioRead(float[] data)
        {
            for (int i = 0; i < data.Length; ++i)
            {
                data[i] = this.data[position++];
            }
        }

        public void OnAudioReadNew(float[] data)
        {
            if (compiled)
            {
                //Debug.Log("AudioRead Enter: " + data.Length + " " + sections[currSection].dur + " " + ++t);
                int dataRemaining = data.Length;
                int i = 0;
                int startSection = currSection;
                int sectionLength = sections[currSection].GetDuration(valence, energy);
                //       Debug.Log("In Audio Read Current Val: " + valence + " energy: " + energy);
                try
                {
                    while (position + dataRemaining > sectionLength)
                    {
                        int diff = sectionLength - position;
                        sections[currSection].getNotes(position, diff, i, ref data);
                        i += diff;
                        dataRemaining -= diff;

                        position = 0;
                        currSection = ++currSection % sections.Length;
                        sectionLength = sections[currSection].GetDuration(valence, energy);
                    }
                    //                Debug.Log("After while loop: " + dataRemaining);
                    sections[currSection].getNotes(position, dataRemaining, i, ref data);
                    position += dataRemaining;
                    //                Debug.Log("Position: " + position);
                }
                catch (Exception e) //IndexOutOfRangeException e)
                {
                    Debug.Log("Error on: " + position + " " + startSection + " " + currSection + " " + " " + dataRemaining + " / " + data.Length);
                    throw e;
                }
                //Debug.Log("AudioRead Exit!");
            }
        }

        public void OnAudioSetPosition(int newPosition)
        {
            //Debug.Log("Set at " + ++t + ": " + newPosition);

            if (false && firstTime)
            {
                position = newPosition;
                currSection = 0;
                Debug.Log("Setting audio position; sections: " + sections.Length + "; " + newPosition);
                while (position > sections[currSection].dur)
                {
                    position -= sections[currSection++].dur;
                    currSection %= sections.Length;
                }
                firstTime = false;
            }
            
        }

        virtual private protected void DetermineForm(int seed)
        {
            System.Random gen = new System.Random(seed);
            double speed = Math.Round(gen.NextDouble() * 140 + 60);
            sections = new Section2[4];
            int SeedA = gen.Next();
            sections[0] = new Section2(SeedA, "A", speed);
            sections[1] = new Section2(SeedA, "A", speed);
            sections[2] = new Section2(gen.Next(), "B", speed);
            sections[3] = new Section2(SeedA, "A", speed);
        }

        // Might be folded into above function
        private protected void AssignSeeds(int seed)
        {

        }

        private protected void AssignInstruments(int seed)
        {

            foreach (Section2 section in sections)
            {
                DrumManager b = new DrumManager(new BassDrumInstrument(Constants.freq, .5f), section.r);
                DrumManager s = new DrumManager(new SnareDrumInstrument(Constants.freq, .5f), section.r);
                DrumManager h = new DrumManager(new CymbalInstrument(Constants.freq, .2f), section.r);

                section.drummer.SetBass(b);
                section.drummer.SetHighHat(h);
                section.drummer.SetSnare(s);
            }
        }

        public AudioClip getClip()
        {
            if (!compiled)
            {
                Compile2();
            }

            if (clip is null)
            {
                clip = AudioClip.Create("whatever", 1, 1, Constants.freq, true, OnAudioReadNew, OnAudioSetPosition);
                firstTime = true;
            }

            return clip;
        }

        public void Compile()
        {
            int offset = 0;
            data = new float[0];
            foreach (Section2 s in sections)
            {
                data = data.Concat(s.Compile()).ToArray();
            }
            compiled = true;
        }

        public void Compile2()
        {
            int totalDur = 0;
            if (sections is null)
            {
                //Debug.Log("This won't print.");
            }
            else
            {
                //Debug.Log(sections.Length);
            }
            //Debug.Log("Testing: " + (sections is null));
            for (int i = 0; i < sections.Length; ++i)
            {
                //Debug.Log("Testing1");
                sections[i].Compile2(valence, energy);
                //Debug.Log("Testing2");
                totalDur += sections[i].GetDuration(valence, energy);
            }
            //Debug.Log("Testing3");
            data = new float[totalDur];
            float useVal = _oldVal == -1 ? valence : _oldVal;
            float useAro = _oldAro == -1 ? energy : _oldAro;
            //Debug.Log(useVal + " " + useAro + " " + valence + " " + energy + " " + position);
            int test = sections[currSection].GetPosition(useVal, useAro, valence, energy, position);
            if (test == -1)
            {
                position = 0;
                ++currSection;
                currSection %= sections.Length;
            }
            else
            {
                position += test;
            }
            compiled = true;
            //Debug.Log("Song should be compiled at " + ++t);
        }

        public static T DecideProb<T>(Dictionary<T, double> d, double valtest, T defaultValue)
        {
            double curr = 0;
            foreach (KeyValuePair<T, double> kv in d)
            {
                curr += kv.Value;
                if (curr >= valtest)
                {
                    return kv.Key;
                }
            }

            return defaultValue;
        }

        public void initRunningScore(Score other, AudioSource source)
        {
            // this.currSection = other.currSection;
            OnAudioSetPosition(other.position);
            source.timeSamples = position;
        }

        public string GetRhythm()
        {
            string retval = "";

            for (int i = 0; i < sections.Length; ++i)
            {
                retval += sections[i].GetRhythm(valence, energy);
            }

            return retval;
        }

        public int GetMeasure()
        {
            return sections[currSection].GetMeasure(valence, energy, position);
        }

        public float GetBeat()
        {
            return sections[currSection].GetBeat(valence, energy, position);
        }
    }

    public class Score2 : Score
    {
        private Score2()
        {

        }

        public Score2(int seed, float energy, float valence)
        {
            this.seed = seed;
            this.energy = energy;
            this.valence = valence;
            this.compiled = false;
            this._oldAro = -1;
            this._oldVal = -1;

            currSection = 0;
            if (seed == 0)
            {
                RhythmTracker r = new RhythmTracker(Constants.freq, "120 D W S W W 1");
                InstrumentManager man1 = new InstrumentManager(new SquareWaveInstrument(Constants.freq), r);
                InstrumentManager man2 = new InstrumentManager(new SineWaveInstrument(Constants.freq), r);
                DrumManager bassDrum = new DrumManager(new BassDrumInstrument(Constants.freq), r);
                DrumManager snareDrum = new DrumManager(new SnareDrumInstrument(Constants.freq), r);

                DrumManagerManager drummer = new DrumManagerManager(bassDrum, snareDrum, new NullDrumManager());

                man1.AddNote(new Note(1, 1, Notes.A4, 1));
                man1.AddNote(new Note(1, 2, Notes.E4, 1));
                man1.AddNote(new Note(1, 3, Notes.A4, 1));
                man1.AddNote(new Note(1, 4, Notes.A5, 1));
                man1.AddNote(new Note(1, 5, Notes.REST, 1));

                man2.AddNote(new Note(1, 4, Notes.E4, 1));

                drummer.BassHit(1, 1);
                drummer.BassHit(1, 3);
                drummer.BassHit(1, 5);
                drummer.BassHit(1, 5.5f);

                drummer.SnareHit(1, 2);
                drummer.SnareHit(1, 4);

                data = BasicMixer.Mix(man1.GetStream(), man2.GetStream(), drummer.GetStream());
                clip = AudioClip.Create("whatever", data.Length, 1, Constants.freq, true, OnAudioRead, OnAudioSetPosition);
            }
            else
            {
                DetermineForm(seed);    // doesn't depend on valence/energy
                AssignSeeds(seed);      // .
                foreach (Section4 section in sections)
                {
                    section.DeriveSection(energy, valence);    // does depend on valence and energy
                }
            }
            compiled = false;
        }

        private protected override void DetermineForm(int seed)
        {
            System.Random gen = new System.Random(seed);
            double speed = Math.Round(gen.NextDouble() * 140 + 60);
            sections = new Section4[4];
            int SeedA = gen.Next();
            sections[0] = new Section4(SeedA, "A", speed);
            sections[1] = new Section4(SeedA, "A", speed);
            sections[2] = new Section4(gen.Next(), "B", speed);
            sections[3] = new Section4(SeedA, "A", speed);
        }
    }

    class Section
    {
        public RhythmTracker r;
        public InstrumentManager[] ins;
        public DrumManagerManager drummer;
        private Dictionary<int, List<Note>>[][] notes;
        private Dictionary<int, Dictionary<int, List<Note>>> spaceHolder;
        string name;
        int sectionSeed;
        private double speed;
        int holdNote;
        public int dur { get; private set; }
        public int measures { get; private set; }

        private System.Random gen;

        private Section()
        {

        }

        public Section(int seed = 0, string n = "", double speed = 120)
        {
            sectionSeed = seed;
            name = n;
            gen = new System.Random(seed);
            //Debug.Log("Section Seed: " + seed);
            ins = new InstrumentManager[9];
            dur = 0;
            this.speed = speed;
            this.holdNote = 0;
            notes = new Dictionary<int, List<Note> >[9][];
            for (int i = 0; i < notes.Length; ++i)
            {
                notes[i] = new Dictionary<int, List<Note>>[3];
                notes[i][0] = new Dictionary<int, List<Note>>(); // Valance
                notes[i][1] = new Dictionary<int, List<Note>>(); // energy
                notes[i][2] = new Dictionary<int, List<Note>>(); // All
            }
        }

        public void DeriveSection(float energy, float valence)
        {
            int melodySeed = gen.Next();
            int harmSeed = gen.Next();
            int rhythmSeed = gen.Next();
            int testVal = gen.Next(0, 6);
            DecideTimeSignature(energy, valence);
            switch (testVal)
            {
                case 0:
                    DeriveMelody(energy, valence, melodySeed);
                    DeriveHarmony(energy, valence, harmSeed);
                    DeriveRhythm(energy, valence, rhythmSeed);
                    break;

                case 1:
                    DeriveMelody(energy, valence, melodySeed);
                    DeriveRhythm(energy, valence, rhythmSeed);
                    DeriveHarmony(energy, valence, harmSeed);
                    break;

                case 2:
                    DeriveHarmony(energy, valence, harmSeed);
                    DeriveMelody(energy, valence, melodySeed);
                    DeriveRhythm(energy, valence, rhythmSeed);
                    break;

                case 3:
                    DeriveHarmony(energy, valence, harmSeed);
                    DeriveRhythm(energy, valence, rhythmSeed);
                    DeriveMelody(energy, valence, melodySeed);
                    break;

                case 4:
                    DeriveRhythm(energy, valence, rhythmSeed);
                    DeriveMelody(energy, valence, melodySeed);
                    DeriveHarmony(energy, valence, harmSeed);
                    break;

                default:
                    DeriveRhythm(energy, valence, rhythmSeed);
                    DeriveHarmony(energy, valence, harmSeed);
                    DeriveMelody(energy, valence, melodySeed);
                    break;
            }
        }

        private void DecideTimeSignature(float energy, float valence)
        {/*
                if (name == "A")
                    r = new RhythmTracker(Constants.freq, "120 D W S W S W S W 4");
                else
                {
                    r = new RhythmTracker(Constants.freq, "240 D W W S W W S W 4");
                }
                */
            string tracker = "";

            // Decide speed
            // a 1 = 60-70 bpm; a 5 = 180-200 bpm
            // double d = (gen.NextDouble() * (29.375 + .625 * energy) + (28.125 + 1.875 * energy)) * 2;
            //double d = (57.5 + 2.5 * energy) * 2;
            double d = speed * 2;

            Dictionary<int, double> measureProb = new Dictionary<int, double>();
            Dictionary<string, double> beatsProb = new Dictionary<string, double>();
            // Decide signatures
            int numMeasures = 0;
            if (valence < 4.0)
            {
                measureProb[1] = 1.0 / 28;
                measureProb[2] = 4.0 / 28;
                measureProb[4] = 9.0 / 28;
                measureProb[8] = 9.0 / 28;
                measureProb[16] = 4.0 / 28;
                measureProb[32] = 1.0 / 28;
                numMeasures = Score.DecideProb(measureProb, gen.NextDouble(), 4);

                // This should be .5 to .25 from 1 to 5 energy.
                //                    beatsProb[" D W S W S W "] = .5625f - .0625f * energy;
                beatsProb[" D W S W S W "] = .28125f - .03125f * energy;
                beatsProb[" D W W S W W "] = .28125f - .03125f * energy;
                beatsProb[" D W S W S W S W "] = .5 - beatsProb[" D W S W S W "];
                beatsProb[" D W W S W W S W "] = beatsProb[" D W S W S W S W "];
                
                tracker = d.ToString();
                tracker += Score.DecideProb(beatsProb, gen.NextDouble(), " D W S W S W S W ");
                tracker += numMeasures.ToString();
            }
            else
            {
                measureProb[1] = 9.0 / 280;
                measureProb[2] = 40.0 / 280;
                measureProb[4] = 90.0 / 280;
                measureProb[8] = 90.0 / 280;
                measureProb[16] = 40.0 / 280;
                measureProb[32] = 9.0 / 280;
                measureProb[-1] = 2.0 / 280;
                numMeasures = Score.DecideProb(measureProb, gen.NextDouble(), 4);
                if (numMeasures == -1)
                {
                    measureProb[-1] = 0;
                    for (int i = 1; i <= 32; ++i)
                    {
                        measureProb[i] = 1.0 / 31;
                    }
                    numMeasures = Score.DecideProb(measureProb, gen.NextDouble(), 8);
                }

                int currMeasure = 0;
                while (currMeasure < numMeasures)
                {
                    if (currMeasure > 0)
                    {
                        tracker += "\n";
                    }
                    string rhythm = "";
                    int countMeasure = 1;
                    // This should be .5 to .25 from 1 to 5 energy.
                    //                    beatsProb[" D W S W S W "] = .5625f - .0625f * energy;
                    beatsProb[" D W S W S W "] = .28125f - .03125f * energy - .125 * (energy - 4);
                    beatsProb[" D W W S W W "] = beatsProb[" D W S W S W "];
                    beatsProb[" D W S W S W S W "] = .21875 + .03125f * energy - .25 * (energy - 4);
                    beatsProb[" D W W S W W S W "] = beatsProb[" D W S W S W S W "];
                    beatsProb["odd"] = .5 * (energy - 4);
                    d = speed * 2;
                    
                    rhythm = Score.DecideProb(beatsProb, gen.NextDouble(), "odd");
                    if (rhythm == "odd")
                    {
                        rhythm = GetOddRhythm(energy, valence);
                        countMeasure = rhythm.Count(b => b == 'D');
                        if (gen.NextDouble() < 3 * (energy - 4) / 4)
                        {
                            d = speed * 4;
                        }
                    }
                    int reps = 1;
                    while (currMeasure + reps * countMeasure < numMeasures && gen.NextDouble() < 1 - .05 * energy)
                    {
                        reps++;
                    }
                    //Debug.Log(rhythm);
                    tracker += d + rhythm + " " + reps;
                    currMeasure += reps * countMeasure;
                }
            }
            // Debug.Log(tracker);
            r = new RhythmTracker(Constants.freq, tracker);
            InstrumentManager silent = new InstrumentManager(new SineWaveInstrument(Constants.freq, 0f), r);
            ins[0] = silent;
            int mCount = 0;
            int lastbeat = 0;
            foreach (RhythmTracker.Measure m in r.GetMeasures())
            {
                mCount++;
                // Debug.Log("Beats: " + m.beats.Length);
                silent.AddNote(new Note(mCount, m.beats.Length, Notes.REST, 1));
                lastbeat = m.beats.Length;
            }
            dur = (int)(r.GetTime(mCount, lastbeat + 1) * Constants.freq);
            measures = mCount;
//            Debug.Log("Last: " + mCount + " " + lastbeat + " " + dur);
        }
        private void DeriveMelody(float energy, float valence, int seed)
        {
            gen = new System.Random(seed);
            RhythmTracker.Measure start = r.GetMeasures().First();
            int numIntervals, interval1, interval2;
            NoteHolder[] otherNotes = new NoteHolder[0];
            InstrumentManager melody = new InstrumentManager(new SquareWaveInstrument(), r);
            ins[1] = melody;

            if (gen.Next(1) == 0)
            {
                numIntervals = 2;
                interval1 = gen.Next(-24, 24);
                interval2 = gen.Next(-12, 12);
            }
            else
            {
                numIntervals = 1;
                interval1 = gen.Next(-24, 24);
                interval2 = 0;
            }

            if (holdNote == 0)
            {
                holdNote = gen.Next(24, Notes.totalNotes - 24) + 1;
                if (holdNote + interval1 < 1)
                {
                    holdNote += 24;
                }
                if (holdNote + interval1 + interval2 < 1)
                {
                    holdNote += 12;
                }
                if (holdNote + interval1 >= Notes.totalNotes)
                {
                    holdNote -= 24;
                }
                if (holdNote + interval1 + interval2 >= Notes.totalNotes)
                {
                    holdNote -= 12;
                }
            }
            else
            {
                holdNote += 36;
            }

 //           NoteHolder startingNote = new NoteHolder(1, 1, pitch: Notes.Enumerate(holdNote)), firstInterval = new NoteHolder(0, 0), secondInterval = new NoteHolder(0, 0);
            int measureCount = 0;
            foreach (RhythmTracker.Measure m in r.GetMeasures())
            {
                measureCount++;
                NoteHolder startingNote = new NoteHolder(measureCount, 1, pitch: Notes.Enumerate(holdNote)), firstInterval = new NoteHolder(0, 0), secondInterval = new NoteHolder(0, 0);
                int countStrong = 0, numStrong = 0;
                for (int i = 0; i < m.beats.Length; ++i)
                {
                    if (m.beats[i] == BeatClass.S || m.beats[i] == BeatClass.D)
                    {
                        numStrong++;
                    }
                }

                //Figure out where to place notes based on pattern of strong beats and number of intervals.
                if (numIntervals == 1)
                {
                    if (numStrong % 2 == 0)
                    {
                        for (int i = 0; i < m.beats.Length; ++i)
                        {
                            if (m.beats[i] == BeatClass.S || m.beats[i] == BeatClass.D)
                            {
                                countStrong++;
                                if (countStrong == numStrong / 2)
                                {
                                    firstInterval = new NoteHolder(measureCount, i + 1, pitch: Notes.Enumerate(holdNote + interval1));
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < m.beats.Length; ++i)
                        {
                            if (m.beats[i] == BeatClass.S || m.beats[i] == BeatClass.D)
                            {
                                countStrong++;
                                if (countStrong == (numStrong + 1) / 2)
                                {
                                    firstInterval = new NoteHolder(measureCount, i + 1, pitch: Notes.Enumerate(holdNote + interval1));
                                    break;
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (numStrong % 3 == 0)
                    {
                        for (int i = 0; i < m.beats.Length; ++i)
                        {
                            if (m.beats[i] == BeatClass.S || m.beats[i] == BeatClass.D)
                            {
                                countStrong++;
                                if (countStrong == numStrong / 3)
                                {
                                    firstInterval = new NoteHolder(measureCount, i + 1, pitch: Notes.Enumerate(holdNote + interval1));
                                }
                                else if (countStrong == numStrong / 3 * 2)
                                {
                                    secondInterval = new NoteHolder(measureCount, i + 1, pitch: Notes.Enumerate(holdNote + interval1 + interval2));
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        // Just in case??
                        if (numStrong == 1)
                        {
                            firstInterval = new NoteHolder(measureCount, 1.5f, pitch: Notes.Enumerate(holdNote + interval1));
                            secondInterval = new NoteHolder(measureCount, 2, pitch: Notes.Enumerate(holdNote + interval1 + interval2));
                        }
                        else if (numStrong == 2)
                        {
                            for (int i = 1; i < m.beats.Length; ++i)
                            {
                                if (m.beats[i] == BeatClass.S || m.beats[i] == BeatClass.D)
                                {
                                    firstInterval = new NoteHolder(measureCount, i / 2.0f + 1, pitch: Notes.Enumerate(holdNote + interval1));
                                    secondInterval = new NoteHolder(measureCount, i + 1, pitch: Notes.Enumerate(holdNote + interval1 + interval2));
                                    break;
                                }
                            }
                        }
                        else if (numStrong % 3 == 1)
                        {
                            for (int i = 0; i < m.beats.Length; ++i)
                            {
                                if (m.beats[i] == BeatClass.S || m.beats[i] == BeatClass.D)
                                {
                                    countStrong++;
                                    if (countStrong == (numStrong - 1) / 3)
                                    {
                                        firstInterval = new NoteHolder(measureCount, i + 1, pitch: Notes.Enumerate(holdNote + interval1));
                                    }
                                    else if (countStrong == (numStrong - 1) / 3 * 2)
                                    {
                                        secondInterval = new NoteHolder(measureCount, i + 1, pitch: Notes.Enumerate(holdNote + interval1 + interval2));
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            for (int i = 0; i < m.beats.Length; ++i)
                            {
                                if (m.beats[i] == BeatClass.S || m.beats[i] == BeatClass.D)
                                {
                                    countStrong++;
                                    if (countStrong == (numStrong - 2) / 3)
                                    {
                                        firstInterval = new NoteHolder(measureCount, i + 1, pitch: Notes.Enumerate(holdNote + interval1));
                                    }
                                    else if (countStrong == (numStrong + 1) / 3 * 2)
                                    {
                                        secondInterval = new NoteHolder(measureCount, i + 1, pitch: Notes.Enumerate(holdNote + interval1 + interval2));
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                /*               for (int i = 0; i < m.beats.Length; ++i)
                               {
                                   if (i + 1 != firstInterval.beat && i + 1 != secondInterval.beat)
                                   {
                                       otherNotes.Append(new NoteHolder(measureCount, i + 1, 1));
                                   }
                               }*/



                if (numIntervals == 2)
                {
                    //Debug.Log(String.Concat("Starting Note: ", startingNote.measure, ", ", startingNote.beat, ", ", startingNote.pitch, ", ", firstInterval.beat - startingNote.beat,
                    //    "\nFirst Interval: ", firstInterval.measure, ", ", firstInterval.beat, ", ", firstInterval.pitch, ", ", secondInterval.beat - firstInterval.beat,
                    //    "\nSecond Interval: ", secondInterval.measure, ", ", secondInterval.beat, ", ", secondInterval.pitch, ", ", m.beats.Length - secondInterval.beat));
                    melody.AddNote(new Note(firstInterval.measure, firstInterval.beat, firstInterval.pitch, (secondInterval.beat - firstInterval.beat) / 2));
                    melody.AddNote(new Note(secondInterval.measure, secondInterval.beat, secondInterval.pitch, (m.beats.Length - secondInterval.beat) / 2));
                }
                else
                {
                    //Debug.Log(String.Concat("Starting Note: ", startingNote.measure, ", ", startingNote.beat, ", ", startingNote.pitch, ", ", firstInterval.beat - startingNote.beat,
                    //    "\nFirst Interval: ", firstInterval.measure, ", ", firstInterval.beat, ", ", firstInterval.pitch, ", ", m.beats.Length - firstInterval.beat));
                    melody.AddNote(new Note(firstInterval.measure, firstInterval.beat, firstInterval.pitch, (m.beats.Length - firstInterval.beat) / 2));
                }
            }
        }
        private void DeriveHarmony(float energy, float valence, int seed)
        {
            gen = new System.Random(seed);

            InstrumentManager bass = new InstrumentManager(new SineWaveInstrument(), r);
            ins[5] = bass;

            int mCount = 0;
            NoteHolder[] possibleNotes = new NoteHolder[0];
            long measure = 0;
            if (holdNote == 0)
            {
                holdNote = gen.Next(12, 36) + 1;
            }
            else
            {
                while (holdNote >= 36)
                {
                    holdNote -= 12;
                }
            }
            foreach (RhythmTracker.Measure m in r.GetMeasures())
            {
                mCount++;
                /*                int strongBeats = 0, currentBeat = 0;
                                for (int i = 0; i < m.beats.Length; ++i)
                                {
                                    switch (m.beats[i])
                                    {
                                        case BeatClass.D:
                                        case BeatClass.S:
                                            strongBeats++;
                                            break;
                                    }
                                }
                */
                for (int i = 0; i < m.beats.Length; ++i)
                {
                    float pitch = 0, length = 0;
                    string type = "";
                    switch (measure)
                    {
                        case 0:
                            switch (m.beats[i])
                            {
                                case BeatClass.D:
                                    pitch = Notes.Enumerate(holdNote); // Notes.E1;
                                    length = m.beats.Length;
                                    type = "D";
                                    break;

                                case BeatClass.S:
                                    pitch = Notes.Enumerate(holdNote + 7);// Notes.B2;
                                    length = 1f;
                                    type = "S";
                                    break;
                            }
                            break;

                        case 1:
                            switch (m.beats[i])
                            {
                                case BeatClass.D:
                                    pitch = Notes.Enumerate(holdNote + 7);
                                    length = m.beats.Length;
                                    type = "D";
                                    break;

                                case BeatClass.S:
                                    pitch = Notes.Enumerate(holdNote + 12);
                                    length = 1f;
                                    type = "S";
                                    break;
                            }
                            break;

                        case 2:
                            switch (m.beats[i])
                            {
                                case BeatClass.D:
                                    pitch = Notes.Enumerate(holdNote + 12);
                                    length = m.beats.Length;
                                    type = "D";
                                    break;

                                case BeatClass.S:
                                    pitch = Notes.Enumerate(holdNote + 19);
                                    length = 1f;
                                    type = "S";
                                    break;
                            }
                            break;

                        case 3:
                            switch (m.beats[i])
                            {
                                case BeatClass.D:
                                    pitch = Notes.Enumerate(holdNote + 7);
                                    length = m.beats.Length;
                                    type = "D";
                                    break;

                                case BeatClass.S:
                                    pitch = Notes.Enumerate(holdNote + 14);
                                    length = 1f;
                                    type = "S";
                                    break;
                            }
                            break;
                    }
                    if (type != "")
                    {
                        //Debug.Log(String.Concat(mCount, " ", i + 1, " ", length, " ", pitch, " ", type));
                        possibleNotes = possibleNotes.Append(new NoteHolder(mCount, i + 1, length, pitch, type)).ToArray();
                    }
                }

                measure++;
                measure %= 4;
                /*
                for (int i = 0; i < m.beats.Length; ++i)
                {
                    switch (m.beats[i])
                    {
                        case BeatClass.D:
                            bassHit = true;
                            goto case BeatClass.S;

                        case BeatClass.S:
                            if (bassHit)
                                drummer.BassHit(mCount, i+1);
                            else
                                drummer.SnareHit(mCount, i+1);

                            bassHit = !bassHit;
                            break;
                    }

                    drummer.HighHatHit(mCount, i + 1);
                }
*/

            }
            
            // Debug.Log("Before Note HOlder: " + possibleNotes.Length);
            foreach (NoteHolder n in possibleNotes)
            {
                switch (n.type)
                {
                    case "D":
                        bass.AddNote(new Note(n.measure, n.beat, n.pitch, n.length));
                        break;

                    case "S":
                        int test = gen.Next(0, (int)(25 / valence));
                        if (test == 0)
                        {
//                            bass.AddNote(new Note(n.measure, n.beat, n.pitch, n.length));
                        }
                        break;
                }
            }
            
        }
        private void DeriveRhythm(float energy, float valence, int seed)
        {
            gen = new System.Random(seed);


            drummer = new DrumManagerManager();

            int mCount = 0;
            NoteHolder[] possibleNotes = new NoteHolder[0];
            foreach (RhythmTracker.Measure m in r.GetMeasures())
            {
                mCount++;
                int strongBeats = 0, currentBeat = 0;
                for (int i = 0; i < m.beats.Length; ++i)
                {
                    switch (m.beats[i])
                    {
                        case BeatClass.D:
                        case BeatClass.S:
                            strongBeats++;
                            break;
                    }
                }

                if (mCount == r.GetMeasures().ToArray().Length)
                {
                    int countStrongBeats = 0, i = 0;
                    bool timeToBreak = false;
                    while (true)
                    {
                        switch (m.beats[i])
                        {
                            case BeatClass.D:
                                if (countStrongBeats < strongBeats / 2)
                                {
                                    countStrongBeats++;
                                    possibleNotes = possibleNotes.Append(new NoteHolder(mCount, i + 1, type: "B")).ToArray();
                                }
                                else
                                {
                                    timeToBreak = true;
                                }
                                break;

                            case BeatClass.S:
                                if (countStrongBeats < strongBeats / 2)
                                {
                                    countStrongBeats++;
                                    if (currentBeat % 2 != 0 && (currentBeat != strongBeats || strongBeats % 2 == 0))
                                        possibleNotes = possibleNotes.Append(new NoteHolder(mCount, i + 1, type: "B")).ToArray();
                                    else
                                        possibleNotes = possibleNotes.Append(new NoteHolder(mCount, i + 1, type: "S")).ToArray();
                                }
                                else
                                {
                                    timeToBreak = true;
                                }
                                break;
                        }
                        if (timeToBreak)
                        {
                            i++;
                            break;
                        }

                        possibleNotes = possibleNotes.Append(new NoteHolder(mCount, i + 1, type: "H")).ToArray();
                        i++;
                    }

                    GetFill(mCount, i, m);
                }
                else
                {
                    for (int i = 0; i < m.beats.Length; ++i)
                    {
                        switch (m.beats[i])
                        {
                            case BeatClass.D:
                                currentBeat++;
                                possibleNotes = possibleNotes.Append(new NoteHolder(mCount, i + 1, type: "B")).ToArray();
                                break;

                            case BeatClass.S:
                                currentBeat++;
                                if (currentBeat % 2 != 0 && (currentBeat != strongBeats || strongBeats % 2 == 0))
                                    possibleNotes = possibleNotes.Append(new NoteHolder(mCount, i + 1, type: "B")).ToArray();
                                else
                                    possibleNotes = possibleNotes.Append(new NoteHolder(mCount, i + 1, type: "S")).ToArray();

                                break;
                        }

                        possibleNotes = possibleNotes.Append(new NoteHolder(mCount, i + 1, type: "H")).ToArray();
                    }
                }
            }

            // Debug.Log("Before Note HOlder: " + possibleNotes.Length);
            foreach (NoteHolder n in possibleNotes)
            {
                int test = gen.Next(0, (int)(25 / valence));
                //Debug.Log("Test: " + test + "; valence: " + valence);
                switch (n.type)
                {
                    case "B":
                        drummer.BassHit(n.measure, n.beat);
                        if (test == 0)
                        {
                            drummer.BassHit(n.measure, n.beat + .5f);
                        }
                        break;

                    case "S":
                        drummer.SnareHit(n.measure, n.beat);
                        if (test == 0)
                        {
                            drummer.SnareHit(n.measure, n.beat + .5f);
                        }
                        break;

                    case "H":
                        drummer.HighHatHit(n.measure, n.beat);
                        if (test == 0)
                        {
                            drummer.HighHatHit(n.measure, n.beat + .5f);
                        }
                        break;
                }
            }
        }

        private void GetFill(int measure, int startBeat, RhythmTracker.Measure m)
        {

            //Fill
            for (int i = startBeat; i <= m.beats.Length; ++i)
            {
                drummer.SnareHit(measure, i);
                drummer.SnareHit(measure, i + .25f);
                drummer.SnareHit(measure, i + .5f);
                drummer.SnareHit(measure, i + .75f);
            }

        }


        public float[] getNotes(int pos, int dur)
        {
//            Debug.Log("GetNotes Dur: " + dur);
            float[][] i = new float[0][];
            foreach (InstrumentManager im in ins)
            {
                if (!(im is null))
                {
                    i = i.Append(im.GetStream(pos, dur)).ToArray();
                }
            }
            return (PreVolumeMixer.Mix(i));
        }

        public void getNotes(int pos, int dur, int start, ref float[] output)
        {
            //            Debug.Log("GetNotes Dur: " + dur);
            float[][] i = new float[0][];
            foreach (InstrumentManager im in ins)
            {
                if (!(im is null))
                {
                    i = i.Append(im.GetStream(pos, dur)).ToArray();
                }
            }
            PreVolumeMixer.Mix(start, ref output, i);
        }

        public float[] Compile()
        {
            float[][] i = new float[0][];
            foreach (InstrumentManager im in ins)
            {
                if (!(im is null))
                {
                    i = i.Append(im.GetStream()).ToArray();
                }
            }
            return (PreVolumeMixer.Mix(i));
        }

        public string GetOddRhythm(float energy, float valence)
        {
            Dictionary<string, double> oddRhythms = new Dictionary<string, double>();
            oddRhythms[" D W W S W "] = .05;
            oddRhythms[" D W S W W "] = .05;
            oddRhythms[" D W S W S "] = .05;
            oddRhythms[" D W S W S W S "] = .05;
            oddRhythms[" D W S W S W W "] = .05;
            oddRhythms[" D W S W W S W "] = .05;
            oddRhythms[" D W W S W S W "] = .05;
            oddRhythms[" D W W S W W S "] = .05;
            oddRhythms[" D W S W S W S W S W S "] = .05;
            oddRhythms[" D W S W S W S W S W W "] = .05;
            oddRhythms[" D W S W S W S W W S W "] = .05;
            oddRhythms[" D W S W S W W S W S W "] = .05;
            oddRhythms[" D W S W W S W S W S W "] = .05;
            oddRhythms[" D W W S W S W S W S W "] = .05;
            oddRhythms[" D W W S W W S W S W W "] = .05;
            oddRhythms[" D W W S W W S W W S W "] = .05;
            oddRhythms[" D W W S W S W W S W W "] = .05;
            oddRhythms[" D W S W W S W W S W W "] = .05;
            oddRhythms[" D W S W S W W S W W S "] = .05;
            oddRhythms["odd"] = .05;
            string test = Score.DecideProb(oddRhythms, gen.NextDouble(), "odd");
            if (test != "odd")
            {
                return (test);
            }

            StringBuilder retval = new StringBuilder();
            Dictionary<string, double> start = new Dictionary<string, double>(), remain = new Dictionary<string, double>();
            start[" D W W"] = .5;
            start[" D W"] = .5;

            retval.Append(Score.DecideProb(start, gen.NextDouble(), " D W W "));

            remain["eps"] = .01;
            remain[" S W"] = .3;
            remain[" S W W"] = .3;
            remain[" D W"] = .19;
            remain[" D W W"] = .19;
            remain["Seps"] = .01;

            test = Score.DecideProb(remain, gen.NextDouble(), "S W ");

            while (test != "eps")
            {
                if (test == "Seps")
                {
                    retval.Append(" S");
                    break;
                }
                retval.Append(test);

                if (test[1] == 'S')
                {
                    remain["eps"] += .05;
                    remain[" S W"] -= .08;
                    remain[" S W W"] -= .08;
                    remain[" D W"] += .03;
                    remain[" D W W"] += .03;
                    remain["Seps"] += .05;
                }
                else
                {
                    remain["eps"] -= .01;
                    remain[" S W"] += .09;
                    remain[" S W W"] += .09;
                    remain[" D W"] -= .08;
                    remain[" D W W"] -= .08;
                    remain["Seps"] -= .01;
                }
                test = Score.DecideProb(remain, gen.NextDouble(), "eps");
            }
            retval.Append(" ");
            return (retval.ToString());
        }

        private class NoteHolder
        {
            public int measure;
            public float beat, length, pitch;
            public string type;

            public NoteHolder(int measure, float beat, float length = 0, float pitch = Notes.REST, string type = "")
            {
                this.measure = measure;
                this.beat = beat;
                this.length = length;
                this.pitch = pitch;
                this.type = type;
            }
        }

    }


    class Section2
    {
        public RhythmTracker r;

        public Dictionary<int, Dictionary<int, int>> rhythms; // Valence, then energy, as energy will (currently) make the most difference...
        private protected Dictionary<int, int> rhythmLinks;
        private protected RhythmTracker[] rhythmStack;
        public InstrumentManager[] ins;
        public DrumManagerManager drummer;
        private protected Dictionary<int, Dictionary<int, List<Note>>>[][] notes; // Wait, we need one for each valence or energy value too... And now each rhythm...
        private protected Dictionary<int, Dictionary<int, List<Note>>>[] staticNotes; //Only for melody, for now...
        string name;
        int sectionSeed;
        private protected double speed;
        int holdNote;
        public int dur { get; private set; }
        public int measures { get; private set; }

        private protected System.Random gen;

        private Section2()
        {

        }

        public Section2(int seed = 0, string n = "", double speed = 120)
        {
            sectionSeed = seed;
            name = n;
            gen = new System.Random(seed);
            //Debug.Log("Section Seed: " + seed);
            ins = new InstrumentManager[12];
            dur = 0;
            this.speed = speed;
            this.holdNote = 0;
            //notes = new Dictionary<int, Dictionary<int, List<Note>>>[13][];
            //for (int i = 0; i < notes.Length; ++i)
            //{
            //    notes[i] = new Dictionary<int, Dictionary<int, List<Note>>>();
            //}
        }

        private protected void addNote(Dictionary<int, Dictionary<int, List<Note>>> dict, int key1, int key2, Note note, bool copyOld = true)
        {
            if (!dict.ContainsKey(key1))
            {
                dict.Add(key1, new Dictionary<int, List<Note>>());
                if (copyOld)
                {
                    int prevKey = GetMin(dict, key1 - 1);
                    if (prevKey > -1)
                    {
                        foreach (int i in dict[prevKey].Keys)
                        {
                            dict[key1].Add(i, new List<Note>());
                            foreach (Note n in dict[prevKey][i])
                            {
                                dict[key1][i].Add(n);
                            }
                        }
                    }
                }
            }
            if (!dict[key1].ContainsKey(key2))
            {
                dict[key1].Add(key2, new List<Note>());
                if (copyOld)
                {
                    int prevKey = GetMin(dict[key1], key2 - 1);
                    if (prevKey > -1)
                    {
                        foreach (Note n in dict[key1][prevKey])
                        {
                            dict[key1][key2].Add(n);
                        }
                    }
                }
            }
            dict[key1][key2].Add(note);
        }

        private protected void addNoteToRangeOfVals(Dictionary<int, Dictionary<int, List<Note>>> dict, int key1, int maxKey1, int key2, Note note, bool copyOld = true)
        {
            addNote(dict, key1, key2, note, copyOld);
            foreach (int testVal in dict.Keys)
            {
                if (testVal <= key1 || testVal > maxKey1)
                {
                    continue;
                }
                if (dict[testVal].ContainsKey(key2))
                {
                    dict[testVal][key2].Add(note);
                }
            }
        }
        private protected void addToDict<T>(Dictionary<int, Dictionary<int, List<T>>> dict, int key1, int key2, T note, bool copyOld = true)
        {
            if (!dict.ContainsKey(key1))
            {
                dict.Add(key1, new Dictionary<int, List<T>>());
                if (copyOld)
                {
                    int prevKey = GetMin(dict, key1 - 1);
                    if (prevKey > -1)
                    {
                        foreach (int i in dict[prevKey].Keys)
                        {
                            dict[key1].Add(i, new List<T>());
                            foreach (T n in dict[prevKey][i])
                            {
                                dict[key1][i].Add(n);
                            }
                        }
                    }
                }
            }
            if (!dict[key1].ContainsKey(key2))
            {
                dict[key1].Add(key2, new List<T>());
                if (copyOld)
                {
                    int prevKey = GetMin(dict[key1], key2 - 1);
                    if (prevKey > -1)
                    {
                        foreach (T n in dict[key1][prevKey])
                        {
                            dict[key1][key2].Add(n);
                        }
                    }
                }
            }
            dict[key1][key2].Add(note);
        }
        private protected int GetMin<T>(Dictionary<int, T> d, int valToFind)
        {
            if (d.ContainsKey(valToFind))
            {
                return valToFind;
            }
            int hold = -1;
            foreach (int testKey in d.Keys.Reverse())
            {
                if (testKey <= valToFind && testKey > hold)
                {
                    hold = testKey;
                }
            }
            return (hold);
        }

        private protected int GetNextKey<T>(Dictionary<int, T> d, int keyToFind)
        {
            if (d.ContainsKey(++keyToFind))
            {
                return (keyToFind);
            }
            int hold = d.Keys.Max();
            foreach (int testKey in d.Keys.Reverse())
            {
                if (testKey >= keyToFind && testKey < hold)
                {
                    hold = testKey;
                }
            }
            return (hold);
        }

        private protected void DeepCopy<T>(Dictionary<int, T> from, Dictionary<int, T> to)
        {
            to = new Dictionary<int, T>(from);
        }

        public void DeriveSection(float energy, float valence)
        {
            int melodySeed = gen.Next();
            int harmSeed = gen.Next();
            int rhythmSeed = gen.Next();
            int testVal = gen.Next(0, 6);
            DecideTimeSignature2();
            switch (testVal)
            {
                case 0:
                    DeriveMelody(energy, valence, melodySeed);
                    DeriveHarmony(energy, valence, harmSeed);
                    DeriveRhythm(energy, valence, rhythmSeed);
                    break;

                case 1:
                    DeriveMelody(energy, valence, melodySeed);
                    DeriveRhythm(energy, valence, rhythmSeed);
                    DeriveHarmony(energy, valence, harmSeed);
                    break;

                case 2:
                    DeriveHarmony(energy, valence, harmSeed);
                    DeriveMelody(energy, valence, melodySeed);
                    DeriveRhythm(energy, valence, rhythmSeed);
                    break;

                case 3:
                    DeriveHarmony(energy, valence, harmSeed);
                    DeriveRhythm(energy, valence, rhythmSeed);
                    DeriveMelody(energy, valence, melodySeed);
                    break;

                case 4:
                    DeriveRhythm(energy, valence, rhythmSeed);
                    DeriveMelody(energy, valence, melodySeed);
                    DeriveHarmony(energy, valence, harmSeed);
                    break;

                default:
                    DeriveRhythm(energy, valence, rhythmSeed);
                    DeriveHarmony(energy, valence, harmSeed);
                    DeriveMelody(energy, valence, melodySeed);
                    break;
            }
        }

        private protected void DecideTimeSignature(float energy, float valence)
        {/*
                if (name == "A")
                    r = new RhythmTracker(Constants.freq, "120 D W S W S W S W 4");
                else
                {
                    r = new RhythmTracker(Constants.freq, "240 D W W S W W S W 4");
                }
                */
            string tracker = "";

            // Decide speed
            // a 1 = 60-70 bpm; a 5 = 180-200 bpm
            // double d = (gen.NextDouble() * (29.375 + .625 * energy) + (28.125 + 1.875 * energy)) * 2;
            //double d = (57.5 + 2.5 * energy) * 2;
            double d = speed * 2;

            Dictionary<int, double> measureProb = new Dictionary<int, double>();
            Dictionary<string, double> beatsProb = new Dictionary<string, double>();
            // Decide signatures
            int numMeasures = 0;
            double numMeasuresDecider = gen.NextDouble();
            if (valence < 4.0)
            {
                measureProb[1] = 1.0 / 28;
                measureProb[2] = 4.0 / 28;
                measureProb[4] = 9.0 / 28;
                measureProb[8] = 9.0 / 28;
                measureProb[16] = 4.0 / 28;
                measureProb[32] = 1.0 / 28;
                numMeasures = Score.DecideProb(measureProb, gen.NextDouble(), 4);

                // This should be .5 to .25 from 1 to 5 energy.
                //                    beatsProb[" D W S W S W "] = .5625f - .0625f * energy;
                beatsProb[" D W S W S W "] = .28125f - .03125f * energy;
                beatsProb[" D W W S W W "] = .28125f - .03125f * energy;
                beatsProb[" D W S W S W S W "] = .5 - beatsProb[" D W S W S W "];
                beatsProb[" D W W S W W S W "] = beatsProb[" D W S W S W S W "];

                tracker = d.ToString();
                tracker += Score.DecideProb(beatsProb, gen.NextDouble(), " D W S W S W S W ");
                tracker += numMeasures.ToString();
            }
            else
            {
                measureProb[1] = 9.0 / 280;
                measureProb[2] = 40.0 / 280;
                measureProb[4] = 90.0 / 280;
                measureProb[8] = 90.0 / 280;
                measureProb[16] = 40.0 / 280;
                measureProb[32] = 9.0 / 280;
                measureProb[-1] = 2.0 / 280;
                numMeasures = Score.DecideProb(measureProb, gen.NextDouble(), 4);
                if (numMeasures == -1)
                {
                    measureProb[-1] = 0;
                    for (int i = 1; i <= 32; ++i)
                    {
                        measureProb[i] = 1.0 / 31;
                    }
                    numMeasures = Score.DecideProb(measureProb, gen.NextDouble(), 8);
                }

                int currMeasure = 0;
                while (currMeasure < numMeasures)
                {
                    if (currMeasure > 0)
                    {
                        tracker += "\n";
                    }
                    string rhythm = "";
                    int countMeasure = 1;
                    // This should be .5 to .25 from 1 to 5 energy.
                    //                    beatsProb[" D W S W S W "] = .5625f - .0625f * energy;
                    beatsProb[" D W S W S W "] = .28125f - .03125f * energy - .125 * (energy - 4);
                    beatsProb[" D W W S W W "] = beatsProb[" D W S W S W "];
                    beatsProb[" D W S W S W S W "] = .21875 + .03125f * energy - .25 * (energy - 4);
                    beatsProb[" D W W S W W S W "] = beatsProb[" D W S W S W S W "];
                    beatsProb["odd"] = .5 * (energy - 4);
                    d = speed * 2;

                    rhythm = Score.DecideProb(beatsProb, gen.NextDouble(), "odd");
                    if (rhythm == "odd")
                    {
                        rhythm = GetOddRhythm();
                        countMeasure = rhythm.Count(b => b == 'D');
                        if (gen.NextDouble() < 3 * (energy - 4) / 4)
                        {
                            d = speed * 4;
                        }
                    }
                    int reps = 1;
                    while (currMeasure + reps * countMeasure < numMeasures && gen.NextDouble() < 1 - .05 * energy)
                    {
                        reps++;
                    }
                   // Debug.Log(rhythm);
                    tracker += d + rhythm + " " + reps;
                    currMeasure += reps * countMeasure;
                }
            }
            // Debug.Log(tracker);
            r = new RhythmTracker(Constants.freq, tracker);
            InstrumentManager silent = new InstrumentManager(new SineWaveInstrument(Constants.freq, 0f), r);
            ins[0] = silent;
            int mCount = 0;
            int lastbeat = 0;
            foreach (RhythmTracker.Measure m in r.GetMeasures())
            {
                mCount++;
                // Debug.Log("Beats: " + m.beats.Length);
                //addNote(notes[0], 0, 0, new Note(mCount, m.beats.Length, Notes.REST, 1));
                silent.AddNote(new Note(mCount, m.beats.Length, Notes.REST, 1));
                lastbeat = m.beats.Length;
            }
            dur = (int)(r.GetTime(mCount, lastbeat + 1) * Constants.freq);
            measures = mCount;
            //            Debug.Log("Last: " + mCount + " " + lastbeat + " " + dur);
        }
        private protected void DecideTimeSignature2()
        {
            //string tracker = "";
            Dictionary<int, Dictionary<int, int>> trackers = new Dictionary<int, Dictionary<int, int>>(), oldTrackers = trackers;
            Dictionary<int, Dictionary<int, int>> currMeasures = new Dictionary<int, Dictionary<int, int>>(), oldMeasures = currMeasures;
            Dictionary<string, int> tempTrackersLink = new Dictionary<string, int>();
            List<int[]> tempTrackersStats = new List<int[]>();
            List<string> tempTrackersStack = new List<string>();
            Dictionary<int, int> measures = new Dictionary<int, int>();
            Dictionary<int, int[]> rhythmStuff;
            // Decide speed
            double d = speed * 2;

            Dictionary<int, double> measureProb = new Dictionary<int, double>();
            double numMeasuresDecider = gen.NextDouble();

            if (numMeasuresDecider < 1.0 / 28)
            {
                if (numMeasuresDecider < 1.0 / 280)// impossible || numMeasuresDecider >= 1 - 1.0 / 28)
                {
                    measures[1000] = 1;
                    measureProb[-1] = 0;
                    for (int i = 1; i <= 32; ++i)
                    {
                        measureProb[i] = 1.0 / 31;
                    }
                    measures[4] = Score.DecideProb(measureProb, gen.NextDouble(), 8);
                }
                else
                {
                    measures[0] = 1;
                }
            }
            else if (numMeasuresDecider >= 1 - 1.0 / 28)
            {
                if (numMeasuresDecider >= 1 - 1.0 / 280)
                {
                    measures[1000] = 32;
                    measureProb[-1] = 0;
                    for (int i = 1; i <= 32; ++i)
                    {
                        measureProb[i] = 1.0 / 31;
                    }
                    measures[4] = Score.DecideProb(measureProb, gen.NextDouble(), 8);
                }
                else
                {
                    measures[0] = 32;
                }
            }
            else if (numMeasuresDecider < 5.0 / 28)
            {
                measures[0] = 2;
            }
            else if (numMeasuresDecider < 14.0 / 28)
            {
                measures[0] = 4;
            }
            else if (numMeasuresDecider < 23.0 / 28)
            {
                measures[0] = 8;
            }
            else if (numMeasuresDecider < 27.0 / 28)
            {
                measures[0] = 4;
            }
            // Decide signatures
            // This should be .5 to .25 from 1 to 5 energy.
            /*             //                    beatsProb[" D W S W S W "] = .5625f - .0625f * energy;
                         beatsProb[" D W S W S W "] = .28125f - .03125f * energy - .125 * (valence - 4);    // .25 - .125 / .125 - 0   / 0 - .25 / 0 - .125 
                         beatsProb[" D W W S W W "] = beatsProb[" D W S W S W "];                            // .25 - .125 / .125 - 0   / .25 - .5 / .125 - .25
                         beatsProb[" D W S W S W S W "] = .21875 + .03125f * energy - .125 * (valence - 4); // .25 - .375 / .125 - .25 / .5 - .75 / .25 - .625
                         beatsProb[" D W W S W W S W "] = beatsProb[" D W S W S W S W "];                    // .25 - .375 / .125 - .25 / .75 - 1 / .625 - 1
                         beatsProb["odd"] = .5 * (valence - 4);                                              // 0 - .5
            */
            //                double tempDouble;
            //                int tempInt;
            // Give indexes for each rhythm; get notes for each set, all valences and energys.
            int currMeasure = 0;
            bool doContinue = false;
            do
            {
                oldTrackers = new Dictionary<int, Dictionary<int, int>>(trackers);
                oldMeasures = new Dictionary<int, Dictionary<int, int>>(currMeasures);
                ++currMeasure;
                doContinue = false;
                double beatsDecider = gen.NextDouble();
    //            Dictionary<int, Dictionary<int, string>> holdRhythms = new Dictionary<int, Dictionary<int, string>>();
     //           holdRhythms[0] = new Dictionary<int, string>();
               // Dictionary<string, string> rhythms = new Dictionary<string, string>();
                int oddRhythmSeed = gen.Next(), repsSeed = gen.Next();
                string oddRhythm = GetOddRhythm(new System.Random(oddRhythmSeed));
                rhythmStuff = GetEnergyRhythmStuff(repsSeed);

              //  Dictionary<sting, int[]> 
                int next = 0;
                if (beatsDecider < .25)
                {
//                    holdRhythms[0][0] = " D W S W S W ";
                    next = 5000;
                    if (beatsDecider >= .125)
                    {
                        next = (int)Math.Ceiling(9000 - 32000 * beatsDecider);
                        //                        tempDouble = (9 - 32 * beatsDecider);
                        //                        tempInt = (int)Math.Ceiling(tempDouble * 1000);
                        AddLine(0, next, 5000, " D W W S W W ");
//                        holdRhythms[0][(int)Math.Ceiling(9000 - 32000 * beatsDecider)] = " D W W S W W ";
                    }
                    AddLine(0, 0, next, " D W S W S W ");
                }
                else if (beatsDecider < .5)
                {
                    next = (int)Math.Ceiling(9000 - 16000 * beatsDecider);
                    AddLine(0, next, 5000, " D W S W S W S W ");
                    AddLine(0, 0, next, " D W W S W W ");
//                    holdRhythms[0][0] = " D W W S W W ";
                    //                    tempDouble = 9 - 16 * beatsDecider;
//                    holdRhythms[0][(int)Math.Ceiling(9000 - 16000 * beatsDecider)] = " D W S W S W S W ";
                }
                else if (beatsDecider < .75)
                {
                    next = 5000;
//                    holdRhythms[0][0] = " D W S W S W S W ";
                    if (beatsDecider >= .625)
                    {
                        next = (int)Math.Ceiling(25000 - 32000 * beatsDecider);
                        //tempDouble = 25 - 32 * beatsDecider;
                        //                      holdRhythms[0][(int)Math.Ceiling(25000 - 32000 * beatsDecider)] = " D W W S W W S W ";
                        AddLine(0, next, 5000, " D W W S W W S W ");
                    }
                    AddLine(0, 0, next, " D W S W S W S W ");
                }
                else
                {
                    //                    holdRhythms[0][0] = " D W W S W W S W ";
                    AddLine(0, 0, 5000, " D W W S W W S W ");
                }
                /**/
                for (int tempInt = 4000; tempInt <= 5000; ++tempInt)
//                for (double v = 0; v <= 1; v += .001)
                {
                    double v = (tempInt - 4000) / 1000.0;
                    //holdRhythms[tempInt] = new Dictionary<int, string>();
                    if (beatsDecider < .25 - v * .125)
                    {
                        next = 5000;
                      //  holdRhythms[tempInt][0] = " D W S W S W ";
                        if (beatsDecider >= .125 - v * .125)
                        {
                            //                        tempDouble = (9 - 32 * beatsDecider);
                            //                        tempInt = (int)Math.Ceiling(tempDouble * 1000);
                            next = (int)Math.Ceiling(9000 - 32000 * beatsDecider - 4000 * v);
                            // holdRhythms[tempInt][(int)Math.Ceiling(9000 - 32000 * beatsDecider - 4000 * v)] = " D W W S W W ";
                            AddLine(tempInt, next, 5000, " D W W S W W ");
                        }
                        AddLine(tempInt, 0, 5000, " D W S W S W ");
                    }
                    else if (beatsDecider < .5 - v * .25)
                    {
                        next = (int)Math.Ceiling(9000 - 16000 * beatsDecider - 4000 * v);
                        // holdRhythms[tempInt][0] = " D W W S W W ";
                        //                    tempDouble = 9 - 16 * beatsDecider;
                        // holdRhythms[tempInt][(int)Math.Ceiling(9000 - 16000 * beatsDecider - 4000 * v)] = " D W S W S W S W ";
                        AddLine(tempInt, 0, next, " D W W S W W ");
                        AddLine(tempInt, next, 5000, " D W S W S W S W ");
                    }
                    else if (beatsDecider < .75 - v * .375)
                    {
                        next = 5000;
                        //holdRhythms[tempInt][0] = " D W S W S W S W ";
                        if (beatsDecider >= .625 - v * .375)
                        {
                            next = (int)Math.Ceiling(25000 - 32000 * beatsDecider);
                            //tempDouble = 25 - 32 * beatsDecider;
                            // holdRhythms[tempInt][(int)Math.Ceiling(25000 - 32000 * beatsDecider)] = " D W W S W W S W ";
                            AddLine(tempInt, next, 5000, " D W W S W W S W ");
                        }
                        AddLine(tempInt, 0, next, " D W S W S W S W ");
                    }
                    else if (beatsDecider < 1 - v * .5)
                    {
                        // holdRhythms[tempInt][0] = " D W W S W W S W ";
                        AddLine(tempInt, 0, 5000, " D W W S W W S W ");
                    }
                    else
                    {
//                        holdRhythms[tempInt][0] = "odd";
                        AddLine(tempInt, 0, 5000, oddRhythm);
                    }

                }
                //Debug.Log("Created strings");
                // Create Rhythms
                

            } while (doContinue && currMeasure <= 50);// currMeasure < numMeasures);

            if (currMeasure > 50)
            {
                //Debug.Log("May have broken out due to currMeasures...");
            }
                
            rhythms = new Dictionary<int, Dictionary<int, int>>();
            rhythmLinks = new Dictionary<int, int>();
            rhythmStack = new RhythmTracker[tempTrackersLink.Count];
            notes = new Dictionary<int, Dictionary<int, List<Note>>>[tempTrackersLink.Count][];
            int j = 0;
            foreach (int index in tempTrackersLink.Values)
            {
                rhythmStack[j] = new RhythmTracker(Constants.freq, tempTrackersStack[index]);
                rhythmLinks[index] = j;
                notes[j] = new Dictionary<int, Dictionary<int, List<Note>>>[13];
                for (int k = 0; k < notes[j].Length; ++k)
                {
                    notes[j][k] = new Dictionary<int, Dictionary<int, List<Note>>>();
                }

                int mCount = 0;
                int lastbeat = 0;
                foreach (RhythmTracker.Measure m in rhythmStack[j].GetMeasures())
                {
                    mCount++;
                    //Debug.Log("Beats: " + m.beats.Length);
                    addNote(notes[j][0], 0, 0, new Note(mCount, m.beats.Length, Notes.REST, 1), false);
                    lastbeat = m.beats.Length;
                   // Debug.Log("LastBeat: " + lastbeat);
                }
                dur = (int)(rhythmStack[j].GetTime(mCount, lastbeat + 1) * Constants.freq);
                if (mCount > this.measures)
                {
                    this.measures = mCount;
                }
                //Debug.Log("End of loop " + j);
                j++;
            }
            foreach (int val in trackers.Keys)
            {
                rhythms[val] = new Dictionary<int, int>();
                foreach (int aro in trackers[val].Keys)
                {
                    // Debug.Log("Did this get executed?");
                    //Debug.Log("Linking: " + val + "/" + aro);
                    int rawIndex = rhythmLinks[trackers[val][aro]];
                    rhythms[val][aro] = trackers[val][aro];
                    r = rhythmStack[rawIndex];
                    //Debug.Log(r);
                }
            }
            //            Debug.Log("Last: " + mCount + " " + lastbeat + " " + dur);

            void AddLine(int valence, int energy, int nextKey, string rhythm)
            {
                int holdVal = GetMin(oldTrackers, valence);
                int holdAro, measureAro;
                int currIndex = -1;
                measureAro = GetMin(measures, energy);
                string tempAddition = "";

                // Does this need to be included in rhythm stuff checks?
                if (holdVal != -1)
                {
                    holdAro = GetMin(oldTrackers[holdVal], energy);
                    if (holdAro != -1)
                    {
                        if (measures[measureAro] <= oldMeasures[holdVal][holdAro])
                        {
                            return;
                        }
                    }
                }

                if (!trackers.ContainsKey(valence))
                {
                    if (holdVal == -1)
                    {
                        trackers.Add(valence, new Dictionary<int, int>());
                        currMeasures.Add(valence, new Dictionary<int, int>());
                    }
                    else
                    {
                        trackers[valence] = new Dictionary<int, int>(oldTrackers[holdVal]);
                        currMeasures[valence] = new Dictionary<int, int>(oldMeasures[holdVal]);
                        foreach (int i in trackers[valence].Values)
                        {
                            //Debug.Log("Tracker index in counter update: " + i);
                            tempTrackersStats[i][1]++;
                        }
                    }
                }

                int numMeasures = measures[measureAro];
                if (!trackers[valence].ContainsKey(energy))
                {
                    holdAro = GetMin(trackers[valence], energy);
                    if (holdAro == -1 || holdVal == -1)
                    {
                        trackers[valence].Add(energy, -1);
                        currMeasures[valence].Add(energy, 0);
                    }
                    else
                    {
                        //Debug.Log("Valence/energy/holdAro/index: " + valence + "/" + energy + "/" + holdAro + "/" + trackers[valence][holdAro]);
                        trackers[valence][energy] = trackers[valence][holdAro];
                        currMeasures[valence][energy] = currMeasures[valence][holdAro];
                        tempTrackersStats[trackers[valence][energy]][1]++;
                    }
                }

                currIndex = trackers[valence][energy];

                if (currMeasures[valence][energy] > 0)
                {
                    //                    trackers[valence][energy] += "\n";
                    tempAddition += "\n";
                }
                int countMeasure = rhythm.Count(b => b == 'D');

                int reps = 1;
                int holdKey = GetMin(rhythmStuff, energy);
                //Debug.Log("Holdkey is " + holdKey);
                //Still need to split out rhythm stuff...
                d = rhythmStuff[holdKey][0] * speed;
                while (currMeasures[valence][energy] + reps * countMeasure < numMeasures && reps < rhythmStuff[holdKey][1])
                {
                    reps++;
                }
                //                        Debug.Log(rhythm);
                // trackers[valence][energy] += d + rhythm + " " + reps;
                tempAddition +=  d + rhythm + " " + reps;

                currMeasures[valence][energy] += reps * countMeasure;
                if (currMeasures[valence][energy] < numMeasures)
                {
                    //need to finish...
                    doContinue = true;
                }
                if (currIndex == -1)
                {
                    trackers[valence][energy] = AddIndex(valence, energy, tempAddition);
                }
                else
                {
                    trackers[valence][energy] = AddIndex(valence, energy, tempTrackersStack[currIndex] + tempAddition);
                }

                //Debug.Log("Holdkey " + holdKey + " worked.");
                if (energy < nextKey)
                {
                    foreach (int aroOther in rhythmStuff.Keys)
                    {
                        if (aroOther > energy && aroOther <= nextKey)
                        {
                            //Debug.Log("aroOther is " + aroOther);
                            if (!trackers[valence].ContainsKey(aroOther))
                            {
                                //Debug.Log("In Trackers: " + valence + "/" + aroOther + "/" + holdVal);
                                if (holdVal == -1)
                                {
                                    trackers[valence].Add(aroOther, -1);
                                    currMeasures[valence].Add(aroOther, 0);
                                }
                                else
                                {
                                    int temp = GetMin(oldTrackers[holdVal], aroOther);
                                    //Debug.Log("Temp: " + temp);
                                    if (temp == -1)
                                    {
                                        trackers[valence].Add(aroOther, -1);
                                        currMeasures[valence].Add(aroOther, 0);
                                    }
                                    else
                                    {
                                        trackers[valence][aroOther] = oldTrackers[holdVal][temp];
                                        currMeasures[valence][aroOther] = oldMeasures[holdVal][temp];
                                        tempTrackersStats[trackers[valence][aroOther]][1]++;
                                    }
                                }
                            }
                            //Debug.Log("Before CurrIndex: " + valence);
                            currIndex = trackers[valence][aroOther];
                            tempAddition = "";
                            if (currMeasures[valence][aroOther] > 0)
                            {
                                tempAddition += "\n";
                            }
                            countMeasure = rhythm.Count(b => b == 'D');
                            reps = 1;
                            //Debug.Log("Before Reps: " + valence);
                            d = rhythmStuff[aroOther][0] * speed * 2;
                            while (currMeasures[valence][aroOther] + reps * countMeasure < numMeasures && reps < rhythmStuff[holdKey][1])
                            {
                                reps++;
                            }
                            //                        Debug.Log(rhythm);
                            tempAddition += d + rhythm + " " + reps;
                            currMeasures[valence][aroOther] += reps * countMeasure;
                            if (currMeasures[valence][aroOther] < numMeasures)
                            {
                                //need to finish...
                                doContinue = true;
                            }
                            //Debug.Log("aroOther " + aroOther + " worked.");
                            if (currIndex == -1)
                            {
                                trackers[valence][aroOther] = AddIndex(valence, aroOther, tempAddition);
                            }
                            else
                            {
                                trackers[valence][aroOther] = AddIndex(valence, aroOther, tempTrackersStack[currIndex] + tempAddition);
                            }
                        }
                    }
                }
            }

            int AddIndex(int valence, int energy, string rhythm)
            {
                int currIndex = trackers[valence][energy];
                if (currIndex > -1)
                {
                    tempTrackersStats[currIndex][1]--;
                    if (tempTrackersStats[currIndex][1] == 0)
                    {
                        tempTrackersLink.Remove(tempTrackersStack[currIndex]);
                    }
                }

                if (tempTrackersLink.ContainsKey(rhythm))
                {
                    currIndex = tempTrackersLink[rhythm];
                    trackers[valence][energy] = currIndex;
                    tempTrackersStats[currIndex][1]++;
                    if (valence < tempTrackersStats[currIndex][2])
                    {
                        tempTrackersStats[currIndex][2] = valence;
                    }
                    if (valence > tempTrackersStats[currIndex][3])
                    {
                        tempTrackersStats[currIndex][3] = valence;
                    }
                    if (energy < tempTrackersStats[currIndex][4])
                    {
                        tempTrackersStats[currIndex][4] = energy;
                    }
                    if (energy > tempTrackersStats[currIndex][5])
                    {
                        tempTrackersStats[currIndex][5] = energy;
                    }
                }
                else
                {
                    tempTrackersStack.Add(rhythm);
                    currIndex = tempTrackersStack.Count - 1;
                    tempTrackersStats.Add(new int[] {0, 1, valence, valence, energy, energy});
                    tempTrackersLink.Add(rhythm, currIndex);
                }

                return (currIndex);
            }
        }

        private protected Dictionary<int, int[]> GetEnergyRhythmStuff(int seed, int numMeasures = 32)
        {
            Dictionary<int, int[]> dict = new Dictionary<int, int[]>();
            System.Random gen = new System.Random(seed);

            dict[0] = new int[2];
            dict[0][0] = 1;

            double temp = gen.NextDouble();
            int aroKey = (int)Math.Ceiling(4000 * (temp / 3 + 1));
            dict[aroKey] = new int[2];

            dict[aroKey][0] = 2;
            dict[aroKey][1] = 1;
            int holdAro = 5001, aroKey2;
            for (int i = 0; i < numMeasures; ++i)
            {
                aroKey2 = (int) Math.Ceiling(20000 * (1 - gen.NextDouble()));
                if (aroKey2 < holdAro)
                {
                    if (aroKey2 < 1000)
                    {
                        break;
                    }

                    if (!dict.ContainsKey(aroKey2))
                    {
                        dict[aroKey2] = new int[2];
                        if (aroKey2 >= aroKey)
                        {
                            dict[aroKey2][0] = 2;
                        }
                        else
                        {
                            dict[aroKey2][0] = 1;
                        }
                        dict[aroKey2][1] = dict[0][1];
                    }
                    holdAro = aroKey2;
                }

                foreach (int k in dict.Keys)
                {
                    if (k < holdAro)
                    {
                        dict[k][1]++;
                    }
                }
            }
            return dict;
        }
        private protected void DeriveMelody(float energy, float valence, int seed)
        {
            gen = new System.Random(seed);
            int numIntervals, interval1, interval2;
            //            NoteHolder[] otherNotes = new NoteHolder[0];
            //            InstrumentManager melody = new InstrumentManager(new SquareWaveInstrument(), r);
            //           ins[1] = melody;

            staticNotes = new Dictionary<int, Dictionary<int, List<Note>>>[rhythmStack.Length];


            if (gen.Next(1) == 0)
            {
                numIntervals = 2;
                interval1 = gen.Next(-15, 15);
                interval2 = gen.Next(-8, 8);
            }
            else
            {
                numIntervals = 1;
                interval1 = gen.Next(-15, 15);
                interval2 = 0;
            }

            if (holdNote == 0)
            {
                holdNote = gen.Next(24, Notes.totalNotes - 24) + 1;
                if (holdNote + interval1 * 2 < 1)
                {
                    holdNote += 24;
                }
                if (holdNote + interval1 * 2 + interval2 * 2 < 1)
                {
                    holdNote += 12;
                }
                if (holdNote + interval1 * 2 >= Notes.totalNotes)
                {
                    holdNote -= 24;
                }
                if (holdNote + interval1 * 2 + interval2 * 2 >= Notes.totalNotes)
                {
                    holdNote -= 12;
                }
            }
            else
            {
                holdNote += 36;
            }

            for (int index = 0; index < rhythmStack.Length; ++index)
            {

                int measureCount = 0;

                int totalMeasures = rhythmStack[index].GetMeasures().Count();
                int genMain = gen.Next(), genSub = gen.Next();
                staticNotes[index] = new Dictionary<int, Dictionary<int, List<Note>>>();

                foreach (RhythmTracker.Measure m in rhythmStack[index].GetMeasures())
                {
                    measureCount++;
                    NoteHolder startingNote = new NoteHolder(measureCount, 1, pitch: Notes.Enumerate(holdNote), noteNumber : holdNote), firstInterval = null, secondInterval = null;
                    int countStrong = 0, numStrong = 0;
                    for (int i = 0; i < m.beats.Length; ++i)
                    {
                        if (m.beats[i] == BeatClass.S || m.beats[i] == BeatClass.D)
                        {
                            numStrong++;
                        }
                    }

                    //Figure out where to place notes based on pattern of strong beats and number of intervals.
                    if (numIntervals == 1)
                    {
                        if (numStrong % 2 == 0)
                        {
                            for (int i = 0; i < m.beats.Length; ++i)
                            {
                                if (m.beats[i] == BeatClass.S || m.beats[i] == BeatClass.D)
                                {
                                    countStrong++;
                                    if (countStrong == numStrong / 2)
                                    {
                                        startingNote.length = i + 1 - startingNote.beat;
                                        firstInterval = new NoteHolder(measureCount, i + 1, m.beats.Length - i - 1, noteNumber : interval1);
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            for (int i = 0; i < m.beats.Length; ++i)
                            {
                                if (m.beats[i] == BeatClass.S || m.beats[i] == BeatClass.D)
                                {
                                    countStrong++;
                                    if (countStrong == (numStrong + 1) / 2)
                                    {
                                        startingNote.length = i + 1 - startingNote.beat;
                                        firstInterval = new NoteHolder(measureCount, i + 1, m.beats.Length - i - 1, noteNumber: interval1);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        if (numStrong % 3 == 0)
                        {
                            for (int i = 0; i < m.beats.Length; ++i)
                            {
                                if (m.beats[i] == BeatClass.S || m.beats[i] == BeatClass.D)
                                {
                                    countStrong++;
                                    if (countStrong == numStrong / 3 * 2)
                                    {
                                        startingNote.length = i + 1 - startingNote.beat;
                                        firstInterval = new NoteHolder(measureCount, i + 1, noteNumber: interval1);
                                    }
                                    else if (countStrong == numStrong)
                                    {
                                        firstInterval.length = i + 1 - firstInterval.beat;
                                        secondInterval = new NoteHolder(measureCount, i + 1, m.beats.Length - i - 1, noteNumber: interval2);
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Just in case??
                            if (numStrong == 1)
                            {
                                startingNote.length = .5f;
                                firstInterval = new NoteHolder(measureCount, 1.5f, .5f, noteNumber: interval1);
                                secondInterval = new NoteHolder(measureCount, 2, m.beats.Length - 2, noteNumber: interval2);
                            }
                            else if (numStrong == 2)
                            {
                                for (int i = 1; i < m.beats.Length; ++i)
                                {
                                    if (m.beats[i] == BeatClass.S || m.beats[i] == BeatClass.D)
                                    {
                                        startingNote.length = i / 2.0f + 1 - startingNote.beat;
                                        firstInterval = new NoteHolder(measureCount, i / 2.0f + 1, i / 2.0f, noteNumber: interval1);
                                        secondInterval = new NoteHolder(measureCount, i + 1, m.beats.Length - i - 1, noteNumber: interval2);
                                        break;
                                    }
                                }
                            }
                            else if (numStrong % 3 == 1)
                            {
                                for (int i = 0; i < m.beats.Length; ++i)
                                {
                                    if (m.beats[i] == BeatClass.S || m.beats[i] == BeatClass.D)
                                    {
                                        countStrong++;
                                        if (countStrong == (numStrong - 1) / 3 * 2)
                                        {
                                            startingNote.length = i + 1 - startingNote.beat;
                                            firstInterval = new NoteHolder(measureCount, i + 1, noteNumber: interval1);
                                        }
                                        else if (countStrong == numStrong - 1)
                                        {
                                            firstInterval.length = i + 1 - firstInterval.beat;
                                            secondInterval = new NoteHolder(measureCount, i + 1, m.beats.Length - i - 1, noteNumber: interval2);
                                            break;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                for (int i = 0; i < m.beats.Length; ++i)
                                {
                                    if (m.beats[i] == BeatClass.S || m.beats[i] == BeatClass.D)
                                    {
                                        countStrong++;
                                        if (countStrong == (numStrong - 2) / 3 * 2)
                                        {
                                            startingNote.length = i + 1 - startingNote.beat;
                                            firstInterval = new NoteHolder(measureCount, i + 1, noteNumber: interval1);
                                        }
                                        else if (countStrong == numStrong + 1)
                                        {
                                            firstInterval.length = i + 1 - firstInterval.beat;
                                            secondInterval = new NoteHolder(measureCount, i + 1, m.beats.Length - i - 1, noteNumber: interval2);
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        /*               for (int i = 0; i < m.beats.Length; ++i)
                                       {
                                           if (i + 1 != firstInterval.beat && i + 1 != secondInterval.beat)
                                           {
                                               otherNotes.Append(new NoteHolder(measureCount, i + 1, 1));
                                           }
                                       }*/
                        int test = 0;
                        if (measureCount == 1 || (totalMeasures % 2 == 0 && measureCount - 1 == totalMeasures / 2) || (totalMeasures % 2 == 1 && measureCount == totalMeasures))
                        {
                            test = 1;
                            DeriveMelodyNotes(new System.Random(genMain), startingNote, firstInterval, secondInterval, ref staticNotes[index]);
                        }
                        else if ((totalMeasures % 2 == 0 && measureCount == totalMeasures) || (totalMeasures % 2 == 1 && (totalMeasures + 1) / 2 == measureCount))
                        {
                            if (secondInterval is null)
                            {
                                test = 2;
                                float temp;
                                temp = startingNote.beat;
                                startingNote.beat = firstInterval.beat;
                                firstInterval.beat = temp;
                                temp = startingNote.length;
                                startingNote.length = firstInterval.length;
                                firstInterval.length = temp;
                                int newTemp = startingNote.noteNumber;
                                startingNote.noteNumber = -firstInterval.noteNumber;
                                firstInterval.noteNumber = newTemp + firstInterval.noteNumber; // Not quite right...
                                DeriveMelodyNotes(new System.Random(genSub), firstInterval, startingNote, null, ref staticNotes[index]);
                            }
                            else
                            {
                                test = 3;
                                float temp;
                                temp = secondInterval.beat;
                                secondInterval.beat = firstInterval.beat;
                                firstInterval.beat = temp;
                                temp = secondInterval.length;
                                secondInterval.length = firstInterval.length;
                                firstInterval.length = temp;
                                DeriveMelodyNotes(new System.Random(genSub), startingNote, secondInterval, firstInterval, ref staticNotes[index]);
                            }
                        }
                        else
                        {
                            if (secondInterval is null)
                            {
                                test = 4;
                                DeriveMelodyNotes(gen, startingNote, new NoteHolder(firstInterval.measure, firstInterval.beat, firstInterval.length, noteNumber: firstInterval.noteNumber), null, ref staticNotes[index]);
                            }
                            else
                            {
                                test = 5;
                                DeriveMelodyNotes(gen, startingNote, new NoteHolder(firstInterval.measure, firstInterval.beat, firstInterval.length, noteNumber: firstInterval.noteNumber)
                                    , new NoteHolder(secondInterval.measure, secondInterval.beat, secondInterval.length, noteNumber: secondInterval.noteNumber), ref staticNotes[index]);
                            }
                        }
                       // Debug.Log("Test for measure " + measureCount + " is: " + test);
                        // SHould really end the first round of valence/aro above and create new one to handle the actual notes....
                        //                        melody.AddNote(new Note(startingNote.measure, startingNote.beat, startingNote.pitch, firstInterval.beat - startingNote.beat));
                        //addNote(staticNotes[index], 0, 0, new Note(startingNote.measure, startingNote.beat, startingNote.pitch, firstInterval.beat - startingNote.beat));
                        //if (numIntervals == 2)
                        //{
                        //    //Debug.Log(String.Concat("Starting Note: ", startingNote.measure, ", ", startingNote.beat, ", ", startingNote.pitch, ", ", firstInterval.beat - startingNote.beat,
                        //    //    "\nFirst Interval: ", firstInterval.measure, ", ", firstInterval.beat, ", ", firstInterval.pitch, ", ", secondInterval.beat - firstInterval.beat,
                        //    //    "\nSecond Interval: ", secondInterval.measure, ", ", secondInterval.beat, ", ", secondInterval.pitch, ", ", m.beats.Length - secondInterval.beat));


                        //    addNote(staticNotes[index], 0, 0, new Note(firstInterval.measure, firstInterval.beat, firstInterval.pitch, (secondInterval.beat - firstInterval.beat) / 2));
                        //    addNote(staticNotes[index], 0, 0, new Note(secondInterval.measure, secondInterval.beat, secondInterval.pitch, (m.beats.Length - secondInterval.beat) / 2));
                        //    //                   melody.AddNote(new Note(firstInterval.measure, firstInterval.beat, firstInterval.pitch, (secondInterval.beat - firstInterval.beat) / 2));
                        //    //                   melody.AddNote(new Note(secondInterval.measure, secondInterval.beat, secondInterval.pitch, (m.beats.Length - secondInterval.beat) / 2));
                        //}
                        //else
                        //{
                        //    Debug.Log(String.Concat("Starting Note: ", startingNote.measure, ", ", startingNote.beat, ", ", startingNote.pitch, ", ", firstInterval.beat - startingNote.beat,
                        //        "\nFirst Interval: ", firstInterval.measure, ", ", firstInterval.beat, ", ", firstInterval.pitch, ", ", m.beats.Length - firstInterval.beat));
                        //    addNote(notes[index][1], 0, 0, new Note(firstInterval.measure, firstInterval.beat, firstInterval.pitch, (m.beats.Length - firstInterval.beat) / 2));
                        //    //                   melody.AddNote(new Note(firstInterval.measure, firstInterval.beat, firstInterval.pitch, (m.beats.Length - firstInterval.beat) / 2));
                        //}
                    }
                }
            }
        }

        virtual private protected void DeriveMelodyNotes(System.Random rand, NoteHolder start, NoteHolder interval1, NoteHolder interval2, ref Dictionary<int, Dictionary<int, List<Note>>> retval)
        {
            addNoteToRangeOfVals(retval, 0, 5000, 0, new Note(start.measure, start.beat, start.pitch, start.length), true);
            //Debug.Log("Deriving Melody; start note stats: " + start.measure + " " + start.beat + " " + start.pitch + " " + start.length + " " + start.noteNumber);
            if (!(interval1 is null))
            {
                float pitch1, pitch2;
                pitch1 = getPitchFromInterval(start.noteNumber, interval1.noteNumber, true);
                pitch2 = getPitchFromInterval(start.noteNumber, interval1.noteNumber, false);

                if (pitch1 == pitch2)
                {
                    addNoteToRangeOfVals(retval, 0, 5000, 0, new Note(interval1.measure, interval1.beat, pitch1, interval1.length), true);
                }
                else
                {
                    int val = rand.Next(1000, 5001);
                    addNoteToRangeOfVals(retval, 0, val-1, 0, new Note(interval1.measure, interval1.beat, pitch1, interval1.length), true);
                    addNoteToRangeOfVals(retval, val, 5000, 0, new Note(interval1.measure, interval1.beat, pitch2, interval1.length), true);
                }
                //Debug.Log("Deriving Melody; interval1 stats: " + interval1.measure + " " + interval1.beat + " " + pitch2 + " " + interval1.length + " " + interval1.noteNumber);
            }
            if (!(interval2 is null))
            {
                float pitch1, pitch2;
                pitch1 = getPitchFromInterval(start.noteNumber, interval2.noteNumber, true);
                pitch2 = getPitchFromInterval(start.noteNumber, interval2.noteNumber, false);

                //Debug.Log("Deriving Melody; interval2 stats: " + interval2.measure + " " + interval2.beat + " " + pitch2 + " " + interval2.length + " " + interval1.noteNumber);

                if (pitch1 == pitch2)
                {
                    addNoteToRangeOfVals(retval, 0, 5000, 0, new Note(interval2.measure, interval2.beat, pitch1, interval2.length), true);
                }
                else
                {
                    int val = rand.Next(1000, 5001);
                    addNoteToRangeOfVals(retval, 0, val - 1, 0, new Note(interval2.measure, interval2.beat, pitch1, interval2.length), true);
                    addNoteToRangeOfVals(retval, val, 5000, 0, new Note(interval2.measure, interval2.beat, pitch2, interval2.length), true);
                }
            }
        }
            virtual private protected float getPitchFromInterval(int startTone, int interval, bool isConsonant)
            {
                float retval2 = 0f;
                switch (interval)
                {
                    case 4:
                        retval2 = Notes.Enumerate(startTone + 5);
                        break;
                    case 5:
                        retval2 = Notes.Enumerate(startTone + 7);
                        break;
                    case -4:
                        retval2 = Notes.Enumerate(startTone - 5);
                        break;
                    case -5:
                        retval2 = Notes.Enumerate(startTone - 7);
                        break;
                    case 8:
                        retval2 = Notes.Enumerate(startTone + 8);
                        break;
                    case -8:
                        retval2 = Notes.Enumerate(startTone - 8);
                        break;
                    case 11:
                        retval2 = Notes.Enumerate(startTone + 17);
                        break;
                    case 12:
                        retval2 = Notes.Enumerate(startTone + 19);
                        break;
                    case 15:
                        retval2 = Notes.Enumerate(startTone + 24);
                        break;
                    case -11:
                        retval2 = Notes.Enumerate(startTone - 17);
                        break;
                    case -12:
                        retval2 = Notes.Enumerate(startTone - 19);
                        break;
                    case -15:
                        retval2 = Notes.Enumerate(startTone - 24);
                        break;

                    case 2:
                        if (isConsonant)
                        {
                            retval2 = Notes.Enumerate(startTone + 2);
                        }
                        else
                        {
                            retval2 = Notes.Enumerate(startTone + 1);
                        }
                        break;

                    case 3:
                        if (isConsonant)
                        {
                            retval2 = Notes.Enumerate(startTone + 4);
                        }
                        else
                        {
                            retval2 = Notes.Enumerate(startTone + 3);
                        }
                        break;

                    case 6:
                        if (isConsonant)
                        {
                            retval2 = Notes.Enumerate(startTone + 9);
                        }
                        else
                        {
                            retval2 = Notes.Enumerate(startTone + 8);
                        }
                        break;

                    case 7:
                        if (isConsonant)
                        {
                            retval2 = Notes.Enumerate(startTone + 11);
                        }
                        else
                        {
                            retval2 = Notes.Enumerate(startTone + 10);
                        }
                        break;

                    case 9:
                        if (isConsonant)
                        {
                            retval2 = Notes.Enumerate(startTone + 14);
                        }
                        else
                        {
                            retval2 = Notes.Enumerate(startTone + 6); // Not a mistake...
                        }
                        break;

                    case 10:
                        if (isConsonant)
                        {
                            retval2 = Notes.Enumerate(startTone + 16);
                        }
                        else
                        {
                            retval2 = Notes.Enumerate(startTone + 15);
                        }
                        break;

                    case 13:
                        if (isConsonant)
                        {
                            retval2 = Notes.Enumerate(startTone + 21);
                        }
                        else
                        {
                            retval2 = Notes.Enumerate(startTone + 20);
                        }
                        break;

                    case 14:
                        if (isConsonant)
                        {
                            retval2 = Notes.Enumerate(startTone + 23);
                        }
                        else
                        {
                            retval2 = Notes.Enumerate(startTone + 22);
                        }
                        break;

                    case -2:
                        if (!isConsonant)
                        {
                            retval2 = Notes.Enumerate(startTone - 2);
                        }
                        else
                        {
                            retval2 = Notes.Enumerate(startTone - 1);
                        }
                        break;

                    case -3:
                        if (!isConsonant)
                        {
                            retval2 = Notes.Enumerate(startTone - 4);
                        }
                        else
                        {
                            retval2 = Notes.Enumerate(startTone - 3);
                        }
                        break;

                    case -6:
                        if (!isConsonant)
                        {
                            retval2 = Notes.Enumerate(startTone - 9);
                        }
                        else
                        {
                            retval2 = Notes.Enumerate(startTone - 8);
                        }
                        break;

                    case -7:
                        if (!isConsonant)
                        {
                            retval2 = Notes.Enumerate(startTone - 11);
                        }
                        else
                        {
                            retval2 = Notes.Enumerate(startTone - 10);
                        }
                        break;
                        // This one is just totally weird now...
                    case -9:
                        if (isConsonant)
                        {
                            retval2 = Notes.Enumerate(startTone - 14);
                        }
                        else
                        {
                            retval2 = Notes.Enumerate(startTone - 6); // Not a mistake...
                        }
                        break;

                    case -10:
                        if (!isConsonant)
                        {
                            retval2 = Notes.Enumerate(startTone - 16);
                        }
                        else
                        {
                            retval2 = Notes.Enumerate(startTone - 15);
                        }
                        break;

                    case -13:
                        if (!isConsonant)
                        {
                            retval2 = Notes.Enumerate(startTone - 21);
                        }
                        else
                        {
                            retval2 = Notes.Enumerate(startTone - 20);
                        }
                        break;

                    case -14:
                        if (!isConsonant)
                        {
                            retval2 = Notes.Enumerate(startTone - 23);
                        }
                        else
                        {
                            retval2 = Notes.Enumerate(startTone - 22);
                        }
                        break;

                case -1:
                case 1:
                    retval2 = 0;
                    break;

                    case 0:
                    default:
                        retval2 = Notes.Enumerate(startTone);
                        break;

                }

                return retval2;
            }
        

        private protected void DeriveMelodyOld(float energy, float valence, int seed)
        {
            gen = new System.Random(seed);
            int numIntervals, interval1, interval2;
            //            NoteHolder[] otherNotes = new NoteHolder[0];
            //            InstrumentManager melody = new InstrumentManager(new SquareWaveInstrument(), r);
            //           ins[1] = melody;


            if (gen.Next(1) == 0)
            {
                numIntervals = 2;
                interval1 = gen.Next(-24, 25);
                interval2 = gen.Next(-12, 13);
            }
            else
            {
                numIntervals = 1;
                interval1 = gen.Next(-24, 25);
                interval2 = 0;
            }

            if (holdNote == 0)
            {
                holdNote = gen.Next(24, Notes.totalNotes - 24) + 1;
                if (holdNote + interval1 < 1)
                {
                    holdNote += 24;
                }
                if (holdNote + interval1 + interval2 < 1)
                {
                    holdNote += 12;
                }
                if (holdNote + interval1 >= Notes.totalNotes)
                {
                    holdNote -= 24;
                }
                if (holdNote + interval1 + interval2 >= Notes.totalNotes)
                {
                    holdNote -= 12;
                }
            }
            else
            {
                holdNote += 36;
            }

            //           NoteHolder startingNote = new NoteHolder(1, 1, pitch: Notes.Enumerate(holdNote)), firstInterval = new NoteHolder(0, 0), secondInterval = new NoteHolder(0, 0);
            for (int index = 0; index < rhythmStack.Length; ++index)
            {

                    int measureCount = 0;
                    // This won't work; could be several different beats...
                    foreach (RhythmTracker.Measure m in rhythmStack[index].GetMeasures())
                    {
                        measureCount++;
                        NoteHolder startingNote = new NoteHolder(measureCount, 1, pitch: Notes.Enumerate(holdNote)), firstInterval = new NoteHolder(0, 0), secondInterval = new NoteHolder(0, 0);
                        int countStrong = 0, numStrong = 0;
                        for (int i = 0; i < m.beats.Length; ++i)
                        {
                            if (m.beats[i] == BeatClass.S || m.beats[i] == BeatClass.D)
                            {
                                numStrong++;
                            }
                        }

                        //Figure out where to place notes based on pattern of strong beats and number of intervals.
                        if (numIntervals == 1)
                        {
                            if (numStrong % 2 == 0)
                            {
                                for (int i = 0; i < m.beats.Length; ++i)
                                {
                                    if (m.beats[i] == BeatClass.S || m.beats[i] == BeatClass.D)
                                    {
                                        countStrong++;
                                        if (countStrong == numStrong / 2)
                                        {
                                            firstInterval = new NoteHolder(measureCount, i + 1, pitch: Notes.Enumerate(holdNote + interval1));
                                            break;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                for (int i = 0; i < m.beats.Length; ++i)
                                {
                                    if (m.beats[i] == BeatClass.S || m.beats[i] == BeatClass.D)
                                    {
                                        countStrong++;
                                        if (countStrong == (numStrong + 1) / 2)
                                        {
                                            firstInterval = new NoteHolder(measureCount, i + 1, pitch: Notes.Enumerate(holdNote + interval1));
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (numStrong % 3 == 0)
                            {
                                for (int i = 0; i < m.beats.Length; ++i)
                                {
                                    if (m.beats[i] == BeatClass.S || m.beats[i] == BeatClass.D)
                                    {
                                        countStrong++;
                                        if (countStrong == numStrong / 3)
                                        {
                                            firstInterval = new NoteHolder(measureCount, i + 1, pitch: Notes.Enumerate(holdNote + interval1));
                                        }
                                        else if (countStrong == numStrong / 3 * 2)
                                        {
                                            secondInterval = new NoteHolder(measureCount, i + 1, pitch: Notes.Enumerate(holdNote + interval1 + interval2));
                                            break;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // Just in case??
                                if (numStrong == 1)
                                {
                                    firstInterval = new NoteHolder(measureCount, 1.5f, pitch: Notes.Enumerate(holdNote + interval1));
                                    secondInterval = new NoteHolder(measureCount, 2, pitch: Notes.Enumerate(holdNote + interval1 + interval2));
                                }
                                else if (numStrong == 2)
                                {
                                    for (int i = 1; i < m.beats.Length; ++i)
                                    {
                                        if (m.beats[i] == BeatClass.S || m.beats[i] == BeatClass.D)
                                        {
                                            firstInterval = new NoteHolder(measureCount, i / 2.0f + 1, pitch: Notes.Enumerate(holdNote + interval1));
                                            secondInterval = new NoteHolder(measureCount, i + 1, pitch: Notes.Enumerate(holdNote + interval1 + interval2));
                                            break;
                                        }
                                    }
                                }
                                else if (numStrong % 3 == 1)
                                {
                                    for (int i = 0; i < m.beats.Length; ++i)
                                    {
                                        if (m.beats[i] == BeatClass.S || m.beats[i] == BeatClass.D)
                                        {
                                            countStrong++;
                                            if (countStrong == (numStrong - 1) / 3)
                                            {
                                                firstInterval = new NoteHolder(measureCount, i + 1, pitch: Notes.Enumerate(holdNote + interval1));
                                            }
                                            else if (countStrong == (numStrong - 1) / 3 * 2)
                                            {
                                                secondInterval = new NoteHolder(measureCount, i + 1, pitch: Notes.Enumerate(holdNote + interval1 + interval2));
                                                break;
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    for (int i = 0; i < m.beats.Length; ++i)
                                    {
                                        if (m.beats[i] == BeatClass.S || m.beats[i] == BeatClass.D)
                                        {
                                            countStrong++;
                                            if (countStrong == (numStrong - 2) / 3)
                                            {
                                                firstInterval = new NoteHolder(measureCount, i + 1, pitch: Notes.Enumerate(holdNote + interval1));
                                            }
                                            else if (countStrong == (numStrong + 1) / 3 * 2)
                                            {
                                                secondInterval = new NoteHolder(measureCount, i + 1, pitch: Notes.Enumerate(holdNote + interval1 + interval2));
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        /*               for (int i = 0; i < m.beats.Length; ++i)
                                       {
                                           if (i + 1 != firstInterval.beat && i + 1 != secondInterval.beat)
                                           {
                                               otherNotes.Append(new NoteHolder(measureCount, i + 1, 1));
                                           }
                                       }*/


                        // SHould really end the first round of valence/aro above and create new one to handle the actual notes....
//                        melody.AddNote(new Note(startingNote.measure, startingNote.beat, startingNote.pitch, firstInterval.beat - startingNote.beat));
                        addNote(notes[index][1], 0, 0, new Note(startingNote.measure, startingNote.beat, startingNote.pitch, firstInterval.beat - startingNote.beat));
                        if (numIntervals == 2)
                        {
                            //Debug.Log(String.Concat("Starting Note: ", startingNote.measure, ", ", startingNote.beat, ", ", startingNote.pitch, ", ", firstInterval.beat - startingNote.beat,
                           //     "\nFirst Interval: ", firstInterval.measure, ", ", firstInterval.beat, ", ", firstInterval.pitch, ", ", secondInterval.beat - firstInterval.beat,
                           //     "\nSecond Interval: ", secondInterval.measure, ", ", secondInterval.beat, ", ", secondInterval.pitch, ", ", m.beats.Length - secondInterval.beat));
                            addNote(notes[index][1], 0, 0, new Note(firstInterval.measure, firstInterval.beat, firstInterval.pitch, (secondInterval.beat - firstInterval.beat) / 2));
                            addNote(notes[index][1], 0, 0, new Note(secondInterval.measure, secondInterval.beat, secondInterval.pitch, (m.beats.Length - secondInterval.beat) / 2));
                            //                   melody.AddNote(new Note(firstInterval.measure, firstInterval.beat, firstInterval.pitch, (secondInterval.beat - firstInterval.beat) / 2));
                            //                   melody.AddNote(new Note(secondInterval.measure, secondInterval.beat, secondInterval.pitch, (m.beats.Length - secondInterval.beat) / 2));
                        }
                        else
                        {
                            //Debug.Log(String.Concat("Starting Note: ", startingNote.measure, ", ", startingNote.beat, ", ", startingNote.pitch, ", ", firstInterval.beat - startingNote.beat,
                             //   "\nFirst Interval: ", firstInterval.measure, ", ", firstInterval.beat, ", ", firstInterval.pitch, ", ", m.beats.Length - firstInterval.beat));
                            addNote(notes[index][1], 0, 0, new Note(firstInterval.measure, firstInterval.beat, firstInterval.pitch, (m.beats.Length - firstInterval.beat) / 2));
                            //                   melody.AddNote(new Note(firstInterval.measure, firstInterval.beat, firstInterval.pitch, (m.beats.Length - firstInterval.beat) / 2));
                        }
                    }
                }
            }
        }
        private protected void DeriveHarmony(float energy, float valence, int seed)
        {
            gen = new System.Random(seed);

            InstrumentManager bass = new InstrumentManager(new SineWaveInstrument(), r);
            ins[5] = bass;

            int mCount = 0;
//            NoteHolder[] possibleNotes = new NoteHolder[0];
            List<NoteHolder>[] possibleNotes = new List<NoteHolder>[rhythmStack.Length];
            long measure = 0;
            if (holdNote == 0)
            {
                holdNote = gen.Next(12, 36) + 1;
            }
            else
            {
                while (holdNote >= 36)
                {
                    holdNote -= 12;
                }
            }
            for (int index = 0; index < rhythmStack.Length; ++index)
            { 
                possibleNotes[index] = new List<NoteHolder>();
                    mCount = 0;
                    foreach (RhythmTracker.Measure m in rhythmStack[index].GetMeasures())
                    {
                        mCount++;
                        /*                int strongBeats = 0, currentBeat = 0;
                                        for (int i = 0; i < m.beats.Length; ++i)
                                        {
                                            switch (m.beats[i])
                                            {
                                                case BeatClass.D:
                                                case BeatClass.S:
                                                    strongBeats++;
                                                    break;
                                            }
                                        }
                        */
                        for (int i = 0; i < m.beats.Length; ++i)
                        {
                            float pitch = 0, length = 0;
                            string type = "";
                            switch (measure)
                            {
                                case 0:
                                    switch (m.beats[i])
                                    {
                                        case BeatClass.D:
                                            pitch = Notes.Enumerate(holdNote); // Notes.E1;
                                            length = m.beats.Length;
                                            type = "D";
                                            break;

                                        case BeatClass.S:
                                            pitch = Notes.Enumerate(holdNote + 7);// Notes.B2;
                                            length = 1f;
                                            type = "S";
                                            break;
                                    }
                                    break;

                                case 1:
                                    switch (m.beats[i])
                                    {
                                        case BeatClass.D:
                                            pitch = Notes.Enumerate(holdNote + 7);
                                            length = m.beats.Length;
                                            type = "D";
                                            break;

                                        case BeatClass.S:
                                            pitch = Notes.Enumerate(holdNote + 12);
                                            length = 1f;
                                            type = "S";
                                            break;
                                    }
                                    break;

                                case 2:
                                    switch (m.beats[i])
                                    {
                                        case BeatClass.D:
                                            pitch = Notes.Enumerate(holdNote + 12);
                                            length = m.beats.Length;
                                            type = "D";
                                            break;

                                        case BeatClass.S:
                                            pitch = Notes.Enumerate(holdNote + 19);
                                            length = 1f;
                                            type = "S";
                                            break;
                                    }
                                    break;

                                case 3:
                                    switch (m.beats[i])
                                    {
                                        case BeatClass.D:
                                            pitch = Notes.Enumerate(holdNote + 7);
                                            length = m.beats.Length;
                                            type = "D";
                                            break;

                                        case BeatClass.S:
                                            pitch = Notes.Enumerate(holdNote + 14);
                                            length = 1f;
                                            type = "S";
                                            break;
                                    }
                                    break;
                            }
                            if (type != "")
                            {
                                //Debug.Log(String.Concat(mCount, " ", i + 1, " ", length, " ", pitch, " ", type));
                                possibleNotes[index].Add(new NoteHolder(mCount, i + 1, length, pitch, type));
                            }
                        }

                        measure++;
                        measure %= 4;
                        /*
                        for (int i = 0; i < m.beats.Length; ++i)
                        {
                            switch (m.beats[i])
                            {
                                case BeatClass.D:
                                    bassHit = true;
                                    goto case BeatClass.S;

                                case BeatClass.S:
                                    if (bassHit)
                                        drummer.BassHit(mCount, i+1);
                                    else
                                        drummer.SnareHit(mCount, i+1);

                                    bassHit = !bassHit;
                                    break;
                            }

                            drummer.HighHatHit(mCount, i + 1);
                        }
        */

                    }
            }
            // Debug.Log("Before Note HOlder: " + possibleNotes.Length);
            for (int i = 0; i < possibleNotes.Length; ++i)
            {
                foreach (NoteHolder n in possibleNotes[i])
                {
                    switch (n.type)
                    {
                        case "D":
                            addNote(notes[i][5], 0, 0, new Note(n.measure, n.beat, n.pitch, n.length));
                            //                        bass.AddNote(new Note(n.measure, n.beat, n.pitch, n.length));
                            break;

                        case "S":
                            //int val = (int)(gen.NextDouble() * 25000);
                            //if (val < 1)
                            //{
                            //    val = 0;
                            //}
                            //if (val < 5)
                            //{
                            //    addNote(notes[i][5], val, 0, new Note(n.measure, n.beat, n.pitch, n.length));
                            //}
                            break;
                    }
                }
            }

        }
        private protected void DeriveRhythm(float energy, float valence, int seed)
        {

            int maxNotes = 0;
           List<NoteHolder>[] possibleNotes = new List<NoteHolder>[rhythmStack.Length];

            for (int index = 0; index < rhythmStack.Length; ++index)
            {
                //Debug.Log("Testing 0");
                possibleNotes[index] = new List<NoteHolder>();
                    int mCount = 0;
                    //                  NoteHolder[] possibleNotes = new NoteHolder[0];
                    int countNotes = 0;
                    //Debug.Log("Testing 1");
                    foreach (RhythmTracker.Measure m in rhythmStack[index].GetMeasures())
                    {
                        //Debug.Log("Testing 2");
                        mCount++;
                        int strongBeats = 0, currentBeat = 0;
                        for (int i = 0; i < m.beats.Length; ++i)
                        {
                            switch (m.beats[i])
                            {
                                case BeatClass.D:
                                case BeatClass.S:
                                    strongBeats++;
                                    break;
                            }
                        }

                        if (mCount == r.GetMeasures().ToArray().Length)
                        {
                            int countStrongBeats = 0, i = 0;
                            bool timeToBreak = false;
                            while (true)
                            {
                                switch (m.beats[i])
                                {
                                    case BeatClass.D:
                                        if (countStrongBeats < strongBeats / 2)
                                        {
                                            countStrongBeats++;
                                            //                                            possibleNotes = possibleNotes.Append(new NoteHolder(mCount, i + 1, type: "B")).ToArray();
                                            possibleNotes[index].Add(new NoteHolder(mCount, i + 1, type: "B"));
                                            possibleNotes[index].Add(new NoteHolder(mCount, i + 1.5f, type: "CB", noteNumber: countNotes++));
                                        }
                                        else
                                        {
                                            timeToBreak = true;
                                        }
                                        break;

                                    case BeatClass.S:
                                        if (countStrongBeats < strongBeats / 2)
                                        {
                                            countStrongBeats++;
                                            if (currentBeat % 2 != 0 && (currentBeat != strongBeats || strongBeats % 2 == 0))
                                            {
                                            //                                            possibleNotes = possibleNotes.Append(new NoteHolder(mCount, i + 1, type: "B")).ToArray();
                                            possibleNotes[index].Add(new NoteHolder(mCount, i + 1, type: "B"));
                                            possibleNotes[index].Add(new NoteHolder(mCount, i + 1.5f, type: "CB", noteNumber: countNotes++));
                                        }
                                        else
                                            {
                                                possibleNotes[index].Add(new NoteHolder(mCount, i + 1, type: "S"));
                                                possibleNotes[index].Add(new NoteHolder(mCount, i + 1.5f, type: "CS", noteNumber: countNotes++));
                                            }
                                            //                                            possibleNotes = possibleNotes.Append(new NoteHolder(mCount, i + 1, type: "S")).ToArray();
                                        }
                                        else
                                        {
                                            timeToBreak = true;
                                        }
                                        break;
                                }
                                if (timeToBreak)
                                {
//                                    i++;
                                    break;
                                }

                                possibleNotes[index].Add(new NoteHolder(mCount, i + 1, type: "H"));
                                possibleNotes[index].Add(new NoteHolder(mCount, i + 1.5f, type: "CH", noteNumber: countNotes++));
                                i++;
                            }

//                            GetFill(mCount, i, m, val, aro);
                            possibleNotes[index].Add(new NoteHolder(mCount, i + 1, type: "F", noteNumber: countNotes++));
                        }
                        else
                        {
                            for (int i = 0; i < m.beats.Length; ++i)
                            {
                                switch (m.beats[i])
                                {
                                    case BeatClass.D:
                                        currentBeat++;
                                        {
                                        possibleNotes[index].Add(new NoteHolder(mCount, i + 1, type: "B"));
                                        possibleNotes[index].Add(new NoteHolder(mCount, i + 1.5f, type: "CB", noteNumber: countNotes++));
                                    }
                                    break;

                                    case BeatClass.S:
                                        currentBeat++;
                                        if (currentBeat % 2 != 0 && (currentBeat != strongBeats || strongBeats % 2 == 0))
                                        {
                                        possibleNotes[index].Add(new NoteHolder(mCount, i + 1, type: "B"));
                                        possibleNotes[index].Add(new NoteHolder(mCount, i + 1.5f, type: "CB", noteNumber: countNotes++));
                                    }
                                    else
                                        {
                                        possibleNotes[index].Add(new NoteHolder(mCount, i + 1, type: "S"));
                                        possibleNotes[index].Add(new NoteHolder(mCount, i + 1.5f, type: "CS", noteNumber: countNotes++));
                                    }

                                    break;
                                }

                            possibleNotes[index].Add(new NoteHolder(mCount, i + 1, type: "H"));
                            possibleNotes[index].Add(new NoteHolder(mCount, i + 1.5f, type: "CH", noteNumber: countNotes++));
                        }
                    }
                    }

                     //Debug.Log("Before Note HOlder: " + possibleNotes[index].Count);
                    List<NoteHolder> toRemove = new List<NoteHolder>();
                    foreach (NoteHolder n in possibleNotes[index])
                    {
//                        int test = gen.Next(0, (int)(25 / valence));
//                        Debug.Log("Test: " + test + "; valence: " + valence);
                        switch (n.type)
                        {
                            case "B":
                                addNote(notes[index][10], 0, 0, new Note(n.measure, n.beat, n.pitch, n.length), false);
                                toRemove.Add(n);
                                break;

                            case "S":
                                addNote(notes[index][11], 0, 0, new Note(n.measure, n.beat, n.pitch, n.length), false);
                                toRemove.Add(n);
                                break;

                            case "H":
                                addNote(notes[index][12], 0, 0, new Note(n.measure, n.beat, n.pitch, n.length), false);
                                toRemove.Add(n);
                                break;
                        }
                    }
                   // Debug.Log("Before removing Notes");
                    foreach (NoteHolder n in toRemove)
                    {
                        possibleNotes[index].Remove(n);
                    }
                   // Debug.Log("After Removing Notes");
                    /*
                    int test = possibleNotes.Length;
                    if (test > maxNotes)
                    {
                        maxNotes = test;
                    }*/
                
            }
            //Debug.Log("Finished first loop");
            // Can't check rhythms, needs to be notes...
            {
                gen = new System.Random(seed);
//                int nextVal = GetNextKey(rhythms, val);

                for (int index = 0; index < rhythmStack.Length; ++index)
                {
                    List<NoteHolder> toRemove = new List<NoteHolder>();
                    foreach (NoteHolder n in possibleNotes[index])
                    {
                        bool add = false;
                        double test;
                        int holdVal, valToAdd = 0;
                        switch (n.type)
                        {
                            case "F":
                                List<Note> fillNotes = GetFill2(n.measure, n.beat, rhythmStack[index].GetMeasures().ElementAt(n.measure - 1), 0, 0);
                                foreach (Note fn in fillNotes)
                                {
                                    addNote(notes[index][11], 0, 0, fn, false);
                                }
                                toRemove.Add(n);
                                break;

                            case "CB":
                                // No way to know in which order notes will be added,
                                // so need to figure out how to add notes to the larger valences...
                                test = gen.NextDouble();
                                if (test <= .04)
                                {
                                    valToAdd = 0;
                                    add = true;
                                }
                                else if (test <= .20)
                                {
                                    valToAdd = (int) Math.Floor(test * 25000);
                                    add = true;
                                }
                                if (add)
                                {
                                    addNote(notes[index][10], 0, valToAdd, new Note(n.measure, n.beat, n.pitch, n.length), true);
                                }
                                toRemove.Add(n);
                                break;

                            case "CS":
                                test = gen.NextDouble();
                                if (test <= .04)
                                {
                                    valToAdd = 0;
                                    add = true;
                                }
                                else if (test <= .20)
                                {
                                    valToAdd = (int)Math.Floor(test * 25000);
                                    add = true;
                                }
                                if (add)
                                {
                                    addNote(notes[index][11], 0, valToAdd, new Note(n.measure, n.beat, n.pitch, n.length), true);
                                }
                                toRemove.Add(n);
                                break;

                            case "CH":
                                test = gen.NextDouble();
                                if (test <= .04)
                                {
                                    valToAdd = 0;
                                    add = true;
                                }
                                else if (test <= .20)
                                {
                                    valToAdd = (int)Math.Floor(test * 25000);
                                    add = true;
                                }
                                if (add)
                                {
                                    addNote(notes[index][12], 0, valToAdd, new Note(n.measure, n.beat, n.pitch, n.length), true);
                                }
                                toRemove.Add(n);
                                break;
                        }

                    }
                    foreach (NoteHolder n in toRemove)
                    {
                        possibleNotes[index].Remove(n);
                    }

                }
            }
/*
            foreach (int val in possibleNotes.Keys)
            {

                foreach (int aro in possibleNotes[val].Keys)
                {

                }
            }
            {
                int test = gen.Next(0, (int)(25 / valence));
                Debug.Log("Test: " + test + "; valence: " + valence);
            }
*/
        }

        private protected void GetFill(int measure, int startBeat, RhythmTracker.Measure m, int val, int aro)
        {

            //Fill
            for (int i = startBeat; i <= m.beats.Length; ++i)
            {
                drummer.SnareHit(measure, i);
                drummer.SnareHit(measure, i + .25f);
                drummer.SnareHit(measure, i + .5f);
                drummer.SnareHit(measure, i + .75f);
            }

        }

        private protected List<Note> GetFill2(int measure, float startBeat, RhythmTracker.Measure m, int val, int aro)
        {
            List<Note> notes = new List<Note>();
            //Fill
            for (int i = (int)Math.Floor(startBeat); i <= m.beats.Length; ++i)
            {
                /*
                addNote(notes[11], val, aro, new Note(measure, i, 0, 0), true);
 //               addNote(notes[11], val, aro, new Note(measure, i+.25f, 0, 0), true);
                addNote(notes[11], val, aro, new Note(measure, i+.5f, 0, 0), true);
 //               addNote(notes[11], val, aro, new Note(measure, i+.75f, 0, 0), true);
                */
                notes.Add(new Note(measure, i, 0, 0));
                notes.Add(new Note(measure, i + .5f, 0, 0));
            }

            return notes;
        }


        public float[] getNotes(int pos, int dur)
        {
            //            Debug.Log("GetNotes Dur: " + dur);
            float[][] i = new float[0][];
            foreach (InstrumentManager im in ins)
            {
                if (!(im is null))
                {
                    i = i.Append(im.GetStream(pos, dur)).ToArray();
                }
            }
            return (PreVolumeMixer.Mix(i));
        }

        public void getNotes(int pos, int dur, int start, ref float[] output)
        {
                        //Debug.Log("GetNotes Pos: " + pos + "; Dur: " + dur + "; Start: " + start);
            float[][] i = new float[0][];
            int count = 0;
            foreach (InstrumentManager im in ins)
            {
                if (!(im is null))
                {
                    i = i.Append(im.GetStream(pos, dur)).ToArray();
                    count++;
                }
                
            }
            //Debug.Log("Get Notes found " + count + " non-null instruments (should be 6)");
            PreVolumeMixer.Mix(start, ref output, i);
        }
        // Third parameter is in samples (not seconds), as caller won't know the rhythm.
        public int GetMeasure(float valence, float energy, int position)
        {
            int holdVal = (int)(valence * 1000);
            int holdAro = (int)(energy * 1000);
            holdVal = GetMin(rhythms, holdVal);
            holdAro = GetMin(rhythms[holdVal], holdAro);
            return rhythmStack[rhythmLinks[rhythms[holdVal][holdAro]]].GetMeasureSamples(position);
        }

        public float GetBeat(float valence, float energy, int position)
        {
            int holdVal = (int)(valence * 1000);
            int holdAro = (int)(energy * 1000);
            holdVal = GetMin(rhythms, holdVal);
            holdAro = GetMin(rhythms[holdVal], holdAro);
            return rhythmStack[rhythmLinks[rhythms[holdVal][holdAro]]].GetBeatSamples(position);
        }

        public int GetPosition(float valence, float energy, int measure, float beat)
        {
            int holdVal = (int)(valence * 1000);
            int holdAro = (int)(energy * 1000);
            holdVal = GetMin(rhythms, holdVal);
            holdAro = GetMin(rhythms[holdVal], holdAro);
           // Debug.Log("In GetPosition; val/aro/index: " + holdVal + "/" + holdAro + "/" + rhythms[holdVal][holdAro]);
            return rhythmStack[rhythmLinks[rhythms[holdVal][holdAro]]].GetTimeSamples(measure, beat);
        }
        // Maybe return offset? Seems like can be changed during function?
        public int GetPosition(float oldVal, float oldAro, float newVal, float newAro, int position)
        {
            int holdPosition = position;
            int holdVal = GetMin(rhythms, (int)(oldVal * 1000));
            int holdAro = GetMin(rhythms[holdVal], (int)(oldAro * 1000));
            int firstIndex = rhythmLinks[rhythms[holdVal][holdAro]];
            int holdVal2 = GetMin(rhythms, (int)(newVal * 1000));
            int holdAro2 = GetMin(rhythms[holdVal2], (int)(newAro * 1000));
            int secondIndex = rhythmLinks[rhythms[holdVal2][holdAro2]];
            if (firstIndex == secondIndex)
            {
                return 0;
            }

            int measure = rhythmStack[firstIndex].GetMeasureSamples(holdPosition);
            float beat = rhythmStack[firstIndex].GetBeatSamples(holdPosition);

            measure = rhythmStack[secondIndex].GetTimeSamples(measure, beat, RhythmTracker.PositionFinder.nextMeasure);
            if (measure == -1)
            {
                return -1;
            }
            else
            {
                return measure - holdPosition;
            }
        }
        public int GetMeasures(float valence, float energy)
        {
            int holdVal = (int)(valence * 1000);
            int holdAro = (int)(energy * 1000);
            holdVal = GetMin(rhythms, holdVal);
            holdAro = GetMin(rhythms[holdVal], holdAro);
            return rhythmStack[rhythmLinks[rhythms[holdVal][holdAro]]].GetMeasures().Count();
        }

        public int GetDuration(float valence, float energy)
        {
            int holdVal = (int)(valence * 1000);
            int holdAro = (int)(energy * 1000);
     //       Debug.Log("In Get Duration; Before GetMin: Val: " + valence + " Aro: " + energy + " holdVal: " + holdVal + " holdAro: " + holdAro);
            holdVal = GetMin(rhythms, holdVal);
            holdAro = GetMin(rhythms[holdVal], holdAro);
            int index = rhythmLinks[rhythms[holdVal][holdAro]];
            int mCount = rhythmStack[index].GetMeasures().Count();
            int lastbeat = rhythmStack[index].GetMeasures().Last().beats.Length;
       //     Debug.Log("In Get Duration; After GetMin: Val: " + valence + " Aro: " + energy + " holdVal: " + holdVal + " holdAro: " + holdAro + " measure: " + mCount + " beat: " + lastbeat);
            return (int)(rhythmStack[index].GetTime(mCount, lastbeat + 1) * Constants.freq);
        }

        public string GetRhythm(float valence, float energy)
        {
            StringBuilder s = new StringBuilder();
            int holdVal = (int)(valence * 1000);
            int holdAro = (int)(energy * 1000);
            holdVal = GetMin(rhythms, holdVal);
            holdAro = GetMin(rhythms[holdVal], holdAro);
            string temp = rhythmStack[rhythmLinks[rhythms[holdVal][holdAro]]].ToString();

            string[] lines = temp.Split('\n');
            for (int i = 0; i < lines.Length; ++i)
            {
                string[] tokens = lines[i].Split(' ');
                StringBuilder t = new StringBuilder(tokens[0]);
                for (int j = 2; j < tokens.Length; j++)
                {
                    t.Append(" ").Append(tokens[j]);
                }

                    for (int j = int.Parse(tokens[1].Remove(0,1)); j > 0; --j)
                {
                    s.AppendLine(t.ToString());
                }
            }

            return s.ToString();
        }

        public float[] Compile()
        {
            float[][] i = new float[0][];
            
            foreach (InstrumentManager im in ins)
            {
                if (!(im is null))
                {
                    i = i.Append(im.GetStream()).ToArray();
                }
            }
            return (PreVolumeMixer.Mix(i));
        }

        public void Compile2(float val, float energy)
        {
            // Set speed, measures and dur...
            List<InstrumentManager> retval = new List<InstrumentManager>() ;
            ins = new InstrumentManager[12];
            // Need to add instrument... need to get valence and energy from Score...
            int holdVal, holdAro, rVal, rAro;
            holdVal = (int)(val * 1000);
            holdAro = (int)(energy * 1000);
            //Debug.Log("Testing");
            rVal = GetMin(rhythms, holdVal); ;
            //Debug.Log("Testing2");
            rAro = GetMin(rhythms[rVal], holdAro);
            //Debug.Log("Testing3");
            int index = rhythmLinks[rhythms[rVal][rAro]];
            //Debug.Log("Testing4");
            //Debug.Log("Given val/energy: " + val + "/" + energy + "; Found rhythmic val/energy: " + rVal + "/" + rAro);
            RhythmTracker r = rhythmStack[index];
            //Debug.Log("Rhythm: " + r);
            //speed = rhythms[holdVal][holdAro].
            measures = GetMeasures(val, energy);
            dur = GetDuration(val, energy);
            //Debug.Log("Measures: " + measures + "; Duration: " + dur);
            /**/
            ins[0] = new InstrumentManager(new NullInstrument(), r);
            //Debug.Log("Silent Notes");
            foreach (int v in notes[index][0].Keys)
            {
                if (v < holdVal && v >= 0)
                {
                    foreach (int a in notes[index][0][v].Keys)
                    {
                        if (a < holdAro && a >= 0)
                        {
                            foreach (Note n in notes[index][0][v][a])
                            {
                                ins[0].AddNote(n);
                                //Debug.Log(n);
                            }
                        }
                    }
                }
            }
            ins[0].Sort();
            ins[1] = new InstrumentManager(new SquareWaveInstrument(), r);
            int staticV = GetMin(staticNotes[index], holdVal);
            int staticA = GetMin(staticNotes[index][staticV], holdAro);
            //Debug.Log("Melody Notes (?)" + staticV + "/" + staticA + "/" + staticNotes[index][staticV][staticA].Count);
            foreach (Note n in staticNotes[index][staticV][staticA])
                            {
                                ins[1].AddNote(n);
                                //Debug.Log(n);
                            }
            ins[1].Sort();

            ins[2] = new InstrumentManager(new SineWaveInstrument(), r);
            //Debug.Log("Bass Notes (?)");
            foreach (int v in notes[index][5].Keys)
            {
                if (v < holdVal && v >= 0)
                {
                    foreach (int a in notes[index][5][v].Keys)
                    {
                        if (a < holdAro && a >= 0)
                        {
                            foreach (Note n in notes[index][5][v][a])
                            {
                                ins[2].AddNote(n);
                                //Debug.Log(n);
                            }
                        }
                    }
                }
            }
            ins[2].Sort();

            /**/
            ins[3] = new DrumManager(new BassDrumInstrument(Constants.freq, .5f), r);
           // Debug.Log("Bass Drum Notes (?)");
            int count = 0;
            foreach (int v in notes[index][10].Keys)
            {
                if (v < holdVal && v >= 0)
                {
                    foreach (int a in notes[index][10][v].Keys)
                    {
                        if (a < holdAro && a >= 0)
                        {
                            foreach (Note n in notes[index][10][v][a])
                            {
                                ins[3].AddNote(n);
                                //Debug.Log(n);
                                count++;
                            }
                        }
                    }
                }
            }
            ins[3].Sort();

            //Debug.Log("Bass Notes added: " + count);

            ins[4] = new DrumManager(new SnareDrumInstrument(Constants.freq, .5f), r);
            //Debug.Log("Snare Drum Notes (?)");
            foreach (int v in notes[index][11].Keys)
            {
                if (v < holdVal && v >= 0)
                {
                    foreach (int a in notes[index][11][v].Keys)
                    {
                        if (a < holdAro && a >= 0)
                        {
                            foreach (Note n in notes[index][11][v][a])
                            {
                                ins[4].AddNote(n);
                                //Debug.Log(n);
                            }
                        }
                    }
                }
            }
            ins[4].Sort();

            ins[5] = new DrumManager(new CymbalInstrument(Constants.freq, .5f), r);
            //Debug.Log("High-hat Notes (?)");
            foreach (int v in notes[index][12].Keys)
            {
                if (v < holdVal && v >= 0)
                {
                    foreach (int a in notes[index][12][v].Keys)
                    {
                        if (a < holdAro && a >= 0)
                        {
                            foreach (Note n in notes[index][12][v][a])
                            {
                                ins[5].AddNote(n);
                                //Debug.Log(n);
                            }
                        }
                    }
                }
            }
            ins[5].Sort();

        }

        public string GetOddRhythm()
        {
            return (GetOddRhythm(gen));
        }
        public string GetOddRhythm(System.Random gen)
        {
            Dictionary<string, double> oddRhythms = new Dictionary<string, double>();
            oddRhythms[" D W W S W "] = .05;
            oddRhythms[" D W S W W "] = .05;
            oddRhythms[" D W S W S "] = .05;
            oddRhythms[" D W S W S W S "] = .05;
            oddRhythms[" D W S W S W W "] = .05;
            oddRhythms[" D W S W W S W "] = .05;
            oddRhythms[" D W W S W S W "] = .05;
            oddRhythms[" D W W S W W S "] = .05;
            oddRhythms[" D W S W S W S W S W S "] = .05;
            oddRhythms[" D W S W S W S W S W W "] = .05;
            oddRhythms[" D W S W S W S W W S W "] = .05;
            oddRhythms[" D W S W S W W S W S W "] = .05;
            oddRhythms[" D W S W W S W S W S W "] = .05;
            oddRhythms[" D W W S W S W S W S W "] = .05;
            oddRhythms[" D W W S W W S W S W W "] = .05;
            oddRhythms[" D W W S W W S W W S W "] = .05;
            oddRhythms[" D W W S W S W W S W W "] = .05;
            oddRhythms[" D W S W W S W W S W W "] = .05;
            oddRhythms[" D W S W S W W S W W S "] = .05;
            oddRhythms["odd"] = .05;
            string test = Score.DecideProb(oddRhythms, gen.NextDouble(), "odd");
            if (test != "odd")
            {
                return (test);
            }

            StringBuilder retval = new StringBuilder();
            Dictionary<string, double> start = new Dictionary<string, double>(), remain = new Dictionary<string, double>();
            start[" D W W"] = .5;
            start[" D W"] = .5;

            retval.Append(Score.DecideProb(start, gen.NextDouble(), " D W W "));

            remain["eps"] = .01;
            remain[" S W"] = .3;
            remain[" S W W"] = .3;
            remain[" D W"] = .19;
            remain[" D W W"] = .19;
            remain["Seps"] = .01;

            test = Score.DecideProb(remain, gen.NextDouble(), " S W");

            while (test != "eps")
            {
                if (test == "Seps")
                {
                    retval.Append(" S");
                    break;
                }
                retval.Append(test);

                if (test[1] == 'S')
                {
                    remain["eps"] += .05;
                    remain[" S W"] -= .08;
                    remain[" S W W"] -= .08;
                    remain[" D W"] += .03;
                    remain[" D W W"] += .03;
                    remain["Seps"] += .05;
                }
                else
                {
                    remain["eps"] -= .01;
                    remain[" S W"] += .09;
                    remain[" S W W"] += .09;
                    remain[" D W"] -= .08;
                    remain[" D W W"] -= .08;
                    remain["Seps"] -= .01;
                }
                test = Score.DecideProb(remain, gen.NextDouble(), "eps");
            }
            retval.Append(" ");
            return (retval.ToString());
        }

        private protected class NoteHolder
        {
            public int measure, noteNumber;
            public float beat, length, pitch, selfVal, selfAro, rhythmVal, rhythmAro;
            public string type;

            public NoteHolder(int measure, float beat, float length = 0, float pitch = Notes.REST, string type = "",
                float selfVal = 0, float selfAro = 0, float rhythmVal = 0, float rhythmAro = 0, int noteNumber = 0)
            {
                this.measure = measure;
                this.beat = beat;
                this.length = length;
                this.pitch = pitch;
                this.type = type;
                this.selfAro = selfAro;
                this.selfVal = selfVal;
                this.rhythmVal = rhythmVal;
                this.rhythmAro = rhythmAro;
                this.noteNumber = noteNumber;
            }
        }

    }

    class Section3
    {
        public RhythmTracker r;

        public Dictionary<int, Dictionary<int, RhythmTracker>> rhythms; // Valence, then energy, as energy will (currently) make the most difference...
        public InstrumentManager[] ins;
        public DrumManagerManager drummer;
        private Dictionary<int, Dictionary<int, List<Note>>>[] notes; // Wait, we need one for each valence or energy value too...
                                                                      //    private Dictionary<int, Dictionary<int, List<Note>>> spaceHolder;
        string name;
        int sectionSeed;
        private double speed;
        int holdNote;
        public int dur { get; private set; }
        public int measures { get; private set; }

        private System.Random gen;

        private Section3()
        {

        }

        public Section3(int seed = 0, string n = "", double speed = 120)
        {
            sectionSeed = seed;
            name = n;
            gen = new System.Random(seed);
            //Debug.Log("Section Seed: " + seed);
            ins = new InstrumentManager[12];
            dur = 0;
            this.speed = speed;
            this.holdNote = 0;
            notes = new Dictionary<int, Dictionary<int, List<Note>>>[13];
            for (int i = 0; i < notes.Length; ++i)
            {
                notes[i] = new Dictionary<int, Dictionary<int, List<Note>>>();
            }
        }

        private void addNote(Dictionary<int, Dictionary<int, List<Note>>> dict, int key1, int key2, Note note, bool copyOld = true)
        {
            if (!dict.ContainsKey(key1))
            {
                dict.Add(key1, new Dictionary<int, List<Note>>());
                if (copyOld)
                {
                    int prevKey = GetMin(dict, key1 - 1);
                    if (prevKey > -1)
                    {
                        foreach (int i in dict[prevKey].Keys)
                        {
                            dict[key1].Add(i, new List<Note>());
                            foreach (Note n in dict[prevKey][i])
                            {
                                dict[key1][i].Add(n);
                            }
                        }
                    }
                }
            }
            if (!dict[key1].ContainsKey(key2))
            {
                dict[key1].Add(key2, new List<Note>());
                if (copyOld)
                {
                    int prevKey = GetMin(dict[key1], key2 - 1);
                    if (prevKey > -1)
                    {
                        foreach (Note n in dict[key1][prevKey])
                        {
                            dict[key1][key2].Add(n);
                        }
                    }
                }
            }
            dict[key1][key2].Add(note);
        }

        private void addNoteToRangeOfVals(Dictionary<int, Dictionary<int, List<Note>>> dict, int key1, int maxKey1, int key2, Note note, bool copyOld = true)
        {
            addNote(dict, key1, key2, note, copyOld);
            foreach (int testVal in dict.Keys)
            {
                if (testVal <= key1 || testVal > maxKey1)
                {
                    continue;
                }
                if (dict[testVal].ContainsKey(key2))
                {
                    dict[testVal][key2].Add(note);
                }
            }
        }
        private void addToDict<T>(Dictionary<int, Dictionary<int, List<T>>> dict, int key1, int key2, T note, bool copyOld = true)
        {
            if (!dict.ContainsKey(key1))
            {
                dict.Add(key1, new Dictionary<int, List<T>>());
                if (copyOld)
                {
                    int prevKey = GetMin(dict, key1 - 1);
                    if (prevKey > -1)
                    {
                        foreach (int i in dict[prevKey].Keys)
                        {
                            dict[key1].Add(i, new List<T>());
                            foreach (T n in dict[prevKey][i])
                            {
                                dict[key1][i].Add(n);
                            }
                        }
                    }
                }
            }
            if (!dict[key1].ContainsKey(key2))
            {
                dict[key1].Add(key2, new List<T>());
                if (copyOld)
                {
                    int prevKey = GetMin(dict[key1], key2 - 1);
                    if (prevKey > -1)
                    {
                        foreach (T n in dict[key1][prevKey])
                        {
                            dict[key1][key2].Add(n);
                        }
                    }
                }
            }
            dict[key1][key2].Add(note);
        }
        private int GetMin<T>(Dictionary<int, T> d, int valToFind)
        {
            if (d.ContainsKey(valToFind))
            {
                return valToFind;
            }
            int hold = -1;
            foreach (int testKey in d.Keys.Reverse())
            {
                if (testKey <= valToFind && testKey > hold)
                {
                    hold = testKey;
                }
            }
            return (hold);
        }

        private int GetNextKey<T>(Dictionary<int, T> d, int keyToFind)
        {
            if (d.ContainsKey(++keyToFind))
            {
                return (keyToFind);
            }
            int hold = d.Keys.Max();
            foreach (int testKey in d.Keys.Reverse())
            {
                if (testKey >= keyToFind && testKey < hold)
                {
                    hold = testKey;
                }
            }
            return (hold);
        }

        private void DeepCopy<T>(Dictionary<int, T> from, Dictionary<int, T> to)
        {
            to = new Dictionary<int, T>(from);
        }

        public void DeriveSection(float energy, float valence)
        {
            int melodySeed = gen.Next();
            int harmSeed = gen.Next();
            int rhythmSeed = gen.Next();
            int testVal = gen.Next(0, 6);
            DecideTimeSignature2();
            switch (testVal)
            {
                case 0:
                    DeriveMelody(energy, valence, melodySeed);
                    DeriveHarmony(energy, valence, harmSeed);
                    DeriveRhythm(energy, valence, rhythmSeed);
                    break;

                case 1:
                    DeriveMelody(energy, valence, melodySeed);
                    DeriveRhythm(energy, valence, rhythmSeed);
                    DeriveHarmony(energy, valence, harmSeed);
                    break;

                case 2:
                    DeriveHarmony(energy, valence, harmSeed);
                    DeriveMelody(energy, valence, melodySeed);
                    DeriveRhythm(energy, valence, rhythmSeed);
                    break;

                case 3:
                    DeriveHarmony(energy, valence, harmSeed);
                    DeriveRhythm(energy, valence, rhythmSeed);
                    DeriveMelody(energy, valence, melodySeed);
                    break;

                case 4:
                    DeriveRhythm(energy, valence, rhythmSeed);
                    DeriveMelody(energy, valence, melodySeed);
                    DeriveHarmony(energy, valence, harmSeed);
                    break;

                default:
                    DeriveRhythm(energy, valence, rhythmSeed);
                    DeriveHarmony(energy, valence, harmSeed);
                    DeriveMelody(energy, valence, melodySeed);
                    break;
            }
        }

        private void DecideTimeSignature(float energy, float valence)
        {/*
                if (name == "A")
                    r = new RhythmTracker(Constants.freq, "120 D W S W S W S W 4");
                else
                {
                    r = new RhythmTracker(Constants.freq, "240 D W W S W W S W 4");
                }
                */
            string tracker = "";

            // Decide speed
            // a 1 = 60-70 bpm; a 5 = 180-200 bpm
            // double d = (gen.NextDouble() * (29.375 + .625 * energy) + (28.125 + 1.875 * energy)) * 2;
            //double d = (57.5 + 2.5 * energy) * 2;
            double d = speed * 2;

            Dictionary<int, double> measureProb = new Dictionary<int, double>();
            Dictionary<string, double> beatsProb = new Dictionary<string, double>();
            // Decide signatures
            int numMeasures = 0;
            double numMeasuresDecider = gen.NextDouble();
            if (valence < 4.0)
            {
                measureProb[1] = 1.0 / 28;
                measureProb[2] = 4.0 / 28;
                measureProb[4] = 9.0 / 28;
                measureProb[8] = 9.0 / 28;
                measureProb[16] = 4.0 / 28;
                measureProb[32] = 1.0 / 28;
                numMeasures = Score.DecideProb(measureProb, gen.NextDouble(), 4);

                // This should be .5 to .25 from 1 to 5 energy.
                //                    beatsProb[" D W S W S W "] = .5625f - .0625f * energy;
                beatsProb[" D W S W S W "] = .28125f - .03125f * energy;
                beatsProb[" D W W S W W "] = .28125f - .03125f * energy;
                beatsProb[" D W S W S W S W "] = .5 - beatsProb[" D W S W S W "];
                beatsProb[" D W W S W W S W "] = beatsProb[" D W S W S W S W "];

                tracker = d.ToString();
                tracker += Score.DecideProb(beatsProb, gen.NextDouble(), " D W S W S W S W ");
                tracker += numMeasures.ToString();
            }
            else
            {
                measureProb[1] = 9.0 / 280;
                measureProb[2] = 40.0 / 280;
                measureProb[4] = 90.0 / 280;
                measureProb[8] = 90.0 / 280;
                measureProb[16] = 40.0 / 280;
                measureProb[32] = 9.0 / 280;
                measureProb[-1] = 2.0 / 280;
                numMeasures = Score.DecideProb(measureProb, gen.NextDouble(), 4);
                if (numMeasures == -1)
                {
                    measureProb[-1] = 0;
                    for (int i = 1; i <= 32; ++i)
                    {
                        measureProb[i] = 1.0 / 31;
                    }
                    numMeasures = Score.DecideProb(measureProb, gen.NextDouble(), 8);
                }

                int currMeasure = 0;
                while (currMeasure < numMeasures)
                {
                    if (currMeasure > 0)
                    {
                        tracker += "\n";
                    }
                    string rhythm = "";
                    int countMeasure = 1;
                    // This should be .5 to .25 from 1 to 5 energy.
                    //                    beatsProb[" D W S W S W "] = .5625f - .0625f * energy;
                    beatsProb[" D W S W S W "] = .28125f - .03125f * energy - .125 * (energy - 4);
                    beatsProb[" D W W S W W "] = beatsProb[" D W S W S W "];
                    beatsProb[" D W S W S W S W "] = .21875 + .03125f * energy - .25 * (energy - 4);
                    beatsProb[" D W W S W W S W "] = beatsProb[" D W S W S W S W "];
                    beatsProb["odd"] = .5 * (energy - 4);
                    d = speed * 2;

                    rhythm = Score.DecideProb(beatsProb, gen.NextDouble(), "odd");
                    if (rhythm == "odd")
                    {
                        rhythm = GetOddRhythm();
                        countMeasure = rhythm.Count(b => b == 'D');
                        if (gen.NextDouble() < 3 * (energy - 4) / 4)
                        {
                            d = speed * 4;
                        }
                    }
                    int reps = 1;
                    while (currMeasure + reps * countMeasure < numMeasures && gen.NextDouble() < 1 - .05 * energy)
                    {
                        reps++;
                    }
                    //Debug.Log(rhythm);
                    tracker += d + rhythm + " " + reps;
                    currMeasure += reps * countMeasure;
                }
            }
            // Debug.Log(tracker);
            r = new RhythmTracker(Constants.freq, tracker);
            InstrumentManager silent = new InstrumentManager(new SineWaveInstrument(Constants.freq, 0f), r);
            ins[0] = silent;
            int mCount = 0;
            int lastbeat = 0;
            foreach (RhythmTracker.Measure m in r.GetMeasures())
            {
                mCount++;
                // Debug.Log("Beats: " + m.beats.Length);
                addNote(notes[0], 0, 0, new Note(mCount, m.beats.Length, Notes.REST, 1));
                silent.AddNote(new Note(mCount, m.beats.Length, Notes.REST, 1));
                lastbeat = m.beats.Length;
            }
            dur = (int)(r.GetTime(mCount, lastbeat + 1) * Constants.freq);
            measures = mCount;
            //            Debug.Log("Last: " + mCount + " " + lastbeat + " " + dur);
        }
        private void DecideTimeSignature2()
        {
            //string tracker = "";
            Dictionary<int, Dictionary<int, string>> trackers = new Dictionary<int, Dictionary<int, string>>(), oldTrackers = trackers;
            Dictionary<int, Dictionary<int, int>> currMeasures = new Dictionary<int, Dictionary<int, int>>(), oldMeasures = currMeasures;
            Dictionary<string, int> tempTrackersLink = new Dictionary<string, int>();
            List<int[]> tempTrackersStats = new List<int[]>();
            List<string> tempTrackersStack = new List<string>();
            Dictionary<int, int> measures = new Dictionary<int, int>();
            Dictionary<int, int[]> rhythmStuff;
            // Decide speed
            double d = speed * 2;

            Dictionary<int, double> measureProb = new Dictionary<int, double>();
            double numMeasuresDecider = gen.NextDouble();

            if (numMeasuresDecider < 1.0 / 28)
            {
                if (numMeasuresDecider < 1.0 / 280)// impossible || numMeasuresDecider >= 1 - 1.0 / 28)
                {
                    measures[1000] = 1;
                    measureProb[-1] = 0;
                    for (int i = 1; i <= 32; ++i)
                    {
                        measureProb[i] = 1.0 / 31;
                    }
                    measures[4] = Score.DecideProb(measureProb, gen.NextDouble(), 8);
                }
                else
                {
                    measures[0] = 1;
                }
            }
            else if (numMeasuresDecider >= 1 - 1.0 / 28)
            {
                if (numMeasuresDecider >= 1 - 1.0 / 280)
                {
                    measures[1000] = 32;
                    measureProb[-1] = 0;
                    for (int i = 1; i <= 32; ++i)
                    {
                        measureProb[i] = 1.0 / 31;
                    }
                    measures[4] = Score.DecideProb(measureProb, gen.NextDouble(), 8);
                }
                else
                {
                    measures[0] = 32;
                }
            }
            else if (numMeasuresDecider < 5.0 / 28)
            {
                measures[0] = 2;
            }
            else if (numMeasuresDecider < 14.0 / 28)
            {
                measures[0] = 4;
            }
            else if (numMeasuresDecider < 23.0 / 28)
            {
                measures[0] = 8;
            }
            else if (numMeasuresDecider < 27.0 / 28)
            {
                measures[0] = 4;
            }
            // Decide signatures
            // This should be .5 to .25 from 1 to 5 energy.
            /*             //                    beatsProb[" D W S W S W "] = .5625f - .0625f * energy;
                         beatsProb[" D W S W S W "] = .28125f - .03125f * energy - .125 * (valence - 4);    // .25 - .125 / .125 - 0   / 0 - .25 / 0 - .125 
                         beatsProb[" D W W S W W "] = beatsProb[" D W S W S W "];                            // .25 - .125 / .125 - 0   / .25 - .5 / .125 - .25
                         beatsProb[" D W S W S W S W "] = .21875 + .03125f * energy - .125 * (valence - 4); // .25 - .375 / .125 - .25 / .5 - .75 / .25 - .625
                         beatsProb[" D W W S W W S W "] = beatsProb[" D W S W S W S W "];                    // .25 - .375 / .125 - .25 / .75 - 1 / .625 - 1
                         beatsProb["odd"] = .5 * (valence - 4);                                              // 0 - .5
            */
            //                double tempDouble;
            //                int tempInt;
            // Give indexes for each rhythm; get notes for each set, all valences and energys.
            int currMeasure = 0;
            bool doContinue = false;
            do
            {
                oldTrackers = new Dictionary<int, Dictionary<int, string>>(trackers);
                oldMeasures = new Dictionary<int, Dictionary<int, int>>(currMeasures);
                ++currMeasure;
                doContinue = false;
                double beatsDecider = gen.NextDouble();
                //            Dictionary<int, Dictionary<int, string>> holdRhythms = new Dictionary<int, Dictionary<int, string>>();
                //           holdRhythms[0] = new Dictionary<int, string>();
                // Dictionary<string, string> rhythms = new Dictionary<string, string>();
                int oddRhythmSeed = gen.Next(), repsSeed = gen.Next();
                string oddRhythm = GetOddRhythm(new System.Random(oddRhythmSeed));
                rhythmStuff = GetEnergyRhythmStuff(repsSeed);

                
                int next = 0;
                if (beatsDecider < .25)
                {
                    //                    holdRhythms[0][0] = " D W S W S W ";
                    next = 5000;
                    if (beatsDecider >= .125)
                    {
                        next = (int)Math.Ceiling(9000 - 32000 * beatsDecider);
                        //                        tempDouble = (9 - 32 * beatsDecider);
                        //                        tempInt = (int)Math.Ceiling(tempDouble * 1000);
                        AddLine(0, next, 5000, " D W W S W W ");
                        //                        holdRhythms[0][(int)Math.Ceiling(9000 - 32000 * beatsDecider)] = " D W W S W W ";
                    }
                    AddLine(0, 0, next, " D W S W S W ");
                }
                else if (beatsDecider < .5)
                {
                    next = (int)Math.Ceiling(9000 - 16000 * beatsDecider);
                    AddLine(0, next, 5000, " D W S W S W S W ");
                    AddLine(0, 0, next, " D W W S W W ");
                    //                    holdRhythms[0][0] = " D W W S W W ";
                    //                    tempDouble = 9 - 16 * beatsDecider;
                    //                    holdRhythms[0][(int)Math.Ceiling(9000 - 16000 * beatsDecider)] = " D W S W S W S W ";
                }
                else if (beatsDecider < .75)
                {
                    next = 5000;
                    //                    holdRhythms[0][0] = " D W S W S W S W ";
                    if (beatsDecider >= .625)
                    {
                        next = (int)Math.Ceiling(25000 - 32000 * beatsDecider);
                        //tempDouble = 25 - 32 * beatsDecider;
                        //                      holdRhythms[0][(int)Math.Ceiling(25000 - 32000 * beatsDecider)] = " D W W S W W S W ";
                        AddLine(0, next, 5000, " D W W S W W S W ");
                    }
                    AddLine(0, 0, next, " D W S W S W S W ");
                }
                else
                {
                    //                    holdRhythms[0][0] = " D W W S W W S W ";
                    AddLine(0, 0, 5000, " D W W S W W S W ");
                }
                /**/
                for (int tempInt = 4000; tempInt <= 5000; ++tempInt)
                //                for (double v = 0; v <= 1; v += .001)
                {
                    double v = (tempInt - 4000) / 1000.0;
                    //holdRhythms[tempInt] = new Dictionary<int, string>();
                    if (beatsDecider < .25 - v * .125)
                    {
                        next = 5000;
                        //  holdRhythms[tempInt][0] = " D W S W S W ";
                        if (beatsDecider >= .125 - v * .125)
                        {
                            //                        tempDouble = (9 - 32 * beatsDecider);
                            //                        tempInt = (int)Math.Ceiling(tempDouble * 1000);
                            next = (int)Math.Ceiling(9000 - 32000 * beatsDecider - 4000 * v);
                            // holdRhythms[tempInt][(int)Math.Ceiling(9000 - 32000 * beatsDecider - 4000 * v)] = " D W W S W W ";
                            AddLine(tempInt, next, 5000, " D W W S W W ");
                        }
                        AddLine(tempInt, 0, 5000, " D W S W S W ");
                    }
                    else if (beatsDecider < .5 - v * .25)
                    {
                        next = (int)Math.Ceiling(9000 - 16000 * beatsDecider - 4000 * v);
                        // holdRhythms[tempInt][0] = " D W W S W W ";
                        //                    tempDouble = 9 - 16 * beatsDecider;
                        // holdRhythms[tempInt][(int)Math.Ceiling(9000 - 16000 * beatsDecider - 4000 * v)] = " D W S W S W S W ";
                        AddLine(tempInt, 0, next, " D W W S W W ");
                        AddLine(tempInt, next, 5000, " D W S W S W S W ");
                    }
                    else if (beatsDecider < .75 - v * .375)
                    {
                        next = 5000;
                        //holdRhythms[tempInt][0] = " D W S W S W S W ";
                        if (beatsDecider >= .625 - v * .375)
                        {
                            next = (int)Math.Ceiling(25000 - 32000 * beatsDecider);
                            //tempDouble = 25 - 32 * beatsDecider;
                            // holdRhythms[tempInt][(int)Math.Ceiling(25000 - 32000 * beatsDecider)] = " D W W S W W S W ";
                            AddLine(tempInt, next, 5000, " D W W S W W S W ");
                        }
                        AddLine(tempInt, 0, next, " D W S W S W S W ");
                    }
                    else if (beatsDecider < 1 - v * .5)
                    {
                        // holdRhythms[tempInt][0] = " D W W S W W S W ";
                        AddLine(tempInt, 0, 5000, " D W W S W W S W ");
                    }
                    else
                    {
                        //                        holdRhythms[tempInt][0] = "odd";
                        AddLine(tempInt, 0, 5000, oddRhythm);
                    }

                }
                //Debug.Log("Created strings");
                // Create Rhythms


            } while (doContinue && currMeasure <= 50);// currMeasure < numMeasures);

            if (currMeasure > 50)
            {
                //Debug.Log("May have broken out due to currMeasures...");
            }

            rhythms = new Dictionary<int, Dictionary<int, RhythmTracker>>();
            foreach (int val in trackers.Keys)
            {
                rhythms[val] = new Dictionary<int, RhythmTracker>();
                foreach (int aro in trackers[val].Keys)
                {
                    // Debug.Log("Did this get executed?");
                    // Debug.Log(tracker);
                    r = new RhythmTracker(Constants.freq, trackers[val][aro]);
                    rhythms[val][aro] = r;
                    //                        InstrumentManager silent = new InstrumentManager(new SineWaveInstrument(Constants.freq, 0f), r);
                    //                        ins[0] = silent;
                    int mCount = 0;
                    int lastbeat = 0;
                    foreach (RhythmTracker.Measure m in r.GetMeasures())
                    {
                        mCount++;
                        // Debug.Log("Beats: " + m.beats.Length);
                        addNote(notes[0], val, aro, new Note(mCount, m.beats.Length, Notes.REST, 1), false);
                        lastbeat = m.beats.Length;
                    }
                    dur = (int)(r.GetTime(mCount, lastbeat + 1) * Constants.freq);
                    if (mCount > this.measures)
                    {
                        this.measures = mCount;
                    }
                }
            }
            //            Debug.Log("Last: " + mCount + " " + lastbeat + " " + dur);

            void AddLine(int valence, int energy, int nextKey, string rhythm)
            {
                int holdVal = GetMin(oldTrackers, valence);
                int holdAro, measureAro;
                measureAro = GetMin(measures, energy);

                // Does this need to be included in rhythm stuff checks?
                if (holdVal != -1)
                {
                    holdAro = GetMin(oldTrackers[holdVal], energy);
                    if (holdAro != -1)
                    {
                        if (measures[measureAro] <= oldMeasures[holdVal][holdAro])
                        {
                            return;
                        }
                    }
                }

                if (!trackers.ContainsKey(valence))
                {
                    if (holdVal == -1)
                    {
                        trackers.Add(valence, new Dictionary<int, string>());
                        currMeasures.Add(valence, new Dictionary<int, int>());
                    }
                    else
                    {
                        trackers[valence] = new Dictionary<int, string>(oldTrackers[holdVal]);
                        currMeasures[valence] = new Dictionary<int, int>(oldMeasures[holdVal]);
                    }
                }

                int numMeasures = measures[measureAro];
                if (!trackers[valence].ContainsKey(energy))
                {
                    holdAro = GetMin(trackers[valence], energy);
                    if (holdAro == -1 || holdVal == -1)
                    {
                        trackers[valence].Add(energy, "");
                        currMeasures[valence].Add(energy, 0);
                    }
                    else
                    {
                        trackers[valence][energy] = trackers[valence][holdAro];
                        currMeasures[valence][energy] = currMeasures[valence][holdAro];
                    }
                }

                if (currMeasures[valence][energy] > 0)
                {
                    trackers[valence][energy] += "\n";
                }
                int countMeasure = rhythm.Count(b => b == 'D');

                int reps = 1;
                int holdKey = GetMin(rhythmStuff, energy);
                //Debug.Log("Holdkey is " + holdKey);
                //Still need to split out rhythm stuff...
                d = rhythmStuff[holdKey][0] * speed * 2;
                while (currMeasures[valence][energy] + reps * countMeasure < numMeasures && reps < rhythmStuff[holdKey][1])
                {
                    reps++;
                }
                //                        Debug.Log(rhythm);
                // trackers[valence][energy] += d + rhythm + " " + reps;
                trackers[valence][energy] += d + rhythm + " " + reps;

                currMeasures[valence][energy] += reps * countMeasure;
                if (currMeasures[valence][energy] < numMeasures)
                {
                    //need to finish...
                    doContinue = true;
                }

                //Debug.Log("Holdkey " + holdKey + " worked.");
                if (energy < nextKey)
                {
                    foreach (int aroOther in rhythmStuff.Keys)
                    {
                        if (aroOther > energy && aroOther <= nextKey)
                        {
                            //Debug.Log("aroOther is " + aroOther);
                            if (!trackers[valence].ContainsKey(aroOther))
                            {
                                if (holdVal == -1)
                                {
                                    trackers[valence].Add(aroOther, "");
                                    currMeasures[valence].Add(aroOther, 0);
                                }
                                else
                                {
                                    int temp = GetMin(oldTrackers[holdVal], aroOther);
                                    if (temp == -1)
                                    {
                                        trackers[valence].Add(aroOther, "");
                                        currMeasures[valence].Add(aroOther, 0);
                                    }
                                    else
                                    {
                                        trackers[valence][aroOther] = oldTrackers[holdVal][temp];
                                        currMeasures[valence][aroOther] = oldMeasures[holdVal][temp];
                                    }
                                }
                            }
                            if (currMeasures[valence][aroOther] > 0)
                            {
                                trackers[valence][aroOther] += "\n";
                            }
                            countMeasure = rhythm.Count(b => b == 'D');
                            reps = 1;
                            holdKey = GetMin(rhythmStuff, aroOther);
                            d = rhythmStuff[holdKey][0] * speed * 2;
                            while (currMeasures[valence][aroOther] + reps * countMeasure < numMeasures && reps < rhythmStuff[holdKey][1])
                            {
                                reps++;
                            }
                            //                        Debug.Log(rhythm);
                            trackers[valence][aroOther] += d + rhythm + " " + reps;
                            currMeasures[valence][aroOther] += reps * countMeasure;
                            if (currMeasures[valence][aroOther] < numMeasures)
                            {
                                //need to finish...
                                doContinue = true;
                            }
                        }
                    }
                }
            }
        }
        /*
        private void DecideTimeSignature2Old()
        {/*
                if (name == "A")
                    r = new RhythmTracker(Constants.freq, "120 D W S W S W S W 4");
                else
                {
                    r = new RhythmTracker(Constants.freq, "240 D W W S W W S W 4");
                }

            //string tracker = "";
            Dictionary<int, Dictionary<int, string>> trackers = new Dictionary<int, Dictionary<int, string>>(), oldTrackers = trackers;
            Dictionary<int, Dictionary<int, int>> currMeasures = new Dictionary<int, Dictionary<int, int>>(), oldMeasures = currMeasures;
            Dictionary<int, int> measures = new Dictionary<int, int>();
            Dictionary<int, int[]> rhythmStuff;
            // Decide speed
            // a 1 = 60-70 bpm; a 5 = 180-200 bpm
            // double d = (gen.NextDouble() * (29.375 + .625 * energy) + (28.125 + 1.875 * energy)) * 2;
            //double d = (57.5 + 2.5 * energy) * 2;
            double d = speed * 2;

            Dictionary<int, double> measureProb = new Dictionary<int, double>();
            //Dictionary<string, double> beatsProb = new Dictionary<string, double>();
            double numMeasuresDecider = gen.NextDouble();

            if (numMeasuresDecider < 1.0 / 28)
            {
                if (numMeasuresDecider < 1.0 / 280)// impossible || numMeasuresDecider >= 1 - 1.0 / 28)
                {
                    measures[1000] = 1;
                    measureProb[-1] = 0;
                    for (int i = 1; i <= 32; ++i)
                    {
                        measureProb[i] = 1.0 / 31;
                    }
                    measures[4] = Score.DecideProb(measureProb, gen.NextDouble(), 8);
                }
                else
                {
                    measures[0] = 1;
                }
            }
            else if (numMeasuresDecider >= 1 - 1.0 / 28)
            {
                if (numMeasuresDecider >= 1 - 1.0 / 280)
                {
                    measures[1000] = 32;
                    measureProb[-1] = 0;
                    for (int i = 1; i <= 32; ++i)
                    {
                        measureProb[i] = 1.0 / 31;
                    }
                    measures[4] = Score.DecideProb(measureProb, gen.NextDouble(), 8);
                }
                else
                {
                    measures[0] = 32;
                }
            }
            else if (numMeasuresDecider < 5.0 / 28)
            {
                measures[0] = 2;
            }
            else if (numMeasuresDecider < 14.0 / 28)
            {
                measures[0] = 4;
            }
            else if (numMeasuresDecider < 23.0 / 28)
            {
                measures[0] = 8;
            }
            else if (numMeasuresDecider < 27.0 / 28)
            {
                measures[0] = 4;
            }
            // Decide signatures
            // This should be .5 to .25 from 1 to 5 energy.
            /*             //                    beatsProb[" D W S W S W "] = .5625f - .0625f * energy;
                         beatsProb[" D W S W S W "] = .28125f - .03125f * energy - .125 * (valence - 4);    // .25 - .125 / .125 - 0   / 0 - .25 / 0 - .125 
                         beatsProb[" D W W S W W "] = beatsProb[" D W S W S W "];                            // .25 - .125 / .125 - 0   / .25 - .5 / .125 - .25
                         beatsProb[" D W S W S W S W "] = .21875 + .03125f * energy - .125 * (valence - 4); // .25 - .375 / .125 - .25 / .5 - .75 / .25 - .625
                         beatsProb[" D W W S W W S W "] = beatsProb[" D W S W S W S W "];                    // .25 - .375 / .125 - .25 / .75 - 1 / .625 - 1
                         beatsProb["odd"] = .5 * (valence - 4);                                              // 0 - .5
            
            //                double tempDouble;
            //                int tempInt;
            // Give indexes for each rhythm; get notes for each set, all valences and energys.
            int currMeasure = 0;
            bool doContinue = false;
            do
            {
                oldTrackers = new Dictionary<int, Dictionary<int, string>>(trackers);
                oldMeasures = new Dictionary<int, Dictionary<int, int>>(currMeasures);
                ++currMeasure;
                doContinue = false;
                double beatsDecider = gen.NextDouble();
                //            Dictionary<int, Dictionary<int, string>> holdRhythms = new Dictionary<int, Dictionary<int, string>>();
                //           holdRhythms[0] = new Dictionary<int, string>();
                // Dictionary<string, string> rhythms = new Dictionary<string, string>();
                int oddRhythmSeed = gen.Next(), repsSeed = gen.Next();
                string oddRhythm = GetOddRhythm(new System.Random(oddRhythmSeed));
                rhythmStuff = GetenergyRhythmStuff(repsSeed);

                //  Dictionary<sting, int[]> 
                int next = 0;
                if (beatsDecider < .25)
                {
                    //                    holdRhythms[0][0] = " D W S W S W ";
                    next = 5000;
                    if (beatsDecider >= .125)
                    {
                        next = (int)Math.Ceiling(9000 - 32000 * beatsDecider);
                        //                        tempDouble = (9 - 32 * beatsDecider);
                        //                        tempInt = (int)Math.Ceiling(tempDouble * 1000);
                        AddLine(0, next, 5000, " D W W S W W ");
                        //                        holdRhythms[0][(int)Math.Ceiling(9000 - 32000 * beatsDecider)] = " D W W S W W ";
                    }
                    AddLine(0, 0, next, " D W S W S W ");
                }
                else if (beatsDecider < .5)
                {
                    next = (int)Math.Ceiling(9000 - 16000 * beatsDecider);
                    AddLine(0, next, 5000, " D W S W S W S W ");
                    AddLine(0, 0, next, " D W W S W W ");
                    //                    holdRhythms[0][0] = " D W W S W W ";
                    //                    tempDouble = 9 - 16 * beatsDecider;
                    //                    holdRhythms[0][(int)Math.Ceiling(9000 - 16000 * beatsDecider)] = " D W S W S W S W ";
                }
                else if (beatsDecider < .75)
                {
                    next = 5000;
                    //                    holdRhythms[0][0] = " D W S W S W S W ";
                    if (beatsDecider >= .625)
                    {
                        next = (int)Math.Ceiling(25000 - 32000 * beatsDecider);
                        //tempDouble = 25 - 32 * beatsDecider;
                        //                      holdRhythms[0][(int)Math.Ceiling(25000 - 32000 * beatsDecider)] = " D W W S W W S W ";
                        AddLine(0, next, 5000, " D W W S W W S W ");
                    }
                    AddLine(0, 0, next, " D W S W S W S W ");
                }
                else
                {
                    //                    holdRhythms[0][0] = " D W W S W W S W ";
                    AddLine(0, 0, 5000, " D W W S W W S W ");
                }
                /*
                for (int tempInt = 4000; tempInt <= 5000; ++tempInt)
                //                for (double v = 0; v <= 1; v += .001)
                {
                    double v = (tempInt - 4000) / 1000.0;
                    //holdRhythms[tempInt] = new Dictionary<int, string>();
                    if (beatsDecider < .25 - v * .125)
                    {
                        next = 5000;
                        //  holdRhythms[tempInt][0] = " D W S W S W ";
                        if (beatsDecider >= .125 - v * .125)
                        {
                            //                        tempDouble = (9 - 32 * beatsDecider);
                            //                        tempInt = (int)Math.Ceiling(tempDouble * 1000);
                            next = (int)Math.Ceiling(9000 - 32000 * beatsDecider - 4000 * v);
                            // holdRhythms[tempInt][(int)Math.Ceiling(9000 - 32000 * beatsDecider - 4000 * v)] = " D W W S W W ";
                            AddLine(tempInt, next, 5000, " D W W S W W ");
                        }
                        AddLine(tempInt, 0, 5000, " D W S W S W ");
                    }
                    else if (beatsDecider < .5 - v * .25)
                    {
                        next = (int)Math.Ceiling(9000 - 16000 * beatsDecider - 4000 * v);
                        // holdRhythms[tempInt][0] = " D W W S W W ";
                        //                    tempDouble = 9 - 16 * beatsDecider;
                        // holdRhythms[tempInt][(int)Math.Ceiling(9000 - 16000 * beatsDecider - 4000 * v)] = " D W S W S W S W ";
                        AddLine(tempInt, 0, next, " D W W S W W ");
                        AddLine(tempInt, next, 5000, " D W S W S W S W ");
                    }
                    else if (beatsDecider < .75 - v * .375)
                    {
                        next = 5000;
                        //holdRhythms[tempInt][0] = " D W S W S W S W ";
                        if (beatsDecider >= .625 - v * .375)
                        {
                            next = (int)Math.Ceiling(25000 - 32000 * beatsDecider);
                            //tempDouble = 25 - 32 * beatsDecider;
                            // holdRhythms[tempInt][(int)Math.Ceiling(25000 - 32000 * beatsDecider)] = " D W W S W W S W ";
                            AddLine(tempInt, next, 5000, " D W W S W W S W ");
                        }
                        AddLine(tempInt, 0, next, " D W S W S W S W ");
                    }
                    else if (beatsDecider < 1 - v * .5)
                    {
                        // holdRhythms[tempInt][0] = " D W W S W W S W ";
                        AddLine(tempInt, 0, 5000, " D W W S W W S W ");
                    }
                    else
                    {
                        //                        holdRhythms[tempInt][0] = "odd";
                        AddLine(tempInt, 0, 5000, oddRhythm);
                    }

                }/*
                Debug.Log("Created strings");
                // Create Rhythms

                /*
                foreach (Dictionary<int, string> v in holdRhythms.Values)
                {
                    foreach (string s in v.Values)
                    {
                        if (!rhythms.ContainsKey(s))
                        {
                            int countMeasure = 1;
                            string tempString = s;
                            if (tempString == "odd")
                            {
                                tempString = GetOddRhythm(new System.Random(oddRhythmSeed));
                                countMeasure = s.Count(b => b == 'D');
                            }
                        }
                    }
                }

                nextTrackers = new Dictionary<int, Dictionary<int, string>>(trackers);
                nextMeasures = new Dictionary<int, Dictionary<int, int>>(currMeasures);
                foreach (int val in holdRhythms.Keys)
                {
                    int holdVal = GetMin(trackers, val);
                    if (!nextTrackers.ContainsKey(val))
                    {
                        if (holdVal == -1)
                        {
                            nextTrackers.Add(val, new Dictionary<int, string>());
                            nextMeasures.Add(val, new Dictionary<int, int>());
                        }
                        else
                        {
                            nextTrackers[val] = new Dictionary<int, string>(trackers[holdVal]);
                            nextMeasures[val] = new Dictionary<int, int>(currMeasures[holdVal]);
                        }
                    }
                    foreach (int aro in holdRhythms[val].Keys)
                    {
                        int holdAro, measureAro;
                        // Does this need to be included in rhythm stuff checks?
                        measureAro = GetMin(measures, aro);
                        if (holdVal != -1)
                        {
                            holdAro = GetMin(trackers[holdVal], aro);
                            if (holdAro != -1)
                            {
                                if (measures[measureAro] <= currMeasures[holdVal][holdAro])
                                {
                                    continue;
                                }
                            }
                        }
                        System.Random gen = new System.Random(repsSeed);
                        int numMeasures = measures[measureAro];
                        int nextKey = GetNextKey(holdRhythms[val], aro);
                        if (!nextTrackers[val].ContainsKey(aro))
                        {
                            holdAro = GetMin(nextTrackers[val], aro);
                            if (holdAro == -1 || holdVal == -1)
                            {
                                nextTrackers[val].Add(aro, "");
                                nextMeasures[val].Add(aro, 0);
                            }
                            else
                            {
                                nextTrackers[val][aro] = trackers[holdVal][holdAro];
                                nextMeasures[val][aro] = currMeasures[holdVal][holdAro];
                            }
                        }

                        if (nextMeasures[val][aro] > 0)
                        {
                            nextTrackers[val][aro] += "\n";
                        }
                        string rhythm = "";
                        int countMeasure = 1;
                        
                        rhythm = holdRhythms[val][aro];
                        if (rhythm == "odd")
                        {
                            rhythm = GetOddRhythm(new System.Random(oddRhythmSeed));
                            countMeasure = rhythm.Count(b => b == 'D');
                            Debug.Log("Rhythm: " + rhythm + " " + countMeasure);
                            // Should be unneccesary...
                            countMeasure = Math.Max(1, countMeasure);
                        }
                        int reps = 1;
                        int holdKey = GetMin(rhythmStuff, aro);
                        Debug.Log("Holdkey is " + holdKey);
                        //Still need to split out rhythm stuff...
                        d = rhythmStuff[holdKey][0] * speed * 2;
                        while (nextMeasures[val][aro] + reps * countMeasure < numMeasures && reps < rhythmStuff[holdKey][1])
                        {
                            reps++;
                        }
                        //                        Debug.Log(rhythm);
                        nextTrackers[val][aro] += d + rhythm + " " + reps;
                        nextMeasures[val][aro] += reps * countMeasure;
                        if (nextMeasures[val][aro] < numMeasures)
                        {
                            //need to finish...
                            doContinue = true;
                        }
                        Debug.Log("Holdkey " + holdKey + " worked.");
                        foreach (int aroOther in rhythmStuff.Keys)
                        {
                            if (aroOther > aro && aroOther <= nextKey)
                            {
                                Debug.Log("aroOther is " + aroOther);
                                if (!nextTrackers[val].ContainsKey(aroOther))
                                {
                                    if (holdVal == -1)
                                    {
                                        nextTrackers[val].Add(aroOther, "");
                                        nextMeasures[val].Add(aroOther, 0);
                                    }
                                    else
                                    {
                                        int temp = GetMin(trackers[holdVal], aroOther);
                                        if (temp == -1)
                                        {
                                            nextTrackers[val].Add(aroOther, "");
                                            nextMeasures[val].Add(aroOther, 0);
                                        }
                                        else
                                        {
                                            nextTrackers[val][aroOther] = trackers[holdVal][temp];
                                            nextMeasures[val][aroOther] = currMeasures[holdVal][temp];
                                        }
                                    }
                                }

                                if (nextMeasures[val][aroOther] > 0)
                                {
                                    nextTrackers[val][aroOther] += "\n";
                                }
                                rhythm = "";
                                countMeasure = 1;
                                Debug.Log("Added new line to tracker...");
                                rhythm = holdRhythms[val][aro];
                                if (rhythm == "odd")
                                {
                                    rhythm = GetOddRhythm(new System.Random(oddRhythmSeed));
                                    countMeasure = rhythm.Count(b => b == 'D');
                                    Debug.Log("Rhythm: " + rhythm + " " + countMeasure);
                                    // Should be unneccesary...
                                    countMeasure = Math.Max(1, countMeasure);
                                }
                                reps = 1;
                                holdKey = GetMin(rhythmStuff, aroOther);
                                d = rhythmStuff[holdKey][0] * speed * 2;
                                while (nextMeasures[val][aroOther] + reps * countMeasure < numMeasures && reps < rhythmStuff[holdKey][1])
                                {
                                    reps++;
                                }
                                //                        Debug.Log(rhythm);
                                nextTrackers[val][aroOther] += d + rhythm + " " + reps;
                                nextMeasures[val][aroOther] += reps * countMeasure;
                                if (nextMeasures[val][aroOther] < numMeasures)
                                {
                                    //need to finish...
                                    doContinue = true;
                                }
                                Debug.Log("aroOther " + aroOther + " worked.");
                            }
                        }
                    }
                }
                
            } while (doContinue && currMeasure <= 50);// currMeasure < numMeasures);

            if (currMeasure > 50)
            {
                Debug.Log("May have broken out due to currMeasures...");
            }

            rhythms = new Dictionary<int, Dictionary<int, RhythmTracker>>();
            foreach (int val in trackers.Keys)
            {
                rhythms[val] = new Dictionary<int, RhythmTracker>();
                foreach (int aro in trackers[val].Keys)
                {
                    // Debug.Log("Did this get executed?");
                    // Debug.Log(tracker);
                    r = new RhythmTracker(Constants.freq, trackers[val][aro]);
                    //Debug.Log("Val/Aro " + val + "/" + aro + "\n" + r);
                    rhythms[val][aro] = r;
                    //                        InstrumentManager silent = new InstrumentManager(new SineWaveInstrument(Constants.freq, 0f), r);
                    //                        ins[0] = silent;
                    int mCount = 0;
                    int lastbeat = 0;
                    foreach (RhythmTracker.Measure m in r.GetMeasures())
                    {
                        mCount++;
                        // Debug.Log("Beats: " + m.beats.Length);
                        addNote(notes[0], val, aro, new Note(mCount, m.beats.Length, Notes.REST, 1), false);
                        lastbeat = m.beats.Length;
                    }
                    dur = (int)(r.GetTime(mCount, lastbeat + 1) * Constants.freq);
                    if (mCount > this.measures)
                    {
                        this.measures = mCount;
                    }
                }
            }
            //            Debug.Log("Last: " + mCount + " " + lastbeat + " " + dur);

            void AddLine(int valence, int energy, int nextKey, string rhythm)
            {
                int holdVal = GetMin(oldTrackers, valence);
                int holdAro, measureAro;
                measureAro = GetMin(measures, energy);

                // Does this need to be included in rhythm stuff checks?
                if (holdVal != -1)
                {
                    holdAro = GetMin(oldTrackers[holdVal], energy);
                    if (holdAro != -1)
                    {
                        if (measures[measureAro] <= oldMeasures[holdVal][holdAro])
                        {
                            return;
                        }
                    }
                }

                if (!trackers.ContainsKey(valence))
                {
                    if (holdVal == -1)
                    {
                        trackers.Add(valence, new Dictionary<int, string>());
                        currMeasures.Add(valence, new Dictionary<int, int>());
                    }
                    else
                    {
                        trackers[valence] = new Dictionary<int, string>(oldTrackers[holdVal]);
                        currMeasures[valence] = new Dictionary<int, int>(oldMeasures[holdVal]);
                    }
                }

                int numMeasures = measures[measureAro];
                if (!trackers[valence].ContainsKey(energy))
                {
                    holdAro = GetMin(trackers[valence], energy);
                    if (holdAro == -1 || holdVal == -1)
                    {
                        trackers[valence].Add(energy, "");
                        currMeasures[valence].Add(energy, 0);
                    }
                    else
                    {
                        trackers[valence][energy] = trackers[valence][holdAro];
                        currMeasures[valence][energy] = currMeasures[valence][holdAro];
                    }
                }

                if (currMeasures[valence][energy] > 0)
                {
                    trackers[valence][energy] += "\n";
                }
                int countMeasure = rhythm.Count(b => b == 'D');

                int reps = 1;
                int holdKey = GetMin(rhythmStuff, energy);
                //Debug.Log("Holdkey is " + holdKey);
                //Still need to split out rhythm stuff...
                d = rhythmStuff[holdKey][0] * speed * 2;
                while (currMeasures[valence][energy] + reps * countMeasure < numMeasures && reps < rhythmStuff[holdKey][1])
                {
                    reps++;
                }
                //                        Debug.Log(rhythm);
                trackers[valence][energy] += d + rhythm + " " + reps;
                currMeasures[valence][energy] += reps * countMeasure;
                if (currMeasures[valence][energy] < numMeasures)
                {
                    //need to finish...
                    doContinue = true;
                }
                //Debug.Log("Holdkey " + holdKey + " worked.");
                if (energy < nextKey)
                {
                    foreach (int aroOther in rhythmStuff.Keys)
                    {
                        if (aroOther > energy && aroOther <= nextKey)
                        {
                            Debug.Log("aroOther is " + aroOther);
                            if (!trackers[valence].ContainsKey(aroOther))
                            {
                                if (holdVal == -1)
                                {
                                    trackers[valence].Add(aroOther, "");
                                    currMeasures[valence].Add(aroOther, 0);
                                }
                                else
                                {
                                    int temp = GetMin(oldTrackers[holdVal], aroOther);
                                    if (temp == -1)
                                    {
                                        trackers[valence].Add(aroOther, "");
                                        currMeasures[valence].Add(aroOther, 0);
                                    }
                                    else
                                    {
                                        trackers[valence][aroOther] = oldTrackers[holdVal][temp];
                                        currMeasures[valence][aroOther] = oldMeasures[holdVal][temp];
                                    }
                                }
                            }

                            if (currMeasures[valence][aroOther] > 0)
                            {
                                trackers[valence][aroOther] += "\n";
                            }
                            countMeasure = rhythm.Count(b => b == 'D');
                            reps = 1;
                            holdKey = GetMin(rhythmStuff, aroOther);
                            d = rhythmStuff[holdKey][0] * speed * 2;
                            while (currMeasures[valence][aroOther] + reps * countMeasure < numMeasures && reps < rhythmStuff[holdKey][1])
                            {
                                reps++;
                            }
                            //                        Debug.Log(rhythm);
                            trackers[valence][aroOther] += d + rhythm + " " + reps;
                            currMeasures[valence][aroOther] += reps * countMeasure;
                            if (currMeasures[valence][aroOther] < numMeasures)
                            {
                                //need to finish...
                                doContinue = true;
                            }
                            //Debug.Log("aroOther " + aroOther + " worked.");
                        }
                    }
                }
            }
        }
        */
        private Dictionary<int, int[]> GetEnergyRhythmStuff(int seed, int numMeasures = 32)
        {
            Dictionary<int, int[]> dict = new Dictionary<int, int[]>();
            System.Random gen = new System.Random(seed);

            dict[0] = new int[2];
            dict[0][0] = 1;

            double temp = gen.NextDouble();
            int aroKey = (int)Math.Ceiling(4000 * (temp / 3 + 1));
            dict[aroKey] = new int[2];

            dict[aroKey][0] = 2;
            dict[aroKey][1] = 1;
            int holdAro = 5001, aroKey2;
            for (int i = 0; i < numMeasures; ++i)
            {
                aroKey2 = (int)Math.Ceiling(20000 * (1 - gen.NextDouble()));
                if (aroKey2 < holdAro)
                {
                    if (aroKey2 < 1000)
                    {
                        break;
                    }

                    if (!dict.ContainsKey(aroKey2))
                    {
                        dict[aroKey2] = new int[2];
                        if (aroKey2 >= aroKey)
                        {
                            dict[aroKey2][0] = 2;
                        }
                        else
                        {
                            dict[aroKey2][0] = 1;
                        }
                        dict[aroKey2][1] = dict[0][1];
                    }
                    holdAro = aroKey2;
                }

                foreach (int k in dict.Keys)
                {
                    if (k < holdAro)
                    {
                        dict[k][1]++;
                    }
                }
            }
            return dict;
        }
        private void DeriveMelody(float energy, float valence, int seed)
        {
            gen = new System.Random(seed);
            int numIntervals, interval1, interval2;
            //            NoteHolder[] otherNotes = new NoteHolder[0];
            //            InstrumentManager melody = new InstrumentManager(new SquareWaveInstrument(), r);
            //           ins[1] = melody;

            if (gen.Next(1) == 0)
            {
                numIntervals = 2;
                interval1 = gen.Next(-24, 25);
                interval2 = gen.Next(-12, 13);
            }
            else
            {
                numIntervals = 1;
                interval1 = gen.Next(-24, 25);
                interval2 = 0;
            }

            if (holdNote == 0)
            {
                holdNote = gen.Next(24, Notes.totalNotes - 24) + 1;
                if (holdNote + interval1 < 1)
                {
                    holdNote += 24;
                }
                if (holdNote + interval1 + interval2 < 1)
                {
                    holdNote += 12;
                }
                if (holdNote + interval1 >= Notes.totalNotes)
                {
                    holdNote -= 24;
                }
                if (holdNote + interval1 + interval2 >= Notes.totalNotes)
                {
                    holdNote -= 12;
                }
            }
            else
            {
                holdNote += 36;
            }

            //           NoteHolder startingNote = new NoteHolder(1, 1, pitch: Notes.Enumerate(holdNote)), firstInterval = new NoteHolder(0, 0), secondInterval = new NoteHolder(0, 0);
            foreach (int val in rhythms.Keys)
            {
                foreach (int aro in rhythms[val].Keys)
                {
                    int measureCount = 0;
                    // This won't work; could be several different beats...
                    foreach (RhythmTracker.Measure m in rhythms[val][aro].GetMeasures())
                    {
                        measureCount++;
                        NoteHolder startingNote = new NoteHolder(measureCount, 1, pitch: Notes.Enumerate(holdNote)), firstInterval = new NoteHolder(0, 0), secondInterval = new NoteHolder(0, 0);
                        int countStrong = 0, numStrong = 0;
                        for (int i = 0; i < m.beats.Length; ++i)
                        {
                            if (m.beats[i] == BeatClass.S || m.beats[i] == BeatClass.D)
                            {
                                numStrong++;
                            }
                        }

                        //Figure out where to place notes based on pattern of strong beats and number of intervals.
                        if (numIntervals == 1)
                        {
                            if (numStrong % 2 == 0)
                            {
                                for (int i = 0; i < m.beats.Length; ++i)
                                {
                                    if (m.beats[i] == BeatClass.S || m.beats[i] == BeatClass.D)
                                    {
                                        countStrong++;
                                        if (countStrong == numStrong / 2)
                                        {
                                            firstInterval = new NoteHolder(measureCount, i + 1, pitch: Notes.Enumerate(holdNote + interval1));
                                            break;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                for (int i = 0; i < m.beats.Length; ++i)
                                {
                                    if (m.beats[i] == BeatClass.S || m.beats[i] == BeatClass.D)
                                    {
                                        countStrong++;
                                        if (countStrong == (numStrong + 1) / 2)
                                        {
                                            firstInterval = new NoteHolder(measureCount, i + 1, pitch: Notes.Enumerate(holdNote + interval1));
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (numStrong % 3 == 0)
                            {
                                for (int i = 0; i < m.beats.Length; ++i)
                                {
                                    if (m.beats[i] == BeatClass.S || m.beats[i] == BeatClass.D)
                                    {
                                        countStrong++;
                                        if (countStrong == numStrong / 3)
                                        {
                                            firstInterval = new NoteHolder(measureCount, i + 1, pitch: Notes.Enumerate(holdNote + interval1));
                                        }
                                        else if (countStrong == numStrong / 3 * 2)
                                        {
                                            secondInterval = new NoteHolder(measureCount, i + 1, pitch: Notes.Enumerate(holdNote + interval1 + interval2));
                                            break;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // Just in case??
                                if (numStrong == 1)
                                {
                                    firstInterval = new NoteHolder(measureCount, 1.5f, pitch: Notes.Enumerate(holdNote + interval1));
                                    secondInterval = new NoteHolder(measureCount, 2, pitch: Notes.Enumerate(holdNote + interval1 + interval2));
                                }
                                else if (numStrong == 2)
                                {
                                    for (int i = 1; i < m.beats.Length; ++i)
                                    {
                                        if (m.beats[i] == BeatClass.S || m.beats[i] == BeatClass.D)
                                        {
                                            firstInterval = new NoteHolder(measureCount, i / 2.0f + 1, pitch: Notes.Enumerate(holdNote + interval1));
                                            secondInterval = new NoteHolder(measureCount, i + 1, pitch: Notes.Enumerate(holdNote + interval1 + interval2));
                                            break;
                                        }
                                    }
                                }
                                else if (numStrong % 3 == 1)
                                {
                                    for (int i = 0; i < m.beats.Length; ++i)
                                    {
                                        if (m.beats[i] == BeatClass.S || m.beats[i] == BeatClass.D)
                                        {
                                            countStrong++;
                                            if (countStrong == (numStrong - 1) / 3)
                                            {
                                                firstInterval = new NoteHolder(measureCount, i + 1, pitch: Notes.Enumerate(holdNote + interval1));
                                            }
                                            else if (countStrong == (numStrong - 1) / 3 * 2)
                                            {
                                                secondInterval = new NoteHolder(measureCount, i + 1, pitch: Notes.Enumerate(holdNote + interval1 + interval2));
                                                break;
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    for (int i = 0; i < m.beats.Length; ++i)
                                    {
                                        if (m.beats[i] == BeatClass.S || m.beats[i] == BeatClass.D)
                                        {
                                            countStrong++;
                                            if (countStrong == (numStrong - 2) / 3)
                                            {
                                                firstInterval = new NoteHolder(measureCount, i + 1, pitch: Notes.Enumerate(holdNote + interval1));
                                            }
                                            else if (countStrong == (numStrong + 1) / 3 * 2)
                                            {
                                                secondInterval = new NoteHolder(measureCount, i + 1, pitch: Notes.Enumerate(holdNote + interval1 + interval2));
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        /*               for (int i = 0; i < m.beats.Length; ++i)
                                       {
                                           if (i + 1 != firstInterval.beat && i + 1 != secondInterval.beat)
                                           {
                                               otherNotes.Append(new NoteHolder(measureCount, i + 1, 1));
                                           }
                                       }*/


                        // SHould really end the first round of valence/aro above and create new one to handle the actual notes....
                        //                        melody.AddNote(new Note(startingNote.measure, startingNote.beat, startingNote.pitch, firstInterval.beat - startingNote.beat));
                        addNote(notes[1], val, aro, new Note(startingNote.measure, startingNote.beat, startingNote.pitch, firstInterval.beat - startingNote.beat));
                        if (numIntervals == 2)
                        {
                            //Debug.Log(String.Concat("Starting Note: ", startingNote.measure, ", ", startingNote.beat, ", ", startingNote.pitch, ", ", firstInterval.beat - startingNote.beat,
                            //    "\nFirst Interval: ", firstInterval.measure, ", ", firstInterval.beat, ", ", firstInterval.pitch, ", ", secondInterval.beat - firstInterval.beat,
                            //    "\nSecond Interval: ", secondInterval.measure, ", ", secondInterval.beat, ", ", secondInterval.pitch, ", ", m.beats.Length - secondInterval.beat));
                            addNote(notes[1], val, aro, new Note(firstInterval.measure, firstInterval.beat, firstInterval.pitch, (secondInterval.beat - firstInterval.beat) / 2));
                            addNote(notes[1], val, aro, new Note(secondInterval.measure, secondInterval.beat, secondInterval.pitch, (m.beats.Length - secondInterval.beat) / 2));
                            //                   melody.AddNote(new Note(firstInterval.measure, firstInterval.beat, firstInterval.pitch, (secondInterval.beat - firstInterval.beat) / 2));
                            //                   melody.AddNote(new Note(secondInterval.measure, secondInterval.beat, secondInterval.pitch, (m.beats.Length - secondInterval.beat) / 2));
                        }
                        else
                        {
                            //Debug.Log(String.Concat("Starting Note: ", startingNote.measure, ", ", startingNote.beat, ", ", startingNote.pitch, ", ", firstInterval.beat - startingNote.beat,
                            //    "\nFirst Interval: ", firstInterval.measure, ", ", firstInterval.beat, ", ", firstInterval.pitch, ", ", m.beats.Length - firstInterval.beat));
                            addNote(notes[1], val, aro, new Note(firstInterval.measure, firstInterval.beat, firstInterval.pitch, (m.beats.Length - firstInterval.beat) / 2));
                            //                   melody.AddNote(new Note(firstInterval.measure, firstInterval.beat, firstInterval.pitch, (m.beats.Length - firstInterval.beat) / 2));
                        }
                    }
                }
            }
        }
        private void DeriveHarmony(float energy, float valence, int seed)
        {
            gen = new System.Random(seed);

            InstrumentManager bass = new InstrumentManager(new SineWaveInstrument(), r);
            ins[5] = bass;

            int mCount = 0;
            //            NoteHolder[] possibleNotes = new NoteHolder[0];
            Dictionary<int, Dictionary<int, List<NoteHolder>>> possibleNotes = new Dictionary<int, Dictionary<int, List<NoteHolder>>>();
            long measure = 0;
            if (holdNote == 0)
            {
                holdNote = gen.Next(12, 36) + 1;
            }
            else
            {
                while (holdNote >= 36)
                {
                    holdNote -= 12;
                }
            }
            foreach (int val in rhythms.Keys)
            {
                int nextVal = GetNextKey(rhythms, val);
                possibleNotes[val] = new Dictionary<int, List<NoteHolder>>();
                foreach (int aro in rhythms[val].Keys)
                {
                    possibleNotes[val][aro] = new List<NoteHolder>();
                    mCount = 0;
                    foreach (RhythmTracker.Measure m in rhythms[val][aro].GetMeasures())
                    {
                        mCount++;
                        /*                int strongBeats = 0, currentBeat = 0;
                                        for (int i = 0; i < m.beats.Length; ++i)
                                        {
                                            switch (m.beats[i])
                                            {
                                                case BeatClass.D:
                                                case BeatClass.S:
                                                    strongBeats++;
                                                    break;
                                            }
                                        }
                        */
                        for (int i = 0; i < m.beats.Length; ++i)
                        {
                            float pitch = 0, length = 0;
                            string type = "";
                            switch (measure)
                            {
                                case 0:
                                    switch (m.beats[i])
                                    {
                                        case BeatClass.D:
                                            pitch = Notes.Enumerate(holdNote); // Notes.E1;
                                            length = m.beats.Length;
                                            type = "D";
                                            break;

                                        case BeatClass.S:
                                            pitch = Notes.Enumerate(holdNote + 7);// Notes.B2;
                                            length = 1f;
                                            type = "S";
                                            break;
                                    }
                                    break;

                                case 1:
                                    switch (m.beats[i])
                                    {
                                        case BeatClass.D:
                                            pitch = Notes.Enumerate(holdNote + 7);
                                            length = m.beats.Length;
                                            type = "D";
                                            break;

                                        case BeatClass.S:
                                            pitch = Notes.Enumerate(holdNote + 12);
                                            length = 1f;
                                            type = "S";
                                            break;
                                    }
                                    break;

                                case 2:
                                    switch (m.beats[i])
                                    {
                                        case BeatClass.D:
                                            pitch = Notes.Enumerate(holdNote + 12);
                                            length = m.beats.Length;
                                            type = "D";
                                            break;

                                        case BeatClass.S:
                                            pitch = Notes.Enumerate(holdNote + 19);
                                            length = 1f;
                                            type = "S";
                                            break;
                                    }
                                    break;

                                case 3:
                                    switch (m.beats[i])
                                    {
                                        case BeatClass.D:
                                            pitch = Notes.Enumerate(holdNote + 7);
                                            length = m.beats.Length;
                                            type = "D";
                                            break;

                                        case BeatClass.S:
                                            pitch = Notes.Enumerate(holdNote + 14);
                                            length = 1f;
                                            type = "S";
                                            break;
                                    }
                                    break;
                            }
                            if (type != "")
                            {
                                //Debug.Log(String.Concat(mCount, " ", i + 1, " ", length, " ", pitch, " ", type));
                                possibleNotes[val][aro].Add(new NoteHolder(mCount, i + 1, length, pitch, type));
                            }
                        }

                        measure++;
                        measure %= 4;
                        /*
                        for (int i = 0; i < m.beats.Length; ++i)
                        {
                            switch (m.beats[i])
                            {
                                case BeatClass.D:
                                    bassHit = true;
                                    goto case BeatClass.S;

                                case BeatClass.S:
                                    if (bassHit)
                                        drummer.BassHit(mCount, i+1);
                                    else
                                        drummer.SnareHit(mCount, i+1);

                                    bassHit = !bassHit;
                                    break;
                            }

                            drummer.HighHatHit(mCount, i + 1);
                        }
        */

                    }
                }
            }
            // Debug.Log("Before Note HOlder: " + possibleNotes.Length);
            foreach (int val in rhythms.Keys)
            {
                foreach (int aro in rhythms[val].Keys)
                {
                    foreach (NoteHolder n in possibleNotes[val][aro])
                    {
                        switch (n.type)
                        {
                            case "D":
                                addNote(notes[5], val, aro, new Note(n.measure, n.beat, n.pitch, n.length));
                                //                        bass.AddNote(new Note(n.measure, n.beat, n.pitch, n.length));
                                break;

                            case "S":
                                int test = gen.Next(0, (int)(25 / valence));
                                if (test == 0)
                                {
                                    //                            bass.AddNote(new Note(n.measure, n.beat, n.pitch, n.length));
                                }
                                break;
                        }
                    }
                }
            }

        }
        private void DeriveRhythm(float energy, float valence, int seed)
        {

            int maxNotes = 0;
            Dictionary<int, Dictionary<int, List<NoteHolder>>> possibleNotes = new Dictionary<int, Dictionary<int, List<NoteHolder>>>();


            foreach (int val in rhythms.Keys)
            {
                //Debug.Log("Testing 0");
                foreach (int aro in rhythms[val].Keys)
                {
                    int mCount = 0;
                    //                  NoteHolder[] possibleNotes = new NoteHolder[0];
                    int countNotes = 0;
                    //Debug.Log("Testing 1");
                    foreach (RhythmTracker.Measure m in rhythms[val][aro].GetMeasures())
                    {
                        //Debug.Log("Testing 2");
                        mCount++;
                        int strongBeats = 0, currentBeat = 0;
                        for (int i = 0; i < m.beats.Length; ++i)
                        {
                            switch (m.beats[i])
                            {
                                case BeatClass.D:
                                case BeatClass.S:
                                    strongBeats++;
                                    break;
                            }
                        }

                        if (mCount == r.GetMeasures().ToArray().Length)
                        {
                            int countStrongBeats = 0, i = 0;
                            bool timeToBreak = false;
                            while (true)
                            {
                                switch (m.beats[i])
                                {
                                    case BeatClass.D:
                                        if (countStrongBeats < strongBeats / 2)
                                        {
                                            countStrongBeats++;
                                            //                                            possibleNotes = possibleNotes.Append(new NoteHolder(mCount, i + 1, type: "B")).ToArray();
                                            addToDict(possibleNotes, val, aro, new NoteHolder(mCount, i + 1, type: "B"), false);
                                            addToDict(possibleNotes, val, aro, new NoteHolder(mCount, i + 1.5f, type: "CB", selfVal: val, selfAro: aro, noteNumber: countNotes++), false);
                                        }
                                        else
                                        {
                                            timeToBreak = true;
                                        }
                                        break;

                                    case BeatClass.S:
                                        if (countStrongBeats < strongBeats / 2)
                                        {
                                            countStrongBeats++;
                                            if (currentBeat % 2 != 0 && (currentBeat != strongBeats || strongBeats % 2 == 0))
                                            {
                                                //                                            possibleNotes = possibleNotes.Append(new NoteHolder(mCount, i + 1, type: "B")).ToArray();
                                                addToDict(possibleNotes, val, aro, new NoteHolder(mCount, i + 1, type: "B"), false);
                                                addToDict(possibleNotes, val, aro, new NoteHolder(mCount, i + 1.5f, type: "CB", selfVal: val, selfAro: aro, noteNumber: countNotes++), false);
                                            }
                                            else
                                            {
                                                addToDict(possibleNotes, val, aro, new NoteHolder(mCount, i + 1, type: "S"), false);
                                                addToDict(possibleNotes, val, aro, new NoteHolder(mCount, i + 1.5f, type: "CS", selfVal: val, selfAro: aro, noteNumber: countNotes++), false);
                                            }
                                            //                                            possibleNotes = possibleNotes.Append(new NoteHolder(mCount, i + 1, type: "S")).ToArray();
                                        }
                                        else
                                        {
                                            timeToBreak = true;
                                        }
                                        break;
                                }
                                if (timeToBreak)
                                {
                                    //                                    i++;
                                    break;
                                }

                                addToDict(possibleNotes, val, aro, new NoteHolder(mCount, i + 1, type: "H"), false);
                                addToDict(possibleNotes, val, aro, new NoteHolder(mCount, i + 1.5f, type: "CH", selfVal: val, selfAro: aro, noteNumber: countNotes++), false);
                                i++;
                            }

                            //                            GetFill(mCount, i, m, val, aro);
                            addToDict(possibleNotes, val, aro, new NoteHolder(mCount, i + 1, type: "F", selfVal: val, selfAro: aro, noteNumber: countNotes++), false);
                        }
                        else
                        {
                            for (int i = 0; i < m.beats.Length; ++i)
                            {
                                switch (m.beats[i])
                                {
                                    case BeatClass.D:
                                        currentBeat++;
                                        {
                                            addToDict(possibleNotes, val, aro, new NoteHolder(mCount, i + 1, type: "B"), false);
                                            addToDict(possibleNotes, val, aro, new NoteHolder(mCount, i + 1.5f, type: "CB", selfVal: val, selfAro: aro, noteNumber: countNotes++), false);
                                        }
                                        break;

                                    case BeatClass.S:
                                        currentBeat++;
                                        if (currentBeat % 2 != 0 && (currentBeat != strongBeats || strongBeats % 2 == 0))
                                        {
                                            addToDict(possibleNotes, val, aro, new NoteHolder(mCount, i + 1, type: "B"), false);
                                            addToDict(possibleNotes, val, aro, new NoteHolder(mCount, i + 1.5f, type: "CB", selfVal: val, selfAro: aro, noteNumber: countNotes++), false);
                                        }
                                        else
                                        {
                                            addToDict(possibleNotes, val, aro, new NoteHolder(mCount, i + 1, type: "S"), false);
                                            addToDict(possibleNotes, val, aro, new NoteHolder(mCount, i + 1.5f, type: "CS", selfVal: val, selfAro: aro, noteNumber: countNotes++), false);
                                        }

                                        break;
                                }

                                addToDict(possibleNotes, val, aro, new NoteHolder(mCount, i + 1, type: "H"), false);
                                addToDict(possibleNotes, val, aro, new NoteHolder(mCount, i + 1.5f, type: "CH", selfVal: val, selfAro: aro, noteNumber: countNotes++), false);
                            }
                        }
                    }

                    //Debug.Log("Before Note HOlder: " + possibleNotes[val][aro].Count);
                    List<NoteHolder> toRemove = new List<NoteHolder>();
                    foreach (NoteHolder n in possibleNotes[val][aro])
                    {
                        //                        int test = gen.Next(0, (int)(25 / valence));
                        //                        Debug.Log("Test: " + test + "; valence: " + valence);
                        switch (n.type)
                        {
                            case "B":
                                addNote(notes[10], val, aro, new Note(n.measure, n.beat, n.pitch, n.length), false);
                                toRemove.Add(n);
                                break;

                            case "S":
                                addNote(notes[11], val, aro, new Note(n.measure, n.beat, n.pitch, n.length), false);
                                toRemove.Add(n);
                                break;

                            case "H":
                                addNote(notes[12], val, aro, new Note(n.measure, n.beat, n.pitch, n.length), false);
                                toRemove.Add(n);
                                break;
                        }
                    }
                    //Debug.Log("Before removing Notes");
                    foreach (NoteHolder n in toRemove)
                    {
                        possibleNotes[val][aro].Remove(n);
                    }
                    //Debug.Log("After Removing Notes");
                    /*
                    int test = possibleNotes.Length;
                    if (test > maxNotes)
                    {
                        maxNotes = test;
                    }*/
                }
            }
            //Debug.Log("Finished first loop");
            // Can't check rhythms, needs to be notes...
            foreach (int val in rhythms.Keys)
            {
                gen = new System.Random(seed);
                int nextVal = GetNextKey(rhythms, val);

                foreach (int aro in rhythms[val].Keys)
                {
                    List<NoteHolder> toRemove = new List<NoteHolder>();
                    foreach (NoteHolder n in possibleNotes[val][aro])
                    {
                        bool add = false;
                        double test;
                        int holdVal, valToAdd = 0;
                        switch (n.type)
                        {
                            case "F":
                                List<Note> fillNotes = GetFill2(n.measure, n.beat, rhythms[val][aro].GetMeasures().ElementAt(n.measure - 1), val, aro);
                                foreach (Note fn in fillNotes)
                                {
                                    addNoteToRangeOfVals(notes[11], val, nextVal, aro, fn, true);
                                }
                                toRemove.Add(n);
                                break;

                            case "CB":
                                // No way to know in which order notes will be added,
                                // so need to figure out how to add notes to the larger valences...
                                test = gen.NextDouble();
                                if (test <= .04)
                                {
                                    valToAdd = val;
                                    add = true;
                                }
                                else if (test <= .20)
                                {
                                    holdVal = (int)Math.Floor(test * 25000);
                                    add = val == GetMin(rhythms, holdVal) && holdVal < nextVal;
                                    valToAdd = holdVal;
                                }
                                if (add)
                                {
                                    addNoteToRangeOfVals(notes[10], valToAdd, nextVal, aro, new Note(n.measure, n.beat, n.pitch, n.length), true);
                                }
                                toRemove.Add(n);
                                break;

                            case "CS":
                                test = gen.NextDouble();
                                if (test <= .04)
                                {
                                    valToAdd = val;
                                    add = true;
                                }
                                else if (test <= .20)
                                {
                                    holdVal = (int)Math.Floor(test * 25000);
                                    add = val == GetMin(rhythms, holdVal) && holdVal < nextVal;
                                    valToAdd = holdVal;
                                }
                                if (add)
                                {
                                    addNoteToRangeOfVals(notes[11], valToAdd, nextVal, aro, new Note(n.measure, n.beat, n.pitch, n.length), true);
                                }
                                toRemove.Add(n);
                                break;

                            case "CH":
                                test = gen.NextDouble();
                                if (test <= .04)
                                {
                                    valToAdd = val;
                                    add = true;
                                }
                                else if (test <= .20)
                                {
                                    holdVal = (int)Math.Floor(test * 25000);
                                    add = val == GetMin(rhythms, holdVal) && holdVal < nextVal;
                                    valToAdd = holdVal;
                                }
                                if (add)
                                {
                                    addNoteToRangeOfVals(notes[12], valToAdd, nextVal, aro, new Note(n.measure, n.beat, n.pitch, n.length), true);
                                }
                                toRemove.Add(n);
                                break;
                        }

                    }
                    foreach (NoteHolder n in toRemove)
                    {
                        possibleNotes[val][aro].Remove(n);
                    }

                }
            }
            /*
                        foreach (int val in possibleNotes.Keys)
                        {

                            foreach (int aro in possibleNotes[val].Keys)
                            {

                            }
                        }
                        {
                            int test = gen.Next(0, (int)(25 / valence));
                            Debug.Log("Test: " + test + "; valence: " + valence);
                        }
            */
        }

        private void GetFill(int measure, int startBeat, RhythmTracker.Measure m, int val, int aro)
        {

            //Fill
            for (int i = startBeat; i <= m.beats.Length; ++i)
            {
                drummer.SnareHit(measure, i);
                drummer.SnareHit(measure, i + .25f);
                drummer.SnareHit(measure, i + .5f);
                drummer.SnareHit(measure, i + .75f);
            }

        }

        private List<Note> GetFill2(int measure, float startBeat, RhythmTracker.Measure m, int val, int aro)
        {
            List<Note> notes = new List<Note>();
            //Fill
            for (int i = (int)Math.Floor(startBeat); i <= m.beats.Length; ++i)
            {
                /*
                addNote(notes[11], val, aro, new Note(measure, i, 0, 0), true);
 //               addNote(notes[11], val, aro, new Note(measure, i+.25f, 0, 0), true);
                addNote(notes[11], val, aro, new Note(measure, i+.5f, 0, 0), true);
 //               addNote(notes[11], val, aro, new Note(measure, i+.75f, 0, 0), true);
                */
                notes.Add(new Note(measure, i, 0, 0));
                notes.Add(new Note(measure, i + .5f, 0, 0));
            }

            return notes;
        }


        public float[] getNotes(int pos, int dur)
        {
            //            Debug.Log("GetNotes Dur: " + dur);
            float[][] i = new float[0][];
            foreach (InstrumentManager im in ins)
            {
                if (!(im is null))
                {
                    i = i.Append(im.GetStream(pos, dur)).ToArray();
                }
            }
            return (PreVolumeMixer.Mix(i));
        }

        public void getNotes(int pos, int dur, int start, ref float[] output)
        {
            //            Debug.Log("GetNotes Dur: " + dur);
            float[][] i = new float[0][];
            int count = 0;
            foreach (InstrumentManager im in ins)
            {
                if (!(im is null))
                {
                    i = i.Append(im.GetStream(pos, dur)).ToArray();
                    count++;
                }

            }
            //Debug.Log("Get Notes found " + count + " non-null instruments (should be 6)");
            PreVolumeMixer.Mix(start, ref output, i);
        }
        // Third parameter is in samples (not seconds), as caller won't know the rhythm.
        public int GetMeasure(float valence, float energy, int position)
        {
            int holdVal = (int)(valence * 1000);
            int holdAro = (int)(energy * 1000);
            holdVal = GetMin(rhythms, holdVal);
            holdAro = GetMin(rhythms[holdVal], holdAro);
            return rhythms[holdVal][holdAro].GetMeasureSamples(position);
        }

        public float GetBeat(float valence, float energy, int position)
        {
            int holdVal = (int)(valence * 1000);
            int holdAro = (int)(energy * 1000);
            holdVal = GetMin(rhythms, holdVal);
            holdAro = GetMin(rhythms[holdVal], holdAro);
            return rhythms[holdVal][holdAro].GetBeatSamples(position);
        }

        public int GetPosition(float valence, float energy, int measure, float beat)
        {
            int holdVal = (int)(valence * 1000);
            int holdAro = (int)(energy * 1000);
            holdVal = GetMin(rhythms, holdVal);
            holdAro = GetMin(rhythms[holdVal], holdAro);
            return rhythms[holdVal][holdAro].GetTimeSamples(measure, beat);
        }
        // Maybe return offset? Seems like can be changed during function?
        public int GetPosition(float oldVal, float oldAro, float newVal, float newAro, int position)
        {
            int holdPosition = position;
            int holdVal = GetMin(rhythms, (int)(oldVal * 1000));
            int holdAro = GetMin(rhythms[holdVal], (int)(oldAro * 1000));
            int holdVal2 = GetMin(rhythms, (int)(newVal * 1000));
            int holdAro2 = GetMin(rhythms[holdVal], (int)(newAro * 1000));
            if (holdVal == holdVal2 && holdAro == holdAro2)
            {
                return 0;
            }
            int measure = rhythms[holdVal][holdAro].GetMeasureSamples(holdPosition);
            float beat = rhythms[holdVal][holdAro].GetBeatSamples(holdPosition);

            measure = rhythms[holdVal2][holdAro2].GetTimeSamples(measure, beat, RhythmTracker.PositionFinder.nextMeasure);
            if (measure == -1)
            {
                return -1;
            }
            else
            {
                return measure - holdPosition;
            }
        }
        public int GetMeasures(float valence, float energy)
        {
            int holdVal = (int)(valence * 1000);
            int holdAro = (int)(energy * 1000);
            holdVal = GetMin(rhythms, holdVal);
            holdAro = GetMin(rhythms[holdVal], holdAro);
            return rhythms[holdVal][holdAro].GetMeasures().Count();
        }

        public int GetDuration(float valence, float energy)
        {
            int holdVal = (int)(valence * 1000);
            int holdAro = (int)(energy * 1000);
            //Debug.Log("In Get Duration; Before GetMin: Val: " + valence + " Aro: " + energy + " holdVal: " + holdVal + " holdAro: " + holdAro);
            holdVal = GetMin(rhythms, holdVal);
            holdAro = GetMin(rhythms[holdVal], holdAro);
            int mCount = rhythms[holdVal][holdAro].GetMeasures().Count();
            int lastbeat = rhythms[holdVal][holdAro].GetMeasures().Last().beats.Length;
            //Debug.Log("In Get Duration; After GetMin: Val: " + valence + " Aro: " + energy + " holdVal: " + holdVal + " holdAro: " + holdAro + " measure: " + mCount + " beat: " + lastbeat);
            return (int)(rhythms[holdVal][holdAro].GetTime(mCount, lastbeat + 1) * Constants.freq);
        }

        public float[] Compile()
        {
            float[][] i = new float[0][];

            foreach (InstrumentManager im in ins)
            {
                if (!(im is null))
                {
                    i = i.Append(im.GetStream()).ToArray();
                }
            }
            return (PreVolumeMixer.Mix(i));
        }

        public void Compile2(float val, float energy)
        {
            // Set speed, measures and dur...
            List<InstrumentManager> retval = new List<InstrumentManager>();
            ins = new InstrumentManager[12];
            // Need to add instrument... need to get valence and energy from Score...
            int holdVal, holdAro, rVal, rAro;
            rVal = GetMin(rhythms, (int)(val * 1000));
            rAro = GetMin(rhythms[rVal], (int)(energy * 1000));
            //Debug.Log("Given val/energy: " + val + "/" + energy + "; Found rhythmic val/energy: " + rVal + "/" + rAro);
            BeatClass[] x = rhythms[rVal][rAro].GetMeasures().First().beats;
            string debug = "";
            foreach (BeatClass b in x)
            {
                debug += b + " ";
            }
            //Debug.Log("Rhythm: " + debug);
            //Debug.Log("Rhythm: " + rhythms[rVal][rAro]);
            //speed = rhythms[holdVal][holdAro].
            measures = GetMeasures(val, energy);
            dur = GetDuration(val, energy);
            //Debug.Log("Measures: " + measures + "; Duration: " + dur);
            /**/
            holdVal = GetMin(notes[0], (int)(val * 1000));
            holdAro = GetMin(notes[0][holdVal], (int)(energy * 1000));
            ins[0] = new InstrumentManager(new NullInstrument(), rhythms[rVal][rAro]);
            //Debug.Log("Silent Notes");
            foreach (Note n in notes[0][holdVal][holdAro])
            {
               // Debug.Log(n);
                ins[0].AddNote(n);
            }
            //Debug.Log("Found emptyNotes val/aro: " + holdVal + "/" + holdAro);
            holdVal = GetMin(notes[1], (int)(val * 1000));
            holdAro = GetMin(notes[1][holdVal], (int)(energy * 1000));
            ins[1] = new InstrumentManager(new SquareWaveInstrument(), rhythms[rVal][rAro]);
            //Debug.Log("Melody Notes (?)");
            foreach (Note n in notes[1][holdVal][holdAro])
            {
                //Debug.Log(n);
                ins[1].AddNote(n);
            }

            holdVal = GetMin(notes[5], (int)(val * 1000));
            holdAro = GetMin(notes[5][holdVal], (int)(energy * 1000));
            ins[2] = new InstrumentManager(new SineWaveInstrument(), rhythms[rVal][rAro]);
            //Debug.Log("Bass Notes (?)");
            foreach (Note n in notes[5][holdVal][holdAro])
            {
                //Debug.Log(n);
                ins[2].AddNote(n);
            }
            /**/
            holdVal = GetMin(notes[10], (int)(val * 1000));
            holdAro = GetMin(notes[10][holdVal], (int)(energy * 1000));
            ins[3] = new DrumManager(new BassDrumInstrument(Constants.freq, .5f), rhythms[rVal][rAro]);
            //Debug.Log("Bass Drum Notes (?)");
            int count = 0;
            foreach (Note n in notes[10][holdVal][holdAro])
            {
                //Debug.Log(n);
                ins[3].AddNote(n);
                count++;
            }
            //Debug.Log("Bass Notes added: " + count);

            holdVal = GetMin(notes[11], (int)(val * 1000));
            holdAro = GetMin(notes[11][holdVal], (int)(energy * 1000));
            ins[4] = new DrumManager(new SnareDrumInstrument(Constants.freq, .5f), rhythms[rVal][rAro]);
            //Debug.Log("Snare Drum Notes (?)");
            foreach (Note n in notes[11][holdVal][holdAro])
            {
                //Debug.Log(n);
                ins[4].AddNote(n);
            }

            holdVal = GetMin(notes[12], (int)(val * 1000));
            holdAro = GetMin(notes[12][holdVal], (int)(energy * 1000));
            ins[5] = new DrumManager(new CymbalInstrument(Constants.freq, .5f), rhythms[rVal][rAro]);
            //Debug.Log("High-hat Notes (?)");
            foreach (Note n in notes[12][holdVal][holdAro])
            {
                //Debug.Log(n);
                ins[5].AddNote(n);
            }
        }

        public string GetOddRhythm()
        {
            return (GetOddRhythm(gen));
        }
        public string GetOddRhythm(System.Random gen)
        {
            Dictionary<string, double> oddRhythms = new Dictionary<string, double>();
            oddRhythms[" D W W S W "] = .05;
            oddRhythms[" D W S W W "] = .05;
            oddRhythms[" D W S W S "] = .05;
            oddRhythms[" D W S W S W S "] = .05;
            oddRhythms[" D W S W S W W "] = .05;
            oddRhythms[" D W S W W S W "] = .05;
            oddRhythms[" D W W S W S W "] = .05;
            oddRhythms[" D W W S W W S "] = .05;
            oddRhythms[" D W S W S W S W S W S "] = .05;
            oddRhythms[" D W S W S W S W S W W "] = .05;
            oddRhythms[" D W S W S W S W W S W "] = .05;
            oddRhythms[" D W S W S W W S W S W "] = .05;
            oddRhythms[" D W S W W S W S W S W "] = .05;
            oddRhythms[" D W W S W S W S W S W "] = .05;
            oddRhythms[" D W W S W W S W S W W "] = .05;
            oddRhythms[" D W W S W W S W W S W "] = .05;
            oddRhythms[" D W W S W S W W S W W "] = .05;
            oddRhythms[" D W S W W S W W S W W "] = .05;
            oddRhythms[" D W S W S W W S W W S "] = .05;
            oddRhythms["odd"] = .05;
            string test = Score.DecideProb(oddRhythms, gen.NextDouble(), "odd");
            if (test != "odd")
            {
                return (test);
            }

            StringBuilder retval = new StringBuilder();
            Dictionary<string, double> start = new Dictionary<string, double>(), remain = new Dictionary<string, double>();
            start[" D W W"] = .5;
            start[" D W"] = .5;

            retval.Append(Score.DecideProb(start, gen.NextDouble(), " D W W "));

            remain["eps"] = .01;
            remain[" S W"] = .3;
            remain[" S W W"] = .3;
            remain[" D W"] = .19;
            remain[" D W W"] = .19;
            remain["Seps"] = .01;

            test = Score.DecideProb(remain, gen.NextDouble(), "S W ");

            while (test != "eps")
            {
                if (test == "Seps")
                {
                    retval.Append(" S");
                    break;
                }
                retval.Append(test);

                if (test[1] == 'S')
                {
                    remain["eps"] += .05;
                    remain[" S W"] -= .08;
                    remain[" S W W"] -= .08;
                    remain[" D W"] += .03;
                    remain[" D W W"] += .03;
                    remain["Seps"] += .05;
                }
                else
                {
                    remain["eps"] -= .01;
                    remain[" S W"] += .09;
                    remain[" S W W"] += .09;
                    remain[" D W"] -= .08;
                    remain[" D W W"] -= .08;
                    remain["Seps"] -= .01;
                }
                test = Score.DecideProb(remain, gen.NextDouble(), "eps");
            }
            retval.Append(" ");
            return (retval.ToString());
        }

        private class NoteHolder
        {
            public int measure, noteNumber;
            public float beat, length, pitch, selfVal, selfAro, rhythmVal, rhythmAro;
            public string type;

            public NoteHolder(int measure, float beat, float length = 0, float pitch = Notes.REST, string type = "",
                float selfVal = 0, float selfAro = 0, float rhythmVal = 0, float rhythmAro = 0, int noteNumber = 0)
            {
                this.measure = measure;
                this.beat = beat;
                this.length = length;
                this.pitch = pitch;
                this.type = type;
                this.selfAro = selfAro;
                this.selfVal = selfVal;
                this.rhythmVal = rhythmVal;
                this.rhythmAro = rhythmAro;
                this.noteNumber = noteNumber;
            }
        }

    }

    class Section4 : Section2
    {
        public Section4(int seed = 0, string n = "", double speed = 120) : base(seed, n, speed)
        {
        }

        override private protected void DeriveMelodyNotes(System.Random rand, NoteHolder start, NoteHolder interval1, NoteHolder interval2, ref Dictionary<int, Dictionary<int, List<Note>>> retval)
        {
            bool hasFirst = false, firstSame = false;
            float pitch1 = 0, pitch2 = 0;
            //Debug.Log("Deriving Melody; start note stats: " + start.measure + " " + start.beat + " " + start.pitch + " " + start.length + " " + start.noteNumber);
            if (!(interval1 is null))
            {
                hasFirst = true;
                // get number of beats (halves, for now; assume same measure...)
                //float beatsRaw = interval1.beat - start.beat;
                int beats = Mathf.FloorToInt((interval1.beat - start.beat) * 2) - 1;

                if (beats > 0 && rand.NextDouble() < .5)
                {
                    double test = rand.NextDouble();
                    pitch1 = getPitchFromInterval(start.noteNumber, interval1.noteNumber + (test < .5 ? -1 : 1), true);
                    pitch2 = getPitchFromInterval(start.noteNumber, interval1.noteNumber + (test < .5 ? -1 : 1), false);
                    firstSame = pitch1 == pitch2;
                    if (firstSame)
                    {
                        addNoteToRangeOfVals(retval, 0, 5000, 0, new Note(interval1.measure, interval1.beat - .5f, pitch1, .5f), true);
                    }
                    else
                    {
                        int val = rand.Next(1000, 5001);
                        addNoteToRangeOfVals(retval, 0, val - 1, 0, new Note(interval1.measure, interval1.beat - .5f, pitch1, .5f), true);
                        addNoteToRangeOfVals(retval, val, 5000, 0, new Note(interval1.measure, interval1.beat - .5f, pitch2, .5f), true);
                    }
                    start.length -= .5f;
                }
                pitch1 = getPitchFromInterval(start.noteNumber, interval1.noteNumber, true);
                pitch2 = getPitchFromInterval(start.noteNumber, interval1.noteNumber, false);
                firstSame = pitch1 == pitch2;

                //Debug.Log("Deriving Melody; interval1 stats: " + interval1.measure + " " + interval1.beat + " " + pitch2 + " " + interval1.length + " " + interval1.noteNumber);
            }
            if (!(interval2 is null))
            {
                int beats = Mathf.FloorToInt((interval2.beat - interval1.beat) * 2) - 1;

                if (beats > 0 && rand.NextDouble() < .5)
                {
                    if (hasFirst)
                    {
                        interval1.length -= .5f;
                        if (firstSame)
                        {
                            addNoteToRangeOfVals(retval, 0, 5000, 0, new Note(interval1.measure, interval1.beat, pitch1, interval1.length), true);
                        }
                        else
                        {
                            int val = rand.Next(1000, 5001);
                            addNoteToRangeOfVals(retval, 0, val - 1, 0, new Note(interval1.measure, interval1.beat, pitch1, interval1.length), true);
                            addNoteToRangeOfVals(retval, val, 5000, 0, new Note(interval1.measure, interval1.beat, pitch2, interval1.length), true);
                        }
                        hasFirst = false;
                    }
                    double test = rand.NextDouble();
                    pitch1 = getPitchFromInterval(start.noteNumber, interval2.noteNumber + (test < .5 ? -1 : 1), true);
                    pitch2 = getPitchFromInterval(start.noteNumber, interval2.noteNumber + (test < .5 ? -1 : 1), false);
                    firstSame = pitch1 == pitch2;
                    if (firstSame)
                    {
                        addNoteToRangeOfVals(retval, 0, 5000, 0, new Note(interval2.measure, interval2.beat - .5f, pitch1, .5f), true);
                    }
                    else
                    {
                        int val = rand.Next(1000, 5001);
                        addNoteToRangeOfVals(retval, 0, val - 1, 0, new Note(interval2.measure, interval2.beat - .5f, pitch1, .5f), true);
                        addNoteToRangeOfVals(retval, val, 5000, 0, new Note(interval2.measure, interval2.beat - .5f, pitch2, .5f), true);
                    }
                    start.length -= .5f;
                }
                if (hasFirst)
                {
                    interval1.length -= .5f;
                    if (firstSame)
                    {
                        addNoteToRangeOfVals(retval, 0, 5000, 0, new Note(interval1.measure, interval1.beat, pitch1, interval1.length), true);
                    }
                    else
                    {
                        int val = rand.Next(1000, 5001);
                        addNoteToRangeOfVals(retval, 0, val - 1, 0, new Note(interval1.measure, interval1.beat, pitch1, interval1.length), true);
                        addNoteToRangeOfVals(retval, val, 5000, 0, new Note(interval1.measure, interval1.beat, pitch2, interval1.length), true);
                    }
                }
                pitch1 = getPitchFromInterval(start.noteNumber, interval2.noteNumber, true);
                pitch2 = getPitchFromInterval(start.noteNumber, interval2.noteNumber, false);

                //Debug.Log("Deriving Melody; interval2 stats: " + interval2.measure + " " + interval2.beat + " " + pitch2 + " " + interval2.length + " " + interval1.noteNumber);

                if (pitch1 == pitch2)
                {
                    addNoteToRangeOfVals(retval, 0, 5000, 0, new Note(interval2.measure, interval2.beat, pitch1, interval2.length), true);
                }
                else
                {
                    int val = rand.Next(1000, 5001);
                    addNoteToRangeOfVals(retval, 0, val - 1, 0, new Note(interval2.measure, interval2.beat, pitch1, interval2.length), true);
                    addNoteToRangeOfVals(retval, val, 5000, 0, new Note(interval2.measure, interval2.beat, pitch2, interval2.length), true);
                }
            }
            else
            {
                if (hasFirst)
                {
                    if (firstSame)
                    {
                        addNoteToRangeOfVals(retval, 0, 5000, 0, new Note(interval1.measure, interval1.beat, pitch1, interval1.length), true);
                    }
                    else
                    {
                        int val = rand.Next(1000, 5001);
                        addNoteToRangeOfVals(retval, 0, val - 1, 0, new Note(interval1.measure, interval1.beat, pitch1, interval1.length), true);
                        addNoteToRangeOfVals(retval, val, 5000, 0, new Note(interval1.measure, interval1.beat, pitch2, interval1.length), true);
                    }
                }
            }
            addNoteToRangeOfVals(retval, 0, 5000, 0, new Note(start.measure, start.beat, start.pitch, start.length), true);
        }
        override private protected float getPitchFromInterval(int startTone, int interval, bool isConsonant)
        {
            float retval2 = 0f;
            switch (interval)
            {
                case 4:
                    retval2 = Notes.Enumerate(startTone + 5);
                    break;
                case 5:
                    retval2 = Notes.Enumerate(startTone + 7);
                    break;
                case -4:
                    retval2 = Notes.Enumerate(startTone - 5);
                    break;
                case -5:
                    retval2 = Notes.Enumerate(startTone - 7);
                    break;
                case 8:
                    retval2 = Notes.Enumerate(startTone + 8);
                    break;
                case -8:
                    retval2 = Notes.Enumerate(startTone - 8);
                    break;
                case 11:
                    retval2 = Notes.Enumerate(startTone + 17);
                    break;
                case 12:
                    retval2 = Notes.Enumerate(startTone + 19);
                    break;
                case 15:
                    retval2 = Notes.Enumerate(startTone + 24);
                    break;
                case -11:
                    retval2 = Notes.Enumerate(startTone - 17);
                    break;
                case -12:
                    retval2 = Notes.Enumerate(startTone - 19);
                    break;
                case -15:
                    retval2 = Notes.Enumerate(startTone - 24);
                    break;

                case 2:
                    if (isConsonant)
                    {
                        retval2 = Notes.Enumerate(startTone + 2);
                    }
                    else
                    {
                        retval2 = Notes.Enumerate(startTone + 1);
                    }
                    break;

                case 3:
                    if (isConsonant)
                    {
                        retval2 = Notes.Enumerate(startTone + 4);
                    }
                    else
                    {
                        retval2 = Notes.Enumerate(startTone + 3);
                    }
                    break;

                case 6:
                    if (isConsonant)
                    {
                        retval2 = Notes.Enumerate(startTone + 9);
                    }
                    else
                    {
                        retval2 = Notes.Enumerate(startTone + 8);
                    }
                    break;

                case 7:
                    if (isConsonant)
                    {
                        retval2 = Notes.Enumerate(startTone + 11);
                    }
                    else
                    {
                        retval2 = Notes.Enumerate(startTone + 10);
                    }
                    break;

                case 9:
                    if (isConsonant)
                    {
                        retval2 = Notes.Enumerate(startTone + 14);
                    }
                    else
                    {
                        retval2 = Notes.Enumerate(startTone + 6); // Not a mistake...
                    }
                    break;

                case 10:
                    if (isConsonant)
                    {
                        retval2 = Notes.Enumerate(startTone + 16);
                    }
                    else
                    {
                        retval2 = Notes.Enumerate(startTone + 15);
                    }
                    break;

                case 13:
                    if (isConsonant)
                    {
                        retval2 = Notes.Enumerate(startTone + 21);
                    }
                    else
                    {
                        retval2 = Notes.Enumerate(startTone + 20);
                    }
                    break;

                case 14:
                    if (isConsonant)
                    {
                        retval2 = Notes.Enumerate(startTone + 23);
                    }
                    else
                    {
                        retval2 = Notes.Enumerate(startTone + 22);
                    }
                    break;

                case -2:
                    if (!isConsonant)
                    {
                        retval2 = Notes.Enumerate(startTone - 2);
                    }
                    else
                    {
                        retval2 = Notes.Enumerate(startTone - 1);
                    }
                    break;

                case -3:
                    if (!isConsonant)
                    {
                        retval2 = Notes.Enumerate(startTone - 4);
                    }
                    else
                    {
                        retval2 = Notes.Enumerate(startTone - 3);
                    }
                    break;

                case -6:
                    if (!isConsonant)
                    {
                        retval2 = Notes.Enumerate(startTone - 9);
                    }
                    else
                    {
                        retval2 = Notes.Enumerate(startTone - 8);
                    }
                    break;

                case -7:
                    if (!isConsonant)
                    {
                        retval2 = Notes.Enumerate(startTone - 11);
                    }
                    else
                    {
                        retval2 = Notes.Enumerate(startTone - 10);
                    }
                    break;
                // This one is just totally weird now...
                case -9:
                    if (isConsonant)
                    {
                        retval2 = Notes.Enumerate(startTone - 14);
                    }
                    else
                    {
                        retval2 = Notes.Enumerate(startTone - 6); // Not a mistake...
                    }
                    break;

                case -10:
                    if (!isConsonant)
                    {
                        retval2 = Notes.Enumerate(startTone - 16);
                    }
                    else
                    {
                        retval2 = Notes.Enumerate(startTone - 15);
                    }
                    break;

                case -13:
                    if (!isConsonant)
                    {
                        retval2 = Notes.Enumerate(startTone - 21);
                    }
                    else
                    {
                        retval2 = Notes.Enumerate(startTone - 20);
                    }
                    break;

                case -14:
                    if (!isConsonant)
                    {
                        retval2 = Notes.Enumerate(startTone - 23);
                    }
                    else
                    {
                        retval2 = Notes.Enumerate(startTone - 22);
                    }
                    break;

                case -1:
                case 1:
                    retval2 = 0;
                    break;

                case 0:
                default:
                    retval2 = Notes.Enumerate(startTone);
                    break;

            }

            return retval2;
        }
    }

    class RhythmTracker : IEquatable<RhythmTracker>
    {
        BeatClass[][] sigs;
        public Harmony[][] harmonies; //Is this a good idea?
        int[] repeats;
        float[] bpm;
        float frequency;

        public RhythmTracker (float freq, params string[] sup)
        {
            frequency = freq;
            foreach (string s2 in sup)
            {
                char[] seps = { '\n' };
                string[] s = s2.Split(seps, StringSplitOptions.RemoveEmptyEntries);
                sigs = new BeatClass[s.Length][];
                repeats = new int[s.Length];
                bpm = new float[s.Length];


                for (int i = 0; i < s.Length; ++i)
                {
                    //Debug.Log(s[i]);
                    seps[0] = ' ';
                    string[] tokens = s[i].Split(seps, StringSplitOptions.RemoveEmptyEntries);
                    if (tokens.Length < 3)
                    {
                        //Debug.Log("Bad measure definition: " + s[i]);
                    }
                    else
                    {
                        //Debug.Log("Tokens Len: " + tokens.Length + " " + tokens);
                        sigs[i] = new BeatClass[tokens.Length - 2];
                        bpm[i] = float.Parse(tokens[0]);
                        string log = "";
                        for (int j = 1; j < tokens.Length - 1; j++)
                        {
                            switch (tokens[j])
                            {
                                case "D":
                                case "down":
                                    sigs[i][j - 1] = BeatClass.D;
                                    break;

                                case "S":
                                case "strong":
                                    sigs[i][j - 1] = BeatClass.S;
                                    break;

                                case "W":
                                case "weak":
                                default:
                                    sigs[i][j - 1] = BeatClass.W;
                                    break;
                            }
                            log += sigs[i][j - 1];
                        }
                        repeats[i] = int.Parse(tokens.Last());
                        //Debug.Log(log);
                    }
                }
            }
        }

        // Returns time in seconds
        public float GetTime(int measure, float beat)
        {
            float retval = 0f;
            int j = 0, k = 0;
     //     Debug.Log("Measure: " + measure + "; Beat: " + beat);
            for (int i = 1; i < measure; i++)
            {
                retval += sigs[j].Length / bpm[j];
                if (++k >= repeats[j])
                {
                    k = 0;
                    j = (j + 1) % repeats.Length;
                }
            }
            beat -= 1f;

            if (beat > 0)
            {
                while (beat >= sigs[j].Length)
                {
                    retval += sigs[j].Length / bpm[j];
                    beat -= sigs[j].Length;
                    if (++k >= repeats[j])
                    {
                        k = 0;
                        j = (j + 1) % repeats.Length;
                    }
                }
     //         Debug.Log("Add End; j = " + j);
                retval += beat / bpm[j];
            }
            else
            {
                beat *= -1;
                if (--k < 0)
                {
                    if (--j < 0)
                        j = repeats.Length - 1;

                    k = repeats[j] - 1;
                }
     //         Debug.Log("Subtracting. j = " + j + " k = " + k);
                while (beat >= sigs[j].Length)
                {
     //             Debug.Log("Subtract while");
                    retval -= sigs[j].Length / bpm[j];
                    beat -= sigs[j].Length;
                    if (--k < 0)
                    {
                        j = (j - 1) % repeats.Length;
                        k = repeats[j] - 1;
                    }
                }
     //         Debug.Log("Subtract Ended; j = " + j);
                retval -= beat / bpm[j];
            }
            retval *= 60;
    //      Debug.Log("Returning: " + retval);
            return (retval);
        }

        public int GetTimeSamples(int measure, float beat, PositionFinder pf = PositionFinder.raw)
        {
            if (pf == PositionFinder.raw)
            {
                return (int)Math.Ceiling(GetTime(measure, beat) / frequency);
            }
            int countMeasures = 0, measureIndex = -1;
            for (int i = 0; i < repeats.Length; ++i)
            {
                countMeasures += repeats[i];
                if (measureIndex == -1 && countMeasures >= measure)
                {
                    measureIndex = i;
                }
            }
            if (measure > countMeasures)
            {
                return -1;
            }
            if (--beat > sigs[measureIndex].Length)
            {
                switch (pf)
                {
                    case PositionFinder.nextBeat:
                        if (beat + 1 <= sigs[measureIndex].Length)
                        {
                            return (int)Math.Ceiling(GetTime(measure, beat + 1) / frequency);
                        }
                        goto case PositionFinder.nextMeasure;

                    case PositionFinder.nextMeasure:
                        if (measure + 1 <= countMeasures)
                        {
                            return (int)Math.Ceiling(GetTime(measure + 1, 1) / frequency);
                        }
                        goto default;

                    default:
                        return -1;

                }
            }

            return (int)Math.Ceiling(GetTime(measure, beat) / frequency);
        }

        public float GetDuration(int measure, float beat, float len)
        {
            float retval;
            int j = 0, k = 0;
            for (int i = 1; i < measure; i++)
            {
                if (++k >= repeats[j])
                {
                    k = 0;
                    j = (j + 1) % repeats.Length;
                }
            }
            beat -= 1f;

            if (beat + len < sigs[j].Length)
            {
                retval = len / bpm[j];
            }
            else
            {
                retval = 0.0f;
                while (beat + len >= sigs[j].Length)
                {
                    retval += (sigs[j].Length - beat) / bpm[j];
                    len -= sigs[j].Length - beat;
                    beat = 0;
                    if (++k >= repeats[j])
                    {
                        k = 0;
                        j = (j + 1) % repeats.Length;
                    }
                }
            }
            return (retval * 60f);
        }
        public float GetDurationOriginalTempo(int measure, float beat, float len)
        {
            float retval;
            int j = 0, k = 0;
            for (int i = 1; i < measure; i++)
            {
                if (++k >= repeats[j])
                {
                    k = 0;
                    j = (j + 1) % repeats.Length;
                }
            }

            retval = len * 60f / bpm[j];

            return (retval);
        }

        // The parameter should be in seconds
        public int GetMeasure(float time)
        {
            int measure = 0;
            float retval = 0f;
            int j = 0, k = 0;
            time /= 60;
            //     Debug.Log("Measure: " + measure + "; Beat: " + beat);
            do
            {
                measure++;
                retval += sigs[j].Length / bpm[j];
                if (++k >= repeats[j])
                {
                    k = 0;
                    j = (j + 1) % repeats.Length;
                }
            } while (retval < time);
            return --measure;
        }

        public int GetMeasureSamples(float samples)
        {
            return GetMeasure(samples / frequency);
        }

        // The parameter should be in seconds
        public float GetBeat(float time)
        {
            float beat = 0;
            float retval = 0f;
            int j = 0, k = 0;
            time /= 60; // Convert to minutes (the m of bpm)
            //     Debug.Log("Measure: " + measure + "; Beat: " + beat);
            while (retval + sigs[j].Length / bpm[j] < time)
            {
                retval += sigs[j].Length / bpm[j];
                if (++k >= repeats[j])
                {
                    k = 0;
                    j = (j + 1) % repeats.Length;
                }
            }
            beat = (time - retval) * bpm[j] + 1f;
            return (beat);
        }

        public float GetBeatSamples(int samples)
        {
            return GetBeat(samples / frequency);
        }

        public IEnumerable<Measure> GetMeasures()
        {
            for (int i = 0; i < sigs.Length; ++i)
            {
                for (int j = 0; j < repeats[i]; ++j) {

                    int last = 0, k = 1;
                    while (k < sigs[i].Length) { 
                        while (k < sigs[i].Length && sigs[i][k] != BeatClass.D) k++;
                        if (k == sigs[i].Length && last == 0)
                        {
//                            Debug.Log("Return Full");
                            yield return new Measure(bpm[i], sigs[i]);
                        }
                        else
                        {
 //                           Debug.Log("Return Partial: " + (last) + " - " + (k - 1));
                            BeatClass[] ret = new BeatClass[k - last];
                            string log = "";
                            for (int m = last; m < k; ++m)
                            {
                                ret[m - last] = sigs[i][m];
                                log += sigs[i][m] + " ";
                            }
                            last = k++;
 //                           Debug.Log("Partial return: " + log);
                            yield return new Measure(bpm[i], ret);
                        }
                    }
                }
            }
        }

        public float getDuration()
        {
            float retval = 0f;

            for (int i = 0; i < bpm.Length; ++i)
            {
                if (bpm[i] > 0)
                {
                    retval += sigs[i].Length / bpm[i] * repeats[i] * 60;
                }
            }
            return (retval);
        }

        public struct Measure
        {
            public float bpm;
            public BeatClass [] beats;

            public Measure(float bpm, BeatClass [] beats)
            {
                this.bpm = bpm;
                this.beats = beats;
            }

        }

        public enum PositionFinder
        {
            raw, nextBeat, nextMeasure, nextSection
        }
        public override string ToString()
        {
            StringBuilder retval = new StringBuilder();
            for (int i = 0; i < sigs.Length; ++i)
            {
                retval = retval.Append(bpm[i]).Append(" x").Append(repeats[i]);
                for (int j = 0; j < sigs[i].Length; ++j)
                {
                    retval = retval.Append(" ").Append(sigs[i][j]);
                }
                retval = retval.AppendLine();
            }
            return retval.ToString();
        }

        public override bool Equals(object obj) => this.Equals(obj as RhythmTracker);

        public override int GetHashCode() => (sigs, repeats).GetHashCode();

        public bool Equals(RhythmTracker other)
        {
            if (other is null)
                return false;

            if (System.Object.ReferenceEquals(this, other))
            {
                return true;
            }

            if (this.GetType() != other.GetType())
            {
                return false;
            }

            //Actual checks go here...
            if (this.sigs.Length != other.sigs.Length)
            {
                return false;
            }

            for (int i = 0; i < repeats.Length; ++i)
            {
                if (repeats[i] != other.repeats[i])
                {
                    return false;
                }
            }
            for (int i = 0; i < sigs.Length; ++i)
            {
                if (sigs[i].Length != other.sigs[i].Length)
                {
                    return false;
                }
                for (int j = 0; j < sigs[i].Length; ++j)
                {
                    if (sigs[i][j] != other.sigs[i][j])
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public static bool operator ==(RhythmTracker lhs, RhythmTracker rhs)
        {
            if (lhs is null)
            {
                if (rhs is null)
                {
                    return true;
                }

                return false;
            }

            return lhs.Equals(rhs);
        }

        public static bool operator !=(RhythmTracker lhs, RhythmTracker rhs) => !(lhs == rhs);
    }


    public struct OldSeed
    {
        int pos;
        float valence;
        float energy;

        public OldSeed(int p, float v, float a)
        {
            pos = p;
            valence = v;
            energy = a;
        }
    }

    enum BeatClass { weak = 0, strong, down, W = 0, S, D}
}

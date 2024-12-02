using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;

namespace OddRhythms
{
    [InitializeOnLoad]
    [ExecuteAlways]
    [Serializable]
    public class OddRhythms : MonoBehaviour
    {
        public SeedObject seed;
        public SeedObject[] prevObject = new SeedObject[10], favObject = new SeedObject[10];
        //public int test = 0;

        private AudioSource source;
        private bool playing = false;
        private ScoreManager scoreManager;
        private SeedObject currentlyPlaying;
        private System.Threading.Thread thread;
        private bool threadRunning;
        private int threadTotal = 1, threadCount = 0;

        public void Start()
        {
            if (Application.isPlaying && !(seed is null) && seed.seed != 0)
            {
                seed.TestValidate(seed._valence, seed._energy);
                Stop();
                Play(seed);
            }
        }

        public void Awake()
        {
            if (seed is null)
            {
                seed = new SeedObject(0, this);
            }
            //Debug.Log("Awaken!");

        }

        public void Update()
        {
            if (currentlyPlaying != null)
            {
                //Debug.Log("Updating the OddRythyms");
                currentlyPlaying.TestValidate(currentlyPlaying._valence, currentlyPlaying._energy);
            }
        }

        public void Reset()
        {
            //Debug.Log("Resetting");
            seed.InitSeed();
            foreach (SeedObject o in favObject)
            {
                if (!(o is null))
                {
                    //Debug.Log("Found seed?");
                    o.InitSeed();
                }
            }

            foreach (SeedObject o in prevObject)
            {
                if (!(o is null))
                {
                   // Debug.Log("Found seed?");
                    o.InitSeed();
                }
            }
        }

        public void OnDestroy()
        {
            Stop();
        }

        private static int GenerateRandom(int min, int max)
        {
            System.Random rand = new System.Random();
            int i = rand.Next(min, max);
//            Debug.Log("Generated 2: " + i);
            return i;
        }

        internal void GetNewSeed()
        {
            for (int i = Math.Min(prevObject.Length, 9); i > 0; --i)
            {
                prevObject[i] = prevObject[i-1];
            }
            prevObject[0] = new SeedObject(OddRhythms.GenerateRandom(1, System.Int32.MaxValue), this);
        }

        public SeedObject CreateRandomSeed(float valence = 3, float energy = 3)
        {
            int i = GenerateRandom(1, int.MaxValue);
           // Debug.Log("Generated 5: " + i);
            return new SeedObject(i, this, valence, energy);
        }

        internal void SetFav(int i)
        {
            if (favObject[favObject.Length - 1] is null || !favObject[favObject.Length - 1].isValid)
            {
                int found = 0;
                while (found < favObject.Length && !(favObject[found] is null) && favObject[found].isValid)
                {
                    found++;
                }
                favObject[found] = prevObject[i];
            }
            else
            {
                favObject.Append(prevObject[i]);
            }

            for (int j = i; j < prevObject.Length - 1; ++j)
            {
                prevObject[j] = prevObject[j + 1];
            }
            prevObject[prevObject.Length - 1] = null;
        }

        internal void RemFav(int i)
        {
            for (int j = 9; j > 0; --j)
            {
                prevObject[j] = prevObject[j - 1];
            }

            prevObject[0] = favObject[i];
            for (int j = i; j < favObject.Length - 1; ++j)
            {
                favObject[j] = favObject[j + 1];
            }
            favObject[favObject.Length - 1] = null;
        }

        internal void SetAsSeed(int i, bool isFav)
        {
            SeedObject hold;
            if (isFav)
            {
                hold = favObject[i];
            }
            else
            {
                hold = prevObject[i];
            }
            seed = new SeedObject(hold.seed, this, hold._valence, hold._energy, hold.note);
        }

        internal void PlayOrStop(int i, bool prev)
        {
            if (playing)
            {
                Stop();
            }
            else
            {
                Play(i, prev);
            }
            playing = !playing;
        }
        public void PlayOrStop(SeedObject s)
        {
            if (playing)
            {
                Stop();
            }
            else
            {
                Play(s);
            }
            playing = !playing;
        }

        public void Play(SeedObject seed)
        {
            source = transform.GetComponent<AudioSource>();
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
                source.transform.SetParent(transform);
                source.playOnAwake = false;
                source.loop = true;
                source.tag = "Finish";
            }

            //          Task.Run(() => {
            //                source.clip = seed.getSong();
            //              Debug.Log("Message");
            //            source.Play();
            //          Debug.Log("Song Should be playing...");
            //       });
            //            source.clip = seed.getSong();
            //          source.Play();
            if (scoreManager is null)
            {
                scoreManager = new ScoreManager();
            }
            scoreManager.ChangeScore(seed.getSong(), ref source);
            source.time = 0;
            source.PlayDelayed(0);
            //Debug.Log("End of Play function");
            currentlyPlaying = seed;
        }

        internal void Play(int i, bool prev)
        {
            //Debug.Log("Play(" + i + ", " + prev + ")");
            if (prev)
            {
                Play(prevObject[i]);
            }
            else
            {
                Play(favObject[i]);
            }
        }

        public void Stop()
        {
            if (playing)
            {
                AudioSource source = transform.GetComponent<AudioSource>();
                //Debug.Log("Stopping");
                scoreManager.Stop();
                //Debug.Log("Still Stopping");
                if (source is null)
                {
                    source = transform.parent.GetComponent<AudioSource>();
                }
                if (source != null)
                {
                    source.Stop();
                    if (source.tag == "Finish")
                    {
                        source.tag = "Untagged";
                        if (Application.isPlaying)
                        {
                            GameObject.Destroy(source);
                        }
                        else
                        {
                            GameObject.DestroyImmediate(source);
                        }
                    }
                }

                currentlyPlaying = null;
            }
        }

        public System.Collections.IEnumerator ChangeSeedSettings(SeedObject seed, float valence, float energy)
        {
            threadCount++;
            while (threadTotal != threadCount)
            {
                if (threadTotal > threadCount)
                    yield break;
                else
                    yield return null;
            }
            thread = new System.Threading.Thread(seed.InitSeed);
            //Debug.Log("Create Thread");
            thread.IsBackground = true;
            //Debug.Log("Set Background Thread");
            thread.Start();

            //Debug.Log("Start Thread");
            while (thread.IsAlive)
            {
                yield return null;
            }
/*            thread = new System.Threading.Thread(seed.getSong().Compile);
            thread.IsBackground = true;
            thread.Start();
            while (thread.IsAlive)
            {
                yield return null;
            }
*/
            //Debug.Log("Called Seed Changed with: " + valence + " and " + energy + ".");
            if (playing && currentlyPlaying == seed && seed._valence == valence && seed._energy == energy)
            {
                scoreManager.ChangeEnergyOrValence(valence, energy);
//                source.Play();
            }
            thread = null;
            if (threadCount > threadTotal)
            {
                threadTotal = threadCount;
            }
            else
            {
                threadTotal++;
            }
        }

        private class ThreadData
        {
            private SeedObject seed;
            private float valence, energy;

            public ThreadData(SeedObject s, float v, float a)
            {
                seed = s;
                valence = v;
                energy = a;
            }

            public void DoThread()
            {
                seed.InitSeed();
                
            }
        }

        internal void SeedChanged(SeedObject seed, float oldValence, float oldEnergy)
        {
            StartCoroutine(ChangeSeedSettings(seed, oldValence, oldEnergy));
        }

        internal void SeedChanged(SeedObject seed, Task t, float oldValence, float oldEnergy)
        {
            StartCoroutine(HandleSeedChanged(seed, t, oldValence, oldEnergy));
        }
        internal System.Collections.IEnumerator HandleSeedChanged(SeedObject seed, Task t, float oldValence, float oldEnergy)
        {
            //Debug.Log("Start Coroutine");
            if (!t.IsCompleted)
            {
                yield return null;
            }
            //Debug.Log("Called Seed Changed with: " + oldValence + " and " + oldenergy + ".");
            if (playing && currentlyPlaying == seed)
            {
                scoreManager.ChangeScore(currentlyPlaying.getSong(), ref source);
                source.Play();
            }
        }

        public string GetRhythm()
        {
            string retval = "";

            if (!(currentlyPlaying is null))
            {
                retval = currentlyPlaying.getSong().GetRhythm();
            }
            return retval;
        }

        public int GetMeasure()
        {
            int retval = 0;
            if (!(currentlyPlaying is null))
            {
                retval = currentlyPlaying.getSong().GetMeasure();
            }

            return retval;
        }

        public float GetBeat()
        {
            float retval = 0;

            if (!(currentlyPlaying is null))
            {
                retval = currentlyPlaying.getSong().GetBeat();
            }

            return retval;
        }
    }
    [InitializeOnLoad]
    [ExecuteAlways]
    [Serializable]
    public class SeedObject
    {
        public int seed = 0;/*
        public float valence { get { return _valence; }
            set { float oldValue = _valence;
                  _valence = Mathf.Clamp(value, 1, 5); 
                Task t = InitSeed();
                Debug.Log("Calling Seed Changed?");
                if (oldValue != _valence)
                {
                    t.Wait();
                    parent.SeedChanged(oldValue, energy); 
                }
            } }
        public float energy { get { return _energy; } 
            set { float oldValue = _energy;
                _energy = Mathf.Clamp(value, 1, 5);
                Debug.Log("Calling Seed Changed?");
                Task t = InitSeed();
                if (oldValue != _energy)
                {
                    t.Wait();
                    parent.SeedChanged(valence, oldValue);
                }
            } }*/
        public float _valence, _energy;// = 3f, _energy = 3f;
        [TextArea]
        public string note = "";
        public Version version;
        [SerializeField]
        [Range(1f, 5f)]
        private float currValence, currEnergy;// = 3f, currenergy = 3f;
        [SerializeField]
        private Version currVersion;
        [SerializeField]
        private int currSeed;

        private Score score;
        [SerializeField]
        private OddRhythms parent;
        [SerializeField]
        private bool _isValid = false;

        internal bool isValid { get { return _isValid; } private set { } }
 //       public SeedObjectUnityObject dis;
        public SeedObject(int seed, OddRhythms par, float valence = 3, float energy = 3, string note = "")
        {
//            Task.Run(() => fillScores(seed, version));

            this.seed = seed;
            this.currSeed = seed;
            this._valence = valence;
            this._energy = energy;
            this.currValence = valence;
            this.currEnergy = energy;
            this.version = Version.V1_0_1;
            this.currVersion = Version.V1_0_1;
            this.note = note;
            Task.Run(() => fillScore(valence, energy, version));
            this.parent = par;
            this._isValid = true;
            /*
            dis = par.gameObject.AddComponent<SeedObjectUnityObject>();
            if (dis)
            {
                dis.energy = energy;
                dis.note = note;
                dis.valence = valence;
                dis.seed = seed;
                dis.SetFields(seed, valence, energy, note, this);
            }*/
        }

        public void SetValues(int seed, OddRhythms par, float valence = 3, float energy = 3, string note = "")
        {
            //            Task.Run(() => fillScores(seed, version));
            this.seed = seed;
            this._valence = valence;
            this._energy = energy;
            this.note = note;
            Task.Run(() => fillScore(valence, energy));
            this.parent = par;
            this._isValid = true;
        }



        public void InitSeed()

        {
            fillScore(currValence, currEnergy);
        }

        private void fillScore(float valence, float energy)
        {
            /*
            if (scores is null)
            {
                scores = new Score[16008002];
            }
            if (scores[getIndex(valence, energy)] is null)
            {
                scores[getIndex(valence, energy)] = MusicGenerator.CreateScore(seed, energy, valence, version);
            }*/
            if (score is null)
            {
                score = MusicGenerator.CreateScore(seed, energy, valence, version);
            }
            else
            {
                score.valence = valence;
                score.energy = energy;
                score.Compile2();
            }
        }

        private void fillScore(float valence, float energy, Version v)
        {
            if (score is null)
            {
                //Debug.Log("score was null: " + seed);
                score = MusicGenerator.CreateScore(seed, energy, valence, version);
            }
            else
            {
                if (score.valence != valence || score.energy != energy)
                {
                    //Debug.Log("score was not null");
                    score.valence = valence;
                    score.energy = energy;
                    score.Compile2();
                }
            }
        }

        public void Play()
        {
            if (!(parent is null))
            {
                parent.Play(this);
            }
        }

        internal Score getSong()
        {
            float val = currValence, aro = currEnergy;
            //Debug.Log("Start get Song");
            fillScore(val, aro);
            //Debug.Log("ScoreFilled");
            return (score);
        }

        private int getIndex(float valence, float energy)
        {
            float retval = ((valence - 1) * 4000 + (energy - 1)) * 4001f / 4;
            int result = Mathf.RoundToInt(retval);
            //Debug.Log("Index for " + valence + " and " + energy + " is " + retval + " rounds to " + result);
            return (result);
//           return (Mathf.RoundToInt(((valence - 1) * 4001 + (energy - 1)) * 4001f / 4;));
        }

        internal void TestValidate(float valence, float energy)
        {
            if (currVersion != version || currSeed != seed)
            {
                score = null;
                currVersion = version;
                currEnergy = energy;
                currValence = valence;
                parent.SeedChanged(this, valence, energy);
            }
            else
            {
                if (currEnergy != energy || currValence != valence)
                {
                    //Debug.Log("Calling Seed Changed?");
                    currEnergy = energy;
                    currValence = valence;
                    //                this.energy = energy;
                    //                this.valence = valence;
                    //parent.ChangeSeedSettings(this, valence, energy);
                    parent.SeedChanged(this, valence, energy);
                    //Task t = InitSeed();
                    //parent.SeedChanged(this, t, valence, energy);
                }
            }
        }

        public override string ToString()
        {
            return ("Seed: " + seed + "; Val: " + _valence + "; Energy:" + _energy);
        }
    }

    public enum Version
    {
        V1_0_0, V1_0_1
    }
    /*
    [Serializable]
    [ExecuteInEditMode]
    public class SeedObjectUnityObject : MonoBehaviour
    {
        public int seed;
        public float arousal, valence;
        public string note;

        private float oldArousal, oldValence;
        private SeedObject owner;

        public void Start()
        {
            oldArousal = arousal;
            oldValence = valence;
        }

        public void OnValidate()
        {
            bool change = false;

            Debug.Log(oldArousal + " " + arousal + " " + oldValence + " " + valence);
            if (oldArousal != arousal)
            {
                change = true;
                oldArousal = arousal;
            }
            if (oldValence != valence)
            {
                change = true;
                oldValence = valence;
            }
            owner.note = note;
            owner.seed = seed;
            if (change)
            {
                owner.TestValidate(valence, arousal);
            }
        }

        public void SetFields(int s, float v, float a, string n, SeedObject o)
        {
            seed = s;
            valence = v;
            arousal = a;
            note = n;
            owner = o;
        }

    }
    */
}

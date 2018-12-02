using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Trace
{
    public class TrajectoryVocabulary
    {
        private const int FINEST_LEVEL_DESCRIPTOR_RESOLUTION = 64;
        private const float MAXIMUM_ALLOWABLE_MATCH_COST = 4f;

        [Serializable]
        private class Serialization
        {
            [Serializable]
            public class KnownString
            {
                [Serializable]
                public class IntArray
                {
                    public int[] Ints;
                }

                public string Name;
                public IntArray[] Strings;
            }

            public Vector3[] Alphabet;
            public KnownString[] KnownStrings;
        }

        private class VocabularyItem
        {
            private const float DEFAULT_AVERAGE_LEVENSHTEIN_DISTANCE = 1f;

            public List<Levenshtein.String<int>> Strings { get; private set; }

            private int centroidIdx;
            private float averageLevenshteinDistance;

            public VocabularyItem(List<Levenshtein.String<int>> strings)
            {
                this.Strings = strings;
                this.centroidIdx = -1;
                this.averageLevenshteinDistance = -1f;

                if (strings.Count > 0)
                {
                    RecalculateRepresentations(this.Strings);
                }
            }

            public void Add(Levenshtein.String<int> str)
            {
                this.Strings.Add(str);
                this.RecalculateRepresentations(this.Strings);
            }

            public float GetMatchCost(Levenshtein.String<int> str)
            {
                float distance = str.Distance(this.Strings[this.centroidIdx]);
                return distance / this.averageLevenshteinDistance; // Linear error metric, for now.
            }

            private void RecalculateRepresentations(List<Levenshtein.String<int>> strings)
            {
                var distances = strings.Select(a => strings.Sum(b => a.Distance(b))).ToList();
                this.centroidIdx = distances.IndexOf(distances.Min());

                if (strings.Count > 1)
                {
                    // Local variable for lambda capture.
                    int idx = this.centroidIdx;
                    float totalDistance = strings.Sum(str => str.Distance(strings[idx]));
                    this.averageLevenshteinDistance = totalDistance / (strings.Count - 1);
                }
                else
                {
                    this.averageLevenshteinDistance = DEFAULT_AVERAGE_LEVENSHTEIN_DISTANCE;
                }
            }
        }

        private readonly Vector3[] vectorsAlphabet;
        private Levenshtein.Alphabet<int> levenshteinAlphabet;
        private Dictionary<string, VocabularyItem> knownStrings;

        public static TrajectoryVocabulary Load(string serializationLocation)
        {
            if (File.Exists(serializationLocation))
            {
                string json = File.ReadAllText(serializationLocation);
                return new TrajectoryVocabulary(JsonUtility.FromJson<Serialization>(json));
            }
            else
            {
                return new TrajectoryVocabulary();
            }
        }

        private TrajectoryVocabulary()
        {
            this.vectorsAlphabet = Utils.GenerateAlphabet(fixedValues: Vector3.forward);
            this.levenshteinAlphabet = CreateLevenshteinAlphabet(this.vectorsAlphabet);
            this.knownStrings = new Dictionary<string, VocabularyItem>();
        }

        private TrajectoryVocabulary(Serialization serialization)
        {
            this.vectorsAlphabet = serialization.Alphabet;
            this.levenshteinAlphabet = CreateLevenshteinAlphabet(this.vectorsAlphabet);

            this.knownStrings = new Dictionary<string, VocabularyItem>();
            foreach (var knownString in serialization.KnownStrings)
            {
                var strings = new List<Levenshtein.String<int>>();
                foreach (var s in knownString.Strings)
                {
                    strings.Add(new Levenshtein.String<int>(s.Ints, this.levenshteinAlphabet));
                }

                this.knownStrings.Add(knownString.Name, new VocabularyItem(strings));
            }
        }

        public void Save(string serializationLocation)
        {
            if (File.Exists(serializationLocation))
            {
                File.Delete(serializationLocation);
            }

            File.WriteAllText(serializationLocation, this.ToString());
        }

        public override string ToString()
        {
            var serialization = new Serialization()
            {
                Alphabet = this.vectorsAlphabet,
                KnownStrings = this.knownStrings.Select(
                    pair => new Serialization.KnownString()
                    {
                        Name = pair.Key,
                        Strings = pair.Value.Strings.Select(
                            str => new Serialization.KnownString.IntArray() { Ints = str.Characters }).ToArray()
                    }).ToArray()
            };

            return JsonUtility.ToJson(serialization, true /* TODO DEBUG */);
        }

        public void AddTrajectoryWithName(Trajectory trajectory, string name)
        {
            if (!this.knownStrings.ContainsKey(name))
            {
                this.knownStrings.Add(name, new VocabularyItem(new List<Levenshtein.String<int>>()));
            }

            this.knownStrings[name].Add(ToLevenshteinString(trajectory));
        }

        // TODO: I really hate this pattern.  Find a way to accomplish this without
        // either using obnoxious patterns or opening the door for confusing usage.
        public bool TryRecognizeTrajectory(Trajectory trajectory, out string result)
        {
            var trajectoryString = ToLevenshteinString(trajectory);

            result = null;
            float bestCost = MAXIMUM_ALLOWABLE_MATCH_COST;

            foreach (var pair in this.knownStrings)
            {
                float cost = pair.Value.GetMatchCost(trajectoryString);
                if (cost < bestCost)
                {
                    result = pair.Key;
                    bestCost = cost;
                }
            }
            
            return result != null;
        }

        private Levenshtein.String<int> ToLevenshteinString(Trajectory trajectory)
        {
            var trajectoryDescriptor = GetTrajectoryDescriptor(
                trajectory,
                this.vectorsAlphabet,
                FINEST_LEVEL_DESCRIPTOR_RESOLUTION);
            return new Levenshtein.String<int>(trajectoryDescriptor, this.levenshteinAlphabet);
        }
        
        private static int[] GetTrajectoryDescriptor(Trajectory trajectory, Vector3[] tokens, int targetResolution)
        {
            var descriptor = new List<int>();

            for (int res = targetResolution; res > 2; res /= 2)
            {
                descriptor.AddRange(trajectory.ResampleAtTargetResolution(res).Tokenize(tokens));
            }

            return descriptor.ToArray();
        }

        // TODO: Create substantially less hackneyed distance functions.  Optimizer?
        // TODO: This is a bug factory in the making.  The way this method is written
        // makes some pretty scary unenforced assumptions about the relationship between
        // the characters and their indices within the Levenshtein alphabet, deeply
        // abusing the fact that they are one and the same.  While that is currently the
        // case and really is determined within this function, changing it will likely
        // cause the algorithm to go haywire in extremely unpredictable ways.  Once the
        // approach is validated, look for ways to make this implementation slightly 
        // less terrifying.
        private static Levenshtein.Alphabet<int> CreateLevenshteinAlphabet(Vector3[] vectors, params int[][] knownStrings)
        {
            const float DEFAULT_EXPECTED_COUNT_PER_STRING = 1f;

            var counts = Enumerable.Repeat(0f, vectors.Length).ToArray();
            foreach (var str in knownStrings)
            {
                foreach (var idx in str)
                {
                    counts[idx]++;
                }
            }
            var expectedCountPerString = counts.Select(
                count => count > 0 ? count / knownStrings.Length : DEFAULT_EXPECTED_COUNT_PER_STRING).ToArray();

            return new Levenshtein.Alphabet<int>(
                Enumerable.Range(0, vectors.Length).ToArray(),
                idx => 1f / expectedCountPerString[idx],
                idx => 1f / expectedCountPerString[idx],
                (a, b) => 1f - Vector3.Dot(vectors[a], vectors[b]));
        }
    }
}

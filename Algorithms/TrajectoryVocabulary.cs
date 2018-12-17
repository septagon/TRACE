using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Trace
{
    public class TrajectoryVocabulary
    {
        private const int FINEST_LEVEL_DESCRIPTOR_RESOLUTION = 32;
        private const float MAXIMUM_ALLOWABLE_MATCH_COST = 4f;

        [Serializable]
        private class Serialization
        {
            [Serializable]
            public class KnownTrajectory
            {
                [Serializable]
                public class Descriptor
                {
                    [Serializable]
                    public class IntArray
                    {
                        public int[] Ints;
                    }

                    public IntArray[] Strings;
                }

                public string Name;
                public Descriptor[] Descriptors;
            }

            public Vector3[] Alphabet;
            public KnownTrajectory[] KnownTrajectories;
        }

        private class TrajectoryDescriptor
        {
            public List<Levenshtein.String<int>> Strings { get; private set; }

            public TrajectoryDescriptor(List<List<int>> descriptor, Levenshtein.Alphabet<int> alphabet)
            {
                this.Strings = descriptor.Select(ints => new Levenshtein.String<int>(ints.ToArray(), alphabet)).ToList();
            }

            public TrajectoryDescriptor(Trajectory trajectory, Vector3[] tokens, Levenshtein.Alphabet<int> alphabet) 
                : this(GetTrajectoryDescriptor(trajectory, tokens, FINEST_LEVEL_DESCRIPTOR_RESOLUTION), alphabet)
            { }

            private static List<List<int>> GetTrajectoryDescriptor(Trajectory trajectory, Vector3[] tokens, int targetResolution)
            {
                var descriptor = new List<List<int>>();

                for (int res = targetResolution; res > 2; res /= 2)
                {
                    descriptor.Add(trajectory.ResampleAtTargetResolution(res).Tokenize(tokens));
                }

                return descriptor;
            }

            public float Distance(TrajectoryDescriptor other)
            {
                Debug.Assert(this.Strings.Count == other.Strings.Count);

                float totalDistance = 0;
                for (int idx = 0; idx < this.Strings.Count; idx++)
                {
                    float weight = Mathf.Pow(2f, idx);
                    totalDistance += this.Strings[idx].Distance(other.Strings[idx]);
                }

                return totalDistance;
            }
        }

        private class VocabularyItem
        {
            private const float DEFAULT_AVERAGE_COST = 1f;

            public List<TrajectoryDescriptor> Descriptors { get; private set; }

            private int centroidIdx;
            private float averageLevenshteinDistance;

            public VocabularyItem(List<TrajectoryDescriptor> strings)
            {
                this.Descriptors = strings;
                this.centroidIdx = -1;
                this.averageLevenshteinDistance = -1f;

                if (strings.Count > 0)
                {
                    RecalculateRepresentations(this.Descriptors);
                }
            }

            public void Add(TrajectoryDescriptor descriptor)
            {
                this.Descriptors.Add(descriptor);
                this.RecalculateRepresentations(this.Descriptors);
            }

            // TODO: Try best match score?  Sum of squared distances?
            public float GetMatchCost(TrajectoryDescriptor descriptor)
            {
                float cost = descriptor.Distance(this.Descriptors[this.centroidIdx]);
                return cost / this.averageLevenshteinDistance; // Linear error metric, for now.
            }

            public float GetMatchMinimumEditDistance(TrajectoryDescriptor descriptor)
            {
                return this.Descriptors.Min(desc => desc.Distance(descriptor));
            }

            private void RecalculateRepresentations(List<TrajectoryDescriptor> strings)
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
                    this.averageLevenshteinDistance = DEFAULT_AVERAGE_COST;
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
            foreach (var knownGlyph in serialization.KnownTrajectories)
            {
                var strings = new List<TrajectoryDescriptor>();
                foreach (var desc in knownGlyph.Descriptors)
                {
                    var jaggedArray = desc.Strings.Select(ints => ints.Ints.ToList()).ToList();
                    strings.Add(new TrajectoryDescriptor(jaggedArray, this.levenshteinAlphabet));
                }

                this.knownStrings.Add(knownGlyph.Name, new VocabularyItem(strings));
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
                KnownTrajectories = this.knownStrings.Select(
                    pair => new Serialization.KnownTrajectory()
                    {
                        Name = pair.Key,
                        Descriptors = pair.Value.Descriptors.Select(
                            desc => new Serialization.KnownTrajectory.Descriptor()
                            {
                                Strings = desc.Strings.Select(str =>
                                {
                                    return new Serialization.KnownTrajectory.Descriptor.IntArray()
                                    {
                                        Ints = str.Characters
                                    };
                                }).ToArray()
                            }).ToArray()
                    }).ToArray()
            };

            return JsonUtility.ToJson(serialization);
        }

        public void AddTrajectoryWithName(Trajectory trajectory, string name)
        {
            if (!this.knownStrings.ContainsKey(name))
            {
                this.knownStrings.Add(name, new VocabularyItem(new List<TrajectoryDescriptor>()));
            }

            this.knownStrings[name].Add(new TrajectoryDescriptor(trajectory, this.vectorsAlphabet, this.levenshteinAlphabet));
        }

        // TODO: I really hate this pattern.  Find a way to accomplish this without
        // either using obnoxious patterns or opening the door for confusing usage.
        public bool TryRecognizeTrajectory(Trajectory trajectory, out string result)
        {
            var descriptor = new TrajectoryDescriptor(trajectory, this.vectorsAlphabet, this.levenshteinAlphabet);
            result = null;

            var allowableMatches = this.knownStrings.Where(pair => pair.Value.GetMatchCost(descriptor) < MAXIMUM_ALLOWABLE_MATCH_COST);
            if (allowableMatches.Count() == 0)
            {
                return false;
            }
            else
            {
                float bestDistance = float.MaxValue;
                foreach (var pair in allowableMatches)
                {
                    float distance = pair.Value.GetMatchMinimumEditDistance(descriptor);
                    if (distance < bestDistance)
                    {
                        result = pair.Key;
                        bestDistance = distance;
                    }
                }
                return result != null;
            }
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

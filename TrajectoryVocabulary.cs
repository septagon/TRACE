using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Trace
{
    public class TrajectoryVocabulary
    {
        private const int FINEST_LEVEL_DESCRIPTOR_RESOLUTION = 64;
        private const float MAXIMUM_ALLOWABLE_MATCH_COST = 4f;

        [Serializable]
        private class TrajectoryVocabularySerialization
        {
            public Vector3[] Alphabet;
            public Dictionary<string, int[][]> KnownStrings;
        }

        private readonly Vector3[] vectorsAlphabet;
        private Levenshtein.Alphabet<int> levenshteinAlphabet;
        private Dictionary<string, List<Levenshtein.String<int>>> knownStrings;

        public TrajectoryVocabulary()
        {
            this.vectorsAlphabet = Utils.GenerateAlphabet();
            this.levenshteinAlphabet = CreateLevenshteinAlphabet(this.vectorsAlphabet);
            this.knownStrings = new Dictionary<string, List<Levenshtein.String<int>>>();
        }

        public TrajectoryVocabulary(string json) 
            : this(JsonUtility.FromJson<TrajectoryVocabularySerialization>(json))
        { }

        private TrajectoryVocabulary(TrajectoryVocabularySerialization serialization)
        {
            this.vectorsAlphabet = serialization.Alphabet;
            this.levenshteinAlphabet = CreateLevenshteinAlphabet(this.vectorsAlphabet);

            this.knownStrings = new Dictionary<string, List<Levenshtein.String<int>>>();
            foreach (var pair in serialization.KnownStrings)
            {
                var strings = new List<Levenshtein.String<int>>();
                foreach (var s in pair.Value)
                {
                    strings.Add(new Levenshtein.String<int>(s, this.levenshteinAlphabet));
                }

                this.knownStrings.Add(pair.Key, strings);
            }
        }

        public override string ToString()
        {
            var serialization = new TrajectoryVocabularySerialization()
            {
                Alphabet = this.vectorsAlphabet,
                KnownStrings = this.knownStrings.ToDictionary(pair => pair.Key,
                pair =>
                {
                    return pair.Value.Select(str => str.Characters).ToArray();
                })
            };

            return JsonUtility.ToJson(serialization);
        }

        public void AddTrajectoryWithName(Trajectory trajectory, string name)
        {
            if (!this.knownStrings.ContainsKey(name))
            {
                this.knownStrings.Add(name, new List<Levenshtein.String<int>>());
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
                float cost = GetGroupMatchCost(trajectoryString, pair.Value);
                if (cost < bestCost)
                {
                    result = pair.Key;
                    bestCost = cost;
                }
            }

            Debug.Log("Cost: " + bestCost);

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
        private static Levenshtein.Alphabet<int> CreateLevenshteinAlphabet(Vector3[] vectors)
        {
            return new Levenshtein.Alphabet<int>(
                Enumerable.Range(0, vectors.Length).ToArray(),
                idx => 1f,
                idx => 1f,
                (a, b) => 1f - Vector3.Dot(vectors[a], vectors[b]));
        }

        private static float GetGroupMatchCost<T>(Levenshtein.String<T> str, List<Levenshtein.String<T>> group)
        {
            // TODO: Something less ham-handed than this.
            return group.Average(g => g.Distance(str));
        }
    }
}

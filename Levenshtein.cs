using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

/**
 * Theory described here: https://en.wikipedia.org/wiki/Levenshtein_distance
 * Algorithm described here: https://en.wikipedia.org/wiki/Wagner%E2%80%93Fischer_algorithm
 * 
 * A basic, barely-tested implementation of the Wagner-Fischer algorithm for
 * Levenshtein distance, modified to support variable costs of insertion, deletion, 
 * and substitution.
 */
namespace Trace.Levenshtein
{
    // Alphabet class to support arbitrary alphabets, making the usage a little
    // more general.
    public class Alphabet<T>
    {
        private Dictionary<T, int> characterToIdx;
        private float[] insertionCosts;
        private float[] deletionCosts;
        private float[,] substitutionCosts;

        public int GetCharacterIdx(T character)
        {
            return this.characterToIdx[character];
        }

        public float GetInsertionCost(int idx)
        {
            return this.insertionCosts[idx];
        }

        public float GetDeletionCost(int idx)
        {
            return this.deletionCosts[idx];
        }

        public float GetSubstitutionCost(int idx1, int idx2)
        {
            int min = Math.Min(idx1, idx2);
            int max = Math.Max(idx1, idx2);

            return this.substitutionCosts[min, max];
        }

        // Basic constructor, in case you don't want to customize your costs.
        public Alphabet(T[] characters) : this(characters, _ => 1f, _ => 1f, (a, b) => a.Equals(b) ? 0f : 1f) { }

        // Fully-featured constructor with complete control of edit costs.
        public Alphabet(
            T[] characters,
            Func<T, float> charToInsertionCost,
            Func<T, float> charToDeletionCost,
            Func<T, T, float> charsToSubstitutionCost)
        {
            this.characterToIdx = new Dictionary<T, int>();
            this.insertionCosts = new float[characters.Length];
            this.deletionCosts = new float[characters.Length];
            this.substitutionCosts = new float[characters.Length, characters.Length];

            for (int outerIdx = 0; outerIdx < characters.Length; outerIdx++)
            {
                T c = characters[outerIdx];
                this.characterToIdx[c] = outerIdx;
                this.insertionCosts[outerIdx] = charToInsertionCost(c);
                this.deletionCosts[outerIdx] = charToDeletionCost(c);

                for (int innerIdx = outerIdx; innerIdx < characters.Length; innerIdx++)
                {
                    this.substitutionCosts[outerIdx, innerIdx] =
                        charsToSubstitutionCost(c, characters[innerIdx]);
                }
            }
        }
    }

    public class String<T>
    {
        private Alphabet<T> alphabet;
        private int[] characterIdxs;

        // NOTE: This is here exclusively to support serialization.
        // Remove when a better option is available, as we really 
        // shouldn't be storing more of T than is absolutely necessary.
        public T[] Characters { get; private set; }

        private int Length
        {
            get
            {
                return this.characterIdxs.Length;
            }
        }

        private int this[int idx]
        {
            get
            {
                return this.characterIdxs[idx];
            }
        }

        public String(T[] characters, Alphabet<T> alphabet)
        {
            this.alphabet = alphabet;
            this.characterIdxs = characters.Select(c => alphabet.GetCharacterIdx(c)).ToArray();

            this.Characters = characters;
        }

        public float Distance(String<T> other)
        {
            return Distance(this, other);
        }

        // This is the core of it: an implementation of the Wagner-Fisher algorithm.
        public static float Distance(String<T> from, String<T> to)
        {
            // Comparing strings built from different alphabets is nonsense.
            Debug.Assert(from.alphabet == to.alphabet);
            var alphabet = from.alphabet;

            // Initialize the cost matrix, populating the first row and column.
            float[,] costMatrix = new float[from.Length + 1, to.Length + 1];
            costMatrix[0, 0] = 0f;
            for (int idx = 0; idx < from.Length; idx++)
            {
                costMatrix[idx + 1, 0] = costMatrix[idx, 0] + alphabet.GetInsertionCost(from[idx]);
            }
            for (int idx = 0; idx < to.Length; idx++)
            {
                costMatrix[0, idx + 1] = costMatrix[0, idx] + alphabet.GetInsertionCost(to[idx]);
            }

            // Populate the cost matrix.
            for (int fromIdx = 0; fromIdx < from.Length; fromIdx++)
            {
                for (int toIdx = 0; toIdx < to.Length; toIdx++)
                {
                    float insertionCost = costMatrix[fromIdx + 1, toIdx] +  // TODO: Why are the indices this way and not the other way?
                        alphabet.GetInsertionCost(to[toIdx]);
                    float deletionCost = costMatrix[fromIdx, toIdx + 1] +  // TODO: Why are the indices this way and not the other way?
                        alphabet.GetDeletionCost(from[fromIdx]);
                    float substitutionCost = costMatrix[fromIdx, toIdx] +
                        alphabet.GetSubstitutionCost(from[fromIdx], to[toIdx]);

                    costMatrix[fromIdx + 1, toIdx + 1] = Utils.Min(insertionCost, deletionCost, substitutionCost);
                }
            }

            // Could reconstruct the edit path, if you care; if not, return the distance.
            return costMatrix[from.Length, to.Length];
        }
    }
}
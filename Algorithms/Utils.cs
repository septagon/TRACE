using System;
using System.Linq;
using UnityEngine;

namespace Trace
{
    public static class Utils
    {
        public static Vector3[] GenerateAlphabet(
            int alphabetSize = 128,
            int iterations = 256,
            float startingStepSize = 0.1f,
            float endingStepSize = 0.001f,
            System.Random random = null,
            params Vector3[] fixedValues)
        {
            const float EPSILON = 0.001f;
            const float EPSILON_SQUARED = EPSILON * EPSILON;
            const int DEFAULT_RANDOM_SEED = 11311;

            random = random ?? new System.Random(DEFAULT_RANDOM_SEED);

            var alphabet = new Vector3[alphabetSize];

            for (int idx = 0; idx < alphabet.Length; idx++)
            {
                alphabet[idx] = new Vector3(
                    (float)random.NextDouble() - 0.5f,
                    (float)random.NextDouble() - 0.5f,
                    (float)random.NextDouble() - 0.5f)
                    .normalized;
            }
            
            for (int idx = 0; idx < fixedValues.Length; idx++)
            {
                alphabet[idx] = fixedValues[idx];
            }

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                float stepSize = Lerp(startingStepSize, endingStepSize, iteration / (iterations - 1f));

                for (int idx = fixedValues.Length; idx < alphabetSize; idx++)
                {
                    var here = alphabet[idx];
                    var force = alphabet.Select(there =>
                    {
                        var vec = here - there;
                        if (vec.sqrMagnitude < EPSILON_SQUARED)
                        {
                            return new Vector3(0f, 0f, 0f);
                        }
                        else
                        {
                            return vec / vec.magnitude / vec.sqrMagnitude;
                        }
                    }).Aggregate(new Vector3(0f, 0f, 0f), (acc, pt) => acc + pt);
                    alphabet[idx] = here + stepSize * force;
                    alphabet[idx].Normalize();
                }
            }

            return alphabet;
        }

        public static float Lerp(float l, float r, float t)
        {
            return (1f - t) * l + t * r;
        }

        public static float Min(params float[] values)
        {
            return values.Min();
        }
    }
}

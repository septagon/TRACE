using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Trace
{
    public class Trajectory : IEnumerable<Vector3>
    {
        private List<Vector3> points;
        private float segmentLength;

        public float Length
        {
            get
            {
                return this.points.Count * this.segmentLength;
            }
        }

        public Trajectory(float segmentLength = 0.01f)
        {
            this.points = new List<Vector3>();
            this.segmentLength = segmentLength;
        }

        public void Add(Vector3 point)
        {
            if (this.points.Count == 0)
            {
                this.points.Add(point);
                return;
            }

            Func<float> getT = () => this.segmentLength / Vector3.Distance(this.points.Last(), point);
            for (float t = getT(); t <= 1f; t = getT())
            {
                this.points.Add((1f - t) * this.points.Last() + t * point);
            }
        }

        public Trajectory ResampleAtTargetResolution(int targetResolution)
        {
            var resampled = new Trajectory(this.Length / targetResolution);
            foreach (var pt in this)
            {
                resampled.Add(pt);
            }
            return resampled;
        }

        public List<int> Tokenize(Vector3[] tokens)
        {
            var tokenization = new List<int>();

            for (int idx = 2; idx < this.points.Count; idx++)
            {
                var segmentDir = TransformSegmentDir(this.points[idx - 2], this.points[idx - 1], this.points[idx]);

                if (segmentDir.HasValue)
                {
                    var token = TokenizeSegment(segmentDir.Value, tokens);
                    tokenization.Add(token);
                }
            }

            return tokenization;
        }
        
        private static Vector3? TransformSegmentDir(Vector3 prior, Vector3 from, Vector3 to)
        {
            const float DOT_PRODUCT_SAMPLE_REJECTION_THRESHOLD = 0.98f;

            Vector3 transformForward = Vector3.Normalize(from - prior);
            Vector3 inverseFromDir = Vector3.Normalize(-from);

            if (Math.Abs(Vector3.Dot(transformForward, inverseFromDir)) > DOT_PRODUCT_SAMPLE_REJECTION_THRESHOLD)
            {
                return null;
            }

            Vector3 transformUp = Vector3.Cross(transformForward, inverseFromDir);

            var rotation = Quaternion.LookRotation(transformForward, transformUp);
            return Quaternion.Inverse(rotation) * Vector3.Normalize(to - from);
        }

        private static int TokenizeSegment(Vector3 segmentDir, Vector3[] tokens)
        {
            int bestMatch = 0;
            float bestScore = Vector3.Dot(tokens[0], segmentDir);
            for (int idx = 1; idx < tokens.Length; idx++)
            {
                float score = Vector3.Dot(tokens[idx], segmentDir);
                if (score > bestScore)
                {
                    bestMatch = idx;
                    bestScore = score;
                }
            }

            return bestMatch;
        }

        public IEnumerator<Vector3> GetEnumerator()
        {
            return this.points.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}

using System;
using System.IO;
using UnityEngine;

namespace Trace
{
    public class Tracer
    {
        public event Action<Trajectory, string> OnTraceRecognized = (_, __) => { };
        public event Action<Trajectory> OnTraceNotRecognized = _ => { };

        private TrajectoryVocabulary vocabulary;
        private readonly string VOCABULARY_FILE_NAME = "trace_vocabulary.json";
        private string VocabularyFile
        {
            get
            {
                return Path.Combine(Application.dataPath, VOCABULARY_FILE_NAME);
            }
        }

        public abstract class BaseTraceCreator : IDisposable
        {
            public abstract void AddPoint(Vector3 point, Vector3 referencePoint);

            public abstract void Dispose();
        }

        private class TraceCreator : BaseTraceCreator
        {
            private event Action<Trajectory> onDisposed;
            private Trajectory trajectory;

            public TraceCreator(Tracer tracer)
            {
                this.onDisposed += tracer.TrajectoryCreationHandler;
                this.trajectory = new Trajectory(/* TODO: Consider making default segment length variable. */);
            }
            
            public override void AddPoint(Vector3 point, Vector3 referencePoint)
            {
                this.trajectory.Add(point - referencePoint);
            }

            public override void Dispose()
            {
                onDisposed(this.trajectory);
                this.onDisposed = null;
            }
        }

        public Tracer()
        {
            this.vocabulary = TrajectoryVocabulary.Load(this.VocabularyFile);
        }

        public BaseTraceCreator GetTraceCreator()
        {
            return new TraceCreator(this);
        }

        public void AddTrajectoryWithName(Trajectory trajectory, string name)
        {
            this.vocabulary.AddTrajectoryWithName(trajectory, name);
        }

        public void Save()
        {
            this.vocabulary.Save(this.VocabularyFile);
        }

        private void TrajectoryCreationHandler(Trajectory trajectory)
        {
            string recognized;
            if (this.vocabulary.TryRecognizeTrajectory(trajectory, out recognized))
            {
                OnTraceRecognized(trajectory, recognized);
            }
            else
            {
                OnTraceNotRecognized(trajectory);
            }
        }
    }
}

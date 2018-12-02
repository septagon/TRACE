using System;
using UnityEngine;

namespace Trace
{
    public class Tracer
    {
        private int newTrajectories = 0;

        public event Action<string> OnTraceRecognized = _ => { };

        private TrajectoryVocabulary vocabulary;

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
            this.vocabulary = new TrajectoryVocabulary();
        }

        public BaseTraceCreator GetTraceCreator()
        {
            return new TraceCreator(this);
        }

        public void TrajectoryCreationHandler(Trajectory trajectory)
        {
            string recognized;
            if (this.vocabulary.TryRecognizeTrajectory(trajectory, out recognized))
            {
                OnTraceRecognized(recognized);

                // TODO: Hack.
                this.vocabulary.AddTrajectoryWithName(trajectory, recognized);
            }
            else
            {
                // TODO: Do something if and only if the mode is right.
                this.vocabulary.AddTrajectoryWithName(trajectory, "" + this.newTrajectories++);
            }
        }
    }
}

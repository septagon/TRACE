using System;
using System.Collections;
using Trace;
using UnityEngine;

public class TrajectoryRecorder : MonoBehaviour
{
    [SerializeField]
    private Transform hand;

    [SerializeField]
    private Transform head;

    [SerializeField]
    private TMPro.TextMeshPro tmp;

    [SerializeField]
    private int takesPerName = 5;

    [SerializeField]
    private string[] names;

    private bool IsInGrippedState
    {
        get
        {
            const float TRIGGER_DOWN_THRESHOLD = 0.9f;
            return
                OVRInput.Get(OVRInput.RawAxis1D.RHandTrigger) > TRIGGER_DOWN_THRESHOLD &&
                OVRInput.Get(OVRInput.RawAxis1D.RIndexTrigger) > TRIGGER_DOWN_THRESHOLD;
        }
    }

    private void Start()
    {
        StartCoroutine(RecordTrajectoriesCoroutine());
    }

    private IEnumerator RecordTrajectoriesCoroutine()
    {
        var tracer = new Trace.Tracer();

        for (int nameIdx = 0; nameIdx < this.names.Length; nameIdx++)
        {
            for (int takeIdx = 0; takeIdx < this.takesPerName; takeIdx++)
            {
                string nameAndTake = this.names[nameIdx] + ", take " + takeIdx;

                Action<Trajectory> onNotRecognized = traj =>
                {
                    tracer.AddTrajectoryWithName(traj, this.names[nameIdx]);
                };
                Action<Trajectory, string> onRecognized = (traj, recognizedName) =>
                {
                    if (this.names[nameIdx] != recognizedName)
                    {
                        Debug.LogWarning(this.names[nameIdx] + " was recognized as " + recognizedName + "; trajectories may be ambiguous.");
                    }

                    tracer.AddTrajectoryWithName(traj, this.names[nameIdx]);
                };

                tracer.OnTraceNotRecognized += onNotRecognized;
                tracer.OnTraceRecognized += onRecognized;

                this.tmp.text = "Next: " + nameAndTake;
                while (!this.IsInGrippedState)
                {
                    yield return null;
                }

                this.tmp.text = "Recording " + nameAndTake + "...";
                using (var traceCreator = tracer.GetTraceCreator())
                {
                    while (this.IsInGrippedState)
                    {
                        traceCreator.AddPoint(this.hand.transform.position, this.head.transform.position);
                        yield return null;
                    }
                }

                tracer.OnTraceNotRecognized -= onNotRecognized;
                tracer.OnTraceRecognized -= onRecognized;

                this.tmp.text = "Recorded " + nameAndTake + "!";
                yield return new WaitForSeconds(1f);
            }
        }

        this.tmp.text = "Done!";
        
        tracer.Save();
    }
}

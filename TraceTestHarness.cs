using System.Collections;
using Trace;
using UnityEngine;

public class TraceTestHarness : MonoBehaviour
{
    private Trace.Tracer tracer;

    public Transform hand;
    public Transform head;
    public TMPro.TextMeshPro tmp;

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
        this.tracer = new Trace.Tracer();
        this.tracer.OnTraceRecognized += OnTraceRecognizedHandler;
        this.tracer.OnTraceNotRecognized += OnTraceNotRecognizedHandler;

        StartCoroutine(NotTracingCoroutine());
    }

    private void OnDestroy()
    {
        this.tracer.OnTraceRecognized -= OnTraceRecognizedHandler;
        this.tracer.OnTraceNotRecognized -= OnTraceNotRecognizedHandler;

        this.tracer.Save();
    }

    private IEnumerator NotTracingCoroutine()
    {
        while (!this.IsInGrippedState)
        {
            yield return null;
        }

        StartCoroutine(TracingCoroutine());
    }

    private IEnumerator TracingCoroutine()
    {
        using (var traceCreator = this.tracer.GetTraceCreator())
        {
            while (this.IsInGrippedState)
            {
                traceCreator.AddPoint(this.hand.transform.position, this.head.transform.position);
                yield return null;
            }
        }

        StartCoroutine(NotTracingCoroutine());
    }

    private void OnTraceRecognizedHandler(Trajectory trajectory, string recognized)
    {
        this.tmp.text = recognized;
    }

    private int idx = 0;
    private void OnTraceNotRecognizedHandler(Trajectory trajectory)
    {
        this.tmp.text = "?";
    }
}

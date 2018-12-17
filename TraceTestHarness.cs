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

        // Holy CRAP that's way more fun than it should be.  I think I have something here.
        if (recognized == "fireball")
        {
            Vector3 throwDir = Vector3.zero;
            Vector3? prior = null;
            float multiplier = 1f;
            foreach (var v in trajectory)
            {
                if (prior.HasValue)
                {
                    throwDir += multiplier++ * (v - prior.Value);
                }
                prior = v;
            }
            throwDir.Normalize();

            // Aim assist-ish thing.
            var lookDir = this.head.forward;
            float t = Mathf.Min(1f, Mathf.Pow(1f - Vector3.Dot(throwDir, lookDir), 2f));
            var dir = t * throwDir + (1f - t) * lookDir;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.position = this.hand.position + 0.1f * dir;
            go.transform.localScale *= 0.1f;
            go.GetComponent<MeshRenderer>().material.color = Color.red;
            var rigidbody = go.AddComponent<Rigidbody>();
            rigidbody.useGravity = false;
            rigidbody.AddForce(dir * 1000f);
        }
    }

    private int idx = 0;
    private void OnTraceNotRecognizedHandler(Trajectory trajectory)
    {
        this.tmp.text = "?";
    }
}

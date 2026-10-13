using System;
using System.Collections.Generic;
using UnityEngine;

namespace RVM.Experimental
{
    // Reads contact impulse only. Never reads Rigidbody.mass or a specimen/profile label.
    public sealed class RvmLoadCell : MonoBehaviour
    {
        public float minimumGrams = 5, maximumGrams = 1000;
        public int windowSamples = 30;
        public bool noiseEnabled;
        public float noiseAmplitudeGrams = .5f;
        public int noiseSeed = 106;
        public float grams, standardDeviation, forceNewtons;
        public bool valid;
        public string reason = "No contact";
        public int contactBodies;
        public Rigidbody ContactBody {get;private set;}
        readonly HashSet<Rigidbody> bodies = new HashSet<Rigidbody>();
        readonly Queue<float> samples = new Queue<float>();
        float impulse, maximumSpeed, tareGrams;
        Rigidbody previousBody;
        System.Random random;

        void Awake() { random = new System.Random(noiseSeed); }
        void OnCollisionEnter(Collision c) { Accumulate(c); }
        void OnCollisionStay(Collision c) { Accumulate(c); }
        void Accumulate(Collision c)
        {
            var body = c.rigidbody;
            if (!body || body.isKinematic) return;
            bodies.Add(body);
            impulse += Mathf.Abs(Vector3.Dot(c.impulse, Vector3.up));
            maximumSpeed = Mathf.Max(maximumSpeed, body.linearVelocity.magnitude);
        }
        public bool TareEmpty()
        {
            if (contactBodies != 0) return false;
            tareGrams = 0; samples.Clear(); return true;
        }
        void FixedUpdate()
        {
            // Callbacks after the previous physics step produced this impulse.
            contactBodies = bodies.Count;
            Rigidbody bodyId = null; foreach (var id in bodies) bodyId = id;
            ContactBody=contactBodies==1?bodyId:null;
            var guard=bodyId?bodyId.GetComponent<RvmSupportMonitor>():null;
            bool foreignSupport=guard&&guard.ForeignContact;
            float gravity = Mathf.Abs(Physics.gravity.y);
            forceNewtons = impulse / Time.fixedDeltaTime;
            float raw = gravity > .001f ? forceNewtons / gravity * 1000 - tareGrams : 0;
            valid = false;
            if (foreignSupport || contactBodies != 1 || maximumSpeed > .025f || gravity < .001f ||
                Mathf.Abs(Physics.gravity.x) + Mathf.Abs(Physics.gravity.z) > .001f)
            {
                samples.Clear(); grams = 0; standardDeviation = 0;
                reason = foreignSupport ? "Other surface contact" : contactBodies == 0 ? "No contact / sleeping body" : contactBodies > 1 ? "Multiple bodies" : "Moving / invalid gravity";
            }
            else
            {
                if (bodyId != previousBody) samples.Clear();
                samples.Enqueue(raw);
                while (samples.Count > Mathf.Max(5, windowSamples)) samples.Dequeue();
                double sum = 0, squares = 0;
                foreach (float value in samples) { sum += value; squares += value * value; }
                grams = (float)(sum / samples.Count);
                standardDeviation = (float)Math.Sqrt(Math.Max(0, squares / samples.Count - grams * grams));
                bool stable = samples.Count >= Mathf.Max(5, windowSamples) && standardDeviation <= Mathf.Max(.5f, grams * .005f);
                // Tiny endpoint tolerance for the physics solver, not an extended measuring range.
                bool inRange = grams >= minimumGrams - .01f && grams <= maximumGrams + .01f;
                valid = stable && inRange;
                reason = !stable ? "Settling" : !inRange ? "Outside 5–1000 g range" : "Stable contact measurement";
                if (valid && noiseEnabled) grams += (float)(random.NextDouble() * 2 - 1) * noiseAmplitudeGrams;
            }
            previousBody = bodyId;
            impulse = 0; maximumSpeed = 0; bodies.Clear();
        }
    }
}

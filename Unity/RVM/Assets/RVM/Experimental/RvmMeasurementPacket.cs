using System;

namespace RVM.Experimental
{
    // Transport preparation only. The receiver must also validate its own request,
    // session and receive timeout; sender age alone cannot prove network freshness.
    [Serializable]
    public sealed class RvmMeasurementPacket
    {
        public int contractVersion = 106;
        public string sessionId;
        public uint productId, requestId;
        public bool imageValid, scaleValid, probeValid;
        public float lengthMetres, widthMetres, massGrams;
        public float transmission, reflection, metal, magnetic, ageSeconds;

        public static RvmMeasurementPacket Create(string session, uint product, uint request,
            RvmMaterialDecision.Reading reading)
        {
            if (string.IsNullOrEmpty(session) || product == 0 || request == 0)
                throw new ArgumentException("Measurement identity is required");
            foreach (float v in new[] { reading.length, reading.width, reading.grams,
                reading.transmission, reading.reflection, reading.metal, reading.magnetic, reading.ageSeconds })
                if (float.IsNaN(v) || float.IsInfinity(v))
                    throw new ArgumentException("Non-finite measurement cannot be exported");
            if (reading.ageSeconds < 0 || reading.ageSeconds > 2 ||
                !reading.imageValid || !reading.scaleValid || !reading.probeValid)
                throw new InvalidOperationException("A fresh, complete measurement is required");
            return new RvmMeasurementPacket {
                sessionId=session, productId=product, requestId=request,
                imageValid=reading.imageValid, scaleValid=reading.scaleValid, probeValid=reading.probeValid,
                lengthMetres=reading.length, widthMetres=reading.width, massGrams=reading.grams,
                transmission=reading.transmission, reflection=reading.reflection,
                metal=reading.metal, magnetic=reading.magnetic, ageSeconds=reading.ageSeconds
            };
        }
    }
}

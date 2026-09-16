using System;
using System.Collections.Generic;
using UnityEngine;

namespace OTA.Corridor.Annotation
{
    public struct ConductorSpec
    {
        public string model;
        public float diameterMm;

        public ConductorSpec(string m, float d)
        {
            model = m;
            diameterMm = d;
        }
    }

    /// <summary>
    /// High-precision Catenary Curve & Conductor Sag Fitting Engine:
    /// 1. Projected 2D profile RANSAC / Density Peak histogram clustering for candidate points.
    /// 2. Analytical Least Squares solution for true mid-span sag f*.
    /// 3. Cross-sectional LiDAR point spread estimation for millimeter conductor diameter.
    /// 4. Automatic matching to DL/T standard ACSR overhead conductor specifications (LGJ series).
    /// </summary>
    public static class SagFinder
    {
        public static readonly ConductorSpec[] StandardSpecs = new ConductorSpec[]
        {
            new ConductorSpec("LGJ-150/25", 17.1f),
            new ConductorSpec("LGJ-185/25", 19.0f),
            new ConductorSpec("LGJ-240/30", 21.6f),
            new ConductorSpec("LGJ-300/40", 24.2f),
            new ConductorSpec("LGJ-400/35", 26.8f),
            new ConductorSpec("LGJ-500/45", 30.0f),
            new ConductorSpec("LGJ-630/45", 33.6f),
            new ConductorSpec("LGJ-720/50", 36.2f)
        };

        private struct CandidatePoint
        {
            public Vector3 point;
            public float u;
            public float sag;
            public float phi;
            public float impliedF;
        }

        public static ConductorAnnotation AnalyzeConductor(
            string spanId,
            string phase,
            Vector3 start,
            Vector3 end,
            Vector3[] points,
            byte[] classes = null,
            float corridorWidth = 3.0f,
            int curveSegments = 64)
        {
            float dx = end.x - start.x;
            float dz = end.z - start.z;
            float Lh = Mathf.Sqrt(dx * dx + dz * dz);
            float h = end.y - start.y;
            if (Lh < 1.0f) Lh = 1.0f;

            Vector2 fwd = new Vector2(dx / Lh, dz / Lh);
            Vector2 normal = new Vector2(-fwd.y, fwd.x);

            float halfWidth = corridorWidth * 0.5f;
            float lowerHangingY = Mathf.Min(start.y, end.y);
            float groundEstimate = lowerHangingY - 45f;

            List<CandidatePoint> candidates = new List<CandidatePoint>();

            if (points != null && points.Length > 0)
            {
                // Dynamic stride: ensure dense sampling even in large clouds
                int stride = Mathf.Max(1, points.Length / 250000);

                for (int i = 0; i < points.Length; i += stride)
                {
                    if (classes != null && classes.Length == points.Length)
                    {
                        byte cls = classes[i];
                        // Class 2 is ground, skip if known
                        if (cls == 2) continue;
                    }

                    Vector3 p = points[i];
                    if (p.y < groundEstimate + 3.0f) continue; // ground clearance

                    float px = p.x - start.x;
                    float pz = p.z - start.z;

                    float t = px * fwd.x + pz * fwd.y;
                    float u = t / Lh;
                    if (u < 0.03f || u > 0.97f) continue; // skip near tower attachments

                    float perp = Mathf.Abs(px * normal.x + pz * normal.y);
                    if (perp > halfWidth) continue;

                    float chordY = start.y + u * h;
                    float sag = chordY - p.y;
                    if (sag <= 0.1f || sag > 40.0f) continue; // must hang below chord

                    float phi = 4.0f * u * (1.0f - u);
                    if (phi < 0.05f) continue;

                    float impliedF = sag / phi;
                    if (impliedF >= 0.5f && impliedF <= 45.0f)
                    {
                        candidates.Add(new CandidatePoint
                        {
                            point = p,
                            u = u,
                            sag = sag,
                            phi = phi,
                            impliedF = impliedF
                        });
                    }
                }
            }

            float bestSag = 0f;
            List<CandidatePoint> inliers = new List<CandidatePoint>();

            if (candidates.Count >= 5)
            {
                // 1. Histogram density peak clustering (bin size 0.25m from 0.5m to 40m)
                float binSize = 0.25f;
                int numBins = Mathf.CeilToInt(40.0f / binSize);
                int[] bins = new int[numBins];

                for (int i = 0; i < candidates.Count; i++)
                {
                    int b = Mathf.Clamp(Mathf.FloorToInt(candidates[i].impliedF / binSize), 0, numBins - 1);
                    bins[b]++;
                }

                int maxBin = 0;
                int maxCount = 0;
                for (int b = 0; b < numBins; b++)
                {
                    // 3-bin window smoothing
                    int count = bins[b];
                    if (b > 0) count += bins[b - 1] / 2;
                    if (b < numBins - 1) count += bins[b + 1] / 2;

                    if (count > maxCount)
                    {
                        maxCount = count;
                        maxBin = b;
                    }
                }

                float fMode = (maxBin + 0.5f) * binSize;

                // 2. Select inliers within tolerance
                for (int i = 0; i < candidates.Count; i++)
                {
                    var cp = candidates[i];
                    if (Mathf.Abs(cp.impliedF - fMode) <= 0.85f &&
                        Mathf.Abs(cp.sag - fMode * cp.phi) <= 0.40f)
                    {
                        inliers.Add(cp);
                    }
                }

                // 3. Analytical Least Squares on inliers: minimize sum (sag_i - f * phi_i)^2
                if (inliers.Count >= 5)
                {
                    float sumNumerator = 0f;
                    float sumDenominator = 0f;
                    for (int i = 0; i < inliers.Count; i++)
                    {
                        sumNumerator += inliers[i].sag * inliers[i].phi;
                        sumDenominator += inliers[i].phi * inliers[i].phi;
                    }

                    if (sumDenominator > 0.0001f)
                    {
                        bestSag = sumNumerator / sumDenominator;
                    }
                }
            }

            // Fallback: standard DL/T 5582 empirical sag
            if (bestSag < 0.5f)
            {
                bestSag = Mathf.Clamp((Lh * Lh) / (8f * 1200f), 2.0f, 30.0f);
            }

            Vector3 bestMidPoint = new Vector3(
                (start.x + end.x) * 0.5f,
                (start.y + end.y) * 0.5f - bestSag,
                (start.z + end.z) * 0.5f
            );

            Vector3[] curve = GenerateCatenaryPoints(start, end, Lh, h, bestSag, curveSegments);

            // 4. Conductor diameter & model inversion
            float estimatedDiamMm = 26.8f;
            string matchedModel = "LGJ-400/35";

            if (inliers.Count >= 5)
            {
                float sumR = 0f;
                List<float> rads = new List<float>();

                for (int i = 0; i < inliers.Count; i++)
                {
                    var p = inliers[i].point;
                    float u = inliers[i].u;
                    Vector3 curvePt = new Vector3(
                        Mathf.Lerp(start.x, end.x, u),
                        start.y + u * h - 4f * bestSag * u * (1f - u),
                        Mathf.Lerp(start.z, end.z, u)
                    );
                    float r = Vector3.Distance(p, curvePt);
                    rads.Add(r);
                    sumR += r;
                }

                float meanR = sumR / inliers.Count;
                float sumVar = 0f;
                for (int i = 0; i < rads.Count; i++)
                {
                    float diff = rads[i] - meanR;
                    sumVar += diff * diff;
                }
                float stdR = Mathf.Sqrt(sumVar / inliers.Count);

                // Effective wire diameter: 2 * (meanR + 0.3 * stdR) converted to mm
                float calcDiam = 2f * (meanR + 0.25f * stdR) * 1000f;
                calcDiam = Mathf.Clamp(calcDiam, 16.0f, 40.0f);

                // Match closest standard spec
                float minDiff = float.MaxValue;
                for (int s = 0; s < StandardSpecs.Length; s++)
                {
                    float d = Mathf.Abs(StandardSpecs[s].diameterMm - calcDiam);
                    if (d < minDiff)
                    {
                        minDiff = d;
                        estimatedDiamMm = StandardSpecs[s].diameterMm;
                        matchedModel = StandardSpecs[s].model;
                    }
                }
            }

            var annotation = new ConductorAnnotation(spanId, phase, start, end, bestMidPoint, bestSag, curve);
            annotation.estimatedDiameterMm = estimatedDiamMm;
            annotation.conductorModel = matchedModel;
            annotation.inlierPointCount = inliers.Count;

            return annotation;
        }

        public static Vector3[] GenerateCatenaryPoints(Vector3 start, Vector3 end, float Lh, float h, float sag, int segments)
        {
            Vector3[] pts = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float u = (float)i / segments;
                float x = Mathf.Lerp(start.x, end.x, u);
                float z = Mathf.Lerp(start.z, end.z, u);
                float y = start.y + u * h - 4f * sag * u * (1f - u);
                pts[i] = new Vector3(x, y, z);
            }
            return pts;
        }

        public static float GetChordYAt(Vector3 start, Vector3 end, float x, float z)
        {
            float dx = end.x - start.x;
            float dz = end.z - start.z;
            float Lh2 = dx * dx + dz * dz;
            if (Lh2 < 0.0001f) return (start.y + end.y) * 0.5f;

            float t = ((x - start.x) * dx + (z - start.z) * dz) / Lh2;
            return start.y + t * (end.y - start.y);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace ThisWillFloat.Demo
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshCollider))]
    public sealed class EditableIslandMeshCanvas : MonoBehaviour
    {
        [SerializeField] private int vertexResolution = 145;
        [SerializeField] private float size = 560f;
        [SerializeField] private float seaFloorHeight = -2.6f;
        [SerializeField] private float waterLevel = 0f;
        [SerializeField] private float[] heights;
        [SerializeField] private int[] paintLayers;

        public int VertexResolution => vertexResolution;
        public int QuadResolution => Mathf.Max(1, vertexResolution - 1);
        public float Size => size;
        public float SeaFloorHeight => seaFloorHeight;
        public float WaterLevel => waterLevel;

        public Vector3 EstimateVolcanoCenterWorld()
        {
            EnsureData();
            Vector2 local = EstimateVolcanoCenterLocal();
            float height = SampleHeightLocal(local.x, local.y);
            return transform.TransformPoint(new Vector3(local.x, height, local.y));
        }

        public void Initialize(int resolution, float canvasSize, float floorHeight, float seaLevel)
        {
            vertexResolution = Mathf.Clamp(resolution, 17, 513);
            size = Mathf.Max(64f, canvasSize);
            seaFloorHeight = floorHeight;
            waterLevel = seaLevel;
            heights = new float[vertexResolution * vertexResolution];
            paintLayers = new int[QuadResolution * QuadResolution];

            for (int i = 0; i < heights.Length; i++)
            {
                heights[i] = seaFloorHeight;
            }

            for (int i = 0; i < paintLayers.Length; i++)
            {
                paintLayers[i] = 1;
            }

            RebuildMesh();
        }

        public void CopyState(out float[] heightCopy, out int[] paintCopy)
        {
            EnsureData();
            heightCopy = (float[])heights.Clone();
            paintCopy = (int[])paintLayers.Clone();
        }

        public void RestoreState(float[] heightCopy, int[] paintCopy)
        {
            if (heightCopy == null || paintCopy == null)
            {
                return;
            }

            EnsureData();
            if (heightCopy.Length != heights.Length || paintCopy.Length != paintLayers.Length)
            {
                return;
            }

            heights = (float[])heightCopy.Clone();
            paintLayers = (int[])paintCopy.Clone();
            SanitizeTerrainData();
            RebuildMesh();
        }

        public bool HasValidVisibleLand()
        {
            EnsureData();
            int landVertices = 0;
            float maxHeight = float.NegativeInfinity;
            float minHeight = float.PositiveInfinity;

            for (int i = 0; i < heights.Length; i++)
            {
                float height = heights[i];
                if (float.IsNaN(height) || float.IsInfinity(height))
                {
                    return false;
                }

                if (height > waterLevel + 0.6f)
                {
                    landVertices++;
                }

                maxHeight = Mathf.Max(maxHeight, height);
                minHeight = Mathf.Min(minHeight, height);
            }

            int requiredLandVertices = Mathf.Max(96, heights.Length / 90);
            return landVertices >= requiredLandVertices
                && maxHeight > waterLevel + 8f
                && minHeight >= seaFloorHeight - 0.25f
                && maxHeight <= 180f;
        }

        public void PolishVolcanoLandmark()
        {
            PolishVolcanoLandmark(null);
        }

        public void PolishVolcanoLandmark(Texture2D volcanoStamp)
        {
            EnsureData();

            Vector2 center = EstimateVolcanoCenterLocal();
            float footprintRadius = Mathf.Clamp(size * 0.285f, 300f, 455f);
            float transitionRadius = footprintRadius * 1.18f;
            float craterRadius = footprintRadius * 0.105f;
            float rimRadius = footprintRadius * 0.165f;
            float skirtHeight = waterLevel + 4.2f;
            float peakHeight = Mathf.Clamp(size * 0.112f, 118f, 166f);
            float craterFloorHeight = peakHeight - Mathf.Clamp(size * 0.026f, 26f, 36f);
            float spacing = size / QuadResolution;
            float half = size * 0.5f;
            Vector2 stampRange = GetStampRange(volcanoStamp);

            for (int z = 0; z < vertexResolution; z++)
            {
                for (int x = 0; x < vertexResolution; x++)
                {
                    float localX = -half + x * spacing;
                    float localZ = -half + z * spacing;
                    Vector2 offset = new Vector2(localX - center.x, localZ - center.y);
                    float angle = Mathf.Atan2(offset.y, offset.x);
                    float angleWarp = 1f
                        + Mathf.Sin(angle * 2.15f + 0.55f) * 0.085f
                        + Mathf.Cos(angle * 4.4f - 1.2f) * 0.065f
                        + Mathf.Sin(angle * 7.2f + 2.1f) * 0.03f;
                    float directionalStretch = 1f + Mathf.Cos(angle - 0.85f) * 0.08f;
                    float radius = offset.magnitude;
                    float warpedDistance = radius / Mathf.Max(0.72f, angleWarp * directionalStretch);
                    float footprintDistance = warpedDistance / Mathf.Max(1f, footprintRadius);
                    float transitionDistance = warpedDistance / Mathf.Max(1f, transitionRadius);

                    if (transitionDistance > 1f)
                    {
                        continue;
                    }

                    float current = GetHeight(x, z);
                    if (current <= waterLevel + 0.15f && transitionDistance > 0.92f)
                    {
                        continue;
                    }

                    float target;
                    if (footprintDistance > 1f)
                    {
                        float skirt01 = Smooth01(Mathf.InverseLerp(transitionRadius, footprintRadius, warpedDistance));
                        target = Mathf.Lerp(waterLevel + 2.2f, skirtHeight, skirt01);
                    }
                    else
                    {
                        float d = Mathf.Clamp01(footprintDistance);
                        float stamp = SampleStampNormalized(volcanoStamp, offset, footprintRadius, stampRange);
                        float radialFade = Smooth01(Mathf.Clamp01((1.04f - d) / 0.2f));
                        float broadBase = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Pow(d, 1.42f)), 1.08f);
                        float upperSteepening = Smooth01(Mathf.InverseLerp(0.86f, 0.18f, d));
                        float proceduralMountain = Mathf.Clamp01((broadBase * 0.72f + upperSteepening * broadBase * 0.28f) * radialFade);
                        float stampMountain = Mathf.Pow(Smooth01(stamp), 1.18f) * radialFade;
                        float mountain01 = volcanoStamp != null
                            ? Mathf.Clamp01(Mathf.Lerp(proceduralMountain, stampMountain, 0.38f))
                            : proceduralMountain;
                        target = Mathf.Lerp(skirtHeight, peakHeight, mountain01);

                        float ridgeMask = Smooth01(Mathf.Clamp01((1f - d) / 0.92f)) * Smooth01(Mathf.Clamp01((d - 0.16f) / 0.46f));
                        float radialRidge = Mathf.Sin(angle * 17.0f + Mathf.PerlinNoise(x * 0.031f, z * 0.031f) * 4.6f);
                        float ridgeCut = Mathf.Pow(Mathf.Abs(radialRidge), 1.75f) * ridgeMask;
                        float fineNoise = Mathf.PerlinNoise(x * 0.092f + 19.2f, z * 0.092f + 7.4f) - 0.5f;
                        float stampDetail = volcanoStamp != null ? (stamp - 0.5f) * ridgeMask * 5.2f : 0f;
                        target += ridgeCut * 5.6f + fineNoise * Mathf.Lerp(0.6f, 4.4f, ridgeMask) + stampDetail;

                        float craterBowl = Mathf.Exp(-(warpedDistance * warpedDistance) / (2f * craterRadius * craterRadius));
                        float rimBoost = Mathf.Exp(-Mathf.Pow((warpedDistance - rimRadius) / Mathf.Max(1f, footprintRadius * 0.038f), 2f));
                        target -= craterBowl * (peakHeight - craterFloorHeight);
                        target += rimBoost * Mathf.Clamp(size * 0.0065f, 6f, 9f);

                        if (warpedDistance < craterRadius * 0.72f)
                        {
                            float floorBlend = Smooth01(warpedDistance / Mathf.Max(1f, craterRadius * 0.72f));
                            target = Mathf.Lerp(craterFloorHeight, target, floorBlend);
                        }
                    }

                    float overwrite = footprintDistance <= 0.98f ? 1f : Smooth01(Mathf.Clamp01((1f - transitionDistance) / 0.18f));
                    float next = Mathf.Lerp(current, target, overwrite);
                    SetHeight(x, z, Mathf.Clamp(next, seaFloorHeight, 150f));
                }
            }

            SmoothVolcanoSkirt(center, footprintRadius * 0.62f, 0.022f);
            SmoothVolcanoSkirt(center, transitionRadius, 0.065f);
            PaintVolcanoTransition(center, footprintRadius * 0.38f, footprintRadius * 0.72f, transitionRadius);
            RebuildMesh();
        }

        public void RepaintReadableIslandBiomes()
        {
            EnsureData();

            Vector2 center = EstimateVolcanoCenterLocal();
            float coneRadius = Mathf.Clamp(size * 0.12f, 118f, 180f);
            float volcanicRadius = coneRadius * 1.05f;
            float vegetationInnerRadius = volcanicRadius * 0.78f;
            float vegetationOuterRadius = coneRadius * 2.25f;
            float spacing = size / QuadResolution;
            float half = size * 0.5f;

            for (int z = 0; z < QuadResolution; z++)
            {
                for (int x = 0; x < QuadResolution; x++)
                {
                    float localX = -half + (x + 0.5f) * spacing;
                    float localZ = -half + (z + 0.5f) * spacing;
                    Vector2 offset = new Vector2(localX - center.x, localZ - center.y);
                    float angle = Mathf.Atan2(offset.y, offset.x);
                    float warp = 1f
                        + Mathf.Sin(angle * 2.7f + 0.55f) * 0.1f
                        + Mathf.Cos(angle * 4.9f - 1.2f) * 0.06f;
                    float volcanoDistance = offset.magnitude / Mathf.Max(1f, warp);
                    float height = AverageQuadHeight(x, z);
                    float shoreNoise = Mathf.PerlinNoise(localX * 0.052f + 13.7f, localZ * 0.052f + 42.4f);

                    int layer;
                    if (height <= waterLevel + Mathf.Lerp(0.7f, 1.15f, shoreNoise))
                    {
                        layer = 1;
                    }
                    else if (height <= waterLevel + Mathf.Lerp(3.05f, 3.7f, shoreNoise))
                    {
                        layer = 0;
                    }
                    else if (volcanoDistance <= volcanicRadius || height >= waterLevel + 28.5f)
                    {
                        layer = 2;
                    }
                    else if (volcanoDistance > vegetationInnerRadius && volcanoDistance < vegetationOuterRadius && height <= waterLevel + 20.5f)
                    {
                        layer = 3;
                    }
                    else
                    {
                        layer = 0;
                    }

                    SetPaintLayer(x, z, layer);
                }
            }

            RebuildMesh();
        }

        public void ResizePreserveWorld(float newSize, int newResolution)
        {
            EnsureData();
            newSize = Mathf.Max(size, newSize);
            newResolution = Mathf.Clamp(newResolution, vertexResolution, 513);

            float oldSize = size;
            int oldResolution = vertexResolution;
            int oldQuadResolution = Mathf.Max(1, oldResolution - 1);
            float[] oldHeights = heights;
            int[] oldPaintLayers = paintLayers;

            vertexResolution = newResolution;
            size = newSize;
            heights = new float[vertexResolution * vertexResolution];
            paintLayers = new int[QuadResolution * QuadResolution];

            float newHalf = newSize * 0.5f;
            float spacing = newSize / QuadResolution;

            for (int z = 0; z < vertexResolution; z++)
            {
                for (int x = 0; x < vertexResolution; x++)
                {
                    float localX = -newHalf + x * spacing;
                    float localZ = -newHalf + z * spacing;
                    SetHeight(x, z, SampleOldHeight(localX, localZ, oldSize, oldResolution, oldHeights));
                }
            }

            for (int z = 0; z < QuadResolution; z++)
            {
                for (int x = 0; x < QuadResolution; x++)
                {
                    float localX = -newHalf + (x + 0.5f) * spacing;
                    float localZ = -newHalf + (z + 0.5f) * spacing;
                    SetPaintLayer(x, z, SampleOldPaint(localX, localZ, oldSize, oldQuadResolution, oldPaintLayers));
                }
            }

            RebuildMesh();
        }

        public void MakeStarterIsland()
        {
            EnsureData();
            int res = vertexResolution;
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    Vector2 normalized = GridToNormalized(x, z);
                    float angle = Mathf.Atan2(normalized.y, normalized.x);
                    float radius = normalized.magnitude;
                    float edge = 0.72f + Mathf.Sin(angle * 2.1f) * 0.08f + Mathf.Cos(angle * 4.7f) * 0.055f + Mathf.Sin(angle * 8.4f) * 0.025f;
                    float inside = Smooth01((edge - radius) / 0.12f);
                    float center = Mathf.Clamp01(1f - radius / 0.33f);
                    float crater = Mathf.Clamp01(1f - Mathf.Abs(radius - 0.115f) / 0.048f);
                    float beachSlope = Mathf.Lerp(1.0f, 5.2f, Mathf.Pow(Mathf.Clamp01(1f - radius / Mathf.Max(0.001f, edge)), 0.58f));
                    float volcano = Mathf.Pow(center, 2.0f) * 21f - crater * 5.4f;
                    float surfaceNoise = (Mathf.PerlinNoise(x * 0.035f + 18.1f, z * 0.035f + 5.9f) - 0.5f) * 0.75f;
                    float worldY = Mathf.Lerp(seaFloorHeight, beachSlope + volcano + surfaceNoise, inside);
                    SetHeight(x, z, worldY);
                }
            }

            for (int z = 0; z < QuadResolution; z++)
            {
                for (int x = 0; x < QuadResolution; x++)
                {
                    Vector2 normalized = GridToNormalized(x, z);
                    float radius = normalized.magnitude;
                    int layer = 0;
                    if (radius > 0.68f)
                    {
                        layer = 1;
                    }
                    else if (radius < 0.34f)
                    {
                        layer = 2;
                    }
                    else if (radius < 0.58f)
                    {
                        layer = 3;
                    }

                    SetPaintLayer(x, z, layer);
                }
            }

            RebuildMesh();
        }

        public void GenerateRecoveredReferenceIsland()
        {
            EnsureData();

            float half = size * 0.5f;
            float spacing = size / QuadResolution;
            Vector2 volcanoCenter = new Vector2(18f, 14f);

            for (int z = 0; z < vertexResolution; z++)
            {
                for (int x = 0; x < vertexResolution; x++)
                {
                    float localX = -half + x * spacing;
                    float localZ = -half + z * spacing;
                    Vector2 point = new Vector2(localX, localZ);
                    float land = ReferenceIslandMask(point);
                    float shore = Smooth01((land - 0.18f) / 0.2f);
                    float inner = Smooth01((land - 0.46f) / 0.36f);

                    if (shore <= 0.01f)
                    {
                        SetHeight(x, z, seaFloorHeight);
                        continue;
                    }

                    float broadNoise = Mathf.PerlinNoise(localX * 0.014f + 17.1f, localZ * 0.014f + 8.4f) - 0.5f;
                    float fineNoise = Mathf.PerlinNoise(localX * 0.043f + 4.8f, localZ * 0.043f + 91.7f) - 0.5f;
                    float ground = Mathf.Lerp(
                        seaFloorHeight,
                        waterLevel + 0.72f + inner * 2.35f + broadNoise * 0.75f + fineNoise * 0.24f,
                        shore);

                    float volcanoDistance = WarpedVolcanoDistance(point, volcanoCenter);
                    float apron = Smooth01((270f - volcanoDistance) / 88f);
                    ground += apron * 1.05f;

                    if (volcanoDistance < 188f)
                    {
                        Vector2 offset = point - volcanoCenter;
                        float angle = Mathf.Atan2(offset.y, offset.x);
                        float slope01 = Mathf.Clamp01(1f - volcanoDistance / 188f);
                        float coneCore = Mathf.Pow(slope01, 1.28f) * 62f;
                        float rim = Pulse(volcanoDistance, 43f, 12f) * 8.5f;
                        float craterCut = Smooth01((35f - volcanoDistance) / 35f) * 23f;
                        float ridge = Mathf.Sin(angle * 9.0f + Mathf.PerlinNoise(x * 0.05f, z * 0.05f) * 4.5f) * 3.5f * slope01;
                        float rockNoise = (Mathf.PerlinNoise(x * 0.085f + 45.3f, z * 0.085f + 19.2f) - 0.5f) * 5.2f * (0.35f + slope01);
                        float cone = waterLevel + 4.4f + coneCore + rim - craterCut + ridge + rockNoise;
                        float coneBlend = Smooth01((196f - volcanoDistance) / 42f);
                        ground = Mathf.Lerp(ground, cone, coneBlend);
                    }

                    SetHeight(x, z, Mathf.Clamp(ground, seaFloorHeight, 78f));
                }
            }

            for (int z = 0; z < QuadResolution; z++)
            {
                for (int x = 0; x < QuadResolution; x++)
                {
                    float localX = -half + (x + 0.5f) * spacing;
                    float localZ = -half + (z + 0.5f) * spacing;
                    Vector2 point = new Vector2(localX, localZ);
                    float land = ReferenceIslandMask(point);
                    float height = AverageQuadHeight(x, z);
                    float volcanoDistance = WarpedVolcanoDistance(point, volcanoCenter);
                    float noise = Mathf.PerlinNoise(localX * 0.027f + 6.5f, localZ * 0.027f + 31.1f);

                    int layer;
                    if (land < 0.24f || height <= waterLevel + 0.65f)
                    {
                        layer = 1;
                    }
                    else if (volcanoDistance < 205f || height > waterLevel + 11.5f)
                    {
                        layer = 2;
                    }
                    else if (volcanoDistance < 268f)
                    {
                        layer = noise > 0.72f ? 3 : 2;
                    }
                    else if (height <= waterLevel + 2.7f || land < 0.48f)
                    {
                        layer = 0;
                    }
                    else if (volcanoDistance < 430f && noise > 0.22f)
                    {
                        layer = 3;
                    }
                    else
                    {
                        layer = 0;
                    }

                    SetPaintLayer(x, z, layer);
                }
            }

            SmoothRecoveredIslandSurface(volcanoCenter);
            RebuildMesh();
        }

        public void GenerateUltimateStampVolcanoIsland(Texture2D volcanoStamp)
        {
            EnsureData();

            float half = size * 0.5f;
            float spacing = size / QuadResolution;
            float islandRadius = size * 0.345f;
            float mountainRadius = size * 0.292f;
            Vector2 volcanoCenter = new Vector2(-12f, 18f);
            Vector2 stampRange = GetStampRange(volcanoStamp);

            for (int z = 0; z < vertexResolution; z++)
            {
                for (int x = 0; x < vertexResolution; x++)
                {
                    float localX = -half + x * spacing;
                    float localZ = -half + z * spacing;
                    Vector2 point = new Vector2(localX, localZ);
                    Vector2 offset = point - volcanoCenter;
                    float angle = Mathf.Atan2(offset.y, offset.x);
                    float angularWarp = 1f
                        + Mathf.Sin(angle * 2.15f + 0.3f) * 0.055f
                        + Mathf.Cos(angle * 5.1f - 0.8f) * 0.035f
                        + Mathf.Sin(angle * 9.3f + 1.7f) * 0.014f;
                    float stretchedX = offset.x / Mathf.Max(0.72f, angularWarp * 1.035f);
                    float stretchedZ = offset.y / Mathf.Max(0.72f, angularWarp * 0.965f);
                    float distance = Mathf.Sqrt(stretchedX * stretchedX + stretchedZ * stretchedZ);
                    float island01 = distance / Mathf.Max(1f, islandRadius);
                    float shoreMask = Smooth01(Mathf.InverseLerp(1.035f, 0.915f, island01));

                    if (shoreMask <= 0.001f)
                    {
                        SetHeight(x, z, seaFloorHeight);
                        continue;
                    }

                    float broadNoise = Mathf.PerlinNoise(localX * 0.0105f + 13.4f, localZ * 0.0105f + 37.9f) - 0.5f;
                    float beachNoise = Mathf.PerlinNoise(localX * 0.036f + 4.7f, localZ * 0.036f + 81.3f) - 0.5f;
                    float inland = Smooth01(Mathf.InverseLerp(0.98f, 0.72f, island01));
                    float ground = Mathf.Lerp(
                        seaFloorHeight,
                        waterLevel + 0.55f + inland * 4.4f + broadNoise * 0.9f + beachNoise * 0.24f,
                        shoreMask);

                    float mountain01 = distance / Mathf.Max(1f, mountainRadius);
                    if (mountain01 < 1.08f)
                    {
                        float stamp = SampleStampNormalized(volcanoStamp, offset, mountainRadius, stampRange);
                        stamp = Smooth01(Mathf.InverseLerp(0.035f, 0.94f, stamp));

                        float outerFade = Smooth01(Mathf.InverseLerp(1.065f, 0.83f, mountain01));
                        float broadCone = Mathf.Pow(Mathf.Clamp01(1f - mountain01), 1.12f);
                        float rim = Pulse(mountain01, 0.245f, 0.075f);
                        float procedural = Mathf.Clamp01(broadCone * 0.92f + rim * 0.52f);
                        float stampSupported = Mathf.Max(procedural * 0.88f, stamp);
                        float combined = Mathf.Lerp(procedural, stampSupported, volcanoStamp != null ? 0.58f : 0f);
                        combined *= outerFade;

                        float slopeMask = Smooth01(Mathf.InverseLerp(0.25f, 0.43f, mountain01))
                            * Smooth01(Mathf.InverseLerp(0.97f, 0.72f, mountain01));
                        float flowNoise = Mathf.PerlinNoise(
                            Mathf.Cos(angle) * 1.7f + mountain01 * 2.8f + 18.2f,
                            Mathf.Sin(angle) * 1.7f + mountain01 * 3.2f + 44.7f);
                        float flowPhase = angle * 29f
                            + Mathf.Sin(angle * 7f + 0.8f) * 1.35f
                            + flowNoise * 3.4f
                            + mountain01 * 3.1f;
                        float primaryChannel = Mathf.Pow(Mathf.Clamp01(Mathf.Sin(flowPhase) * 0.5f + 0.5f), 7.5f);
                        float secondaryChannel = Mathf.Pow(
                            Mathf.Clamp01(Mathf.Sin(angle * 47f - mountain01 * 5.2f + flowNoise * 2.1f) * 0.5f + 0.5f),
                            10f);
                        float erosionCut = (primaryChannel * 10.5f + secondaryChannel * 4.2f) * slopeMask;
                        float ridgeDetail = (Mathf.PerlinNoise(x * 0.074f + 52.1f, z * 0.074f + 11.8f) - 0.5f)
                            * Mathf.Lerp(1.2f, 6.2f, slopeMask);

                        float volcanoHeight = waterLevel + 5.2f + combined * 152f + rim * 11.5f - erosionCut + ridgeDetail;

                        float craterMask = Smooth01(Mathf.InverseLerp(0.225f, 0.155f, mountain01));
                        if (craterMask > 0f)
                        {
                            float craterFloorNoise = (Mathf.PerlinNoise(x * 0.052f + 7.3f, z * 0.052f + 61.4f) - 0.5f) * 4.5f;
                            float craterFloor = waterLevel + 48f + craterFloorNoise;
                            volcanoHeight = Mathf.Lerp(volcanoHeight, craterFloor, craterMask);
                        }

                        float mountainBlend = Smooth01(Mathf.InverseLerp(1.075f, 0.91f, mountain01));
                        ground = Mathf.Lerp(ground, volcanoHeight, mountainBlend);
                    }

                    SetHeight(x, z, Mathf.Clamp(ground, seaFloorHeight, 172f));
                }
            }

            for (int z = 0; z < QuadResolution; z++)
            {
                for (int x = 0; x < QuadResolution; x++)
                {
                    float localX = -half + (x + 0.5f) * spacing;
                    float localZ = -half + (z + 0.5f) * spacing;
                    Vector2 offset = new Vector2(localX, localZ) - volcanoCenter;
                    float angle = Mathf.Atan2(offset.y, offset.x);
                    float angularWarp = 1f
                        + Mathf.Sin(angle * 2.15f + 0.3f) * 0.055f
                        + Mathf.Cos(angle * 5.1f - 0.8f) * 0.035f;
                    float distance = new Vector2(offset.x / (angularWarp * 1.035f), offset.y / (angularWarp * 0.965f)).magnitude;
                    float mountain01 = distance / Mathf.Max(1f, mountainRadius);
                    float height = AverageQuadHeight(x, z);
                    float ashNoise = Mathf.PerlinNoise(localX * 0.031f + 26.8f, localZ * 0.031f + 5.6f);

                    int layer;
                    if (height <= waterLevel + 0.75f)
                    {
                        layer = 1;
                    }
                    else if (height <= waterLevel + 4.2f)
                    {
                        layer = 0;
                    }
                    else if (mountain01 < 0.155f)
                    {
                        layer = 1;
                    }
                    else if (mountain01 < 0.42f || height >= waterLevel + 76f)
                    {
                        layer = 2;
                    }
                    else if (mountain01 < 1.02f)
                    {
                        layer = ashNoise > 0.43f ? 2 : 3;
                    }
                    else
                    {
                        layer = ashNoise > 0.66f ? 3 : 0;
                    }

                    SetPaintLayer(x, z, layer);
                }
            }

            SmoothVolcanoSkirt(volcanoCenter, mountainRadius * 1.02f, 0.018f);
            RebuildMesh();
        }

        public void GenerateBrightLargeSurvivalIsland()
        {
            EnsureData();

            float half = size * 0.5f;
            float spacing = size / QuadResolution;
            for (int z = 0; z < vertexResolution; z++)
            {
                for (int x = 0; x < vertexResolution; x++)
                {
                    float localX = -half + x * spacing;
                    float localZ = -half + z * spacing;
                    Vector2 point = new Vector2(localX, localZ);

                    float land = EllipseMask(point, new Vector2(-20f, 20f), 650f, 535f, -4f);
                    land = Mathf.Max(land, EllipseMask(point, new Vector2(-520f, 35f), 190f, 230f, -18f));
                    land = Mathf.Max(land, EllipseMask(point, new Vector2(475f, 145f), 210f, 255f, 12f));
                    land = Mathf.Max(land, EllipseMask(point, new Vector2(330f, -365f), 225f, 155f, -12f));

                    float southCove = EllipseMask(point, new Vector2(-30f, -535f), 245f, 175f, 2f);
                    float westCove = EllipseMask(point, new Vector2(-610f, -210f), 145f, 160f, -18f);
                    land = Mathf.Clamp01(land - southCove * 0.84f - westCove * 0.54f);

                    float coastNoise = Mathf.PerlinNoise(localX * 0.012f + 11.4f, localZ * 0.012f + 73.8f) - 0.5f;
                    land = Mathf.Clamp01(land + coastNoise * 0.12f);
                    float shore = Smooth01(Mathf.InverseLerp(0.06f, 0.46f, land));
                    float inland = Smooth01(Mathf.InverseLerp(0.40f, 0.86f, land));

                    if (shore <= 0.001f)
                    {
                        SetHeight(x, z, seaFloorHeight);
                        continue;
                    }

                    float broadNoise = Mathf.PerlinNoise(localX * 0.0065f + 18.1f, localZ * 0.0065f + 41.7f) - 0.5f;
                    float detailNoise = Mathf.PerlinNoise(localX * 0.022f + 5.2f, localZ * 0.022f + 92.3f) - 0.5f;
                    float northHighland = Mathf.Exp(
                        -Mathf.Pow((localX + 95f) / 350f, 2f)
                        -Mathf.Pow((localZ - 205f) / 275f, 2f)) * 102f;
                    float eastRidge = Mathf.Exp(
                        -Mathf.Pow((localX - 360f) / 235f, 2f)
                        -Mathf.Pow((localZ - 100f) / 330f, 2f)) * 72f;
                    float westHill = Mathf.Exp(
                        -Mathf.Pow((localX + 420f) / 245f, 2f)
                        -Mathf.Pow((localZ + 45f) / 290f, 2f)) * 42f;
                    float rollingGround = inland * (5.5f + broadNoise * 7f + detailNoise * 2.2f);
                    float target = waterLevel + 0.45f + rollingGround
                        + northHighland * inland
                        + eastRidge * inland
                        + westHill * inland;
                    float ground = Mathf.Lerp(seaFloorHeight, target, shore);

                    float meadow = EllipseMask(point, new Vector2(-145f, -125f), 245f, 175f, -8f);
                    float meadowHeight = waterLevel + 8.5f + broadNoise * 1.3f;
                    ground = Mathf.Lerp(ground, meadowHeight, meadow * inland * 0.86f);

                    float southBeach = EllipseMask(point, new Vector2(-85f, -400f), 315f, 125f, 2f);
                    float westBeach = EllipseMask(point, new Vector2(-500f, -205f), 165f, 155f, -16f);
                    float southeastBeach = EllipseMask(point, new Vector2(305f, -390f), 220f, 105f, -10f);
                    float beachTerrace = Mathf.Max(southBeach, Mathf.Max(westBeach, southeastBeach));
                    float beachHeight = waterLevel + 3.4f + broadNoise * 0.75f + detailNoise * 0.35f;
                    float beachLand = Smooth01(Mathf.InverseLerp(0.05f, 0.28f, shore));
                    ground = Mathf.Lerp(ground, beachHeight, beachTerrace * beachLand * 0.96f);

                    float northPass = Mathf.Exp(-Mathf.Pow((localX + 35f) / 82f, 2f))
                        * Smooth01(Mathf.InverseLerp(-120f, 360f, localZ));
                    ground = Mathf.Lerp(ground, Mathf.Min(ground, waterLevel + 16f + detailNoise), northPass * inland * 0.62f);

                    float lagoon = EllipseMask(point, new Vector2(245f, -120f), 105f, 78f, 14f);
                    float lagoonFloor = waterLevel - 0.7f + detailNoise * 0.12f;
                    ground = Mathf.Lerp(ground, lagoonFloor, lagoon * 0.96f);

                    SetHeight(x, z, Mathf.Clamp(ground, seaFloorHeight, 132f));
                }
            }

            for (int z = 0; z < QuadResolution; z++)
            {
                for (int x = 0; x < QuadResolution; x++)
                {
                    float height = AverageQuadHeight(x, z);
                    float h0 = GetHeight(x, z);
                    float h1 = GetHeight(x + 1, z);
                    float h2 = GetHeight(x, z + 1);
                    float h3 = GetHeight(x + 1, z + 1);
                    float slope = Mathf.Max(
                        Mathf.Abs(h1 - h0),
                        Mathf.Max(Mathf.Abs(h2 - h0), Mathf.Abs(h3 - h0)));
                    float localX = -half + (x + 0.5f) * spacing;
                    float localZ = -half + (z + 0.5f) * spacing;
                    float biomeNoise = Mathf.PerlinNoise(localX * 0.025f + 27.4f, localZ * 0.025f + 8.6f);
                    Vector2 localPoint = new Vector2(localX, localZ);
                    float meadow = EllipseMask(localPoint, new Vector2(-145f, -125f), 245f, 175f, -8f);
                    float southBeach = EllipseMask(localPoint, new Vector2(-85f, -400f), 315f, 125f, 2f);
                    float westBeach = EllipseMask(localPoint, new Vector2(-500f, -205f), 165f, 155f, -16f);
                    float southeastBeach = EllipseMask(localPoint, new Vector2(305f, -390f), 220f, 105f, -10f);
                    float beachTerrace = Mathf.Max(southBeach, Mathf.Max(westBeach, southeastBeach));
                    Vector2 lagoonOffset = localPoint - new Vector2(245f, -120f);
                    float lagoonDistance = new Vector2(lagoonOffset.x / 120f, lagoonOffset.y / 92f).magnitude;
                    bool lagoonShore = lagoonDistance >= 0.72f && lagoonDistance <= 1.28f;
                    float southBeachEdge = -245f
                        + Mathf.Sin(localX * 0.0105f) * 32f
                        + Mathf.Sin(localX * 0.025f + 1.4f) * 14f;
                    bool guaranteedSouthBeach = localZ < southBeachEdge
                        && localZ > -560f
                        && localX > -620f
                        && localX < 620f;

                    int layer;
                    if (height <= waterLevel + 0.55f)
                    {
                        layer = 1;
                    }
                    else if (guaranteedSouthBeach
                        || ((beachTerrace > 0.075f || lagoonShore)
                        && height <= waterLevel + 16f)
                    )
                    {
                        layer = 0;
                    }
                    else if (meadow > 0.48f && height > waterLevel + 4f)
                    {
                        layer = 3;
                    }
                    else if (height <= waterLevel + 9.5f)
                    {
                        layer = 0;
                    }
                    else if (slope > 3.4f || height > waterLevel + 58f)
                    {
                        layer = 2;
                    }
                    else
                    {
                        layer = biomeNoise > 0.13f ? 3 : 0;
                    }

                    SetPaintLayer(x, z, layer);
                }
            }

            RebuildMesh();
        }

        public void ApplyUltimateStampCalderaVolcano(
            Texture2D volcanoStamp,
            Vector2 volcanoCenter,
            float footprintRadius,
            float summitHeight)
        {
            EnsureData();

            footprintRadius = Mathf.Clamp(footprintRadius, 220f, size * 0.32f);
            summitHeight = Mathf.Clamp(summitHeight, waterLevel + 105f, 178f);
            float transitionRadius = footprintRadius * 1.17f;
            float craterRadius = footprintRadius * 0.20f;
            float rimRadius = footprintRadius * 0.255f;
            float craterFloorHeight = summitHeight - 88f;
            float half = size * 0.5f;
            float spacing = size / QuadResolution;
            Vector2 stampRange = GetStampRange(volcanoStamp);

            for (int z = 0; z < vertexResolution; z++)
            {
                for (int x = 0; x < vertexResolution; x++)
                {
                    float localX = -half + x * spacing;
                    float localZ = -half + z * spacing;
                    Vector2 point = new Vector2(localX, localZ);
                    Vector2 offset = point - volcanoCenter;
                    float angle = Mathf.Atan2(offset.y, offset.x);
                    float angularWarp = 1f
                        + Mathf.Sin(angle * 2.35f + 0.45f) * 0.075f
                        + Mathf.Cos(angle * 5.4f - 0.9f) * 0.045f
                        + Mathf.Sin(angle * 9.2f + 1.8f) * 0.018f;
                    float directionalStretch = 1f + Mathf.Cos(angle - 0.72f) * 0.055f;
                    float warpedDistance = new Vector2(
                        offset.x / Mathf.Max(0.74f, angularWarp * directionalStretch * 1.025f),
                        offset.y / Mathf.Max(0.74f, angularWarp / directionalStretch * 0.975f)).magnitude;
                    float transition01 = warpedDistance / transitionRadius;
                    if (transition01 >= 1f)
                    {
                        continue;
                    }

                    float current = GetHeight(x, z);
                    float mountain01 = warpedDistance / footprintRadius;
                    float edgeBlend = Smooth01(Mathf.InverseLerp(1f, 0.84f, mountain01));
                    float stamp = SampleStampNormalized(volcanoStamp, offset, footprintRadius, stampRange);
                    stamp = Smooth01(Mathf.InverseLerp(0.025f, 0.92f, stamp));

                    float broadCone = Mathf.Pow(Mathf.Clamp01(1f - mountain01), 0.94f);
                    float upperCone = Mathf.Pow(Mathf.Clamp01(1f - mountain01 * 0.92f), 1.7f);
                    float stampRelief = Mathf.Pow(stamp, 0.92f);
                    float mountainShape = Mathf.Clamp01(
                        broadCone * 0.66f
                        + upperCone * 0.22f
                        + stampRelief * 0.26f);
                    mountainShape *= edgeBlend;

                    float target = Mathf.Lerp(waterLevel + 8f, summitHeight - 13f, mountainShape);
                    float slopeMask = Smooth01(Mathf.InverseLerp(0.18f, 0.38f, mountain01))
                        * Smooth01(Mathf.InverseLerp(0.98f, 0.68f, mountain01));
                    float flowNoise = Mathf.PerlinNoise(
                        Mathf.Cos(angle) * 1.45f + mountain01 * 2.7f + 21.4f,
                        Mathf.Sin(angle) * 1.45f + mountain01 * 3.2f + 57.2f);
                    float sectorNoise = Mathf.PerlinNoise(
                        Mathf.Cos(angle) * 0.82f + 71.3f,
                        Mathf.Sin(angle) * 0.82f + 13.7f);
                    float variableFrequency = Mathf.Lerp(12.5f, 21f, sectorNoise);
                    float channelPhase = angle * variableFrequency
                        + Mathf.Sin(angle * 3.2f + 0.8f) * 2.15f
                        + Mathf.Sin(angle * 7.1f - 1.4f) * 0.72f
                        + flowNoise * 4.6f
                        + mountain01 * Mathf.Lerp(1.8f, 5.4f, sectorNoise);
                    float primaryChannel = Mathf.Pow(
                        Mathf.Clamp01(Mathf.Sin(channelPhase) * 0.5f + 0.5f),
                        Mathf.Lerp(4.4f, 7.8f, sectorNoise));
                    float secondaryChannel = Mathf.Pow(
                        Mathf.Clamp01(Mathf.Sin(angle * 8.5f - mountain01 * 3.8f + flowNoise * 3.2f) * 0.5f + 0.5f),
                        5.8f);
                    float channelStrength = Mathf.Lerp(5.5f, 15.5f, sectorNoise);
                    float erosionCut = (primaryChannel * channelStrength + secondaryChannel * 3.6f) * slopeMask;
                    float ridgeNoise = (Mathf.PerlinNoise(x * 0.068f + 42.8f, z * 0.068f + 17.6f) - 0.5f)
                        * Mathf.Lerp(1.2f, 5.8f, slopeMask);
                    target += ridgeNoise - erosionCut;

                    Vector2 craterOffset = offset - new Vector2(-7f, 5f);
                    float craterAngle = Mathf.Atan2(craterOffset.y, craterOffset.x);
                    float craterWarp = 1f
                        + Mathf.Sin(craterAngle * 2.7f + 0.4f) * 0.085f
                        + Mathf.Sin(craterAngle * 5.3f - 1.1f) * 0.035f;
                    float craterDistance = craterOffset.magnitude / Mathf.Max(0.78f, craterWarp);
                    float rimBoost = Mathf.Exp(
                        -Mathf.Pow((craterDistance - rimRadius) / Mathf.Max(1f, footprintRadius * 0.046f), 2f));
                    float rimVariation = 0.78f
                        + Mathf.Sin(craterAngle * 3.1f + 0.5f) * 0.16f
                        + (Mathf.PerlinNoise(Mathf.Cos(craterAngle) + 37f, Mathf.Sin(craterAngle) + 82f) - 0.5f) * 0.34f;
                    float collapsedArcDegrees = Mathf.Abs(Mathf.DeltaAngle(craterAngle * Mathf.Rad2Deg, 214f));
                    float collapsedArc = 1f - Smooth01(Mathf.InverseLerp(20f, 58f, collapsedArcDegrees));
                    target += rimBoost * (22f * rimVariation - collapsedArc * 14f);

                    float craterMask = Smooth01(Mathf.InverseLerp(craterRadius * 1.22f, craterRadius * 0.70f, craterDistance));
                    if (craterMask > 0f)
                    {
                        float floorNoise = (Mathf.PerlinNoise(x * 0.052f + 9.1f, z * 0.052f + 63.8f) - 0.5f) * 5f;
                        float craterFloor = craterFloorHeight + floorNoise;
                        target = Mathf.Lerp(target, craterFloor, craterMask);
                    }

                    float transitionBlend = transition01 <= 0.84f
                        ? 1f
                        : Smooth01(Mathf.InverseLerp(1f, 0.84f, transition01));
                    float next = Mathf.Lerp(current, target, transitionBlend);
                    SetHeight(x, z, Mathf.Clamp(next, seaFloorHeight, 178f));
                }
            }

            for (int z = 0; z < QuadResolution; z++)
            {
                for (int x = 0; x < QuadResolution; x++)
                {
                    float localX = -half + (x + 0.5f) * spacing;
                    float localZ = -half + (z + 0.5f) * spacing;
                    Vector2 offset = new Vector2(localX, localZ) - volcanoCenter;
                    float angle = Mathf.Atan2(offset.y, offset.x);
                    float warp = 1f
                        + Mathf.Sin(angle * 2.35f + 0.45f) * 0.075f
                        + Mathf.Cos(angle * 5.4f - 0.9f) * 0.045f;
                    float distance = offset.magnitude / Mathf.Max(0.76f, warp);
                    float d = distance / footprintRadius;
                    if (d > 1.12f)
                    {
                        continue;
                    }

                    float height = AverageQuadHeight(x, z);
                    float h0 = GetHeight(x, z);
                    float h1 = GetHeight(x + 1, z);
                    float h2 = GetHeight(x, z + 1);
                    float h3 = GetHeight(x + 1, z + 1);
                    float slope = Mathf.Max(
                        Mathf.Abs(h1 - h0),
                        Mathf.Max(Mathf.Abs(h2 - h0), Mathf.Abs(h3 - h0)));
                    float noise = Mathf.PerlinNoise(localX * 0.027f + 31.2f, localZ * 0.027f + 6.8f);

                    if (d < 0.84f || height > waterLevel + 118f)
                    {
                        SetPaintLayer(x, z, 2);
                    }
                    else if (d < 1.04f)
                    {
                        SetPaintLayer(x, z, slope > 4.4f && noise > 0.68f ? 2 : 3);
                    }
                    else if (d < 1.08f && height > waterLevel + 9f)
                    {
                        SetPaintLayer(x, z, slope > 3.1f ? 2 : 3);
                    }
                }
            }

            SmoothVolcanoSkirt(volcanoCenter, transitionRadius, 0.032f);
            RebuildMesh();
        }

        public void ApplyStylizedAsymmetricVolcano(
            Vector2 volcanoCenter,
            float baseRadius,
            float summitHeight)
        {
            EnsureData();

            baseRadius = Mathf.Clamp(baseRadius, 250f, size * 0.31f);
            summitHeight = Mathf.Clamp(summitHeight, waterLevel + 118f, 178f);
            float half = size * 0.5f;
            float spacing = size / QuadResolution;
            Vector2 craterCenter = volcanoCenter + new Vector2(-12f, 7f);
            float craterRotation = 17f * Mathf.Deg2Rad;
            float cosCrater = Mathf.Cos(craterRotation);
            float sinCrater = Mathf.Sin(craterRotation);
            float[] ridgeAngles = { -151f, -74f, 19f, 126f };
            float[] ridgeWidths = { 24f, 15f, 20f, 28f };
            float[] ridgeHeights = { 28f, 18f, 24f, 14f };

            for (int z = 0; z < vertexResolution; z++)
            {
                for (int x = 0; x < vertexResolution; x++)
                {
                    float localX = -half + x * spacing;
                    float localZ = -half + z * spacing;
                    Vector2 point = new Vector2(localX, localZ);
                    Vector2 offset = point - volcanoCenter;
                    float angle = Mathf.Atan2(offset.y, offset.x);
                    float angleDegrees = angle * Mathf.Rad2Deg;

                    float broadLeft = Mathf.Cos(angle - Mathf.PI);
                    float southwestExtension = Mathf.Exp(
                        -Mathf.Pow(Mathf.Abs(Mathf.DeltaAngle(angleDegrees, -142f)) / 31f, 2f));
                    float northeastInlet = Mathf.Exp(
                        -Mathf.Pow(Mathf.Abs(Mathf.DeltaAngle(angleDegrees, 48f)) / 24f, 2f));
                    float southInlet = Mathf.Exp(
                        -Mathf.Pow(Mathf.Abs(Mathf.DeltaAngle(angleDegrees, -82f)) / 19f, 2f));
                    float footprintWarp = 1f
                        + broadLeft * 0.24f
                        + Mathf.Cos(angle * 2f + 0.55f) * 0.09f
                        + Mathf.Sin(angle * 3f - 0.8f) * 0.055f
                        + southwestExtension * 0.13f
                        - northeastInlet * 0.19f
                        - southInlet * 0.11f;
                    float directionalRadius = baseRadius * Mathf.Clamp(footprintWarp, 0.61f, 1.38f);
                    float normalizedDistance = offset.magnitude / directionalRadius;
                    if (normalizedDistance > 1.14f)
                    {
                        continue;
                    }

                    float current = GetHeight(x, z);
                    float steepSide = Mathf.Clamp01(Mathf.Cos(angle) * 0.5f + 0.5f);
                    float slopeExponent = Mathf.Lerp(0.68f, 1.58f, steepSide);
                    float radialShape = Mathf.Pow(
                        Mathf.Clamp01(1f - normalizedDistance),
                        slopeExponent);
                    float peakVariation = Mathf.Cos(angle + 2.35f) * 13f
                        + Mathf.Cos(angle * 2f - 0.35f) * 7f;
                    float baseHeight = waterLevel + 9.5f;
                    float target = Mathf.Lerp(
                        baseHeight,
                        summitHeight + peakVariation,
                        radialShape);

                    float ridgeWindow = Smooth01(Mathf.InverseLerp(0.12f, 0.30f, normalizedDistance))
                        * Smooth01(Mathf.InverseLerp(0.93f, 0.58f, normalizedDistance));
                    for (int ridge = 0; ridge < ridgeAngles.Length; ridge++)
                    {
                        float delta = Mathf.Abs(Mathf.DeltaAngle(angleDegrees, ridgeAngles[ridge]));
                        float angularWeight = Mathf.Exp(
                            -Mathf.Pow(delta / ridgeWidths[ridge], 2f));
                        float radialBias = Mathf.Lerp(
                            1.18f,
                            0.72f,
                            Mathf.Abs(normalizedDistance - (0.42f + ridge * 0.055f)));
                        target += angularWeight * ridgeHeights[ridge] * ridgeWindow * radialBias;
                    }

                    float broadChannelA = Mathf.Exp(
                        -Mathf.Pow(Mathf.Abs(Mathf.DeltaAngle(angleDegrees, -28f)) / 13f, 2f));
                    float broadChannelB = Mathf.Exp(
                        -Mathf.Pow(Mathf.Abs(Mathf.DeltaAngle(angleDegrees, 82f)) / 18f, 2f));
                    target -= (broadChannelA * 14f + broadChannelB * 8f) * ridgeWindow;

                    Vector2 craterOffset = point - craterCenter;
                    float craterX = craterOffset.x * cosCrater + craterOffset.y * sinCrater;
                    float craterZ = -craterOffset.x * sinCrater + craterOffset.y * cosCrater;
                    float craterAngle = Mathf.Atan2(craterZ / 44f, craterX / 61f);
                    float craterAngleDegrees = craterAngle * Mathf.Rad2Deg;
                    float craterWarp = 1f
                        + Mathf.Cos(craterAngle * 2f + 0.7f) * 0.08f
                        + Mathf.Sin(craterAngle * 3f - 0.45f) * 0.045f;
                    float craterDistance = Mathf.Sqrt(
                        craterX * craterX / (61f * 61f)
                        + craterZ * craterZ / (44f * 44f)) / craterWarp;

                    if (craterDistance < 1.34f)
                    {
                        float collapsedDelta = Mathf.Abs(Mathf.DeltaAngle(craterAngleDegrees, -48f));
                        float collapsedSide = 1f - Smooth01(Mathf.InverseLerp(18f, 58f, collapsedDelta));
                        float rimHeightVariation = 12f
                            + Mathf.Cos(craterAngle - 2.2f) * 10f
                            + Mathf.Cos(craterAngle * 2f + 0.4f) * 4f
                            - collapsedSide * 23f;
                        float rimEnvelope = Mathf.Exp(
                            -Mathf.Pow((craterDistance - 1.04f) / 0.19f, 2f));
                        target += rimEnvelope * rimHeightVariation;

                        if (craterDistance < 1.03f)
                        {
                            int face = Mathf.FloorToInt(
                                Mathf.Repeat((craterAngle + Mathf.PI) / (Mathf.PI * 2f), 1f) * 5f);
                            float faceOffset;
                            switch (face)
                            {
                                case 0: faceOffset = -3.5f; break;
                                case 1: faceOffset = 4.5f; break;
                                case 2: faceOffset = 1f; break;
                                case 3: faceOffset = 6.5f; break;
                                default: faceOffset = -1.5f; break;
                            }
                            float floorHeight = waterLevel + 73f
                                + craterX * 0.055f
                                - craterZ * 0.075f
                                + Mathf.Cos(craterAngle * 2f) * 2.2f;
                            float wallBlend = Smooth01(Mathf.InverseLerp(0.38f, 1.02f, craterDistance));
                            float wallTarget = Mathf.Lerp(
                                floorHeight + faceOffset,
                                target,
                                wallBlend);
                            target = wallTarget;
                        }
                    }

                    float transitionBlend = normalizedDistance <= 0.79f
                        ? 1f
                        : Smooth01(Mathf.InverseLerp(1.12f, 0.79f, normalizedDistance));
                    SetHeight(
                        x,
                        z,
                        Mathf.Clamp(Mathf.Lerp(current, target, transitionBlend), seaFloorHeight, 178f));
                }
            }

            for (int z = 0; z < QuadResolution; z++)
            {
                for (int x = 0; x < QuadResolution; x++)
                {
                    float localX = -half + (x + 0.5f) * spacing;
                    float localZ = -half + (z + 0.5f) * spacing;
                    Vector2 offset = new Vector2(localX, localZ) - volcanoCenter;
                    float angle = Mathf.Atan2(offset.y, offset.x);
                    float angleDegrees = angle * Mathf.Rad2Deg;
                    float southwestExtension = Mathf.Exp(
                        -Mathf.Pow(Mathf.Abs(Mathf.DeltaAngle(angleDegrees, -142f)) / 31f, 2f));
                    float northeastInlet = Mathf.Exp(
                        -Mathf.Pow(Mathf.Abs(Mathf.DeltaAngle(angleDegrees, 48f)) / 24f, 2f));
                    float southInlet = Mathf.Exp(
                        -Mathf.Pow(Mathf.Abs(Mathf.DeltaAngle(angleDegrees, -82f)) / 19f, 2f));
                    float footprintWarp = 1f
                        + Mathf.Cos(angle - Mathf.PI) * 0.24f
                        + Mathf.Cos(angle * 2f + 0.55f) * 0.09f
                        + Mathf.Sin(angle * 3f - 0.8f) * 0.055f
                        + southwestExtension * 0.13f
                        - northeastInlet * 0.19f
                        - southInlet * 0.11f;
                    float normalizedDistance = offset.magnitude
                        / (baseRadius * Mathf.Clamp(footprintWarp, 0.61f, 1.38f));
                    if (offset.magnitude <= baseRadius * 1.18f)
                    {
                        SetPaintLayer(x, z, normalizedDistance < 0.86f ? 2 : 3);
                    }
                }
            }

            RebuildMesh();
        }

        public void ApplyStylizedConeLandmarkVolcano(Vector2 volcanoCenter, Vector2 craterCenter)
        {
            EnsureData();

            const float baseRadius = 345f;
            const float transitionRadius = 374f;
            const float baseHeight = 12f;
            const float coneRelief = 220f;
            const float craterAxisX = 34f;
            const float craterAxisZ = 29f;
            const float craterFloorHeight = 205f;
            float half = size * 0.5f;
            float spacing = size / QuadResolution;
            float[] ridgeAngles = { -148f, -76f, 8f, 83f, 157f };
            float[] ridgeWidths = { 23f, 17f, 20f, 16f, 25f };
            float[] ridgeHeights = { 15f, 11f, 17f, 13f, 10f };
            const float craterRotation = -8f * Mathf.Deg2Rad;
            float craterCos = Mathf.Cos(craterRotation);
            float craterSin = Mathf.Sin(craterRotation);

            for (int z = 0; z < vertexResolution; z++)
            {
                for (int x = 0; x < vertexResolution; x++)
                {
                    float localX = -half + x * spacing;
                    float localZ = -half + z * spacing;
                    Vector2 point = new Vector2(localX, localZ);
                    Vector2 offset = point - volcanoCenter;
                    float angle = Mathf.Atan2(offset.y, offset.x);
                    float angleDegrees = angle * Mathf.Rad2Deg;

                    // A restrained footprint warp keeps the cone stylized and readable while
                    // avoiding a mathematically perfect circular base.
                    float footprintWarp = 1f
                        + Mathf.Cos(angle - 2.55f) * 0.055f
                        + Mathf.Sin(angle * 2f + 0.35f) * 0.032f
                        + Mathf.Cos(angle * 3f - 0.7f) * 0.018f;
                    float warpedDistance = offset.magnitude / Mathf.Max(0.86f, footprintWarp);
                    if (warpedDistance >= transitionRadius)
                    {
                        continue;
                    }

                    float current = GetHeight(x, z);
                    float normalizedDistance = warpedDistance / baseRadius;
                    float inward = Mathf.Clamp01(1f - normalizedDistance);

                    // The linear term keeps the lower skirt broad and gentle. The powered term
                    // makes the middle/upper 40 percent accelerate upward into a narrow cone.
                    float coneProfile = inward * 0.30f
                        + Mathf.Pow(inward, 1.62f) * 0.70f;
                    float target = waterLevel + baseHeight + coneProfile * coneRelief;

                    float ridgeWindow = Smooth01(Mathf.InverseLerp(0.10f, 0.25f, normalizedDistance))
                        * Smooth01(Mathf.InverseLerp(0.88f, 0.56f, normalizedDistance));
                    for (int ridge = 0; ridge < ridgeAngles.Length; ridge++)
                    {
                        float angularDelta = Mathf.Abs(Mathf.DeltaAngle(angleDegrees, ridgeAngles[ridge]));
                        float angularWeight = Mathf.Exp(-Mathf.Pow(angularDelta / ridgeWidths[ridge], 2f));
                        float ridgeLength = 0.82f + 0.18f * Mathf.Cos(
                            (normalizedDistance - 0.42f - ridge * 0.025f) * Mathf.PI);
                        target += angularWeight * ridgeHeights[ridge] * ridgeWindow * ridgeLength;
                    }

                    // Two broad valleys separate the five hero ridges. These are large forms,
                    // not high-frequency erosion noise.
                    float valleyA = Mathf.Exp(-Mathf.Pow(
                        Mathf.Abs(Mathf.DeltaAngle(angleDegrees, -32f)) / 15f, 2f));
                    float valleyB = Mathf.Exp(-Mathf.Pow(
                        Mathf.Abs(Mathf.DeltaAngle(angleDegrees, 125f)) / 20f, 2f));
                    target -= (valleyA * 8f + valleyB * 5f) * ridgeWindow;

                    Vector2 craterOffset = point - craterCenter;
                    float craterX = craterOffset.x * craterCos + craterOffset.y * craterSin;
                    float craterZ = -craterOffset.x * craterSin + craterOffset.y * craterCos;
                    float craterAngle = Mathf.Atan2(craterZ / craterAxisZ, craterX / craterAxisX);
                    float craterAngleDegrees = craterAngle * Mathf.Rad2Deg;
                    float craterWarp = 1f
                        + Mathf.Cos(craterAngle * 2f + 0.55f) * 0.055f
                        + Mathf.Sin(craterAngle * 3f - 0.3f) * 0.03f;
                    float craterDistance = Mathf.Sqrt(
                        craterX * craterX / (craterAxisX * craterAxisX)
                        + craterZ * craterZ / (craterAxisZ * craterAxisZ))
                        / Mathf.Max(0.88f, craterWarp);

                    if (craterDistance < 1.32f)
                    {
                        float collapsedDelta = Mathf.Abs(Mathf.DeltaAngle(craterAngleDegrees, -146f));
                        float collapsedSide = 1f - Smooth01(Mathf.InverseLerp(17f, 52f, collapsedDelta));
                        float highRimSide = 1f - Smooth01(Mathf.InverseLerp(
                            22f,
                            76f,
                            Mathf.Abs(Mathf.DeltaAngle(craterAngleDegrees, 34f))));
                        float rimEnvelope = Mathf.Exp(-Mathf.Pow((craterDistance - 1.02f) / 0.22f, 2f));
                        float rimLift = 14f + highRimSide * 7f - collapsedSide * 12f;
                        target += rimEnvelope * rimLift;

                        if (craterDistance < 0.96f)
                        {
                            int wallFace = Mathf.FloorToInt(
                                Mathf.Repeat((craterAngle + Mathf.PI) / (Mathf.PI * 2f), 1f) * 5f);
                            float faceOffset = wallFace == 0 ? -2.5f
                                : wallFace == 1 ? 3.2f
                                : wallFace == 2 ? 1.2f
                                : wallFace == 3 ? 4.4f
                                : -1.4f;
                            float floor = waterLevel + craterFloorHeight
                                + craterX * 0.035f
                                - craterZ * 0.05f
                                + faceOffset;
                            float wallBlend = Smooth01(Mathf.InverseLerp(0.27f, 0.94f, craterDistance));
                            target = Mathf.Lerp(floor, target, wallBlend);
                        }
                    }

                    float transitionBlend = normalizedDistance <= 0.86f
                        ? 1f
                        : Smooth01(Mathf.InverseLerp(transitionRadius, baseRadius * 0.86f, warpedDistance));
                    float next = Mathf.Lerp(current, target, transitionBlend);
                    SetHeight(x, z, Mathf.Clamp(next, seaFloorHeight, 248f));
                }
            }

            RebuildMesh();
        }

        public void ApplyStylizedConeSurfaceRefinement(Vector2 volcanoCenter, Vector2 craterCenter)
        {
            EnsureData();

            float preservedPeak = float.NegativeInfinity;
            for (int i = 0; i < heights.Length; i++) preservedPeak = Mathf.Max(preservedPeak, heights[i]);

            const float baseRadius = 345f;
            const float editRadius = 338f;
            const float craterAxisX = 34f;
            const float craterAxisZ = 29f;
            const float craterRotation = -8f * Mathf.Deg2Rad;
            const float summitShiftMeters = 14f;
            float craterCos = Mathf.Cos(craterRotation);
            float craterSin = Mathf.Sin(craterRotation);
            float shiftAngle = 148f * Mathf.Deg2Rad;
            Vector2 shiftDirection = new Vector2(Mathf.Cos(shiftAngle), Mathf.Sin(shiftAngle));
            float[] ridgeAngles = { -158f, -101f, -39f, 24f, 92f, 161f };
            float[] ridgeWidths = { 25f, 18f, 22f, 16f, 28f, 20f };
            float[] ridgeHeights = { 9f, 7f, 11f, 6f, 9f, 8f };
            float[] ridgeLengths = { 286f, 228f, 304f, 205f, 268f, 244f };
            float[] valleyAngles = { -130f, -70f, -7f, 58f, 127f };
            float[] valleyWidths = { 24f, 19f, 21f, 22f, 25f };
            float[] valleyDepths = { 4f, 5f, 3.5f, 4.5f, 4f };
            float half = size * 0.5f;
            float spacing = size / QuadResolution;

            for (int z = 0; z < vertexResolution; z++)
            {
                for (int x = 0; x < vertexResolution; x++)
                {
                    float localX = -half + x * spacing;
                    float localZ = -half + z * spacing;
                    Vector2 point = new Vector2(localX, localZ);
                    Vector2 offset = point - volcanoCenter;
                    float distance = offset.magnitude;
                    if (distance >= editRadius) continue;

                    Vector2 craterOffset = point - craterCenter;
                    float craterX = craterOffset.x * craterCos + craterOffset.y * craterSin;
                    float craterZ = -craterOffset.x * craterSin + craterOffset.y * craterCos;
                    float craterDistance = Mathf.Sqrt(
                        craterX * craterX / (craterAxisX * craterAxisX)
                        + craterZ * craterZ / (craterAxisZ * craterAxisZ));
                    if (craterDistance <= 1.03f) continue;

                    float angle = Mathf.Atan2(offset.y, offset.x);
                    float angleDegrees = angle * Mathf.Rad2Deg;
                    float normalizedDistance = distance / baseRadius;
                    float outerFade = Smooth01(Mathf.InverseLerp(0.98f, 0.76f, normalizedDistance));
                    float innerFade = Smooth01(Mathf.InverseLerp(32f, 46f, distance));
                    float surfaceWindow = innerFade * outerFade;

                    // Raise the shoulder outside the locked crater so the summit flows into the
                    // upper cone instead of reading as a cylindrical cap.
                    float summitConnection = Mathf.Exp(-Mathf.Pow((distance - 66f) / 58f, 2f))
                        * 15f * outerFade;

                    // A 14m directional mass shift (roughly four percent of the base radius)
                    // makes one side longer/gentler and the opposite side shorter/steeper.
                    float directional = distance > 0.001f
                        ? Vector2.Dot(offset / distance, shiftDirection)
                        : 0f;
                    float shiftWindow = Smooth01(Mathf.InverseLerp(58f, 112f, distance))
                        * Smooth01(Mathf.InverseLerp(305f, 205f, distance));
                    float summitMassShift = directional
                        * (summitShiftMeters / baseRadius)
                        * 65f
                        * shiftWindow;

                    float ridgeDelta = 0f;
                    for (int ridge = 0; ridge < ridgeAngles.Length; ridge++)
                    {
                        float angularDelta = Mathf.Abs(Mathf.DeltaAngle(angleDegrees, ridgeAngles[ridge]));
                        float angularWeight = Mathf.Exp(-Mathf.Pow(angularDelta / ridgeWidths[ridge], 2f));
                        float lengthFade = Smooth01(Mathf.InverseLerp(
                            ridgeLengths[ridge],
                            ridgeLengths[ridge] - 55f,
                            distance));
                        float broadLongitudinal = 0.76f
                            + Mathf.Cos(Mathf.Clamp01(distance / ridgeLengths[ridge]) * Mathf.PI) * 0.24f;
                        ridgeDelta += angularWeight
                            * ridgeHeights[ridge]
                            * surfaceWindow
                            * lengthFade
                            * broadLongitudinal;
                    }

                    float valleyDelta = 0f;
                    for (int valley = 0; valley < valleyAngles.Length; valley++)
                    {
                        float angularDelta = Mathf.Abs(Mathf.DeltaAngle(angleDegrees, valleyAngles[valley]));
                        float angularWeight = Mathf.Exp(-Mathf.Pow(angularDelta / valleyWidths[valley], 2f));
                        valleyDelta += angularWeight * valleyDepths[valley] * surfaceWindow;
                    }

                    float delta = summitConnection + summitMassShift + ridgeDelta - valleyDelta;
                    delta = Mathf.Clamp(delta, -9f, 16f);
                    float current = GetHeight(x, z);
                    SetHeight(x, z, Mathf.Clamp(current + delta, seaFloorHeight, preservedPeak));
                }
            }

            RebuildMesh();
        }

        public void ApplyFinalVolcanoPolish(Vector2 volcanoCenter, Vector2 craterCenter)
        {
            EnsureData();

            float preservedPeak = float.NegativeInfinity;
            for (int i = 0; i < heights.Length; i++) preservedPeak = Mathf.Max(preservedPeak, heights[i]);
            float targetPeak = preservedPeak * 1.06f;

            const float editRadius = 320f;
            const float craterAxisX = 34f;
            const float craterAxisZ = 29f;
            const float craterRotation = -8f * Mathf.Deg2Rad;
            float craterCos = Mathf.Cos(craterRotation);
            float craterSin = Mathf.Sin(craterRotation);
            float[] ridgeAngles = { -151f, -79f, -12f, 61f, 139f };
            float[] ridgeWidths = { 18f, 14f, 17f, 15f, 20f };
            float[] ridgeHeights = { 12.5f, 10f, 13.5f, 10.8f, 12f };
            float[] ridgeLengths = { 286f, 224f, 302f, 252f, 274f };
            float[] valleyAngles = { -118f, -47f, 27f, 102f, 177f };
            float[] valleyWidths = { 17f, 14f, 18f, 15f, 19f };
            float[] valleyDepths = { 6.8f, 7.4f, 6.1f, 7f, 6.5f };
            float half = size * 0.5f;
            float spacing = size / QuadResolution;

            for (int z = 0; z < vertexResolution; z++)
            {
                for (int x = 0; x < vertexResolution; x++)
                {
                    float localX = -half + x * spacing;
                    float localZ = -half + z * spacing;
                    Vector2 point = new Vector2(localX, localZ);
                    Vector2 offset = point - volcanoCenter;
                    float distance = offset.magnitude;
                    if (distance >= editRadius) continue;

                    float angle = Mathf.Atan2(offset.y, offset.x);
                    float angleDegrees = angle * Mathf.Rad2Deg;
                    float outerFade = Smooth01(Mathf.InverseLerp(editRadius, 272f, distance));
                    float ridgeInnerFade = Smooth01(Mathf.InverseLerp(48f, 76f, distance));

                    Vector2 craterOffset = point - craterCenter;
                    float craterX = craterOffset.x * craterCos + craterOffset.y * craterSin;
                    float craterZ = -craterOffset.x * craterSin + craterOffset.y * craterCos;
                    float craterDistance = Mathf.Sqrt(
                        craterX * craterX / (craterAxisX * craterAxisX)
                        + craterZ * craterZ / (craterAxisZ * craterAxisZ));
                    float craterAngle = Mathf.Atan2(craterZ, craterX) * Mathf.Rad2Deg;

                    // Six percent of the existing peak is added only to the compact summit.
                    // The fast radial falloff makes the visual summit about seven percent
                    // narrower without changing the cone base or its established footprint.
                    float summitDistance = craterOffset.magnitude;
                    float summitWeight = 1f - Smooth01(Mathf.InverseLerp(38f, 142f, summitDistance));
                    summitWeight = Mathf.Pow(Mathf.Clamp01(summitWeight), 1.35f);
                    float summitLift = (targetPeak - preservedPeak) * summitWeight;

                    // Keep the 34x29m opening fixed. Only rim hierarchy and interior readability
                    // change: one side rises, the opposite side remains subtly collapsed.
                    if (craterDistance < 1.08f)
                    {
                        float highRim = 1f - Smooth01(Mathf.InverseLerp(
                            20f,
                            74f,
                            Mathf.Abs(Mathf.DeltaAngle(craterAngle, 32f))));
                        float collapsedRim = 1f - Smooth01(Mathf.InverseLerp(
                            18f,
                            60f,
                            Mathf.Abs(Mathf.DeltaAngle(craterAngle, -148f))));
                        float rimEnvelope = Mathf.Exp(-Mathf.Pow((craterDistance - 0.95f) / 0.19f, 2f));
                        float wallEnvelope = Smooth01(Mathf.InverseLerp(0.30f, 0.78f, craterDistance))
                            * (1f - Smooth01(Mathf.InverseLerp(0.82f, 1.02f, craterDistance)));
                        float floorWeight = 1f - Smooth01(Mathf.InverseLerp(0.22f, 0.36f, craterDistance));
                        summitLift *= Mathf.Lerp(1f, 0.26f, floorWeight);
                        summitLift += rimEnvelope * (3.2f + highRim * 2.2f - collapsedRim * 2.4f);
                        summitLift += wallEnvelope * (1.4f + highRim * 0.7f);
                    }

                    float ridgeDelta = 0f;
                    for (int ridge = 0; ridge < ridgeAngles.Length; ridge++)
                    {
                        float angularDelta = Mathf.Abs(Mathf.DeltaAngle(angleDegrees, ridgeAngles[ridge]));
                        float angularWeight = Mathf.Exp(-Mathf.Pow(angularDelta / ridgeWidths[ridge], 2f));
                        float lengthFade = Smooth01(Mathf.InverseLerp(
                            ridgeLengths[ridge],
                            ridgeLengths[ridge] - 64f,
                            distance));
                        float longitudinal = 0.70f
                            + Mathf.Cos(Mathf.Clamp01(distance / ridgeLengths[ridge]) * Mathf.PI) * 0.30f;
                        ridgeDelta += angularWeight
                            * ridgeHeights[ridge]
                            * ridgeInnerFade
                            * outerFade
                            * lengthFade
                            * longitudinal;
                    }

                    float valleyDelta = 0f;
                    for (int valley = 0; valley < valleyAngles.Length; valley++)
                    {
                        float angularDelta = Mathf.Abs(Mathf.DeltaAngle(angleDegrees, valleyAngles[valley]));
                        float angularWeight = Mathf.Exp(-Mathf.Pow(angularDelta / valleyWidths[valley], 2f));
                        valleyDelta += angularWeight
                            * valleyDepths[valley]
                            * ridgeInnerFade
                            * outerFade;
                    }

                    float delta = Mathf.Clamp(summitLift + ridgeDelta - valleyDelta, -3.4f, 16.2f);
                    float current = GetHeight(x, z);
                    SetHeight(x, z, Mathf.Clamp(current + delta, seaFloorHeight, targetPeak));
                }
            }

            RebuildMesh();
        }

        public void ApplyContinuousSummitSlopeFix(Vector2 volcanoCenter, Vector2 craterCenter)
        {
            EnsureData();

            float preservedPeak = float.NegativeInfinity;
            for (int i = 0; i < heights.Length; i++) preservedPeak = Mathf.Max(preservedPeak, heights[i]);

            const float editRadius = 142f;
            const float craterAxisX = 34f;
            const float craterAxisZ = 29f;
            const float craterRotation = -8f * Mathf.Deg2Rad;
            float craterCos = Mathf.Cos(craterRotation);
            float craterSin = Mathf.Sin(craterRotation);
            float half = size * 0.5f;
            float spacing = size / QuadResolution;

            for (int z = 0; z < vertexResolution; z++)
            {
                for (int x = 0; x < vertexResolution; x++)
                {
                    float localX = -half + x * spacing;
                    float localZ = -half + z * spacing;
                    Vector2 point = new Vector2(localX, localZ);
                    Vector2 craterOffset = point - craterCenter;
                    float summitDistance = craterOffset.magnitude;
                    if (summitDistance >= editRadius) continue;

                    float craterX = craterOffset.x * craterCos + craterOffset.y * craterSin;
                    float craterZ = -craterOffset.x * craterSin + craterOffset.y * craterCos;
                    float craterDistance = Mathf.Sqrt(
                        craterX * craterX / (craterAxisX * craterAxisX)
                        + craterZ * craterZ / (craterAxisZ * craterAxisZ));

                    // The opening, inner wall and floor stay locked. Only the broad shelf outside
                    // the rim is converted into a continuous upper-cone slope.
                    if (craterDistance <= 1.08f) continue;

                    float innerWindow = Smooth01(Mathf.InverseLerp(30f, 42f, summitDistance));
                    float outerWindow = Smooth01(Mathf.InverseLerp(editRadius, 126f, summitDistance));

                    // Continue a straight upper-cone envelope from the preserved peak toward
                    // the existing outer slope. Only valleys below that envelope are raised;
                    // vertices already forming ridges remain untouched, so no broad flat cap forms.
                    float angle = Mathf.Atan2(craterOffset.y, craterOffset.x);
                    float broadVariation = Mathf.Sin(angle * 2f + 0.35f) * 2.0f
                        + Mathf.Cos(angle * 3f - 0.7f) * 1.25f;
                    float targetHeight = preservedPeak
                        - Mathf.Max(0f, summitDistance - 31f) * 1.03f
                        + broadVariation;

                    float current = GetHeight(x, z);
                    float desiredLift = Mathf.Max(0f, targetHeight - current) * 0.88f;
                    float lift = Mathf.Min(11.5f, desiredLift) * innerWindow * outerWindow;
                    SetHeight(x, z, Mathf.Min(preservedPeak, current + lift));
                }
            }

            RebuildMesh();
        }

        public void ApplyTargetGuideHeroRidges(Vector2 volcanoCenter, Vector2 craterCenter)
        {
            EnsureData();

            float preservedPeak = float.NegativeInfinity;
            int preservedPeakIndex = 0;
            for (int i = 0; i < heights.Length; i++)
            {
                if (heights[i] > preservedPeak)
                {
                    preservedPeak = heights[i];
                    preservedPeakIndex = i;
                }
            }

            const float baseRadius = 345f;
            const float editRadius = 326f;
            const float craterAxisX = 34f;
            const float craterAxisZ = 29f;
            const float craterRotation = -8f * Mathf.Deg2Rad;
            float craterCos = Mathf.Cos(craterRotation);
            float craterSin = Mathf.Sin(craterRotation);

            // Five large hero ridges and five broad valleys reproduce the target guide's
            // readable volcanic drainage without adding noisy high-frequency erosion.
            float[] ridgeAngles = { -154f, -82f, -13f, 57f, 138f };
            float[] ridgeWidths = { 17f, 14f, 18f, 15f, 21f };
            float[] ridgeHeights = { 18f, 14f, 21f, 15f, 18f };
            float[] ridgeLengths = { 306f, 246f, 314f, 268f, 294f };
            float[] valleyAngles = { -119f, -47f, 23f, 98f, 177f };
            float[] valleyWidths = { 18f, 16f, 19f, 17f, 21f };
            float[] valleyDepths = { 8f, 10f, 7f, 9f, 7f };

            float half = size * 0.5f;
            float spacing = size / QuadResolution;
            for (int z = 0; z < vertexResolution; z++)
            {
                for (int x = 0; x < vertexResolution; x++)
                {
                    int vertexIndex = z * vertexResolution + x;
                    if (vertexIndex == preservedPeakIndex) continue;
                    float localX = -half + x * spacing;
                    float localZ = -half + z * spacing;
                    Vector2 point = new Vector2(localX, localZ);
                    Vector2 offset = point - volcanoCenter;
                    float distance = offset.magnitude;
                    if (distance >= editRadius) continue;

                    Vector2 craterOffset = point - craterCenter;
                    float craterX = craterOffset.x * craterCos + craterOffset.y * craterSin;
                    float craterZ = -craterOffset.x * craterSin + craterOffset.y * craterCos;
                    float craterDistance = Mathf.Sqrt(
                        craterX * craterX / (craterAxisX * craterAxisX)
                        + craterZ * craterZ / (craterAxisZ * craterAxisZ));

                    // Preserve the crater floor and lower inner wall. The upper wall and crest
                    // remain editable so the approved footprint can read as a broken summit
                    // instead of a flat plateau from gameplay distance.
                    if (craterDistance <= 0.60f) continue;

                    float angle = Mathf.Atan2(offset.y, offset.x);
                    float angleDegrees = angle * Mathf.Rad2Deg;
                    float normalizedDistance = distance / baseRadius;
                    float innerFade = Smooth01(Mathf.InverseLerp(28f, 65f, distance));
                    float outerFade = Smooth01(Mathf.InverseLerp(editRadius, 282f, distance));
                    float surfaceWindow = innerFade * outerFade;
                    float summitTaper = Mathf.Lerp(
                        0.34f,
                        1f,
                        Smooth01(Mathf.InverseLerp(55f, 122f, distance)));

                    float ridgeDelta = 0f;
                    float strongestRidge = 0f;
                    for (int ridge = 0; ridge < ridgeAngles.Length; ridge++)
                    {
                        float angularDelta = Mathf.Abs(Mathf.DeltaAngle(angleDegrees, ridgeAngles[ridge]));
                        float angularWeight = Mathf.Exp(-Mathf.Pow(angularDelta / ridgeWidths[ridge], 2f));
                        float lengthFade = Smooth01(Mathf.InverseLerp(
                            ridgeLengths[ridge],
                            ridgeLengths[ridge] - 62f,
                            distance));
                        float longitudinal = Mathf.Lerp(
                            1.0f,
                            0.56f,
                            Smooth01(Mathf.Clamp01(distance / ridgeLengths[ridge])));
                        float contribution = angularWeight * ridgeHeights[ridge]
                            * surfaceWindow * summitTaper * lengthFade * longitudinal;
                        ridgeDelta += contribution;
                        strongestRidge = Mathf.Max(strongestRidge, angularWeight * lengthFade);
                    }

                    float valleyDelta = 0f;
                    for (int valley = 0; valley < valleyAngles.Length; valley++)
                    {
                        float angularDelta = Mathf.Abs(Mathf.DeltaAngle(angleDegrees, valleyAngles[valley]));
                        float angularWeight = Mathf.Exp(-Mathf.Pow(angularDelta / valleyWidths[valley], 2f));
                        float lengthFade = Smooth01(Mathf.InverseLerp(302f, 242f, distance));
                        valleyDelta += angularWeight * valleyDepths[valley]
                            * surfaceWindow * Mathf.Lerp(0.48f, 1f, summitTaper) * lengthFade;
                    }

                    // Break the broad summit shelf between the hero ridges. This is a slow,
                    // upper-slope depression, not a new crater or a repeated radial groove.
                    float shoulderWindow = Mathf.Exp(-Mathf.Pow((distance - 91f) / 78f, 2f))
                        * outerFade;
                    float plateauBreak = (1f - Mathf.Clamp01(strongestRidge)) * shoulderWindow * 2.4f;

                    // The opening footprint is locked, but the outer rim gets four large height
                    // beats so the player silhouette reads as broken rock rather than a flat cap.
                    float rimEnvelope = Mathf.Exp(-Mathf.Pow((craterDistance - 0.98f) / 0.24f, 2f))
                        * Smooth01(Mathf.InverseLerp(0.60f, 0.78f, craterDistance));
                    float rimAngle = Mathf.Atan2(craterZ / craterAxisZ, craterX / craterAxisX);
                    float rimHierarchy = -3.8f
                        + Mathf.Cos(rimAngle * 4f + 0.42f) * 3.1f
                        + Mathf.Sin(rimAngle * 2f - 0.75f) * 1.25f;
                    float collapsedSector = Mathf.Exp(-Mathf.Pow(
                        Mathf.DeltaAngle(rimAngle * Mathf.Rad2Deg, -38f) / 24f, 2f));
                    // The old summit already contains multiple vertices at the locked maximum
                    // height. Raising them would clip into another horizontal plateau, so the
                    // broken profile is created by selective broad cuts while one crest remains.
                    rimHierarchy = Mathf.Clamp(rimHierarchy - collapsedSector * 2.8f, -8.5f, 0f);
                    float rimDelta = rimEnvelope * rimHierarchy;

                    float delta = Mathf.Clamp(ridgeDelta - valleyDelta - plateauBreak + rimDelta, -10f, 21.5f);
                    float current = GetHeight(x, z);
                    SetHeight(x, z, Mathf.Clamp(current + delta, seaFloorHeight, preservedPeak));
                }
            }

            RebuildMesh();
        }

        public void ApplyDesignedLowPolyCrater(Vector2 craterCenter)
        {
            EnsureData();

            float preservedPeak = float.NegativeInfinity;
            for (int i = 0; i < heights.Length; i++) preservedPeak = Mathf.Max(preservedPeak, heights[i]);

            const float craterAxisX = 34f;
            const float craterAxisZ = 29f;
            const float craterRotation = -8f * Mathf.Deg2Rad;
            float cos = Mathf.Cos(craterRotation);
            float sin = Mathf.Sin(craterRotation);
            float half = size * 0.5f;
            float spacing = size / QuadResolution;
            float floorBase = preservedPeak - 28.0f;
            int peakAnchorX = -1;
            int peakAnchorZ = -1;
            float peakAnchorScore = float.PositiveInfinity;

            for (int z = 0; z < vertexResolution; z++)
            {
                for (int x = 0; x < vertexResolution; x++)
                {
                    float localX = -half + x * spacing;
                    float localZ = -half + z * spacing;
                    Vector2 offset = new Vector2(localX, localZ) - craterCenter;
                    float rx = offset.x * cos + offset.y * sin;
                    float rz = -offset.x * sin + offset.y * cos;
                    float distance = Mathf.Sqrt(
                        rx * rx / (craterAxisX * craterAxisX)
                        + rz * rz / (craterAxisZ * craterAxisZ));
                    if (distance > 1.24f) continue;

                    float angle = Mathf.Atan2(rz / craterAxisZ, rx / craterAxisX) * Mathf.Rad2Deg;
                    if (distance >= 0.86f && distance <= 1.04f)
                    {
                        float score = Mathf.Abs(Mathf.DeltaAngle(angle, -50f))
                            + Mathf.Abs(distance - 0.95f) * 32f;
                        if (score < peakAnchorScore)
                        {
                            peakAnchorScore = score;
                            peakAnchorX = x;
                            peakAnchorZ = z;
                        }
                    }
                    float highPrimary = Mathf.Exp(-Mathf.Pow(Mathf.DeltaAngle(angle, -50f) / 55f, 2f)) * 5.2f;
                    float highSecondary = Mathf.Exp(-Mathf.Pow(Mathf.DeltaAngle(angle, 38f) / 44f, 2f)) * 3.4f;
                    float collapsed = Mathf.Exp(-Mathf.Pow(Mathf.DeltaAngle(angle, 158f) / 52f, 2f)) * 6.8f;
                    float broadUndulation = Mathf.Sin((angle + 18f) * Mathf.Deg2Rad * 2f) * 0.85f;
                    float rimHeight = Mathf.Min(
                        preservedPeak,
                        preservedPeak - 4.6f + Mathf.Max(highPrimary, highSecondary) - collapsed + broadUndulation);

                    float floorHeight = floorBase
                        + Mathf.Sin((angle - 12f) * Mathf.Deg2Rad * 2f) * 0.65f;
                    float wallT = Smooth01(Mathf.InverseLerp(0.24f, 0.86f, distance));
                    float target = Mathf.Lerp(floorHeight, rimHeight, wallT);

                    // Keep the rim broad enough to read, then blend back into the already-approved
                    // outer cone before the silhouette-forming slope begins.
                    if (distance > 0.86f)
                    {
                        target = rimHeight;
                    }

                    float current = GetHeight(x, z);
                    if (distance > 1.04f)
                    {
                        float outerBlend = Smooth01(Mathf.InverseLerp(1.04f, 1.24f, distance));
                        target = Mathf.Lerp(rimHeight, current, outerBlend);
                    }

                    SetHeight(x, z, Mathf.Min(preservedPeak, target));
                }
            }

            if (peakAnchorX >= 0 && peakAnchorZ >= 0)
            {
                SetHeight(peakAnchorX, peakAnchorZ, preservedPeak);
            }

            RebuildMesh();
        }

        public void ApplyCraterOnlyTightening(Vector2 craterCenter)
        {
            EnsureData();

            const float editRadius = 148f;
            float half = size * 0.5f;
            float spacing = size / QuadResolution;
            for (int z = 0; z < vertexResolution; z++)
            {
                for (int x = 0; x < vertexResolution; x++)
                {
                    float localX = -half + x * spacing;
                    float localZ = -half + z * spacing;
                    Vector2 offset = new Vector2(localX, localZ) - craterCenter;
                    float angle = Mathf.Atan2(offset.y, offset.x);
                    float angleDegrees = angle * Mathf.Rad2Deg;
                    float warp = 1f
                        + Mathf.Sin(angle * 2.7f + 0.4f) * 0.085f
                        + Mathf.Sin(angle * 5.3f - 1.1f) * 0.035f;
                    float distance = offset.magnitude / Mathf.Max(0.78f, warp);
                    if (offset.magnitude >= editRadius || distance <= 24f)
                    {
                        continue;
                    }

                    // Keep a visible crater floor, but let the inner wall wrap farther inward.
                    // The rim bands are shifted inward and overlapped to make the summit thicker
                    // without widening or flattening the whole volcano.
                    float innerWall = Smooth01(Mathf.InverseLerp(25f, 40f, distance))
                        * Smooth01(Mathf.InverseLerp(64f, 50f, distance));
                    float innerRim = Mathf.Exp(-Mathf.Pow((distance - 53f) / 13f, 2f));
                    float rim = Mathf.Exp(-Mathf.Pow((distance - 72f) / 18f, 2f));
                    float outerShoulder = Mathf.Exp(-Mathf.Pow((distance - 100f) / 27f, 2f));
                    float upperSteepening = Smooth01(Mathf.InverseLerp(146f, 42f, distance));
                    float frontDelta = Mathf.Abs(Mathf.DeltaAngle(angleDegrees, -90f));
                    float frontWeight = 1f - Smooth01(Mathf.InverseLerp(24f, 68f, frontDelta));
                    float collapsedDelta = Mathf.Abs(Mathf.DeltaAngle(angleDegrees, -146f));
                    float collapsedWeight = 1f - Smooth01(Mathf.InverseLerp(18f, 54f, collapsedDelta));
                    float heightVariation = 0.86f
                        + Mathf.Sin(angle * 2.2f + 0.35f) * 0.12f
                        + Mathf.Cos(angle * 3.7f - 0.8f) * 0.07f;
                    float outerFade = Smooth01(Mathf.InverseLerp(editRadius, 130f, offset.magnitude));
                    float baseLift = upperSteepening * 19.5f;
                    float asymmetricRimLift = innerWall * (6.4f + frontWeight * 1.8f)
                        + innerRim * (5.2f + frontWeight * 1.2f)
                        + rim * (8.8f * heightVariation + frontWeight * 1.4f)
                        + outerShoulder * 2.2f;
                    asymmetricRimLift *= Mathf.Lerp(1f, 0.44f, collapsedWeight);
                    float delta = (baseLift + asymmetricRimLift) * outerFade;
                    delta = Mathf.Min(delta, 26f);

                    float current = GetHeight(x, z);
                    SetHeight(x, z, Mathf.Min(198f, current + Mathf.Max(0f, delta)));
                }
            }

            RebuildMesh();
        }

        public bool ContainsWorldPoint(Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            float half = size * 0.5f;
            return local.x >= -half && local.x <= half && local.z >= -half && local.z <= half;
        }

        public Vector3 ClampWorldPoint(Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            float half = size * 0.5f;
            local.x = Mathf.Clamp(local.x, -half, half);
            local.z = Mathf.Clamp(local.z, -half, half);
            local.y = SampleHeightLocal(local.x, local.z);
            return transform.TransformPoint(local);
        }

        public float SampleHeightWorld(Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            return transform.TransformPoint(new Vector3(local.x, SampleHeightLocal(local.x, local.z), local.z)).y;
        }

        public int SamplePaintLayerWorld(Vector3 worldPoint)
        {
            EnsureData();
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            float half = size * 0.5f;
            float spacing = size / QuadResolution;
            int x = Mathf.Clamp(Mathf.FloorToInt((local.x + half) / spacing), 0, QuadResolution - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt((local.z + half) / spacing), 0, QuadResolution - 1);
            return GetPaintLayer(x, z);
        }

        public void Sculpt(Vector3 worldPoint, float radius, float strength, IslandSculptMode mode, float flattenHeight, int paintLayer)
        {
            EnsureData();
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            float half = size * 0.5f;
            float spacing = size / QuadResolution;
            int centerX = Mathf.RoundToInt((local.x + half) / spacing);
            int centerZ = Mathf.RoundToInt((local.z + half) / spacing);
            int gridRadius = Mathf.Max(1, Mathf.CeilToInt(radius / spacing));
            int startX = Mathf.Clamp(centerX - gridRadius, 0, vertexResolution - 1);
            int endX = Mathf.Clamp(centerX + gridRadius, 0, vertexResolution - 1);
            int startZ = Mathf.Clamp(centerZ - gridRadius, 0, vertexResolution - 1);
            int endZ = Mathf.Clamp(centerZ + gridRadius, 0, vertexResolution - 1);
            float strength01 = Mathf.Clamp01(strength);

            if (mode == IslandSculptMode.Smooth)
            {
                SmoothHeights(startX, endX, startZ, endZ, centerX, centerZ, gridRadius, strength01);
            }
            else if (mode == IslandSculptMode.Paint)
            {
                PaintQuads(local, radius, strength01, paintLayer);
            }
            else
            {
                for (int z = startZ; z <= endZ; z++)
                {
                    for (int x = startX; x <= endX; x++)
                    {
                        float falloff = BrushFalloff(x, z, centerX, centerZ, gridRadius);
                        if (falloff <= 0f)
                        {
                            continue;
                        }

                        float current = GetHeight(x, z);
                        float next = current;
                        if (mode == IslandSculptMode.DrawLand)
                        {
                            float target = Mathf.Max(current, 1.35f);
                            next = Mathf.Lerp(current, target, falloff * Mathf.Max(0.45f, strength01));
                        }
                        else if (mode == IslandSculptMode.Erase)
                        {
                            next = Mathf.Lerp(current, seaFloorHeight, falloff * Mathf.Max(0.55f, strength01));
                        }
                        else if (mode == IslandSculptMode.Raise)
                        {
                            next = current + falloff * strength01 * 2.25f;
                        }
                        else if (mode == IslandSculptMode.Lower)
                        {
                            next = current - falloff * strength01 * 2.25f;
                        }
                        else if (mode == IslandSculptMode.Flatten)
                        {
                            next = Mathf.Lerp(current, flattenHeight, falloff * strength01);
                        }

                        SetHeight(x, z, Mathf.Clamp(next, seaFloorHeight, 78f));
                    }
                }

                if (mode == IslandSculptMode.DrawLand || mode == IslandSculptMode.Flatten || mode == IslandSculptMode.Raise)
                {
                    PaintQuads(local, radius * 0.92f, Mathf.Max(0.5f, strength01), 0);
                    PaintShorelineRing(local, radius * 1.12f, radius * 0.72f, 1, Mathf.Max(0.45f, strength01));
                }
                else if (mode == IslandSculptMode.Erase || mode == IslandSculptMode.Lower)
                {
                    PaintQuads(local, radius, Mathf.Max(0.4f, strength01), 1);
                }
            }

            RebuildMesh();
        }

        public void RebuildMesh()
        {
            EnsureData();
            SanitizeTerrainData();
            int res = vertexResolution;
            int quadRes = QuadResolution;
            float half = size * 0.5f;
            float spacing = size / quadRes;
            Vector3[] vertices = new Vector3[res * res];
            Vector2[] uvs = new Vector2[vertices.Length];

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    int index = VertexIndex(x, z);
                    float localX = -half + x * spacing;
                    float localZ = -half + z * spacing;
                    vertices[index] = new Vector3(localX, GetHeight(x, z), localZ);
                    uvs[index] = new Vector2(localX * 0.085f, localZ * 0.085f);
                }
            }

            List<int>[] subTriangles =
            {
                new List<int>(quadRes * quadRes),
                new List<int>(quadRes * quadRes),
                new List<int>(quadRes * quadRes),
                new List<int>(quadRes * quadRes)
            };

            for (int z = 0; z < quadRes; z++)
            {
                for (int x = 0; x < quadRes; x++)
                {
                    int layer = Mathf.Clamp(GetPaintLayer(x, z), 0, 3);
                    int a = VertexIndex(x, z);
                    int b = VertexIndex(x + 1, z);
                    int c = VertexIndex(x, z + 1);
                    int d = VertexIndex(x + 1, z + 1);
                    subTriangles[layer].Add(a);
                    subTriangles[layer].Add(c);
                    subTriangles[layer].Add(b);
                    subTriangles[layer].Add(b);
                    subTriangles[layer].Add(c);
                    subTriangles[layer].Add(d);
                }
            }

            Mesh mesh = new Mesh
            {
                name = "Editable Island Mesh",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
                vertices = vertices,
                uv = uvs,
                subMeshCount = 4
            };

            for (int i = 0; i < 4; i++)
            {
                mesh.SetTriangles(subTriangles[i], i);
            }

            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            MeshFilter filter = GetComponent<MeshFilter>();
            Mesh oldMesh = filter.sharedMesh;
            filter.sharedMesh = mesh;

            MeshCollider meshCollider = GetComponent<MeshCollider>();
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = mesh;

            if (Application.isEditor && !Application.isPlaying && oldMesh != null)
            {
                DestroyImmediate(oldMesh);
            }
        }

        private void SanitizeTerrainData()
        {
            if (heights != null)
            {
                for (int i = 0; i < heights.Length; i++)
                {
                    float height = heights[i];
                    if (float.IsNaN(height) || float.IsInfinity(height))
                    {
                        height = seaFloorHeight;
                    }

                    heights[i] = Mathf.Clamp(height, seaFloorHeight, 260f);
                }
            }

            if (paintLayers != null)
            {
                for (int i = 0; i < paintLayers.Length; i++)
                {
                    paintLayers[i] = Mathf.Clamp(paintLayers[i], 0, 3);
                }
            }
        }

        private void SmoothHeights(int startX, int endX, int startZ, int endZ, int centerX, int centerZ, int gridRadius, float strength)
        {
            float[] copy = (float[])heights.Clone();
            for (int z = startZ; z <= endZ; z++)
            {
                for (int x = startX; x <= endX; x++)
                {
                    float falloff = BrushFalloff(x, z, centerX, centerZ, gridRadius);
                    if (falloff <= 0f)
                    {
                        continue;
                    }

                    float sum = 0f;
                    int count = 0;
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int sx = Mathf.Clamp(x + dx, 0, vertexResolution - 1);
                            int sz = Mathf.Clamp(z + dz, 0, vertexResolution - 1);
                            sum += copy[VertexIndex(sx, sz)];
                            count++;
                        }
                    }

                    SetHeight(x, z, Mathf.Lerp(copy[VertexIndex(x, z)], sum / count, falloff * strength));
                }
            }
        }

        private void PaintQuads(Vector3 localCenter, float radius, float strength, int layer)
        {
            int quadRes = QuadResolution;
            float half = size * 0.5f;
            float spacing = size / quadRes;
            float radiusSq = radius * radius;
            for (int z = 0; z < quadRes; z++)
            {
                for (int x = 0; x < quadRes; x++)
                {
                    Vector2 center = new Vector2(-half + (x + 0.5f) * spacing, -half + (z + 0.5f) * spacing);
                    float distSq = (center - new Vector2(localCenter.x, localCenter.z)).sqrMagnitude;
                    if (distSq <= radiusSq && Random.value <= Mathf.Clamp01(strength + 0.2f))
                    {
                        SetPaintLayer(x, z, layer);
                    }
                }
            }
        }

        private void PaintShorelineRing(Vector3 localCenter, float outerRadius, float innerRadius, int layer, float strength)
        {
            int quadRes = QuadResolution;
            float half = size * 0.5f;
            float spacing = size / quadRes;
            for (int z = 0; z < quadRes; z++)
            {
                for (int x = 0; x < quadRes; x++)
                {
                    Vector2 center = new Vector2(-half + (x + 0.5f) * spacing, -half + (z + 0.5f) * spacing);
                    float distance = Vector2.Distance(center, new Vector2(localCenter.x, localCenter.z));
                    if (distance >= innerRadius && distance <= outerRadius && Random.value <= Mathf.Clamp01(strength + 0.1f))
                    {
                        SetPaintLayer(x, z, layer);
                    }
                }
            }
        }

        private void SmoothVolcanoSkirt(Vector2 center, float radius, float strength)
        {
            float[] copy = (float[])heights.Clone();
            float half = size * 0.5f;
            float spacing = size / QuadResolution;
            for (int z = 1; z < vertexResolution - 1; z++)
            {
                for (int x = 1; x < vertexResolution - 1; x++)
                {
                    float localX = -half + x * spacing;
                    float localZ = -half + z * spacing;
                    float distance01 = Vector2.Distance(new Vector2(localX, localZ), center) / Mathf.Max(1f, radius);
                    if (distance01 <= 0.42f || distance01 >= 1.08f)
                    {
                        continue;
                    }

                    float falloff = Smooth01(1f - Mathf.Abs(distance01 - 0.76f) / 0.34f) * strength;
                    float sum = 0f;
                    int count = 0;
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            sum += copy[VertexIndex(x + dx, z + dz)];
                            count++;
                        }
                    }

                    SetHeight(x, z, Mathf.Lerp(copy[VertexIndex(x, z)], sum / count, falloff));
                }
            }
        }

        private void PaintVolcanoTransition(Vector2 center, float baseRadius, float apronRadius, float repairRadius)
        {
            float half = size * 0.5f;
            float spacing = size / QuadResolution;
            for (int z = 0; z < QuadResolution; z++)
            {
                for (int x = 0; x < QuadResolution; x++)
                {
                    Vector2 local = new Vector2(-half + (x + 0.5f) * spacing, -half + (z + 0.5f) * spacing);
                    Vector2 offset = local - center;
                    float angle = Mathf.Atan2(offset.y, offset.x);
                    float warp = 1f + Mathf.Sin(angle * 2.7f + 0.55f) * 0.12f + Mathf.Cos(angle * 4.9f - 1.2f) * 0.085f;
                    float distance = offset.magnitude / Mathf.Max(1f, warp);
                    float height = AverageQuadHeight(x, z);

                    if (height <= waterLevel + 1.15f)
                    {
                        SetPaintLayer(x, z, 1);
                    }
                    else if (height <= waterLevel + 3.35f)
                    {
                        SetPaintLayer(x, z, 0);
                    }
                    else if (distance < baseRadius * 1.34f || height > waterLevel + 21.5f)
                    {
                        SetPaintLayer(x, z, 2);
                    }
                    else if (distance < apronRadius * 1.02f && height > waterLevel + 8.8f)
                    {
                        SetPaintLayer(x, z, 2);
                    }
                    else if (distance < repairRadius)
                    {
                        SetPaintLayer(x, z, 3);
                    }
                }
            }
        }

        private float SampleHeightLocal(float localX, float localZ)
        {
            EnsureData();
            float half = size * 0.5f;
            float spacing = size / QuadResolution;
            int x = Mathf.Clamp(Mathf.RoundToInt((localX + half) / spacing), 0, vertexResolution - 1);
            int z = Mathf.Clamp(Mathf.RoundToInt((localZ + half) / spacing), 0, vertexResolution - 1);
            return GetHeight(x, z);
        }

        private void EnsureData()
        {
            vertexResolution = Mathf.Clamp(vertexResolution, 17, 513);
            int vertexCount = vertexResolution * vertexResolution;
            int quadCount = QuadResolution * QuadResolution;
            if (heights == null || heights.Length != vertexCount)
            {
                heights = new float[vertexCount];
                for (int i = 0; i < heights.Length; i++)
                {
                    heights[i] = seaFloorHeight;
                }
            }

            if (paintLayers == null || paintLayers.Length != quadCount)
            {
                paintLayers = new int[quadCount];
                for (int i = 0; i < paintLayers.Length; i++)
                {
                    paintLayers[i] = 1;
                }
            }
        }

        private float SampleOldHeight(float localX, float localZ, float oldSize, int oldResolution, float[] oldValues)
        {
            float oldHalf = oldSize * 0.5f;
            if (localX < -oldHalf || localX > oldHalf || localZ < -oldHalf || localZ > oldHalf)
            {
                return seaFloorHeight;
            }

            float gx = (localX + oldHalf) / oldSize * (oldResolution - 1);
            float gz = (localZ + oldHalf) / oldSize * (oldResolution - 1);
            int x0 = Mathf.Clamp(Mathf.FloorToInt(gx), 0, oldResolution - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt(gz), 0, oldResolution - 1);
            int x1 = Mathf.Clamp(x0 + 1, 0, oldResolution - 1);
            int z1 = Mathf.Clamp(z0 + 1, 0, oldResolution - 1);
            float tx = gx - x0;
            float tz = gz - z0;
            float a = oldValues[z0 * oldResolution + x0];
            float b = oldValues[z0 * oldResolution + x1];
            float c = oldValues[z1 * oldResolution + x0];
            float d = oldValues[z1 * oldResolution + x1];
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
        }

        private int SampleOldPaint(float localX, float localZ, float oldSize, int oldQuadResolution, int[] oldValues)
        {
            float oldHalf = oldSize * 0.5f;
            if (localX < -oldHalf || localX > oldHalf || localZ < -oldHalf || localZ > oldHalf)
            {
                return 1;
            }

            int x = Mathf.Clamp(Mathf.RoundToInt((localX + oldHalf) / oldSize * (oldQuadResolution - 1)), 0, oldQuadResolution - 1);
            int z = Mathf.Clamp(Mathf.RoundToInt((localZ + oldHalf) / oldSize * (oldQuadResolution - 1)), 0, oldQuadResolution - 1);
            return oldValues[z * oldQuadResolution + x];
        }

        private static float BrushFalloff(int x, int z, int centerX, int centerZ, int radius)
        {
            float dx = (x - centerX) / (float)Mathf.Max(1, radius);
            float dz = (z - centerZ) / (float)Mathf.Max(1, radius);
            float distance = Mathf.Sqrt(dx * dx + dz * dz);
            float value = Mathf.Clamp01(1f - distance);
            return value * value * (3f - 2f * value);
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private static float SampleStamp(Texture2D stamp, Vector2 offset, float radius)
        {
            if (stamp == null || radius <= 0f)
            {
                return 0.5f;
            }

            float u = Mathf.Clamp01((offset.x / (radius * 2f)) + 0.5f);
            float v = Mathf.Clamp01((offset.y / (radius * 2f)) + 0.5f);
            try
            {
                return Mathf.Clamp01(stamp.GetPixelBilinear(u, v).grayscale);
            }
            catch (UnityException)
            {
                return 0.5f;
            }
        }

        private static Vector2 GetStampRange(Texture2D stamp)
        {
            if (stamp == null)
            {
                return new Vector2(0f, 1f);
            }

            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;
            try
            {
                for (int z = 0; z <= 24; z++)
                {
                    for (int x = 0; x <= 24; x++)
                    {
                        float value = Mathf.Clamp01(stamp.GetPixelBilinear(x / 24f, z / 24f).grayscale);
                        min = Mathf.Min(min, value);
                        max = Mathf.Max(max, value);
                    }
                }
            }
            catch (UnityException)
            {
                return new Vector2(0f, 1f);
            }

            if (float.IsNaN(min) || float.IsInfinity(min) || float.IsNaN(max) || float.IsInfinity(max) || max - min < 0.001f)
            {
                return new Vector2(0f, 1f);
            }

            return new Vector2(min, max);
        }

        private static float SampleStampNormalized(Texture2D stamp, Vector2 offset, float radius, Vector2 range)
        {
            float value = SampleStamp(stamp, offset, radius);
            return Mathf.Clamp01(Mathf.InverseLerp(range.x, range.y, value));
        }

        private static float Pulse(float value, float center, float width)
        {
            float normalized = (value - center) / Mathf.Max(0.0001f, width);
            return Mathf.Exp(-normalized * normalized);
        }

        private float ReferenceIslandMask(Vector2 point)
        {
            float land = 0f;
            land = Mathf.Max(land, BoxMask(point, new Vector2(-28f, -8f), 430f, 330f, 2f, 72f));
            land = Mathf.Max(land, EllipseMask(point, new Vector2(-165f, -212f), 250f, 102f, -20f));
            land = Mathf.Max(land, EllipseMask(point, new Vector2(-410f, -245f), 120f, 70f, -16f));
            land = Mathf.Max(land, EllipseMask(point, new Vector2(-445f, 18f), 225f, 58f, 5f));
            land = Mathf.Max(land, EllipseMask(point, new Vector2(-350f, 170f), 178f, 74f, 10f));
            land = Mathf.Max(land, BoxMask(point, new Vector2(25f, 252f), 350f, 84f, 2f, 44f));
            land = Mathf.Max(land, EllipseMask(point, new Vector2(310f, 210f), 175f, 78f, 10f));
            land = Mathf.Max(land, EllipseMask(point, new Vector2(392f, 72f), 145f, 70f, -18f));
            land = Mathf.Max(land, EllipseMask(point, new Vector2(290f, -230f), 146f, 78f, 18f));
            land = Mathf.Max(land, EllipseMask(point, new Vector2(410f, -135f), 90f, 54f, -8f));
            land = Mathf.Max(land, EllipseMask(point, new Vector2(-575f, -198f), 82f, 42f, 21f));
            land = Mathf.Max(land, EllipseMask(point, new Vector2(-560f, 122f), 76f, 38f, -18f));
            land = Mathf.Max(land, EllipseMask(point, new Vector2(560f, -22f), 78f, 38f, -8f));
            land = Mathf.Max(land, EllipseMask(point, new Vector2(540f, 182f), 70f, 36f, 18f));

            float water = 0f;
            water = Mathf.Max(water, EllipseMask(point, new Vector2(280f, 76f), 138f, 92f, 10f));
            water = Mathf.Max(water, EllipseMask(point, new Vector2(-245f, -255f), 118f, 74f, -7f));
            water = Mathf.Max(water, EllipseMask(point, new Vector2(-342f, 54f), 82f, 50f, 4f));
            water = Mathf.Max(water, EllipseMask(point, new Vector2(34f, 312f), 72f, 34f, 0f));

            float edgeNoise = Mathf.PerlinNoise(point.x * 0.024f + 31.4f, point.y * 0.024f + 8.2f) - 0.5f;
            land = Mathf.Clamp01(land + edgeNoise * 0.13f);
            return Mathf.Clamp01(land - water * 0.88f);
        }

        private static float BoxMask(Vector2 point, Vector2 center, float width, float depth, float rotationDegrees, float softness)
        {
            Vector2 delta = point - center;
            float radians = -rotationDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            float rx = delta.x * cos - delta.y * sin;
            float rz = delta.x * sin + delta.y * cos;
            float dx = Mathf.Abs(rx) - width * 0.5f;
            float dz = Mathf.Abs(rz) - depth * 0.5f;
            float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dz, 0f) * Mathf.Max(dz, 0f));
            float inside = Mathf.Min(Mathf.Max(dx, dz), 0f);
            return Smooth01((softness - (outside + inside)) / Mathf.Max(1f, softness));
        }

        private static float EllipseMask(Vector2 point, Vector2 center, float radiusX, float radiusZ, float rotationDegrees)
        {
            Vector2 delta = point - center;
            float radians = -rotationDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            float rx = delta.x * cos - delta.y * sin;
            float rz = delta.x * sin + delta.y * cos;
            float distance = Mathf.Sqrt((rx * rx) / (radiusX * radiusX) + (rz * rz) / (radiusZ * radiusZ));
            float angle = Mathf.Atan2(rz / Mathf.Max(1f, radiusZ), rx / Mathf.Max(1f, radiusX));
            float warp = 1f
                + Mathf.Sin(angle * 3.1f + 0.4f) * 0.06f
                + Mathf.Cos(angle * 5.3f - 1.1f) * 0.035f;
            return Smooth01((1.05f * warp - distance) / 0.22f);
        }

        private static float WarpedVolcanoDistance(Vector2 point, Vector2 center)
        {
            Vector2 offset = point - center;
            float angle = Mathf.Atan2(offset.y, offset.x);
            float warp = 1f
                + Mathf.Sin(angle * 2.8f + 0.75f) * 0.1f
                + Mathf.Cos(angle * 4.6f - 0.2f) * 0.075f;
            return offset.magnitude / Mathf.Max(0.68f, warp);
        }

        private void SmoothRecoveredIslandSurface(Vector2 volcanoCenter)
        {
            float[] copy = (float[])heights.Clone();
            float half = size * 0.5f;
            float spacing = size / QuadResolution;

            for (int z = 1; z < vertexResolution - 1; z++)
            {
                for (int x = 1; x < vertexResolution - 1; x++)
                {
                    float localX = -half + x * spacing;
                    float localZ = -half + z * spacing;
                    Vector2 point = new Vector2(localX, localZ);
                    float volcanoDistance = WarpedVolcanoDistance(point, volcanoCenter);
                    float height = copy[VertexIndex(x, z)];
                    if (height <= waterLevel + 0.35f || volcanoDistance < 58f)
                    {
                        continue;
                    }

                    float sum = 0f;
                    int count = 0;
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            sum += copy[VertexIndex(x + dx, z + dz)];
                            count++;
                        }
                    }

                    float strength = volcanoDistance < 210f ? 0.14f : 0.28f;
                    SetHeight(x, z, Mathf.Lerp(height, sum / count, strength));
                }
            }
        }

        private void LiftVolcanoApproach(Vector2 center, float baseRadius, float apronRadius, float repairRadius)
        {
            float half = size * 0.5f;
            float spacing = size / QuadResolution;
            float outerHeight = waterLevel + 2.2f;
            float footHeight = waterLevel + 13.0f;
            float shoulderHeight = waterLevel + 18.5f;
            float craterGuardRadius = baseRadius * 0.18f;

            for (int z = 0; z < vertexResolution; z++)
            {
                for (int x = 0; x < vertexResolution; x++)
                {
                    float localX = -half + x * spacing;
                    float localZ = -half + z * spacing;
                    Vector2 offset = new Vector2(localX - center.x, localZ - center.y);
                    float angle = Mathf.Atan2(offset.y, offset.x);
                    float warp = 1f
                        + Mathf.Sin(angle * 2.7f + 0.55f) * 0.12f
                        + Mathf.Cos(angle * 4.9f - 1.2f) * 0.085f;
                    float distance = offset.magnitude / Mathf.Max(0.65f, warp);

                    if (distance <= craterGuardRadius || distance >= repairRadius)
                    {
                        continue;
                    }

                    float current = GetHeight(x, z);
                    if (current <= waterLevel + 0.2f)
                    {
                        continue;
                    }

                    float t = Mathf.InverseLerp(repairRadius, baseRadius * 0.72f, distance);
                    float ramp = Mathf.Lerp(outerHeight, shoulderHeight, Smooth01(t));
                    if (distance < apronRadius)
                    {
                        float foot01 = Mathf.InverseLerp(apronRadius, baseRadius * 0.72f, distance);
                        ramp = Mathf.Max(ramp, Mathf.Lerp(footHeight, shoulderHeight, Smooth01(foot01)));
                    }

                    float apronNoise = (Mathf.PerlinNoise(x * 0.065f + 12.1f, z * 0.065f + 77.4f) - 0.5f) * 0.35f;
                    float target = ramp + apronNoise;
                    float blend = distance < apronRadius ? 0.82f : 0.5f;
                    SetHeight(x, z, Mathf.Max(current, Mathf.Lerp(current, target, blend)));
                }
            }
        }

        private Vector2 EstimateVolcanoCenterLocal()
        {
            EnsureData();
            float half = size * 0.5f;
            float spacing = size / QuadResolution;
            Vector2 weighted = Vector2.zero;
            float totalWeight = 0f;
            Vector2 highest = Vector2.zero;
            float highestHeight = float.NegativeInfinity;

            for (int z = 0; z < vertexResolution; z++)
            {
                for (int x = 0; x < vertexResolution; x++)
                {
                    float height = GetHeight(x, z);
                    float localX = -half + x * spacing;
                    float localZ = -half + z * spacing;
                    if (height > highestHeight)
                    {
                        highestHeight = height;
                        highest = new Vector2(localX, localZ);
                    }

                    if (height <= waterLevel + 3f)
                    {
                        continue;
                    }

                    int qx = Mathf.Clamp(x, 0, QuadResolution - 1);
                    int qz = Mathf.Clamp(z, 0, QuadResolution - 1);
                    float paintWeight = GetPaintLayer(qx, qz) == 2 ? 2.2f : 0.7f;
                    float heightWeight = Mathf.Pow(Mathf.Max(0f, height - waterLevel), 1.35f);
                    float weight = paintWeight * heightWeight;
                    weighted += new Vector2(localX, localZ) * weight;
                    totalWeight += weight;
                }
            }

            if (totalWeight > 0.01f)
            {
                return weighted / totalWeight;
            }

            return highestHeight > waterLevel + 1f ? highest : Vector2.zero;
        }

        private Vector2 GridToNormalized(int x, int z)
        {
            return new Vector2(x / (float)(vertexResolution - 1) * 2f - 1f, z / (float)(vertexResolution - 1) * 2f - 1f);
        }

        private int VertexIndex(int x, int z)
        {
            return z * vertexResolution + x;
        }

        private int QuadIndex(int x, int z)
        {
            return z * QuadResolution + x;
        }

        private float GetHeight(int x, int z)
        {
            return heights[VertexIndex(x, z)];
        }

        private void SetHeight(int x, int z, float value)
        {
            heights[VertexIndex(x, z)] = value;
        }

        private int GetPaintLayer(int x, int z)
        {
            return paintLayers[QuadIndex(x, z)];
        }

        private float AverageQuadHeight(int x, int z)
        {
            return (GetHeight(x, z) + GetHeight(x + 1, z) + GetHeight(x, z + 1) + GetHeight(x + 1, z + 1)) * 0.25f;
        }

        private void SetPaintLayer(int x, int z, int layer)
        {
            paintLayers[QuadIndex(x, z)] = Mathf.Clamp(layer, 0, 3);
        }
    }

    public enum IslandSculptMode
    {
        DrawLand,
        Erase,
        Raise,
        Lower,
        Smooth,
        Flatten,
        Paint
    }
}

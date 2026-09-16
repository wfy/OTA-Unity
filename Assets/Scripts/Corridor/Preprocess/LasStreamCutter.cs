using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OTA.Corridor.Hierarchy;
using UnityEngine;

namespace OTA.Corridor.Preprocess
{
    [Serializable]
    public struct Point2D
    {
        public double x;
        public double y;
        public Point2D(double x, double y) { this.x = x; this.y = y; }
    }

    [Serializable]
    public class TowerMarker
    {
        public string id;
        public string name;
        public double easting;
        public double northing;
        public double elevation;
        public double lat;
        public double lon;

        public TowerMarker(string id, string name, double e, double n, double el, double la, double lo)
        {
            this.id = id;
            this.name = name;
            this.easting = e;
            this.northing = n;
            this.elevation = el;
            this.lat = la;
            this.lon = lo;
        }
    }

    [Serializable]
    public class CutSegmentTask
    {
        public int index;
        public string segmentName;
        public TowerMarker towerA;
        public TowerMarker towerB;
        public double halfWidth = 30.0;     // 30m on each side = 60m total
        public double longitudinalExt = 15.0; // 15m beyond tower base
        public string outputFilePath;

        // 2D Polygon in Projected coordinates (Easting, Northing)
        public Point2D[] polygon;
        public double minX, maxX, minY, maxY;

        // Result stats
        public int pointCount = 0;
        public Bounds bounds;
        public double snappedTowerAx, snappedTowerAy, towerAzTop;
        public double snappedTowerBx, snappedTowerBy, towerBzTop;
        public bool isCompleted = false;
        public string error = null;

        public void BuildPolygon()
        {
            double dx = towerB.easting - towerA.easting;
            double dy = towerB.northing - towerA.northing;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.001) len = 0.001;

            double dirX = dx / len;
            double dirY = dy / len;
            double normX = -dirY;
            double normY = dirX;

            double ax = towerA.easting - dirX * longitudinalExt;
            double ay = towerA.northing - dirY * longitudinalExt;
            double bx = towerB.easting + dirX * longitudinalExt;
            double by = towerB.northing + dirY * longitudinalExt;

            Point2D p1 = new Point2D(ax + normX * halfWidth, ay + normY * halfWidth);
            Point2D p2 = new Point2D(bx + normX * halfWidth, by + normY * halfWidth);
            Point2D p3 = new Point2D(bx - normX * halfWidth, by - normY * halfWidth);
            Point2D p4 = new Point2D(ax - normX * halfWidth, ay - normY * halfWidth);

            polygon = new Point2D[] { p1, p2, p3, p4 };

            minX = Math.Min(Math.Min(p1.x, p2.x), Math.Min(p3.x, p4.x));
            maxX = Math.Max(Math.Max(p1.x, p2.x), Math.Max(p3.x, p4.x));
            minY = Math.Min(Math.Min(p1.y, p2.y), Math.Min(p3.y, p4.y));
            maxY = Math.Max(Math.Max(p1.y, p2.y), Math.Max(p3.y, p4.y));
        }

        public bool IsPointInside(double x, double y)
        {
            if (x < minX || x > maxX || y < minY || y > maxY) return false;

            // Point in polygon (Ray casting)
            bool inside = false;
            int j = polygon.Length - 1;
            for (int i = 0; i < polygon.Length; j = i++)
            {
                if (((polygon[i].y > y) != (polygon[j].y > y)) &&
                    (x < (polygon[j].x - polygon[i].x) * (y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x))
                {
                    inside = !inside;
                }
            }
            return inside;
        }
    }

    /// <summary>
    /// Out-of-core high-speed streaming LAS corridor cutter.
    /// Cuts massive LAS files (10GB-100GB) into segment files without loading
    /// points into Unity heap, and dispatches completion events per segment.
    /// </summary>
    public static class LasStreamCutter
    {
        public delegate void SegmentCompletedCallback(CutSegmentTask task);
        public delegate void ProgressCallback(float progress, string message);

        public static async Task ExecuteStreamingCutAsync(
            string sourceLasPath,
            List<CutSegmentTask> segmentTasks,
            string province,
            string city,
            string lineName,
            ProgressCallback onProgress,
            SegmentCompletedCallback onSegmentCompleted,
            CancellationToken cancellationToken)
        {
            var meta = LasMetadataDetector.DetectMetadata(sourceLasPath);
            await ExecuteStreamingCutBatchAsync(
                new List<LasImportMetadata> { meta },
                meta,
                segmentTasks,
                province,
                city,
                lineName,
                onProgress,
                onSegmentCompleted,
                cancellationToken
            );
        }

        public static async Task ExecuteStreamingCutBatchAsync(
            List<LasImportMetadata> sourceMetas,
            LasImportMetadata masterCRS,
            List<CutSegmentTask> segmentTasks,
            string province,
            string city,
            string lineName,
            ProgressCallback onProgress,
            SegmentCompletedCallback onSegmentCompleted,
            CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                ExecuteStreamingCutBatchInternal(sourceMetas, masterCRS, segmentTasks, province, city, lineName, onProgress, onSegmentCompleted, cancellationToken);
            }, cancellationToken);
        }

        private static void ExecuteStreamingCutBatchInternal(
            List<LasImportMetadata> sourceMetas,
            LasImportMetadata masterCRS,
            List<CutSegmentTask> segmentTasks,
            string province,
            string city,
            string lineName,
            ProgressCallback onProgress,
            SegmentCompletedCallback onSegmentCompleted,
            CancellationToken cancellationToken)
        {
            if (sourceMetas == null || sourceMetas.Count == 0 || segmentTasks == null || segmentTasks.Count == 0) return;
            if (masterCRS == null) masterCRS = sourceMetas[0];

            int numSegments = segmentTasks.Count;
            foreach (var t in segmentTasks) t.BuildPolygon();

            // Pre-calculate geographic extents for each segment task (WGS84 Lat/Lon)
            var segGeos = new GeoRect[numSegments];
            for (int s = 0; s < numSegments; s++)
            {
                double la1, lo1, la2, lo2;
                GISCoordinateConverter.ProjectedToLatLon(segmentTasks[s].minX, segmentTasks[s].minY, masterCRS, out la1, out lo1);
                GISCoordinateConverter.ProjectedToLatLon(segmentTasks[s].maxX, segmentTasks[s].maxY, masterCRS, out la2, out lo2);
                segGeos[s] = new GeoRect(
                    Math.Min(la1, la2), Math.Max(la1, la2),
                    Math.Min(lo1, lo2), Math.Max(lo1, lo2)
                );
            }

            // Find base valid template LAS file from sourceMetas to extract header template
            string baseLasPath = null;
            foreach (var m in sourceMetas)
            {
                if (File.Exists(m.filePath)) { baseLasPath = m.filePath; break; }
            }
            if (string.IsNullOrEmpty(baseLasPath)) return;

            byte[] headerBuffer = new byte[375];
            byte[] fullHeaderBuffer;
            byte verMin;
            uint offsetToPoints;
            ushort pointRecordLen;
            double targetScaleX, targetScaleY, targetScaleZ;
            double targetOffsetX, targetOffsetY, targetOffsetZ;

            using (var baseFs = new FileStream(baseLasPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536))
            {
                baseFs.Read(headerBuffer, 0, 375);
                string sig = System.Text.Encoding.ASCII.GetString(headerBuffer, 0, 4);
                if (sig != "LASF") throw new InvalidDataException("Invalid LAS signature: " + sig);

                verMin = headerBuffer[25];
                offsetToPoints = BitConverter.ToUInt32(headerBuffer, 96);
                pointRecordLen = BitConverter.ToUInt16(headerBuffer, 105);

                targetScaleX = BitConverter.ToDouble(headerBuffer, 131);
                targetScaleY = BitConverter.ToDouble(headerBuffer, 139);
                targetScaleZ = BitConverter.ToDouble(headerBuffer, 147);
                targetOffsetX = BitConverter.ToDouble(headerBuffer, 155);
                targetOffsetY = BitConverter.ToDouble(headerBuffer, 163);
                targetOffsetZ = BitConverter.ToDouble(headerBuffer, 171);

                if (Math.Abs(targetScaleX) < 1e-7) targetScaleX = 0.0001;
                if (Math.Abs(targetScaleY) < 1e-7) targetScaleY = 0.0001;
                if (Math.Abs(targetScaleZ) < 1e-7) targetScaleZ = 0.0001;

                fullHeaderBuffer = new byte[offsetToPoints];
                baseFs.Seek(0, SeekOrigin.Begin);
                baseFs.Read(fullHeaderBuffer, 0, (int)offsetToPoints);
            }

            // Prepare segment outputs
            var outStreams = new FileStream[numSegments];
            var pointCounts = new int[numSegments];
            var minXs = new double[numSegments];
            var maxXs = new double[numSegments];
            var minYs = new double[numSegments];
            var maxYs = new double[numSegments];
            var minZs = new double[numSegments];
            var maxZs = new double[numSegments];
            var segOffsetXs = new double[numSegments];
            var segOffsetYs = new double[numSegments];
            var segOffsetZs = new double[numSegments];

            // Tower density grids (15m radius, 0.4m cells = 75x75 grid)
            const int gridSize = 75;
            const double gridCell = 0.4;
            const double gridHalf = 15.0;
            var towerAGrids = new int[numSegments, gridSize, gridSize];
            var towerBGrids = new int[numSegments, gridSize, gridSize];
            var towerAMaxZ = new double[numSegments, gridSize, gridSize];
            var towerBMaxZ = new double[numSegments, gridSize, gridSize];

            for (int s = 0; s < numSegments; s++)
            {
                minXs[s] = double.MaxValue; maxXs[s] = double.MinValue;
                minYs[s] = double.MaxValue; maxYs[s] = double.MinValue;
                minZs[s] = double.MaxValue; maxZs[s] = double.MinValue;

                var seg = segmentTasks[s];
                // Anchor each segment's LAS origin to its bounding box min, rounded down to nearest 1000m
                segOffsetXs[s] = Math.Floor(seg.minX / 1000.0) * 1000.0;
                segOffsetYs[s] = Math.Floor(seg.minY / 1000.0) * 1000.0;
                segOffsetZs[s] = 0.0;

                string outDir = Path.GetDirectoryName(segmentTasks[s].outputFilePath);
                if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

                outStreams[s] = new FileStream(segmentTasks[s].outputFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 262144);

                // Create custom header with this segment's specific origin and scale
                byte[] segHeader = new byte[offsetToPoints];
                Array.Copy(fullHeaderBuffer, 0, segHeader, 0, (int)offsetToPoints);
                Array.Copy(BitConverter.GetBytes(targetScaleX), 0, segHeader, 131, 8);
                Array.Copy(BitConverter.GetBytes(targetScaleY), 0, segHeader, 139, 8);
                Array.Copy(BitConverter.GetBytes(targetScaleZ), 0, segHeader, 147, 8);
                Array.Copy(BitConverter.GetBytes(segOffsetXs[s]), 0, segHeader, 155, 8);
                Array.Copy(BitConverter.GetBytes(segOffsetYs[s]), 0, segHeader, 163, 8);
                Array.Copy(BitConverter.GetBytes(segOffsetZs[s]), 0, segHeader, 171, 8);

                outStreams[s].Write(segHeader, 0, (int)offsetToPoints);
            }

            long totalPointsAllFiles = 0;
            foreach (var m in sourceMetas)
            {
                totalPointsAllFiles += (long)m.header.pointCount;
            }
            if (totalPointsAllFiles <= 0) totalPointsAllFiles = 1;

            long totalPointsRead = 0;
            long lastReportPoints = 0;

            // Stream cut from each source file
            for (int fIdx = 0; fIdx < sourceMetas.Count; fIdx++)
            {
                if (cancellationToken.IsCancellationRequested) break;
                var srcMeta = sourceMetas[fIdx];
                if (!File.Exists(srcMeta.filePath)) continue;

                // Source file geographic extents
                GeoRect srcGeo = GISCoordinateConverter.ConvertBoundsToGeoRect(
                    srcMeta.header.minX, srcMeta.header.maxX,
                    srcMeta.header.minY, srcMeta.header.maxY,
                    srcMeta
                );

                // Filter segments that intersect with this source file
                List<int> overlappingSegs = new List<int>();
                for (int s = 0; s < numSegments; s++)
                {
                    var sg = segGeos[s];
                    bool noOverlap = (sg.maxLat < srcGeo.minLat || sg.minLat > srcGeo.maxLat ||
                                      sg.maxLon < srcGeo.minLon || sg.minLon > srcGeo.maxLon);
                    if (!noOverlap)
                    {
                        overlappingSegs.Add(s);
                    }
                }

                if (overlappingSegs.Count == 0)
                {
                    // Skip file completely if no segment overlaps
                    totalPointsRead += (long)srcMeta.header.pointCount;
                    continue;
                }

                bool sameCRS = (srcMeta.selectedCRS == masterCRS.selectedCRS &&
                                srcMeta.centralMeridian == masterCRS.centralMeridian &&
                                srcMeta.utmZone == masterCRS.utmZone);

                // Calculate coarse bounding boxes in source coordinates for overlapping segments
                double[] coarseMinX = new double[overlappingSegs.Count];
                double[] coarseMaxX = new double[overlappingSegs.Count];
                double[] coarseMinY = new double[overlappingSegs.Count];
                double[] coarseMaxY = new double[overlappingSegs.Count];

                for (int i = 0; i < overlappingSegs.Count; i++)
                {
                    int s = overlappingSegs[i];
                    if (sameCRS)
                    {
                        coarseMinX[i] = segmentTasks[s].minX;
                        coarseMaxX[i] = segmentTasks[s].maxX;
                        coarseMinY[i] = segmentTasks[s].minY;
                        coarseMaxY[i] = segmentTasks[s].maxY;
                    }
                    else
                    {
                        // Reproject segment geo corners to source CRS to obtain coarse bounding box
                        double p1x, p1y, p2x, p2y;
                        GISCoordinateConverter.LatLonToProjected(segGeos[s].minLat, segGeos[s].minLon, srcMeta, out p1x, out p1y);
                        GISCoordinateConverter.LatLonToProjected(segGeos[s].maxLat, segGeos[s].maxLon, srcMeta, out p2x, out p2y);
                        coarseMinX[i] = Math.Min(p1x, p2x) - 10.0;
                        coarseMaxX[i] = Math.Max(p1x, p2x) + 10.0;
                        coarseMinY[i] = Math.Min(p1y, p2y) - 10.0;
                        coarseMaxY[i] = Math.Max(p1y, p2y) + 10.0;
                    }
                }

                using (var srcFs = new FileStream(srcMeta.filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 262144))
                {
                    byte[] srcHdr = new byte[375];
                    srcFs.Read(srcHdr, 0, 375);

                    byte srcVerMin = srcHdr[25];
                    uint srcOffsetToPoints = BitConverter.ToUInt32(srcHdr, 96);
                    ushort srcPointRecordLen = BitConverter.ToUInt16(srcHdr, 105);
                    uint srcLegacyCount = BitConverter.ToUInt32(srcHdr, 107);

                    long srcTotalPoints = srcLegacyCount;
                    if (srcTotalPoints == 0 && srcVerMin >= 4)
                    {
                        srcFs.Seek(247, SeekOrigin.Begin);
                        byte[] c8 = new byte[8];
                        srcFs.Read(c8, 0, 8);
                        srcTotalPoints = (long)BitConverter.ToUInt64(c8, 0);
                    }

                    double srcScaleX = BitConverter.ToDouble(srcHdr, 131);
                    double srcScaleY = BitConverter.ToDouble(srcHdr, 139);
                    double srcScaleZ = BitConverter.ToDouble(srcHdr, 147);
                    double srcOffsetX = BitConverter.ToDouble(srcHdr, 155);
                    double srcOffsetY = BitConverter.ToDouble(srcHdr, 163);
                    double srcOffsetZ = BitConverter.ToDouble(srcHdr, 171);

                    srcFs.Seek(srcOffsetToPoints, SeekOrigin.Begin);

                    int batchSize = 16384;
                    int bufferBytes = batchSize * srcPointRecordLen;
                    byte[] readBuffer = new byte[bufferBytes];
                    long srcPointsRead = 0;

                    while (srcPointsRead < srcTotalPoints)
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        int toReadPoints = (int)Math.Min(batchSize, srcTotalPoints - srcPointsRead);
                        int toReadBytes = toReadPoints * srcPointRecordLen;
                        int bytesRead = srcFs.Read(readBuffer, 0, toReadBytes);
                        if (bytesRead <= 0) break;

                        int actualPoints = bytesRead / srcPointRecordLen;

                        for (int i = 0; i < actualPoints; i++)
                        {
                            int pOff = i * srcPointRecordLen;
                            int rawX = BitConverter.ToInt32(readBuffer, pOff);
                            int rawY = BitConverter.ToInt32(readBuffer, pOff + 4);
                            int rawZ = BitConverter.ToInt32(readBuffer, pOff + 8);

                            double sx = rawX * srcScaleX + srcOffsetX;
                            double sy = rawY * srcScaleY + srcOffsetY;
                            double sz = rawZ * srcScaleZ + srcOffsetZ;

                            for (int oi = 0; oi < overlappingSegs.Count; oi++)
                            {
                                int s = overlappingSegs[oi];
                                if (sx < coarseMinX[oi] || sx > coarseMaxX[oi] || sy < coarseMinY[oi] || sy > coarseMaxY[oi])
                                {
                                    continue;
                                }

                                double px, py, pz = sz;
                                if (sameCRS)
                                {
                                    px = sx;
                                    py = sy;
                                }
                                else
                                {
                                    double pLat, pLon;
                                    GISCoordinateConverter.ProjectedToLatLon(sx, sy, srcMeta, out pLat, out pLon);
                                    GISCoordinateConverter.LatLonToProjected(pLat, pLon, masterCRS, out px, out py);
                                }

                                var seg = segmentTasks[s];
                                if (seg.IsPointInside(px, py))
                                {
                                    // Re-encode point coordinates relative to this segment's origin
                                    double dx = (px - segOffsetXs[s]) / targetScaleX;
                                    double dy = (py - segOffsetYs[s]) / targetScaleY;
                                    double dz = (pz - segOffsetZs[s]) / targetScaleZ;

                                    int newRawX = (int)Math.Round(Math.Max(-2000000000.0, Math.Min(2000000000.0, dx)));
                                    int newRawY = (int)Math.Round(Math.Max(-2000000000.0, Math.Min(2000000000.0, dy)));
                                    int newRawZ = (int)Math.Round(Math.Max(-2000000000.0, Math.Min(2000000000.0, dz)));

                                    byte[] ptBytes = new byte[srcPointRecordLen];
                                    Array.Copy(readBuffer, pOff, ptBytes, 0, srcPointRecordLen);
                                    Array.Copy(BitConverter.GetBytes(newRawX), 0, ptBytes, 0, 4);
                                    Array.Copy(BitConverter.GetBytes(newRawY), 0, ptBytes, 4, 4);
                                    Array.Copy(BitConverter.GetBytes(newRawZ), 0, ptBytes, 8, 4);
                                    outStreams[s].Write(ptBytes, 0, srcPointRecordLen);

                                    pointCounts[s]++;

                                    if (px < minXs[s]) minXs[s] = px;
                                    if (px > maxXs[s]) maxXs[s] = px;
                                    if (py < minYs[s]) minYs[s] = py;
                                    if (py > maxYs[s]) maxYs[s] = py;
                                    if (pz < minZs[s]) minZs[s] = pz;
                                    if (pz > maxZs[s]) maxZs[s] = pz;

                                    // Tower A density accumulation
                                    double dAx = px - seg.towerA.easting;
                                    double dAy = py - seg.towerA.northing;
                                    if (Math.Abs(dAx) < gridHalf && Math.Abs(dAy) < gridHalf)
                                    {
                                        int gx = (int)((dAx + gridHalf) / gridCell);
                                        int gy = (int)((dAy + gridHalf) / gridCell);
                                        if (gx >= 0 && gx < gridSize && gy >= 0 && gy < gridSize)
                                        {
                                            towerAGrids[s, gx, gy]++;
                                            if (pz > towerAMaxZ[s, gx, gy]) towerAMaxZ[s, gx, gy] = pz;
                                        }
                                    }

                                    // Tower B density accumulation
                                    double dBx = px - seg.towerB.easting;
                                    double dBy = py - seg.towerB.northing;
                                    if (Math.Abs(dBx) < gridHalf && Math.Abs(dBy) < gridHalf)
                                    {
                                        int gx = (int)((dBx + gridHalf) / gridCell);
                                        int gy = (int)((dBy + gridHalf) / gridCell);
                                        if (gx >= 0 && gx < gridSize && gy >= 0 && gy < gridSize)
                                        {
                                            towerBGrids[s, gx, gy]++;
                                            if (pz > towerBMaxZ[s, gx, gy]) towerBMaxZ[s, gx, gy] = pz;
                                        }
                                    }
                                }
                            }
                        }

                        srcPointsRead += actualPoints;
                        totalPointsRead += actualPoints;

                        if (totalPointsRead - lastReportPoints >= 500000)
                        {
                            lastReportPoints = totalPointsRead;
                            float pct = Mathf.Clamp01((float)totalPointsRead / totalPointsAllFiles);
                            if (onProgress != null)
                            {
                                onProgress(pct, string.Format("流式扫描裁剪中 [{0}/{1}]... {2:P0} ({3:N0} / {4:N0} 点)",
                                    fIdx + 1, sourceMetas.Count, pct, totalPointsRead, totalPointsAllFiles));
                            }
                        }
                    }
                }
            }

            // Finalize each segment, rewrite header, calculate density peak snapping, and trigger callback
            for (int s = 0; s < numSegments; s++)
            {
                var seg = segmentTasks[s];
                var fs = outStreams[s];
                int count = pointCounts[s];

                if (count > 0)
                {
                    fs.Seek(107, SeekOrigin.Begin);
                    fs.Write(BitConverter.GetBytes((uint)count), 0, 4);

                    if (verMin >= 4 && offsetToPoints >= 255)
                    {
                        fs.Seek(247, SeekOrigin.Begin);
                        fs.Write(BitConverter.GetBytes((ulong)count), 0, 8);
                    }

                    // Rewrite Scale & Offset
                    fs.Seek(131, SeekOrigin.Begin);
                    fs.Write(BitConverter.GetBytes(targetScaleX), 0, 8);
                    fs.Write(BitConverter.GetBytes(targetScaleY), 0, 8);
                    fs.Write(BitConverter.GetBytes(targetScaleZ), 0, 8);
                    fs.Write(BitConverter.GetBytes(segOffsetXs[s]), 0, 8);
                    fs.Write(BitConverter.GetBytes(segOffsetYs[s]), 0, 8);
                    fs.Write(BitConverter.GetBytes(segOffsetZs[s]), 0, 8);

                    // Rewrite Bounds
                    fs.Seek(179, SeekOrigin.Begin);
                    fs.Write(BitConverter.GetBytes(maxXs[s]), 0, 8);
                    fs.Write(BitConverter.GetBytes(minXs[s]), 0, 8);
                    fs.Write(BitConverter.GetBytes(maxYs[s]), 0, 8);
                    fs.Write(BitConverter.GetBytes(minYs[s]), 0, 8);
                    fs.Write(BitConverter.GetBytes(maxZs[s]), 0, 8);
                    fs.Write(BitConverter.GetBytes(minZs[s]), 0, 8);

                    seg.pointCount = count;
                    Vector3 center = new Vector3((float)((minXs[s] + maxXs[s]) * 0.5), (float)((minZs[s] + maxZs[s]) * 0.5), (float)((minYs[s] + maxYs[s]) * 0.5));
                    Vector3 size = new Vector3((float)(maxXs[s] - minXs[s]), (float)(maxZs[s] - minZs[s]), (float)(maxYs[s] - minYs[s]));
                    seg.bounds = new Bounds(center, size);

                    // Peak snapping
                    int maxCountA = 0; int bestAx = gridSize / 2; int bestAy = gridSize / 2;
                    for (int gx = 0; gx < gridSize; gx++)
                    {
                        for (int gy = 0; gy < gridSize; gy++)
                        {
                            if (towerAGrids[s, gx, gy] > maxCountA)
                            {
                                maxCountA = towerAGrids[s, gx, gy];
                                bestAx = gx; bestAy = gy;
                            }
                        }
                    }
                    seg.snappedTowerAx = (seg.towerA.easting - gridHalf) + (bestAx + 0.5) * gridCell;
                    seg.snappedTowerAy = (seg.towerA.northing - gridHalf) + (bestAy + 0.5) * gridCell;
                    seg.towerAzTop = towerAMaxZ[s, bestAx, bestAy];

                    int maxCountB = 0; int bestBx = gridSize / 2; int bestBy = gridSize / 2;
                    for (int gx = 0; gx < gridSize; gx++)
                    {
                        for (int gy = 0; gy < gridSize; gy++)
                        {
                            if (towerBGrids[s, gx, gy] > maxCountB)
                            {
                                maxCountB = towerBGrids[s, gx, gy];
                                bestBx = gx; bestBy = gy;
                            }
                        }
                    }
                    seg.snappedTowerBx = (seg.towerB.easting - gridHalf) + (bestBx + 0.5) * gridCell;
                    seg.snappedTowerBy = (seg.towerB.northing - gridHalf) + (bestBy + 0.5) * gridCell;
                    seg.towerBzTop = towerBMaxZ[s, bestBx, bestBy];

                    seg.isCompleted = true;
                }
                else
                {
                    seg.error = "未在走廊范围内检索到点云";
                }

                fs.Flush();
                fs.Close();
                fs.Dispose();

                if (onSegmentCompleted != null)
                {
                    onSegmentCompleted(seg);
                }
            }

            if (onProgress != null)
            {
                onProgress(1.0f, string.Format("✅ 全部 {0} 个点云多源扫描裁剪完成！已提取 {1} 个档距走廊", sourceMetas.Count, numSegments));
            }
        }
    }
}

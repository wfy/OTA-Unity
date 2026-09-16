using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace OTA.Framework
{
    /// <summary>
    /// Minimal but complete LAS 1.2-1.4 reader (point formats 0-10).
    /// Decodes positions (int32 * scale + offset), intensity, classification,
    /// return number and RGB (16-bit -> normalized float).
    /// </summary>
    public static class LasParser
    {
        public struct LasPointData
        {
            public int count;
            public float[] positions;   // xyz interleaved (n*3)
            public float[] colors;      // rgb 0..1 interleaved (n*3), null if no RGB
            public float[] intensities; // n, null if missing
            public byte[] classes;      // n
            public byte[] returnNumber; // n
            public bool hasColor;
            public bool hasIntensity;
            public double minX, minY, minZ, maxX, maxY, maxZ;
            public double centerX, centerY, centerZ;
            public string error;
        }

        public static LasPointData Parse(string lasPath)
        {
            var result = new LasPointData { error = null };
            try
            {
                using (var reader = new BinaryReader(File.OpenRead(lasPath)))
                {
                    // --- Header ------------------------------------------------
                    var signature = new string(reader.ReadChars(4));
                    if (signature != "LASF")
                    {
                        result.error = "Not a LAS file: " + signature;
                        return result;
                    }
                    reader.ReadUInt16();                       // file source id
                    reader.ReadUInt16();                       // global encoding
                    reader.ReadBytes(4);                       // project id 1
                    reader.ReadChars(2);                       // project id 2
                    reader.ReadChars(2);                       // project id 3
                    reader.ReadBytes(8);                       // project id 4
                    byte versionMajor = reader.ReadByte();
                    byte versionMinor = reader.ReadByte();
                    reader.ReadBytes(32);                      // system id
                    reader.ReadBytes(32);                      // generating software
                    reader.ReadUInt16();                       // creation day
                    reader.ReadUInt16();                       // creation year
                    ushort headerSize = reader.ReadUInt16();
                    uint offsetToPointData = reader.ReadUInt32();
                    uint numVlrs = reader.ReadUInt32();

                    if (versionMajor < 1 || (versionMajor == 1 && versionMinor < 2))
                    {
                        result.error = "Only LAS 1.2+ supported, got " + versionMajor + "." + versionMinor;
                        return result;
                    }

                    // Read the rest of the header by ABSOLUTE offsets — the
                    // sequential cursor is not positioned at 104 after VLRs.
                    byte pointFormatRaw;
                    ushort pointRecordLength;
                    long pointCount;
                    reader.BaseStream.Seek(104, SeekOrigin.Begin);
                    pointFormatRaw = reader.ReadByte();
                    pointRecordLength = reader.ReadUInt16();
                    pointCount = reader.ReadUInt32();           // legacy count

                    byte pf = (byte)(pointFormatRaw & 0x3F);
                    bool gpsTime = (pointFormatRaw & 0x80) != 0;

                    if (pointCount == 0 && versionMinor >= 4 && offsetToPointData >= 247)
                    {
                        reader.BaseStream.Seek(247, SeekOrigin.Begin);
                        pointCount = (long)reader.ReadUInt64();
                    }

                    // scale + offset (bytes 131..189) and bounds (179..227)
                    reader.BaseStream.Seek(131, SeekOrigin.Begin);
                    double scaleX = reader.ReadDouble();
                    double scaleY = reader.ReadDouble();
                    double scaleZ = reader.ReadDouble();
                    double offsetX = reader.ReadDouble();
                    double offsetY = reader.ReadDouble();
                    double offsetZ = reader.ReadDouble();

                    // bounds
                    double maxX = reader.ReadDouble();
                    double minX = reader.ReadDouble();
                    double maxY = reader.ReadDouble();
                    double minY = reader.ReadDouble();
                    double maxZ = reader.ReadDouble();
                    double minZ = reader.ReadDouble();
                    result.minX = minX; result.minY = minY; result.minZ = minZ;
                    result.maxX = maxX; result.maxY = maxY; result.maxZ = maxZ;
                    double cx = (minX + maxX) * 0.5;
                    double cy = (minY + maxY) * 0.5;
                    double cz = (minZ + maxZ) * 0.5;
                    result.centerX = cx;
                    result.centerY = cy;
                    result.centerZ = cz;

                    if (pointCount <= 0 || pointCount > 100_000_000)
                    {
                        result.error = "Bad point count: " + pointCount;
                        return result;
                    }

                    reader.BaseStream.Seek(offsetToPointData, SeekOrigin.Begin);

                    int recordLen = pointRecordLength > 0 ? pointRecordLength : RecordLen(pf, gpsTime);
                    var pointBytes = new byte[recordLen];
                    var positions = new float[pointCount * 3];
                    float[] colors = null;
                    float[] intensities = null;
                    var classes = new byte[pointCount];
                    var returnNumbers = new byte[pointCount];

                    bool hasColor = HasColor(pf);
                    if (hasColor) colors = new float[pointCount * 3];
                    intensities = new float[pointCount];
                    int rgbOff = RgbOff(pf, gpsTime);

                    // Auto-detect color bit depth BEFORE decoding: many tools
                    // store 8-bit colors (0..255) in the 16-bit LAS channel
                    // (e.g. 253), others write full 16-bit (0..65280). Scaling
                    // 8-bit values by 65535 would wash them out to ~0 and the
                    // "has color" check below would wrongly drop them.
                    int maxRawRGB = 0;
                    float colorScale = 1f / 65535f;
                    if (hasColor && rgbOff >= 0)
                    {
                        var sampleBytes = new byte[recordLen];
                        int sstep = Mathf.Max(1, (int)(pointCount / 300));
                        for (long i = 0; i < pointCount; i += sstep)
                        {
                            reader.BaseStream.Seek(offsetToPointData + i * recordLen, SeekOrigin.Begin);
                            reader.Read(sampleBytes, 0, recordLen);
                            int rv = BitConverter.ToUInt16(sampleBytes, rgbOff);
                            int gv = BitConverter.ToUInt16(sampleBytes, rgbOff + 2);
                            int bv = BitConverter.ToUInt16(sampleBytes, rgbOff + 4);
                            if (rv > maxRawRGB) maxRawRGB = rv;
                            if (gv > maxRawRGB) maxRawRGB = gv;
                            if (bv > maxRawRGB) maxRawRGB = bv;
                        }
                        if (maxRawRGB > 255) colorScale = 1f / 65535f;
                        else if (maxRawRGB > 1) colorScale = 1f / 255f;
                        else colorScale = 1f;
                        // A declared-RGB cloud only counts as colored when the
                        // channels actually carry data.
                        hasColor = maxRawRGB > 0;
                        if (!hasColor) colors = null;
                    }

                    reader.BaseStream.Seek(offsetToPointData, SeekOrigin.Begin);

                    for (long i = 0; i < pointCount; i++)
                    {
                        reader.Read(pointBytes, 0, recordLen);
                        int idx = (int)(i * 3);
                        double gx = BitConverter.ToInt32(pointBytes, 0) * scaleX + offsetX;
                        double gy = BitConverter.ToInt32(pointBytes, 4) * scaleY + offsetY;
                        double gz = BitConverter.ToInt32(pointBytes, 8) * scaleZ + offsetZ;

                        positions[idx]     = (float)(gx - cx);
                        positions[idx + 1] = (float)(gy - cy);
                        positions[idx + 2] = (float)(gz - cz);

                        classes[i] = (byte)(pointBytes[15] & 0x1F);
                        returnNumbers[i] = (byte)(pointBytes[14] & 0x07);
                        intensities[i] = BitConverter.ToUInt16(pointBytes, 12) / 65535f;
                        if (hasColor && rgbOff >= 0)
                        {
                            int cidx = (int)(i * 3);
                            colors[cidx]     = BitConverter.ToUInt16(pointBytes, rgbOff) * colorScale;
                            colors[cidx + 1] = BitConverter.ToUInt16(pointBytes, rgbOff + 2) * colorScale;
                            colors[cidx + 2] = BitConverter.ToUInt16(pointBytes, rgbOff + 4) * colorScale;
                        }
                    }

                    result.count = (int)pointCount;
                    result.positions = positions;
                    result.colors = colors;
                    result.intensities = intensities;
                    result.classes = classes;
                    result.returnNumber = returnNumbers;
                    result.hasColor = hasColor;
                    result.hasIntensity = true;
                }
            }
            catch (Exception e)
            {
                result.error = e.Message;
            }
            return result;
        }

        private static bool SkipVlr(BinaryReader reader)
        {
            try
            {
                reader.ReadBytes(2);      // reserved
                reader.ReadChars(16);     // user id
                reader.ReadUInt16();      // record id
                uint len = reader.ReadUInt16();
                reader.ReadChars(32);     // description
                reader.BaseStream.Seek(len, SeekOrigin.Current);
                return true;
            }
            catch { return false; }
        }

        private static int RecordLen(byte pf, bool gps)
        {
            switch (pf)
            {
                case 0: case 1: return 20;
                case 2: return 26;
                case 3: return gps ? 34 : 34;
                case 4: return 57;
                case 5: return 63;
                case 6: return 30;
                case 7: return 36;
                case 8: return 38;
                case 9: case 10: return 59;
                default: return 34;
            }
        }

        private static bool HasColor(byte pf)
        {
            return pf == 2 || pf == 3 || pf == 5 || pf == 7 || pf == 8 || pf == 10;
        }

        private static int RgbOff(byte pf, bool gps)
        {
            // LAS 1.2 formats: RGB starts after base(20)+GPS(8) where present.
            // LAS 1.4 formats 7/8/10: RGB at 30/30/60 (bit7 GPS flag set).
            switch (pf)
            {
                case 2: return 20;
                case 3: return gps ? 28 : 28;
                case 5: return 36;
                case 7: return gps ? 30 : 36;
                case 8: return gps ? 30 : 38;
                case 10: return gps ? 60 : 57;
                default: return -1;
            }
        }

        /// <summary>
        /// Convert parsed data into the PointCloudTools RuntimeViewerDX11 V1
        /// .bin byte layout:
        ///   [byte version=1][int32 count][bool hasColor] +
        ///   per point [float x, y, z] + [float r, g, b if hasColor]
        /// Coordinates are written RAW (LAS scale); the renderer applies
        /// offset/flip via its own useManualOffset/manualOffset/flipYZ fields
        /// (matching the 3DTrackPlan_new LasProcessor workflow).
        /// Returns null on failure (check data.error first).
        /// </summary>
        public static byte[] ConvertToV1Bin(ref LasPointData data)
        {
            if (data.error != null || data.count <= 0) return null;

            const int header = 1 + 4 + 1;
            int stride = data.hasColor ? 24 : 12;
            var bytes = new byte[header + data.count * stride];

            bytes[0] = 1;
            BitConverter.GetBytes(data.count).CopyTo(bytes, 1);
            bytes[5] = data.hasColor ? (byte)1 : (byte)0;

            int di = header;
            for (int i = 0; i < data.count; i++)
            {
                int p = i * 3;
                BitConverter.GetBytes(data.positions[p]).CopyTo(bytes, di); di += 4;
                BitConverter.GetBytes(data.positions[p + 1]).CopyTo(bytes, di); di += 4;
                BitConverter.GetBytes(data.positions[p + 2]).CopyTo(bytes, di); di += 4;
                if (data.hasColor)
                {
                    int c = p;
                    BitConverter.GetBytes(data.colors[c]).CopyTo(bytes, di); di += 4;
                    BitConverter.GetBytes(data.colors[c + 1]).CopyTo(bytes, di); di += 4;
                    BitConverter.GetBytes(data.colors[c + 2]).CopyTo(bytes, di); di += 4;
                }
            }
            return bytes;
        }

        public static void SaveToFile(string binPath, byte[] bin)
        {
            File.WriteAllBytes(binPath, bin);
        }
    }
}

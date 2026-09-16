using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace OTA.Corridor
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public class OpenFileName
    {
        public int structSize = 0;
        public IntPtr dlgOwner = IntPtr.Zero;
        public IntPtr instance = IntPtr.Zero;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string filter = null;
        public IntPtr customFilter = IntPtr.Zero;
        public int maxCustFilter = 0;
        public int filterIndex = 0;
        public IntPtr file = IntPtr.Zero;
        public int maxFile = 0;
        public IntPtr fileTitle = IntPtr.Zero;
        public int maxFileTitle = 0;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string initialDir = null;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string title = null;
        public int flags = 0;
        public short fileOffset = 0;
        public short fileExtension = 0;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string defExt = null;
        public IntPtr custData = IntPtr.Zero;
        public IntPtr hook = IntPtr.Zero;
        public IntPtr templateName = IntPtr.Zero;
        public IntPtr reservedPtr = IntPtr.Zero;
        public int reservedInt = 0;
        public int flagsEx = 0;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct BROWSEINFO
    {
        public IntPtr hwndOwner;
        public IntPtr pidlRoot;
        public IntPtr pszDisplayName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpszTitle;
        public uint ulFlags;
        public IntPtr lpfn;
        public IntPtr lParam;
        public int iImage;
    }

    public static class NativeFileBrowserHelper
    {
        [DllImport("Comdlg32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool GetOpenFileName([In, Out] OpenFileName ofn);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SHBrowseForFolder(ref BROWSEINFO bi);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern bool SHGetPathFromIDList(IntPtr pidl, IntPtr pszPath);

        [DllImport("ole32.dll")]
        public static extern void CoTaskMemFree(IntPtr pv);

        public static string OpenFilePanel(string title, string initialDir, string extensionFilter)
        {
            var files = OpenMultipleLasFilesDialog(initialDir, title);
            return (files != null && files.Length > 0) ? files[0] : null;
        }

        public static string OpenLasFileDialog(string initialDir = "E:/unity/点云")
        {
            var files = OpenMultipleLasFilesDialog(initialDir, "选择电力走廊激光点云 (LAS/LAZ)");
            return (files != null && files.Length > 0) ? files[0] : null;
        }

        /// <summary>
        /// Open native Windows file dialog with multi-selection enabled (supports Ctrl / Shift multi-select).
        /// </summary>
        public static string[] OpenMultipleLasFilesDialog(string initialDir = "E:/unity/点云", string title = "选择电力走廊激光点云 (可按住 Ctrl/Shift 多选)")
        {
            OpenFileName ofn = new OpenFileName();
            ofn.structSize = Marshal.SizeOf(ofn);
            ofn.filter = "LiDAR Point Cloud Files (*.las;*.laz)\0*.las;*.laz\0All Files (*.*)\0*.*\0\0";

            // Allocate 128KB character buffer for multi-select (up to 65,536 wide characters)
            int maxChars = 65536;
            int bufferBytes = maxChars * sizeof(char);
            IntPtr fileBufferPtr = Marshal.AllocHGlobal(bufferBytes);

            try
            {
                // Zero out the buffer
                byte[] zeroBytes = new byte[bufferBytes];
                Marshal.Copy(zeroBytes, 0, fileBufferPtr, bufferBytes);

                ofn.file = fileBufferPtr;
                ofn.maxFile = maxChars;
                ofn.initialDir = string.IsNullOrEmpty(initialDir) || !Directory.Exists(initialDir) ? "E:/unity/点云" : initialDir;
                ofn.title = title;
                // OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_ALLOWMULTISELECT | OFN_NOCHANGEDIR
                ofn.flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000200 | 0x00000008;

                if (GetOpenFileName(ofn))
                {
                    List<string> parts = new List<string>();
                    IntPtr cur = fileBufferPtr;
                    int charSize = sizeof(char); // 2 bytes for Unicode
                    while (true)
                    {
                        string s = Marshal.PtrToStringUni(cur);
                        if (string.IsNullOrEmpty(s)) break;
                        parts.Add(s);
                        cur = new IntPtr(cur.ToInt64() + (s.Length + 1) * charSize);
                    }

                    if (parts.Count == 0) return new string[0];

                    if (parts.Count == 1)
                    {
                        // Single file selected: parts[0] is the full path
                        string path = parts[0].Trim();
                        if (File.Exists(path))
                        {
                            Debug.Log($"[NativeFileBrowserHelper] Selected 1 file: {path}");
                            return new string[] { path };
                        }
                        return new string[0];
                    }
                    else
                    {
                        // Multiple files selected: parts[0] is directory, parts[1..N] are filenames
                        string dir = parts[0].Trim();
                        List<string> result = new List<string>();
                        for (int i = 1; i < parts.Count; i++)
                        {
                            string fn = parts[i].Trim();
                            if (!string.IsNullOrEmpty(fn))
                            {
                                string fullPath = Path.Combine(dir, fn);
                                if (File.Exists(fullPath))
                                {
                                    result.Add(fullPath);
                                }
                            }
                        }
                        Debug.Log($"[NativeFileBrowserHelper] Multi-selected {result.Count} files in {dir}");
                        return result.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[NativeFileBrowserHelper] OpenMultipleLasFilesDialog error: " + ex);
            }
            finally
            {
                Marshal.FreeHGlobal(fileBufferPtr);
            }

            return new string[0];
        }

        /// <summary>
        /// Open native Windows folder browser dialog to select a directory.
        /// </summary>
        public static string OpenFolderPanel(string title = "选择点云所在文件夹 (自动导入目录下全部 LAS/LAZ)")
        {
            IntPtr buffer = Marshal.AllocHGlobal(520 * sizeof(char));
            try
            {
                BROWSEINFO bi = new BROWSEINFO();
                bi.lpszTitle = title;
                // BIF_RETURNONLYFSDIRS (0x0001) | BIF_NEWDIALOGSTYLE (0x0040)
                bi.ulFlags = 0x0001 | 0x0040;
                IntPtr pidl = SHBrowseForFolder(ref bi);
                if (pidl != IntPtr.Zero)
                {
                    try
                    {
                        if (SHGetPathFromIDList(pidl, buffer))
                        {
                            string path = Marshal.PtrToStringUni(buffer);
                            Debug.Log($"[NativeFileBrowserHelper] Selected folder: {path}");
                            return path;
                        }
                    }
                    finally
                    {
                        CoTaskMemFree(pidl);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[NativeFileBrowserHelper] Folder picker error: " + ex.Message);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
            return null;
        }

        /// <summary>
        /// Scan a directory for all .las and .laz files.
        /// </summary>
        public static string[] ScanFolderForLas(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath)) return new string[0];
            var list = new List<string>();
            try
            {
                // Top directory files
                list.AddRange(Directory.GetFiles(folderPath, "*.las", SearchOption.TopDirectoryOnly));
                list.AddRange(Directory.GetFiles(folderPath, "*.laz", SearchOption.TopDirectoryOnly));

                // Immediate subdirectories
                foreach (var sub in Directory.GetDirectories(folderPath))
                {
                    try
                    {
                        list.AddRange(Directory.GetFiles(sub, "*.las", SearchOption.TopDirectoryOnly));
                        list.AddRange(Directory.GetFiles(sub, "*.laz", SearchOption.TopDirectoryOnly));
                    }
                    catch { }
                }
                Debug.Log($"[NativeFileBrowserHelper] Scanned folder {folderPath}, found {list.Count} LAS/LAZ files.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[NativeFileBrowserHelper] ScanFolder error: " + ex.Message);
            }
            return list.ToArray();
        }
    }
}
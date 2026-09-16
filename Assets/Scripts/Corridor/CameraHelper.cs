using UnityEngine;

namespace OTA.Corridor
{
    /// <summary>
    /// Universal camera lookup helper.
    /// Handles scenes where the primary camera is tagged 'RightPointCloud' for DX11 point cloud rendering,
    /// preventing Unity's default Camera.main from returning null.
    /// </summary>
    public static class CameraHelper
    {
        private static Camera _cachedCam;

        public static Camera MainCamera
        {
            get
            {
                if (_cachedCam != null && _cachedCam.isActiveAndEnabled)
                {
                    return _cachedCam;
                }

                // 1. Try standard Camera.main
                _cachedCam = Camera.main;
                if (_cachedCam != null) return _cachedCam;

                // 2. Try camera tagged 'RightPointCloud' (PointCloudViewerDX11 standard)
                GameObject rpc = GameObject.FindWithTag("RightPointCloud");
                if (rpc != null)
                {
                    _cachedCam = rpc.GetComponent<Camera>();
                    if (_cachedCam != null) return _cachedCam;
                }

                // 3. Fallback: Any active Camera in scene
                _cachedCam = Object.FindObjectOfType<Camera>();
                return _cachedCam;
            }
        }
    }
}

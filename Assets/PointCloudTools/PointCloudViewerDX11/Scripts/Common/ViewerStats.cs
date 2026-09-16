// example code for getting tile and point count from V3 viewer

using PointCloudHelpers;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace unitycodercom_PointCloudBinaryViewer
{
    public class ViewerStats : MonoBehaviour
    {
        public PointCloudViewerTilesDX11 viewer;
        public Text statsText;
        public bool autoUpdate = false;

        private void Start()
        {
            if (statsText != null && autoUpdate == true)
            {
                StartCoroutine(StatsUpdate());
            }
        }

        void Update()
        {
            // output stats
            if (Input.GetKeyDown(KeyCode.V))
            {
                Debug.Log("Visible tiles:" + viewer.GetVisibleTileCount() + " Visible points:" + viewer.GetVisiblePointCount() + " Total points:" + viewer.GetTotalPointCount());
            }

            // visualize tile bounds
            if (Input.GetKeyDown(KeyCode.B))
            {
                var bounds = viewer.GetAllTileBounds();
                for (int i = 0, len = bounds.Length; i < len; i++)
                {
                    PointCloudTools.DrawBounds(bounds[i], 60);
                }
            }
        }

        IEnumerator StatsUpdate()
        {
            // wait for viewer to be ready
            while(viewer.InitIsReady()==false)
            {
                yield return new WaitForSeconds(2);
            }

            Debug.Log("Start updating stats..");

            while (true)
            {
                statsText.text = "Visible tiles:" + viewer.GetVisibleTileCount() + " Visible points:" + PointCloudTools.HumanReadableCount(viewer.GetVisiblePointCount()) + " Total points:" + PointCloudTools.HumanReadableCount(viewer.GetTotalPointCount());
                yield return new WaitForSeconds(2);
            }
        }

    }
}
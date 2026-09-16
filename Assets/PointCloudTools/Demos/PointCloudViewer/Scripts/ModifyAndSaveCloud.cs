// examples script for accessing current point cloud points, randomizing their positions, then saving into UCPC (v2) format
// this can be useful if you need to modify point cloud, or you have loaded your own point cloud (or runtime parsed cloud) and want to save it into UCPC format for faster loading

using PointCloudConverter;
using PointCloudConverter.Writers;
using System.IO;
using unitycodercom_PointCloudBinaryViewer;
using UnityEngine;

namespace unitycoder_examples
{
    public class ModifyAndSaveCloud : MonoBehaviour
    {
        public PointCloudViewerDX11 binaryViewerDX11;
        bool isSaving = false;

        void Update()
        {
            // demo: press space to modify existing point positions and colors
            // TODO should do exporting in another thread, now it hangs mainthread while processing
            if (Input.GetKeyDown(KeyCode.Space) && isSaving == false)
            {
                isSaving = true;

                // assign export settings
                var importSettings = new ImportSettings();
                importSettings.batch = false;
                importSettings.exportFormat = PointCloudConverter.Structs.ExportFormat.UCPC;

                // output folder is StreamingAssets/
                var outputFile = Path.Combine(Application.streamingAssetsPath, "export.ucpc");
                importSettings.outputFile = outputFile;
                importSettings.packColors = false;
                importSettings.randomize = false;
                importSettings.writer = new UCPC();

                int pointCount = binaryViewerDX11.GetPointCount();
                importSettings.writer.InitWriter(importSettings, pointCount);

                // loop all points
                for (int i = 0, len = pointCount; i < len; i++)
                {
                    // randomize positions a bit
                    binaryViewerDX11.points[i] += Random.insideUnitSphere * 0.25f;
                    // invert color
                    binaryViewerDX11.pointColors[i] = new Vector3(1 - binaryViewerDX11.pointColors[i].x, 1 - binaryViewerDX11.pointColors[i].y, 1 - binaryViewerDX11.pointColors[i].z);

                    // get point and color
                    float x = binaryViewerDX11.points[i].x;
                    float y = binaryViewerDX11.points[i].y;
                    float z = binaryViewerDX11.points[i].z;
                    float r = binaryViewerDX11.pointColors[i].x;
                    float g = binaryViewerDX11.pointColors[i].y;
                    float b = binaryViewerDX11.pointColors[i].z;

                    // send to exporter
                    importSettings.writer.AddPoint(i, x, y, z, r, g, b);
                }

                Debug.Log("(ModifyAndSaveCloud) Saving file: " + outputFile);
                importSettings.writer.Save();

                // update data to gpu (for viewing only, data is saved already to file)
                binaryViewerDX11.UpdatePointData();
                binaryViewerDX11.UpdateColorData();

                isSaving = false;
            }
        }
    }
}
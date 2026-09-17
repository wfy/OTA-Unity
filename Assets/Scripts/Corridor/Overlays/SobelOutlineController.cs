using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using VertexFragment;

namespace OTA.Corridor.Overlays
{
    /// <summary>
    /// Sobel 轮廓边缘检测后处理控制器。
    /// 动态管理 PostProcessLayer、PostProcessVolume 及 SobelOutline 特效参数，为点云 Mesh 或实体提供清晰边界轮廓。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class SobelOutlineController : MonoBehaviour
    {
        [Header("轮廓开关")]
        public bool outlineEnabled = false;

        [Header("轮廓粗细与权重")]
        [Range(0.1f, 5.0f)]
        public float thickness = 1.8f;

        [Range(0.1f, 10.0f)]
        public float depthMultiplier = 2.0f;

        [Range(0.1f, 15.0f)]
        public float depthBias = 8.0f;

        [Range(0.1f, 10.0f)]
        public float normalMultiplier = 1.0f;

        [Range(0.1f, 20.0f)]
        public float normalBias = 10.0f;

        [Header("轮廓颜色")]
        public Color outlineColor = Color.black;

        private PostProcessVolume volume;
        private SobelOutline outlineEffect;

        private bool isQuickVolume = false;

        private bool _prevOutlineEnabled = false;

        void Awake()
        {
            SetupPostProcessing();
            ApplyOutlineState();
        }

        /// <summary>
        /// 同步后处理启用状态，当轮廓关闭时彻底停用 PostProcessLayer 消除深度法线预通道与 Blit 开销
        /// </summary>
        public void ApplyOutlineState()
        {
            _prevOutlineEnabled = outlineEnabled;
            var cam = GetComponent<Camera>();
            if (cam == null) cam = CameraHelper.MainCamera;
            if (cam != null)
            {
                var layer = cam.GetComponent<PostProcessLayer>();
                if (layer != null)
                {
                    layer.enabled = outlineEnabled;
                }
            }

            // if (volume != null)
            // {
            //     volume.weight = outlineEnabled ? 0.2f : 0f;
            // }

            // if (outlineEffect != null)
            // {
            //     outlineEffect.enabled.Override(outlineEnabled);
            //     if (outlineEnabled)
            //     {
            //         outlineEffect.thickness.Override(thickness);
            //         outlineEffect.depthMultiplier.Override(depthMultiplier);
            //         outlineEffect.depthBias.Override(depthBias);
            //         outlineEffect.normalMultiplier.Override(normalMultiplier);
            //         outlineEffect.normalBias.Override(normalBias);
            //         outlineEffect.color.Override(outlineColor);
            //     }
            // }
        }

        /// <summary>
        /// 初始化相机 PostProcessLayer 与全局 PostProcessVolume
        /// </summary>
        public void SetupPostProcessing()
        {
            var cam = GetComponent<Camera>();
            if (cam == null) cam = CameraHelper.MainCamera;

            if (cam != null)
            {
                var layer = cam.GetComponent<PostProcessLayer>();
                if (layer == null)
                {
                    layer = cam.gameObject.AddComponent<PostProcessLayer>();
                    layer.volumeTrigger = cam.transform;
                    layer.volumeLayer = LayerMask.GetMask("Default");
                }
                layer.enabled = outlineEnabled;
            }

            if (volume == null)
            {
                var sceneVolumes = FindObjectsOfType<PostProcessVolume>();
                for (int i = 0; i < sceneVolumes.Length; i++)
                {
                    var v = sceneVolumes[i];
                    if (v.profile != null && v.profile.HasSettings<SobelOutline>())
                    {
                        volume = v;
                        outlineEffect = v.profile.GetSetting<SobelOutline>();
                        isQuickVolume = false;
                        break;
                    }
                    else if (v.sharedProfile != null && v.sharedProfile.HasSettings<SobelOutline>())
                    {
                        volume = v;
                        outlineEffect = v.sharedProfile.GetSetting<SobelOutline>();
                        isQuickVolume = false;
                        break;
                    }
                }

                if (volume == null)
                {
                    outlineEffect = ScriptableObject.CreateInstance<SobelOutline>();
                    outlineEffect.enabled.Override(outlineEnabled);
                    outlineEffect.thickness.Override(thickness);
                    outlineEffect.depthMultiplier.Override(depthMultiplier);
                    outlineEffect.depthBias.Override(depthBias);
                    outlineEffect.normalMultiplier.Override(normalMultiplier);
                    outlineEffect.normalBias.Override(normalBias);
                    outlineEffect.color.Override(outlineColor);

                    volume = PostProcessManager.instance.QuickVolume(gameObject.layer, 100f, outlineEffect);
                    isQuickVolume = true;
                }
            }
        }

        void Update()
        {
            if (outlineEnabled != _prevOutlineEnabled)
            {
                ApplyOutlineState();
            }

            if (!outlineEnabled) return;

            if (outlineEffect != null)
            {
                outlineEffect.thickness.Override(thickness);
                outlineEffect.depthMultiplier.Override(depthMultiplier);
                outlineEffect.depthBias.Override(depthBias);
                outlineEffect.normalMultiplier.Override(normalMultiplier);
                outlineEffect.normalBias.Override(normalBias);
                outlineEffect.color.Override(outlineColor);
            }
        }

        void OnDestroy()
        {
            if (volume != null && isQuickVolume)
            {
                RuntimeUtilities.DestroyVolume(volume, true, true);
                volume = null;
            }
            if (outlineEffect != null && isQuickVolume)
            {
                Destroy(outlineEffect);
                outlineEffect = null;
            }
        }
    }
}

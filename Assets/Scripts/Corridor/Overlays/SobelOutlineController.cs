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
        [Range(0f, 5.0f)]
        public float thickness = 1.0f;

        [Range(0f, 10.0f)]
        public float depthMultiplier = 2.0f;

        [Range(0f, 15.0f)]
        public float depthBias = 10.0f;

        [Range(0f, 10.0f)]
        public float normalMultiplier = 1.0f;

        [Range(0f, 20.0f)]
        public float normalBias = 10.0f;

        [Header("轮廓颜色")]
        public Color outlineColor = Color.black;

        [Header("描边距离与正交范围控制")]
        [Tooltip("透视模式下描边最大生效距离（米）")]
        [Range(1.0f, 500.0f)]
        public float maxDistance = 45.0f;

        [Tooltip("透视模式下描边淡出过渡距离（米）")]
        [Range(0.5f, 100.0f)]
        public float distanceFade = 15.0f;

        [Tooltip("正交模式下描边最大有效正交尺寸 (orthoSize)")]
        [Range(1.0f, 500.0f)]
        public float maxOrthoSize = 45.0f;

        [Tooltip("正交模式下描边淡出过渡尺寸")]
        [Range(0.5f, 100.0f)]
        public float orthoSizeFade = 15.0f;

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
            if (volume == null || outlineEffect == null)
            {
                SetupPostProcessing();
            }

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

            if (volume != null)
            {
                volume.weight = outlineEnabled ? 1f : 0f;
            }

            if (outlineEffect != null)
            {
                outlineEffect.enabled.Override(outlineEnabled);
                if (outlineEnabled)
                {
                    SyncEffectParameters();
                }
            }
        }

        private void SyncEffectParameters()
        {
            if (outlineEffect == null) return;
            float safeThickness = thickness >= 0.1f ? thickness : 1.0f;
            float safeDepthMultiplier = depthMultiplier >= 0.1f ? depthMultiplier : 2.0f;
            float safeDepthBias = depthBias >= 0.5f ? depthBias : 10.0f;
            float safeNormalMultiplier = normalMultiplier >= 0.1f ? normalMultiplier : 1.0f;
            float safeNormalBias = normalBias >= 0.5f ? normalBias : 10.0f;
            float safeMaxDistance = maxDistance >= 1.0f ? maxDistance : 45.0f;
            float safeDistanceFade = distanceFade >= 0.1f ? distanceFade : 15.0f;
            float safeMaxOrthoSize = maxOrthoSize >= 1.0f ? maxOrthoSize : 45.0f;
            float safeOrthoSizeFade = orthoSizeFade >= 0.1f ? orthoSizeFade : 15.0f;

            outlineEffect.thickness.Override(safeThickness);
            outlineEffect.depthMultiplier.Override(safeDepthMultiplier);
            outlineEffect.depthBias.Override(safeDepthBias);
            outlineEffect.normalMultiplier.Override(safeNormalMultiplier);
            outlineEffect.normalBias.Override(safeNormalBias);
            outlineEffect.maxDistance.Override(safeMaxDistance);
            outlineEffect.distanceFade.Override(safeDistanceFade);
            outlineEffect.maxOrthoSize.Override(safeMaxOrthoSize);
            outlineEffect.orthoSizeFade.Override(safeOrthoSizeFade);
            outlineEffect.color.Override(outlineColor);
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
                    layer.volumeLayer = LayerMask.GetMask("Default", "Post");
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
                    SyncEffectParameters();

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
                SyncEffectParameters();
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

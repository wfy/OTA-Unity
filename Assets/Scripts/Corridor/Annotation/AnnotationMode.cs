using System;

namespace OTA.Corridor.Annotation
{
    public enum AnnotationTargetMode
    {
        None,
        Tower,       // 杆塔标定
        Conductor,   // 导线及弧垂标定
        Insulator,   // 绝缘子标定
        Reclassify,  // 选区重分类
        Measure      // 3D 空间测量
    }
}

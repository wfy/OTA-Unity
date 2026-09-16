using System;
using System.Collections.Generic;
using UnityEngine;

namespace OTA.Corridor.UI
{
    /// <summary>
    /// 国家电网输电数字孪生工业科技风设计系统 (State Grid Industrial Tech UI Theme)
    /// 集中管理企业色彩体系、背景贴图、边框渲染与高保真 IMGUI 控件样式。
    /// </summary>
    public static class IndustrialUITheme
    {
        // -------------------------------------------------------------
        // 1. 国家电网标准工业调色板 (SGCC Industrial Palette Tokens)
        // -------------------------------------------------------------
        // 底板主基调 (深墨绿黑)
        public static readonly Color BgBase          = new Color(0.043f, 0.098f, 0.086f, 0.96f); // #0B1916
        public static readonly Color BgSurface       = new Color(0.063f, 0.129f, 0.114f, 0.94f); // #10211D
        public static readonly Color BgCard          = new Color(0.082f, 0.165f, 0.145f, 0.95f); // #152A25
        public static readonly Color BgCardElevated  = new Color(0.106f, 0.208f, 0.184f, 0.96f); // #1B352F
        public static readonly Color BgInput         = new Color(0.055f, 0.110f, 0.098f, 0.96f); // #0E1C19

        // 科技细边框
        public static readonly Color BorderHairline  = new Color(0.114f, 0.220f, 0.200f, 1.0f);  // #1D3833
        public static readonly Color BorderBright    = new Color(0.000f, 0.650f, 0.560f, 1.0f);  // #00A88F (国网绿高光线)
        public static readonly Color BorderSubtle    = new Color(0.090f, 0.170f, 0.150f, 1.0f);  // #172B26

        // 业务语义特征色
        public static readonly Color SgccGreen       = new Color(0.000f, 0.518f, 0.439f, 1.0f);  // #008470 (国网品牌绿)
        public static readonly Color SgccEmerald     = new Color(0.000f, 0.700f, 0.530f, 1.0f);  // #00B388 (在线 / 合规通过)
        public static readonly Color VoltageGold     = new Color(0.961f, 0.651f, 0.137f, 1.0f);  // #F5A623 (高压金 / 杆塔标识)
        public static readonly Color ConductorCyan   = new Color(0.000f, 0.824f, 0.706f, 1.0f);  // #00D2B4 (输电导线青)
        public static readonly Color AlertRed        = new Color(0.937f, 0.267f, 0.267f, 1.0f);  // #EF4444 (越限告警红)
        public static readonly Color AccentBlue      = new Color(0.150f, 0.450f, 0.850f, 1.0f);  // #2673D9 (辅助蓝)

        // 文字层次
        public static readonly Color TextPrimary     = new Color(0.960f, 0.980f, 0.970f, 1.0f);  // 纯白微冷
        public static readonly Color TextSecondary   = new Color(0.650f, 0.750f, 0.720f, 1.0f);  // 浅灰绿说明
        public static readonly Color TextDim         = new Color(0.420f, 0.530f, 0.500f, 1.0f);  // 注解单位灰
        public static readonly Color TextMuted       = new Color(0.300f, 0.400f, 0.380f, 1.0f);

        // -------------------------------------------------------------
        // 2. 贴图纹理资源缓存 (Textures Cache)
        // -------------------------------------------------------------
        private static Texture2D texBgBase;
        private static Texture2D texBgSurface;
        private static Texture2D texBgCard;
        private static Texture2D texBgCardElevated;
        private static Texture2D texBorderHairline;
        private static Texture2D texBorderBright;
        private static Texture2D texSgccGreen;
        private static Texture2D texVoltageGold;
        private static Texture2D texBtnNormal;
        private static Texture2D texBtnHover;
        private static Texture2D texBtnActive;
        private static Texture2D texOverlayDark;

        private static readonly Dictionary<string, Texture2D> colorTexCache = new Dictionary<string, Texture2D>();

        public static Texture2D GetSolidTexture(Color col)
        {
            string key = string.Format("{0}_{1}_{2}_{3}", col.r, col.g, col.b, col.a);
            if (!colorTexCache.TryGetValue(key, out var tex) || tex == null)
            {
                tex = new Texture2D(1, 1);
                tex.SetPixel(0, 0, col);
                tex.Apply();
                colorTexCache[key] = tex;
            }
            return tex;
        }

        // -------------------------------------------------------------
        // 3. 样式定义 (GUIStyle Definitions)
        // -------------------------------------------------------------
        public static GUIStyle PanelStyle;
        public static GUIStyle CardStyle;
        public static GUIStyle CardElevatedStyle;
        public static GUIStyle HeaderTitleStyle;
        public static GUIStyle SectionHeaderStyle;
        public static GUIStyle SubHeaderStyle;
        public static GUIStyle BodyLabelStyle;
        public static GUIStyle DimLabelStyle;
        public static GUIStyle UnitLabelStyle;
        public static GUIStyle TechButtonStyle;
        public static GUIStyle ActiveButtonStyle;
        public static GUIStyle GoldButtonStyle;
        public static GUIStyle DangerButtonStyle;
        public static GUIStyle TagBadgeStyle;
        public static GUIStyle TickerTextStyle;
        public static GUIStyle TechHeaderStyle => SectionHeaderStyle;

        private static bool isInitialized = false;

        public static void EnsureTextures()
        {
            if (texBgBase != null) return;
            texBgBase = GetSolidTexture(BgBase);
            texBgSurface = GetSolidTexture(BgSurface);
            texBgCard = GetSolidTexture(BgCard);
            texBgCardElevated = GetSolidTexture(BgCardElevated);
            texBorderHairline = GetSolidTexture(BorderHairline);
            texBorderBright = GetSolidTexture(BorderBright);
            texSgccGreen = GetSolidTexture(SgccGreen);
            texVoltageGold = GetSolidTexture(VoltageGold);
            texBtnNormal = GetSolidTexture(new Color(0.08f, 0.16f, 0.14f, 0.95f));
            texBtnHover = GetSolidTexture(new Color(0.11f, 0.22f, 0.19f, 0.98f));
            texBtnActive = GetSolidTexture(new Color(0.00f, 0.55f, 0.45f, 1.0f));
            texOverlayDark = GetSolidTexture(new Color(0.02f, 0.05f, 0.04f, 0.85f));
        }

        public static void EnsureInitialized()
        {
            EnsureTextures();

            if (isInitialized && PanelStyle != null) return;
            if (Event.current == null) return; // GUI.skin can only be called from inside OnGUI

            isInitialized = true;

            // 2. 确保系统字体接入
            SystemFontHelper.EnsureSkinFont(GUI.skin, 12);
            Font cnFont = SystemFontHelper.GetChineseFont(12);

            // 3. 基础容器样式
            PanelStyle = new GUIStyle(GUI.skin.box);
            PanelStyle.normal.background = texBgSurface;
            PanelStyle.border = new RectOffset(1, 1, 1, 1);
            PanelStyle.margin = new RectOffset(0, 0, 0, 0);
            PanelStyle.padding = new RectOffset(8, 8, 8, 8);

            CardStyle = new GUIStyle(GUI.skin.box);
            CardStyle.normal.background = texBgCard;
            CardStyle.border = new RectOffset(1, 1, 1, 1);
            CardStyle.margin = new RectOffset(2, 2, 4, 4);
            CardStyle.padding = new RectOffset(8, 8, 6, 6);

            CardElevatedStyle = new GUIStyle(GUI.skin.box);
            CardElevatedStyle.normal.background = texBgCardElevated;
            CardElevatedStyle.border = new RectOffset(1, 1, 1, 1);
            CardElevatedStyle.padding = new RectOffset(6, 6, 6, 6);

            // 4. 文字层次样式
            HeaderTitleStyle = new GUIStyle(GUI.skin.label);
            if (cnFont != null) HeaderTitleStyle.font = cnFont;
            HeaderTitleStyle.fontSize = 14;
            HeaderTitleStyle.fontStyle = FontStyle.Bold;
            HeaderTitleStyle.normal.textColor = TextPrimary;

            SectionHeaderStyle = new GUIStyle(GUI.skin.label);
            if (cnFont != null) SectionHeaderStyle.font = cnFont;
            SectionHeaderStyle.fontSize = 12;
            SectionHeaderStyle.fontStyle = FontStyle.Bold;
            SectionHeaderStyle.normal.textColor = SgccEmerald;

            SubHeaderStyle = new GUIStyle(GUI.skin.label);
            if (cnFont != null) SubHeaderStyle.font = cnFont;
            SubHeaderStyle.fontSize = 12;
            SubHeaderStyle.fontStyle = FontStyle.Bold;
            SubHeaderStyle.normal.textColor = VoltageGold;

            BodyLabelStyle = new GUIStyle(GUI.skin.label);
            if (cnFont != null) BodyLabelStyle.font = cnFont;
            BodyLabelStyle.fontSize = 12;
            BodyLabelStyle.normal.textColor = TextPrimary;

            DimLabelStyle = new GUIStyle(GUI.skin.label);
            if (cnFont != null) DimLabelStyle.font = cnFont;
            DimLabelStyle.fontSize = 11;
            DimLabelStyle.normal.textColor = TextSecondary;

            UnitLabelStyle = new GUIStyle(GUI.skin.label);
            if (cnFont != null) UnitLabelStyle.font = cnFont;
            UnitLabelStyle.fontSize = 11;
            UnitLabelStyle.normal.textColor = TextDim;

            TickerTextStyle = new GUIStyle(GUI.skin.label);
            if (cnFont != null) TickerTextStyle.font = cnFont;
            TickerTextStyle.fontSize = 11;
            TickerTextStyle.fontStyle = FontStyle.Normal;
            TickerTextStyle.normal.textColor = SgccEmerald;

            // 5. 按钮样式
            TechButtonStyle = new GUIStyle(GUI.skin.button);
            if (cnFont != null) TechButtonStyle.font = cnFont;
            TechButtonStyle.fontSize = 12;
            TechButtonStyle.normal.textColor = TextPrimary;
            TechButtonStyle.normal.background = texBtnNormal;
            TechButtonStyle.hover.background = texBtnHover;
            TechButtonStyle.hover.textColor = Color.white;
            TechButtonStyle.active.background = texBtnActive;
            TechButtonStyle.active.textColor = Color.white;
            TechButtonStyle.border = new RectOffset(2, 2, 2, 2);

            ActiveButtonStyle = new GUIStyle(TechButtonStyle);
            ActiveButtonStyle.normal.background = texSgccGreen;
            ActiveButtonStyle.normal.textColor = Color.white;
            ActiveButtonStyle.fontStyle = FontStyle.Bold;

            GoldButtonStyle = new GUIStyle(TechButtonStyle);
            GoldButtonStyle.normal.background = GetSolidTexture(new Color(0.70f, 0.45f, 0.05f, 0.95f));
            GoldButtonStyle.normal.textColor = Color.white;

            DangerButtonStyle = new GUIStyle(TechButtonStyle);
            DangerButtonStyle.normal.background = GetSolidTexture(new Color(0.65f, 0.15f, 0.15f, 0.95f));
            DangerButtonStyle.normal.textColor = Color.white;

            TagBadgeStyle = new GUIStyle(GUI.skin.label);
            if (cnFont != null) TagBadgeStyle.font = cnFont;
            TagBadgeStyle.fontSize = 10;
            TagBadgeStyle.fontStyle = FontStyle.Bold;
            TagBadgeStyle.alignment = TextAnchor.MiddleCenter;
            TagBadgeStyle.normal.textColor = TextPrimary;
        }

        // -------------------------------------------------------------
        // 4. 绘图辅助函数 (Drawing Helpers)
        // -------------------------------------------------------------
        /// <summary>
        /// 绘制带边框的工业科技背景板
        /// </summary>
        public static void DrawFrame(Rect r, Color bgColor, Color borderColor, float borderWidth = 1f)
        {
            // 底板
            GUI.DrawTexture(r, GetSolidTexture(bgColor));

            // 四边极细边框
            if (borderWidth > 0f)
            {
                Texture2D bTex = GetSolidTexture(borderColor);
                GUI.DrawTexture(new Rect(r.x, r.y, r.width, borderWidth), bTex);                         // Top
                GUI.DrawTexture(new Rect(r.x, r.y + r.height - borderWidth, r.width, borderWidth), bTex); // Bottom
                GUI.DrawTexture(new Rect(r.x, r.y, borderWidth, r.height), bTex);                         // Left
                GUI.DrawTexture(new Rect(r.x + r.width - borderWidth, r.y, borderWidth, r.height), bTex); // Right
            }
        }

        /// <summary>
        /// 绘制水平发光分割线
        /// </summary>
        public static void DrawHairline(float height = 1f, Color? color = null)
        {
            Color c = color ?? BorderHairline;
            Rect r = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(height), GUILayout.ExpandWidth(true));
            GUI.DrawTexture(r, GetSolidTexture(c));
        }

        /// <summary>
        /// 绘制小巧的指标状态胶囊 (Capsule Badge) - GUILayout 版本
        /// </summary>
        public static void DrawBadge(string text, Color bgColor, Color textColor, float width = 0f, float height = 18f)
        {
            GUILayoutOption[] opts = width > 0f 
                ? new GUILayoutOption[] { GUILayout.Width(width), GUILayout.Height(height) }
                : new GUILayoutOption[] { GUILayout.Height(height) };

            Rect r = GUILayoutUtility.GetRect(new GUIContent(text), TagBadgeStyle, opts);
            DrawBadge(r, text, bgColor, textColor);
        }

        /// <summary>
        /// 绘制小巧的指标状态胶囊 (Capsule Badge) - 绝对坐标版本
        /// </summary>
        public static void DrawBadge(Rect r, string text, Color bgColor, Color textColor)
        {
            DrawFrame(r, bgColor, new Color(bgColor.r * 1.3f, bgColor.g * 1.3f, bgColor.b * 1.3f, 1f), 1f);
            
            Color oldCol = TagBadgeStyle.normal.textColor;
            TagBadgeStyle.normal.textColor = textColor;
            GUI.Label(r, text, TagBadgeStyle);
            TagBadgeStyle.normal.textColor = oldCol;
        }

        /// <summary>
        /// 绘制三维世界空间原位标注广告牌卡片 (3D In-World Leader Dimension Card)
        /// </summary>
        public static void Draw3DWorldCard(Vector2 screenCenter, string title, string line1, string line2, string line3)
        {
            float w = 220f;
            float h = 64f;
            Rect r = new Rect(screenCenter.x - w * 0.5f, screenCenter.y - h - 14f, w, h);

            // 绘制卡片底板与边框 (国网深墨绿 + 国网绿发光边框)
            DrawFrame(r, new Color(0.04f, 0.11f, 0.09f, 0.92f), BorderBright, 1.2f);

            // 标头微条
            Rect headR = new Rect(r.x, r.y, r.width, 18f);
            GUI.DrawTexture(headR, GetSolidTexture(new Color(0.00f, 0.45f, 0.38f, 0.5f)));

            // 标题
            GUI.Label(new Rect(r.x + 6, r.y + 1, r.width - 12, 16), title, SubHeaderStyle);

            // 三行数据
            if (!string.IsNullOrEmpty(line1))
                GUI.Label(new Rect(r.x + 8, r.y + 19, r.width - 16, 15), line1, BodyLabelStyle);
            if (!string.IsNullOrEmpty(line2))
                GUI.Label(new Rect(r.x + 8, r.y + 34, r.width - 16, 15), line2, DimLabelStyle);
            if (!string.IsNullOrEmpty(line3))
                GUI.Label(new Rect(r.x + 8, r.y + 48, r.width - 16, 15), line3, UnitLabelStyle);

            // 下方引出线小三角锚点
            Texture2D anchorTex = GetSolidTexture(BorderBright);
            GUI.DrawTexture(new Rect(screenCenter.x - 1f, r.y + h, 2f, 14f), anchorTex);
        }
    }
}

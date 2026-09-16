using System;
using System.Collections.Generic;
using UnityEngine;

namespace OTA.Corridor.UI
{
    public static class SystemFontHelper
    {
        private static Font defaultChineseFont = null;
        private static bool fontInitialized = false;

        private static readonly string[] FontFallbacks = new string[]
        {
            "Microsoft YaHei",
            "SimHei",
            "PingFang SC",
            "WenQuanYi Micro Hei",
            "SimSun",
            "Arial"
        };

        public static Font GetChineseFont(int fontSize = 12)
        {
            if (defaultChineseFont == null && !fontInitialized)
            {
                fontInitialized = true;
                try
                {
                    defaultChineseFont = Font.CreateDynamicFontFromOSFont(FontFallbacks, fontSize);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[SystemFontHelper] Failed to load OS font: " + ex.Message);
                }
            }
            return defaultChineseFont;
        }

        public static void EnsureSkinFont(GUISkin skin, int baseFontSize = 12)
        {
            if (skin == null) return;
            var font = GetChineseFont(baseFontSize);
            if (font == null) return;

            if (skin.font == null || skin.font.name != font.name)
            {
                skin.font = font;
            }

            skin.label.font = font;
            skin.button.font = font;
            skin.box.font = font;
            skin.textField.font = font;
            skin.toggle.font = font;
            skin.window.font = font;
        }

        public static GUIStyle CreateChineseLabel(int fontSize = 12, bool bold = false, Color? color = null)
        {
            var style = new GUIStyle(GUI.skin.label);
            var f = GetChineseFont(fontSize);
            if (f != null) style.font = f;
            style.fontSize = fontSize;
            style.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            if (color.HasValue) style.normal.textColor = color.Value;
            return style;
        }

        public static GUIStyle CreateChineseButton(int fontSize = 12, bool bold = false)
        {
            var style = new GUIStyle(GUI.skin.button);
            var f = GetChineseFont(fontSize);
            if (f != null) style.font = f;
            style.fontSize = fontSize;
            style.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            return style;
        }
    }
}

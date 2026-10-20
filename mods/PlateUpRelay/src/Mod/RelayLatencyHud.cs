using System.Globalization;
using HarmonyLib;
using Kitchen;
using TMPro;
using UnityEngine;

namespace PlateUpRelay
{
    /// <summary>随游戏右下角版本文字创建延迟行，保持与原生界面相同的显示生命周期。</summary>
    [HarmonyPatch(typeof(DisplayVersion), "Awake")]
    internal static class RelayLatencyHudPatch
    {
        /// <summary>只给实际版本文字所在对象挂载一次 HUD。</summary>
        private static void Postfix(DisplayVersion __instance)
        {
            if (__instance.Text != null && __instance.GetComponent<RelayLatencyHud>() == null)
                __instance.gameObject.AddComponent<RelayLatencyHud>().Initialise(__instance);
        }
    }

    /// <summary>版本文字上方的一行实时往返延迟；字号、字体、颜色、缩放跟随原文字。</summary>
    public sealed class RelayLatencyHud : MonoBehaviour
    {
        private DisplayVersion version;
        private TextMeshPro label;
        private float nextRefresh;

        /// <summary>创建独立文本对象，避免复制版本组件后递归触发创建补丁。</summary>
        /// <param name="owner">原生版本文字组件。</param>
        internal void Initialise(DisplayVersion owner)
        {
            version = owner;
            var textObject = new GameObject("PlateUpRelay.Latency", typeof(RectTransform), typeof(TextMeshPro));
            textObject.layer = owner.Text.gameObject.layer;
            label = textObject.GetComponent<TextMeshPro>();
            label.text = "";
            label.enableAutoSizing = false;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.transform.SetParent(owner.Text.transform.parent, false);
            Debug.Log("[PlateUpRelay] Latency HUD attached above the game version.");
        }

        /// <summary>复用版本文字的局部布局；仅显示已测得的样本，空值显示短横线。</summary>
        private void LateUpdate()
        {
            if (version == null || version.Text == null || label == null || Time.unscaledTime < nextRefresh)
                return;
            nextRefresh = Time.unscaledTime + 0.25f;
            TextMeshPro source = version.Text;
            label.enabled = source.enabled && source.gameObject.activeInHierarchy;
            if (!label.enabled || source.textInfo.lineCount == 0)
                return;
            label.font = source.font;
            label.fontSharedMaterial = source.fontSharedMaterial;
            label.fontSize = source.fontSize;
            label.fontStyle = source.fontStyle;
            label.color = source.color;
            label.alignment = source.alignment;
            label.characterSpacing = source.characterSpacing;
            label.wordSpacing = source.wordSpacing;
            label.margin = source.margin;
            label.isOrthographic = source.isOrthographic;
            var target = label.rectTransform;
            var origin = source.rectTransform;
            target.anchorMin = origin.anchorMin;
            target.anchorMax = origin.anchorMax;
            target.pivot = origin.pivot;
            target.sizeDelta = origin.sizeDelta;
            target.localRotation = origin.localRotation;
            target.localScale = origin.localScale;
            // lineHeight 是 TMP 在文本局部坐标中实际计算的行高，不按屏幕像素猜测字号。
            float height = source.textInfo.lineInfo[0].lineHeight;
            target.localPosition = origin.localPosition + Vector3.up * height * origin.localScale.y;
            var sourceRenderer = source.GetComponent<MeshRenderer>();
            var targetRenderer = label.GetComponent<MeshRenderer>();
            targetRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            targetRenderer.sortingOrder = sourceRenderer.sortingOrder;
            RelaySteamTransport.GetLatency(out int? server, out int? room);
            label.text = "Server " + (server?.ToString(CultureInfo.InvariantCulture) ?? "-") +
                " ms, Room " + (room?.ToString(CultureInfo.InvariantCulture) ?? "-") + " ms";
        }

        /// <summary>释放自建文本；字体和材质由游戏拥有，不销毁共享资源。</summary>
        private void OnDestroy()
        {
            if (label != null)
                Destroy(label.gameObject);
        }
    }
}

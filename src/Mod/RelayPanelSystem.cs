using System;
using Kitchen;
using KitchenMods;
using Unity.Entities;
using UnityEngine;

namespace PlateUpRelay
{
    /// <summary>由游戏注入的 ECS 系统拥有面板；随游戏世界创建、更新和释放。</summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public sealed class RelayPanelSystem : GenericSystemBase, IModSystem
    {
        private GameObject panelObject;
        private RelayPanel panel;

        protected override void OnUpdate()
        {
            try
            {
                if (panel == null)
                {
                    ReleasePanel();
                    panelObject = new GameObject("PlateUpRelay.Settings");
                    panel = panelObject.AddComponent<RelayPanel>();
                    Debug.Log("[PlateUpRelay] UI system created the settings panel in the game world.");
                }
                panel.PollShortcut();
            }
            catch (Exception ex)
            {
                // 初始化或输入接口失败时停止本系统，避免每帧异常拖慢整个游戏。
                Enabled = false;
                ReleasePanel();
                Debug.LogError("[PlateUpRelay] UI system disabled after an error: " + ex);
            }
        }

        protected override void OnDestroy()
        {
            ReleasePanel();
            base.OnDestroy();
        }

        private void ReleasePanel()
        {
            if (panelObject != null)
            {
                panelObject.SetActive(false);
                UnityEngine.Object.Destroy(panelObject);
            }
            panel = null;
            panelObject = null;
        }
    }
}

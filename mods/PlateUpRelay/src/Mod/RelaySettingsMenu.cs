using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Kitchen;
using Kitchen.Modules;
using UnityEngine;

namespace PlateUpRelay
{
    /// <summary>在游戏设置中注册中转子菜单，覆盖主菜单与游戏内暂停菜单。</summary>
    [HarmonyPatch(typeof(OptionsMenu<MenuAction>), nameof(OptionsMenu<MenuAction>.CreateSubmenus))]
    internal static class RelayMenuRegistrationPatch
    {
        /// <summary>随原生设置菜单一起创建，并交由 LocalMenuView 管理生命周期。</summary>
        private static void Postfix(OptionsMenu<MenuAction> __instance, ref Dictionary<Type, Menu<MenuAction>> menus)
        {
            menus.Add(typeof(RelaySettingsMenu), new RelaySettingsMenu(__instance.Container, __instance.ModuleList));
        }
    }

    /// <summary>在设置列表开头加入中转入口，使用游戏自身的导航与按钮样式。</summary>
    [HarmonyPatch(typeof(OptionsMenu<MenuAction>), nameof(OptionsMenu<MenuAction>.Setup))]
    internal static class RelayMenuEntryPatch
    {
        private static readonly MethodInfo AddSubmenu = AccessTools.Method(typeof(Menu<MenuAction>), "AddSubmenuButton");

        /// <summary>调用原生受保护方法，使按钮与其他设置保持一致。</summary>
        private static void Prefix(OptionsMenu<MenuAction> __instance)
        {
            AddSubmenu.Invoke(__instance, new object[] { RelayText.Title.Value, typeof(RelaySettingsMenu), false });
        }
    }

    /// <summary>使用原生文本输入保存中转地址；设置生效和实际网络连接分别显示。</summary>
    internal sealed class RelaySettingsMenu : Menu<MenuAction>
    {
        private string input;
        private RelayText feedback;
        private bool active;
        private ButtonElement addressButton;
        private ButtonElement saveButton;
        private ButtonElement disableButton;
        private LabelElement serverStatus;
        private LabelElement roomStatus;
        private LabelElement information;

        /// <summary>复用游戏菜单容器与布局列表。</summary>
        /// <param name="container">原生菜单的控件容器。</param>
        /// <param name="modules">原生菜单的布局与输入列表。</param>
        public RelaySettingsMenu(Transform container, ModuleList modules) : base(container, modules) { }

        /// <inheritdoc />
        public override void Setup(int player_id)
        {
            active = true;
            input = RelaySettings.Address;
            feedback = default;
            // 标题与提示沿用原生设置的 LabelElement，避免标题预制体的类型转换异常。
            AddLabel(RelayText.Title.Value);
            addressButton = AddButton(RelayText.EditAddress(input).Value, _ => EditAddress());
            saveButton = AddButton(RelayText.SaveAndEnable.Value, _ => Apply(input));
            disableButton = AddButton(RelayText.DisableRelay.Value, _ => Apply("", disable: true));
            serverStatus = AddLabel("").SetSize(7f, 0.7f);
            roomStatus = AddLabel("").SetSize(7f, 0.7f);
            information = AddLabel("").SetSize(7f, 1f);
            AddButton(RelayText.Back.Value, _ => RequestPreviousMenu());
            Update();
        }

        /// <inheritdoc />
        public override void Update()
        {
            if (!active)
                return;
            bool busy = RelaySteamTransport.HasRemotePlayers;
            addressButton.SetSelectable(!busy);
            saveButton.SetSelectable(!busy);
            disableButton.SetSelectable(!busy);
            addressButton.SetLabel(RelayText.EditAddress(input).Value);
            serverStatus.SetLabel(RelayText.ServerStatusLabel.Value + RelaySettings.Status.Value);
            roomStatus.SetLabel(RelayText.RoomStatusLabel.Value + RelaySteamTransport.RoomStatus.Value);
            information.SetLabel(busy ? RelayText.LeaveLobbyToChange.Value : feedback.Value ?? RelayText.InviteHint.Value);
        }

        /// <inheritdoc />
        public override void TearDown()
        {
            active = false;
        }

        /// <summary>使用游戏原生输入锁和粘贴处理，取消输入时保留原来的草稿。</summary>
        private void EditAddress()
        {
            if (RelaySteamTransport.HasRemotePlayers)
                return;
            if (TextInputView.Main == null || TextInputView.Main.State == TextInputView.TextInputState.EnteringText)
            {
                feedback = RelayText.InputUnavailable;
                return;
            }
            TextInputView.RequestTextInput(RelayText.AddressLabel.Value, input, 64, (state, value) =>
            {
                if (active && state == TextInputView.TextInputState.TextEntryComplete)
                {
                    input = value;
                    feedback = default;
                }
            });
        }

        /// <summary>校验并提交配置，失败时保留旧连接和用户输入。</summary>
        /// <param name="value">待保存的地址。</param>
        /// <param name="disable">仅明确关闭操作允许空地址。</param>
        private void Apply(string value, bool disable = false)
        {
            try
            {
                if (!disable && string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException(RelayText.EnterAddress.Value);
                RelaySettings.Save(value);
                input = RelaySettings.Address;
                feedback = RelaySettings.Enabled ? RelayText.Saved : RelayText.Disabled;
            }
            catch (Exception ex)
            {
                feedback = new RelayText(ex.Message, ex.Message);
                Debug.LogWarning("[PlateUpRelay] Settings: " + ex.Message);
            }
        }
    }
}

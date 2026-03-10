using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Blish_HUD.Settings;
using Blish_HUD.Settings.UI.Views;
using Microsoft.Xna.Framework;
using System;
using System.Linq;

namespace BhModule.PathingCategoryExplorerPlugin
{
    public class ModuleSettings
    {
        readonly PathingCategoryExplorerPluginModule _module;
        public SettingEntry<bool> AddDeselectRecursively { get; private set; }
        public SettingEntry<bool> AddSelectRecursively { get; private set; }
        public SettingEntry<bool> AddDeselectAllOthers { get; private set; }
        public SettingEntry<bool> FixNodeExpansionBug { get; private set; }
        public ModuleSettings(PathingCategoryExplorerPluginModule module, SettingCollection settings)
        {
            this._module = module;
            AddDeselectRecursively = settings.DefineSetting(nameof(this.AddDeselectRecursively), true, () => "Add Deselect Recursively", () => "");
            AddSelectRecursively = settings.DefineSetting(nameof(this.AddSelectRecursively), true, () => "Add Select Recursively", () => "");
            AddDeselectAllOthers = settings.DefineSetting(nameof(this.AddDeselectAllOthers), true, () => "Add Deselect All Othres", () => "");
            FixNodeExpansionBug = settings.DefineSetting(nameof(this.FixNodeExpansionBug), true, () => "Fix Node Expansion Bug", () => "Fix crash when checking a node after parent re-expansion.");
        }
        public void Unload()
        {
            PathingCategoryExplorerPluginSettingsView.DisposeRootFlowPanel?.Invoke();
        }
    }
    public class PathingCategoryExplorerPluginSettingsView(SettingCollection settings) : View
    {
        static public Action UpadateForgeIntervalTitle;
        static public Action DisposeRootFlowPanel;
        FlowPanel rootFlowPanel;
        readonly SettingCollection settings = settings;
        protected override void Build(Container buildPanel)
        {
            DisposeRootFlowPanel?.Invoke();
            rootFlowPanel = new FlowPanel()
            {
                Size = buildPanel.Size,
                FlowDirection = ControlFlowDirection.LeftToRight,
                ControlPadding = new Vector2(5, 2),
                OuterControlPadding = new Vector2(10, 15),
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.AutoSize,
                AutoSizePadding = new Point(0, 15),
                Parent = buildPanel
            };
            DisposeRootFlowPanel = () =>
            {
                DisposeRootFlowPanel = null;
                rootFlowPanel.Dispose();
            };
            foreach (var setting in settings.Where(s => s.SessionDefined))
            {
                IView settingView;

                if ((settingView = SettingView.FromType(setting, rootFlowPanel.Width)) != null)
                {
                    ViewContainer container = new()
                    {
                        WidthSizingMode = SizingMode.Fill,
                        HeightSizingMode = SizingMode.AutoSize,
                        Parent = rootFlowPanel
                    };
                    container.Show(settingView);
                }
            }
        }
    }
}

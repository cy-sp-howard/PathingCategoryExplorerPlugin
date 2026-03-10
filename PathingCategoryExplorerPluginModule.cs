using Blish_HUD;
using Blish_HUD.Graphics.UI;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using Microsoft.Xna.Framework;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;

namespace BhModule.PathingCategoryExplorerPlugin
{
    [Export(typeof(Blish_HUD.Modules.Module))]
    public class PathingCategoryExplorerPluginModule : Blish_HUD.Modules.Module
    {
        internal static readonly Logger Logger = Logger.GetLogger<PathingCategoryExplorerPluginModule>();
        #region Service Managers
        internal SettingsManager SettingsManager => this.ModuleParameters.SettingsManager;
        internal ContentsManager ContentsManager => this.ModuleParameters.ContentsManager;
        internal DirectoriesManager DirectoriesManager => this.ModuleParameters.DirectoriesManager;
        internal Gw2ApiManager Gw2ApiManager => this.ModuleParameters.Gw2ApiManager;
        #endregion
        public PluginService PluginService { get; private set; }
        public ModuleSettings Settings { get; private set; }
        public static PathingCategoryExplorerPluginModule Instance;
        public static ModuleManager InstanceManager;
        [ImportingConstructor]
        public PathingCategoryExplorerPluginModule([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters)
        {
            Instance = this;
        }

        protected override void DefineSettings(SettingCollection settings)
        {
            Settings = new ModuleSettings(this, settings);
        }
        public override IView GetSettingsView()
        {
            return new PathingCategoryExplorerPluginSettingsView(SettingsManager.ModuleSettings);
        }

        protected override void Initialize()
        {
            PluginService = new PluginService();
            InstanceManager = GameService.Module.Modules.FirstOrDefault(m => m.ModuleInstance == this);
        }
        protected override async Task LoadAsync()
        {
        }
        protected override void Update(GameTime gameTime)
        {
            this.PluginService.Upadate();
        }
        protected override void Unload()
        {
            this.Settings.Unload();
            this.PluginService.Unload();
        }
    }
}

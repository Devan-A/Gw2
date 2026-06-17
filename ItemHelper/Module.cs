using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Input;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using Gw2.ItemHelper.Services;
using Gw2.ItemHelper.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Gw2.ItemHelper
{
    [Export(typeof(Blish_HUD.Modules.Module))]
    public class Module : Blish_HUD.Modules.Module
    {
        private static readonly Logger Logger = Logger.GetLogger<Module>();

        internal static Module ModuleInstance;

        #region Service Managers
        internal SettingsManager SettingsManager => ModuleParameters.SettingsManager;
        internal ContentsManager ContentsManager => ModuleParameters.ContentsManager;
        internal DirectoriesManager DirectoriesManager => ModuleParameters.DirectoriesManager;
        internal Gw2ApiManager Gw2ApiManager => ModuleParameters.Gw2ApiManager;
        #endregion

        private SettingEntry<bool> _autoWatchClipboard;
        private SettingEntry<KeyBinding> _lookupHotkey;

        private CornerIcon _cornerIcon;
        private ItemHelperWindow _window;

        // Clipboard watcher state.
        private double _clipboardTimer;
        private bool _clipboardBusy;
        private string _lastClipboard;

        [ImportingConstructor]
        public Module([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters)
        {
            ModuleInstance = this;
        }

        protected override void DefineSettings(SettingCollection settings)
        {
            _autoWatchClipboard = settings.DefineSetting(
                "AutoWatchClipboard", true,
                () => "Auto-watch clipboard",
                () => "When on, copying an item's chat link in game automatically opens Item Helper and looks the item up.");

            _lookupHotkey = settings.DefineSetting(
                "LookupHotkey",
                new KeyBinding(ModifierKeys.Ctrl | ModifierKeys.Shift, Keys.I),
                () => "Look up copied item (hotkey)",
                () => "Press this to look up the item currently copied to your clipboard.");
        }

        protected override void Initialize()
        {
            _lookupHotkey.Value.Enabled = true;
            _lookupHotkey.Value.Activated += OnHotkeyActivated;
        }

        protected override void OnModuleLoaded(EventArgs e)
        {
            // Background texture is 520x660; reserve the top ~45px for the title bar.
            _window = new ItemHelperWindow(
                Gw2ApiManager,
                ContentsManager.GetTexture("background.png"),
                new Rectangle(0, 0, 520, 660),
                new Rectangle(15, 45, 490, 600))
            {
                Parent = GameService.Graphics.SpriteScreen,
                Title = "Item Helper",
                Subtitle = "What is this item for?",
                SavesPosition = true,
                Id = "Gw2_ItemHelper_MainWindow",
            };

            _window.Location = new Point(
                (GameService.Graphics.SpriteScreen.Width - _window.Width) / 2,
                (GameService.Graphics.SpriteScreen.Height - _window.Height) / 2);

            _cornerIcon = new CornerIcon
            {
                Icon = ContentsManager.GetTexture("icon.png"),
                BasicTooltipText = "Item Helper — look up what an item is for",
                Priority = 17483922,
            };
            _cornerIcon.Click += (s, args) => _window.ToggleWindow();

            base.OnModuleLoaded(e);
        }

        protected override void Update(GameTime gameTime)
        {
            if (_window == null || _autoWatchClipboard == null || !_autoWatchClipboard.Value)
                return;

            _clipboardTimer += gameTime.ElapsedGameTime.TotalSeconds;
            if (_clipboardTimer < 1.2 || _clipboardBusy)
                return;

            _clipboardTimer = 0;
            _clipboardBusy = true;
            _ = CheckClipboardAsync();
        }

        private async Task CheckClipboardAsync()
        {
            try
            {
                string text = await ClipboardUtil.WindowsClipboardService.GetTextAsync();
                if (text == _lastClipboard)
                    return;

                _lastClipboard = text;

                if (ItemLinkParser.ContainsChatLink(text) && ItemLinkParser.TryGetItemId(text, out _))
                {
                    _window.Show();
                    await _window.LookupTextAsync(text);
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Clipboard watch failed.");
            }
            finally
            {
                _clipboardBusy = false;
            }
        }

        private void OnHotkeyActivated(object sender, EventArgs e)
        {
            if (_window == null) return;
            _window.Show();
            _ = _window.LookupClipboardAsync();
        }

        protected override void Unload()
        {
            if (_lookupHotkey?.Value != null)
                _lookupHotkey.Value.Activated -= OnHotkeyActivated;

            _cornerIcon?.Dispose();
            _window?.Dispose();

            ModuleInstance = null;
        }
    }
}

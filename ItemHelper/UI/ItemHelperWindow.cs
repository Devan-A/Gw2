using System;
using System.Threading.Tasks;
using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Modules.Managers;
using Gw2.ItemHelper.Services;
using Gw2Sharp.WebApi.V2.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gw2.ItemHelper.UI
{
    /// <summary>
    /// The main Item Helper window: paste/look-up controls at the top and a
    /// scrollable explanation panel below.
    /// </summary>
    internal class ItemHelperWindow : StandardWindow
    {
        private static readonly Logger Logger = Logger.GetLogger(typeof(ItemHelperWindow));

        private const string Instructions =
            "How to look up an item:\n" +
            "1. In Guild Wars 2, open chat and SHIFT-CLICK an item to drop its link in.\n" +
            "2. Select that link and press Ctrl+C to copy it.\n" +
            "3. Come back here and click \"Look up copied item\" (or just enable auto-watch in settings).";

        private readonly Gw2ApiManager _api;

        private readonly TextBox _input;
        private readonly StandardButton _lookupInputButton;
        private readonly StandardButton _lookupCopiedButton;
        private readonly LoadingSpinner _spinner;

        private readonly Panel _result;
        private readonly Image _icon;
        private readonly Label _name;
        private readonly Label _body;

        public ItemHelperWindow(Gw2ApiManager api, Texture2D background, Rectangle windowRegion, Rectangle contentRegion)
            : base(background, windowRegion, contentRegion)
        {
            _api = api;

            int w = contentRegion.Width;

            var help = new Label
            {
                Parent = this,
                Location = new Point(0, 0),
                Width = w,
                AutoSizeHeight = true,
                WrapText = true,
                Text = Instructions,
            };

            _input = new TextBox
            {
                Parent = this,
                Location = new Point(0, 95),
                Width = w - 130,
                PlaceholderText = "Paste a chat link or type an item id…",
            };
            _input.EnterPressed += async (s, e) => await LookupTextAsync(_input.Text);

            _lookupInputButton = new StandardButton
            {
                Parent = this,
                Location = new Point(w - 120, 94),
                Width = 120,
                Text = "Look up",
            };
            _lookupInputButton.Click += async (s, e) => await LookupTextAsync(_input.Text);

            _lookupCopiedButton = new StandardButton
            {
                Parent = this,
                Location = new Point(0, 132),
                Width = 220,
                Text = "Look up copied item",
            };
            _lookupCopiedButton.Click += async (s, e) => await LookupClipboardAsync();

            _spinner = new LoadingSpinner
            {
                Parent = this,
                Location = new Point(232, 130),
                Size = new Point(32, 32),
                Visible = false,
            };

            _result = new Panel
            {
                Parent = this,
                Location = new Point(0, 178),
                Size = new Point(w, contentRegion.Height - 178),
                CanScroll = true,
            };

            _icon = new Image
            {
                Parent = _result,
                Location = new Point(2, 2),
                Size = new Point(48, 48),
                Visible = false,
            };

            _name = new Label
            {
                Parent = _result,
                Location = new Point(58, 4),
                Width = w - 70,
                AutoSizeHeight = true,
                WrapText = true,
                Font = GameService.Content.DefaultFont18,
            };

            _body = new Label
            {
                Parent = _result,
                Location = new Point(2, 60),
                Width = w - 28,
                AutoSizeHeight = true,
                WrapText = true,
                Font = GameService.Content.DefaultFont16,
            };

            ShowMessage("", "Copy an item's chat link in-game, then look it up here.");
        }

        /// <summary>Reads the clipboard and looks up whatever item link it contains.</summary>
        public async Task LookupClipboardAsync()
        {
            string text = null;
            try
            {
                text = await ClipboardUtil.WindowsClipboardService.GetTextAsync();
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Could not read the clipboard.");
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                ShowMessage("Nothing copied", "Your clipboard is empty. Shift-click an item into chat, " +
                                              "select the link, press Ctrl+C, then try again.");
                return;
            }

            await LookupTextAsync(text);
        }

        /// <summary>Parses text (chat link or item id) and shows the matching item.</summary>
        public async Task LookupTextAsync(string text)
        {
            if (!ItemLinkParser.TryGetItemId(text, out int itemId))
            {
                ShowMessage("Couldn't read that", "That doesn't look like a GW2 item link or id. " +
                                                  "In chat, shift-click an item, copy the link, and try again.");
                return;
            }

            await ShowItemAsync(itemId);
        }

        private async Task ShowItemAsync(int itemId)
        {
            SetBusy(true);
            try
            {
                Item item = await _api.Gw2ApiClient.V2.Items.GetAsync(itemId);
                if (item == null)
                {
                    ShowMessage("Not found", $"No item with id {itemId} was found.");
                    return;
                }

                _name.Text = item.Name;
                _name.TextColor = RarityColor(item);
                SetIcon(item);

                if (ItemExplainer.IsCovered(item))
                {
                    _body.Text = "Loading details…";
                    string explanation = await ItemExplainer.ExplainAsync(item, _api);
                    _body.Text = explanation;
                }
                else
                {
                    _body.Text = NotCoveredMessage(item);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to look up item {0}", itemId);
                ShowMessage("Something went wrong", "Couldn't load that item. Check your internet connection and try again.");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private static string NotCoveredMessage(Item item)
        {
            string category = item.Type.IsUnknown ? "item" : item.Type.Value.ToString().ToLowerInvariant();
            return $"This is a {category}. Item Helper focuses on consumables, crafting materials, " +
                   "containers, and gizmos — the things whose purpose isn't obvious. Weapons, armor, " +
                   "trinkets, and junk usually explain themselves: equip gear, or sell junk to a vendor.";
        }

        private void ShowMessage(string title, string body)
        {
            _icon.Visible = false;
            _name.Text = title;
            _name.TextColor = Color.White;
            _body.Text = body;
        }

        private void SetIcon(Item item)
        {
            string url = item.Icon.Url?.ToString();
            if (string.IsNullOrEmpty(url))
            {
                _icon.Visible = false;
                return;
            }

            _icon.Texture = GameService.Content.GetRenderServiceTexture(url);
            _icon.Visible = true;
        }

        private void SetBusy(bool busy)
        {
            _spinner.Visible = busy;
            _lookupInputButton.Enabled = !busy;
            _lookupCopiedButton.Enabled = !busy;
        }

        private static Color RarityColor(Item item)
        {
            if (item.Rarity.IsUnknown) return Color.White;
            switch (item.Rarity.Value)
            {
                case ItemRarity.Junk: return new Color(170, 170, 170);
                case ItemRarity.Basic: return Color.White;
                case ItemRarity.Fine: return new Color(98, 164, 218);
                case ItemRarity.Masterwork: return new Color(26, 147, 6);
                case ItemRarity.Rare: return new Color(252, 208, 11);
                case ItemRarity.Exotic: return new Color(255, 164, 5);
                case ItemRarity.Ascended: return new Color(251, 62, 141);
                case ItemRarity.Legendary: return new Color(167, 86, 255);
                default: return Color.White;
            }
        }
    }
}

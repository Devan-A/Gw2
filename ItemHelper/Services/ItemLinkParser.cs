using System.Text.RegularExpressions;
using Blish_HUD;
using Gw2Sharp.ChatLinks;

namespace Gw2.ItemHelper.Services
{
    /// <summary>
    /// Extracts a GW2 item id from arbitrary text. Accepts either a GW2 chat
    /// link (e.g. "[&amp;AgF0bAAA]", produced by shift-clicking an item into
    /// chat) or a plain numeric item id typed by the user.
    /// </summary>
    internal static class ItemLinkParser
    {
        private static readonly Logger Logger = Logger.GetLogger(typeof(ItemLinkParser));
        private static readonly Regex ChatLinkPattern = new Regex(@"\[&[A-Za-z0-9+/=]+\]", RegexOptions.Compiled);
        private static readonly Regex NumberPattern = new Regex(@"^\s*(\d{1,7})\s*$", RegexOptions.Compiled);

        /// <summary>Returns true and the item id if the text contains a usable item reference.</summary>
        public static bool TryGetItemId(string text, out int itemId)
        {
            itemId = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;

            // 1) A bare item id, e.g. "19721".
            var num = NumberPattern.Match(text);
            if (num.Success && int.TryParse(num.Groups[1].Value, out itemId))
                return itemId > 0;

            // 2) A chat link somewhere in the text.
            var match = ChatLinkPattern.Match(text);
            if (!match.Success) return false;

            try
            {
                if (Gw2ChatLink.TryParse(match.Value, out IGw2ChatLink link) && link is ItemChatLink itemLink)
                {
                    itemId = itemLink.ItemId;
                    return itemId > 0;
                }
            }
            catch (System.Exception ex)
            {
                Logger.Debug(ex, "Failed to parse chat link '{0}'", match.Value);
            }

            return false;
        }

        /// <summary>True if the text contains a GW2 chat link of any kind (used for clipboard watching).</summary>
        public static bool ContainsChatLink(string text)
            => !string.IsNullOrWhiteSpace(text) && ChatLinkPattern.IsMatch(text);
    }
}

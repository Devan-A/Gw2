using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Blish_HUD;
using Blish_HUD.Modules.Managers;
using Gw2Sharp.WebApi.V2.Models;

namespace Gw2.ItemHelper.Services
{
    /// <summary>
    /// Turns a GW2 API <see cref="Item"/> into beginner-friendly, plain-English
    /// text describing what the item is and what it is used for.
    ///
    /// We deliberately focus on the item categories that confuse new players the
    /// most: consumables, crafting materials, containers and gizmos. Armor,
    /// weapons, trinkets and junk are intentionally treated as "not covered"
    /// because their purpose is usually self-explanatory.
    /// </summary>
    internal static class ItemExplainer
    {
        private static readonly Logger Logger = Logger.GetLogger(typeof(ItemExplainer));

        /// <summary>Item types this helper explains.</summary>
        public static readonly ItemType[] CoveredTypes =
        {
            ItemType.Consumable,
            ItemType.CraftingMaterial,
            ItemType.Container,
            ItemType.Gizmo,
        };

        public static bool IsCovered(Item item)
        {
            if (item == null || item.Type.IsUnknown) return false;
            if (!item.Rarity.IsUnknown && item.Rarity.Value == ItemRarity.Junk) return false;
            return CoveredTypes.Contains(item.Type.Value);
        }

        public static string CategoryLabel(ItemType type)
        {
            switch (type)
            {
                case ItemType.Consumable: return "Consumable";
                case ItemType.CraftingMaterial: return "Crafting Material";
                case ItemType.Container: return "Container";
                case ItemType.Gizmo: return "Gizmo";
                default: return type.ToString();
            }
        }

        /// <summary>
        /// Builds the full explanation. May call the API again to enrich crafting
        /// materials with the recipes that use them; any such failure degrades
        /// gracefully and never throws.
        /// </summary>
        public static async Task<string> ExplainAsync(Item item, Gw2ApiManager api)
        {
            if (item == null) return "Could not load this item.";

            var sb = new StringBuilder();
            sb.AppendLine(Headline(item));

            string desc = Clean(item.Description);
            if (!string.IsNullOrWhiteSpace(desc))
                sb.AppendLine().AppendLine(desc);

            sb.AppendLine().AppendLine(Purpose(item));

            if (!item.Type.IsUnknown && item.Type.Value == ItemType.CraftingMaterial)
            {
                string uses = await CraftingUsesAsync(item.Id, api).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(uses))
                    sb.AppendLine().AppendLine(uses);
            }

            if (item.VendorValue > 0)
                sb.AppendLine().AppendLine($"Sells to a vendor for {FormatCoins(item.VendorValue)}.");

            return sb.ToString().Trim();
        }

        private static string Headline(Item item)
        {
            string rarity = item.Rarity.IsUnknown ? "" : item.Rarity.Value + " ";
            string category = item.Type.IsUnknown ? "Item" : CategoryLabel(item.Type.Value);
            string head = $"{rarity}{category}";
            if (item.Level > 0)
                head += $"  •  Required level: {item.Level}";
            return head;
        }

        private static string Purpose(Item item)
        {
            if (item.Type.IsUnknown) return "An item from Guild Wars 2.";

            switch (item.Type.Value)
            {
                case ItemType.Consumable:
                    return ConsumablePurpose(item);
                case ItemType.Container:
                    return "This is a container. Double-click it in your inventory to open it " +
                           "and receive the items inside. Some containers let you pick one reward " +
                           "from a list, so read the prompt before choosing.";
                case ItemType.CraftingMaterial:
                    return "This is a crafting material. Press the deposit-materials button " +
                           "(default: in your inventory menu) to send it to your account-wide " +
                           "Material Storage, then use it at a crafting station to make gear, " +
                           "food, and other items.";
                case ItemType.Gizmo:
                    return GizmoPurpose(item);
                default:
                    return "An item from Guild Wars 2.";
            }
        }

        private static string ConsumablePurpose(Item item)
        {
            var details = (item as ConsumableItem)?.Details;
            string effect = Clean(details?.Description);
            string duration = "";
            if (details?.DurationMs != null && details.DurationMs.Value > 0)
            {
                double minutes = Convert.ToDouble(details.DurationMs.Value) / 60000.0;
                duration = minutes >= 1
                    ? $" Lasts about {Math.Round(minutes)} min."
                    : $" Lasts about {Math.Round(minutes * 60)} sec.";
            }

            string baseLine;
            ItemConsumableType? type = details != null && !details.Type.IsUnknown ? details.Type.Value : (ItemConsumableType?)null;
            switch (type)
            {
                case ItemConsumableType.Food:
                    baseLine = "Food. Double-click to eat it for a Nourishment buff that improves " +
                               "your stats while it lasts. You can have one Food and one Utility buff active at once.";
                    break;
                case ItemConsumableType.Utility:
                    baseLine = "Utility consumable (such as an oil, potion, or sharpening stone). " +
                               "Double-click to gain a temporary buff that stacks with food.";
                    break;
                case ItemConsumableType.Booze:
                    baseLine = "A drink. Mostly for fun — drinking too much makes your screen wobble!";
                    break;
                case ItemConsumableType.Transmutation:
                    baseLine = "A Transmutation Charge. Use it in the wardrobe to change the appearance " +
                               "(skin) of a piece of gear without changing its stats.";
                    break;
                case ItemConsumableType.Unlock:
                    baseLine = "An unlock. Double-click to permanently add something to your account — " +
                               "such as a skin, dye, recipe, bag slot, or other collection entry.";
                    break;
                case ItemConsumableType.ContractNpc:
                    baseLine = "Summons a vendor or service NPC you can interact with for a short time.";
                    break;
                case ItemConsumableType.UpgradeRemoval:
                    baseLine = "Removes an upgrade (rune or sigil) from a piece of gear so you can reuse it.";
                    break;
                case ItemConsumableType.AppearanceChange:
                    baseLine = "Changes your character's appearance (such as a Total Makeover or name change).";
                    break;
                case ItemConsumableType.Immediate:
                case ItemConsumableType.Generic:
                default:
                    baseLine = "A consumable. Double-click it to use it for its effect.";
                    break;
            }

            if (!string.IsNullOrWhiteSpace(effect))
                baseLine += $"\nEffect: {effect}";
            return baseLine + duration;
        }

        private static string GizmoPurpose(Item item)
        {
            var details = (item as GizmoItem)?.Details;
            ItemGizmoType? type = details != null && !details.Type.IsUnknown ? details.Type.Value : (ItemGizmoType?)null;
            switch (type)
            {
                case ItemGizmoType.ContainerKey:
                    return "A key. It is used to open a specific locked chest or container somewhere in the world.";
                case ItemGizmoType.RentableContractNpc:
                    return "Summons a rentable vendor NPC (such as a trading post or bank express) for a short time.";
                case ItemGizmoType.UnlimitedConsumable:
                    return "A reusable tool. Double-click to use it — it is never consumed, so you keep it forever.";
                case ItemGizmoType.Default:
                default:
                    return "A gizmo. Double-click to use it for its special effect (it may open a menu, " +
                           "play music, give a buff, or start an activity).";
            }
        }

        /// <summary>Looks up a few recipes that consume this material and lists what they make.</summary>
        private static async Task<string> CraftingUsesAsync(int itemId, Gw2ApiManager api)
        {
            try
            {
                var recipeIds = await api.Gw2ApiClient.V2.Recipes.Search.Input(itemId).GetAsync().ConfigureAwait(false);
                if (recipeIds == null || recipeIds.Count == 0)
                    return null;

                var sample = recipeIds.Take(8).ToList();
                var recipes = await api.Gw2ApiClient.V2.Recipes.ManyAsync(sample).ConfigureAwait(false);
                var outputIds = recipes.Select(r => r.OutputItemId).Distinct().Take(6).ToList();
                if (outputIds.Count == 0) return null;

                var outputs = await api.Gw2ApiClient.V2.Items.ManyAsync(outputIds).ConfigureAwait(false);
                var names = outputs.Select(o => o.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
                if (names.Count == 0) return null;

                string list = string.Join(", ", names);
                string more = recipeIds.Count > sample.Count ? ", and more" : "";
                return $"Used in crafting to make: {list}{more}.";
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Recipe lookup failed for item {0}", itemId);
                return null;
            }
        }

        /// <summary>Strips GW2 markup tags (e.g. &lt;c=@flavor&gt;...&lt;/c&gt;) from description text.</summary>
        private static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            text = text.Replace("<br>", "\n");
            text = Regex.Replace(text, "<[^>]+>", "");
            return text.Trim();
        }

        private static string FormatCoins(int copper)
        {
            int gold = copper / 10000;
            int silver = (copper / 100) % 100;
            int cop = copper % 100;
            var parts = new System.Collections.Generic.List<string>();
            if (gold > 0) parts.Add($"{gold}g");
            if (silver > 0) parts.Add($"{silver}s");
            if (cop > 0 || parts.Count == 0) parts.Add($"{cop}c");
            return string.Join(" ", parts);
        }
    }
}

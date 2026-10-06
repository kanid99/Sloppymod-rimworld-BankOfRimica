using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BankOfRimica
{
    public class ShopEntry
    {
        public ThingDef def;
        public ThingDef stuff;
        public string category;
        public string label;

        public bool HasQuality => def.HasComp(typeof(CompQuality));

        /// <summary>Normal-quality price at the shop's markup.</summary>
        public float Price => Mathf.Max(1f, def.GetStatValueAbstract(StatDefOf.MarketValue, stuff) * BankUtility.Settings.ShopPriceMultiplier);

        public float PriceAt(QualityCategory q) => HasQuality ? Mathf.Max(1f, Price * CommodityShop.QualityPriceFactor(q)) : Price;
    }

    /// <summary>One cart line: an item at a chosen quality.</summary>
    public struct CartKey : System.IEquatable<CartKey>
    {
        public ShopEntry entry;
        public QualityCategory quality;

        public CartKey(ShopEntry entry, QualityCategory quality)
        {
            this.entry = entry;
            this.quality = entry.HasQuality ? quality : QualityCategory.Normal;
        }

        public float Price => entry.PriceAt(quality);

        public bool Equals(CartKey other) => entry == other.entry && quality == other.quality;
        public override bool Equals(object obj) => obj is CartKey k && Equals(k);
        public override int GetHashCode() => entry.GetHashCode() * 31 + (int)quality;
    }

    /// <summary>
    /// The Rimica Commodity Exchange: nearly every item in the game, paid for with store credit earned from
    /// custody contracts. Credit can't be turned back into silver, so silver itself isn't for sale.
    /// </summary>
    public static class CommodityShop
    {
        private static List<ShopEntry> catalog;

        public static List<ShopEntry> Catalog => catalog ??= BuildCatalog();

        private static List<ShopEntry> BuildCatalog()
        {
            var list = new List<ShopEntry>();
            foreach (ThingDef d in DefDatabase<ThingDef>.AllDefs)
            {
                if (!Sellable(d)) continue;
                ThingDef stuff = d.MadeFromStuff ? GenStuff.DefaultStuffFor(d) : null;
                if (d.MadeFromStuff && stuff == null) continue;
                list.Add(new ShopEntry
                {
                    def = d,
                    stuff = stuff,
                    category = CategoryOf(d),
                    label = stuff != null ? "ThingMadeOfStuffLabel".Translate(stuff.LabelAsStuff, d.label).CapitalizeFirst().ToString() : d.LabelCap.ToString(),
                });
            }
            return list.OrderBy(e => e.category).ThenBy(e => e.label).ToList();
        }

        private static bool Sellable(ThingDef d)
        {
            if (d == ThingDefOf.Silver || d == BoR_DefOf.BoR_Bullion) return false;
            if (d.BaseMarketValue <= 0f || !d.tradeability.TraderCanSell()) return false;
            if (d.IsCorpse || d.destroyOnDrop || typeof(Pawn).IsAssignableFrom(d.thingClass) || typeof(MinifiedThing).IsAssignableFrom(d.thingClass)) return false;
            if (d.category == ThingCategory.Item) return d.EverHaulable;
            return d.category == ThingCategory.Building && d.Minifiable;
        }

        private static string CategoryOf(ThingDef d)
        {
            if (d.category == ThingCategory.Building) return "Furniture & buildings";
            ThingCategoryDef c = d.FirstThingCategory;
            if (c == null) return "Misc";
            while (c.parent != null && c.parent != ThingCategoryDefOf.Root) c = c.parent;
            return c.LabelCap;
        }

        /// <summary>Better quality costs steeply more; worse quality comes cheap. Relative to normal.</summary>
        public static float QualityPriceFactor(QualityCategory q)
        {
            switch (q)
            {
                case QualityCategory.Awful: return 0.5f;
                case QualityCategory.Poor: return 0.75f;
                case QualityCategory.Good: return 1.5f;
                case QualityCategory.Excellent: return 2.5f;
                case QualityCategory.Masterwork: return 5f;
                case QualityCategory.Legendary: return 10f;
                default: return 1f;
            }
        }

        public static IEnumerable<string> Categories => Catalog.Select(e => e.category).Distinct();

        /// <summary>Builds the ordered things: stacks split by stack limit, gear at the chosen quality, buildings minified.</summary>
        public static List<Thing> MakeThings(ShopEntry entry, int count, QualityCategory quality)
        {
            var things = new List<Thing>();
            if (entry.def.category == ThingCategory.Building)
            {
                for (int i = 0; i < count; i++)
                {
                    Thing b = ThingMaker.MakeThing(entry.def, entry.stuff);
                    b.TryGetComp<CompQuality>()?.SetQuality(quality, ArtGenerationContext.Outsider);
                    things.Add(MinifyUtility.MakeMinified(b));
                }
                return things;
            }
            int remaining = count;
            while (remaining > 0)
            {
                Thing t = ThingMaker.MakeThing(entry.def, entry.stuff);
                t.TryGetComp<CompQuality>()?.SetQuality(quality, ArtGenerationContext.Outsider);
                t.stackCount = Mathf.Min(remaining, entry.def.stackLimit);
                remaining -= t.stackCount;
                things.Add(t);
            }
            return things;
        }
    }
}

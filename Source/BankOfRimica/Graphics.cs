using System.Linq;
using UnityEngine;
using Verse;

namespace BankOfRimica
{
    /// <summary>
    /// Vanilla gold's stack-count bricks, re-tinted to silver. A plain colour multiply can't turn yellow into
    /// silver, so each texture is copied through a render texture and desaturated once at load.
    /// The graphicData color must differ from vanilla gold's so these materials aren't shared with it.
    /// </summary>
    public class Graphic_SilverStackCount : Graphic_StackCount
    {
        public override void Init(GraphicRequest req)
        {
            base.Init(req);
            if (subGraphics == null) return;
            foreach (Graphic sub in subGraphics)
            {
                Material mat = sub?.MatSingle;
                if (mat?.mainTexture is Texture2D tex)
                {
                    Texture2D silver = SilverTint.Make(tex);
                    if (silver != null) mat.mainTexture = silver;
                }
            }
        }
    }

    public static class SilverTint
    {
        public static Texture2D Make(Texture2D src)
        {
            try
            {
                RenderTexture rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
                Graphics.Blit(src, rt);
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, true);
                tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);

                Color32[] px = tex.GetPixels32();
                for (int i = 0; i < px.Length; i++)
                {
                    Color32 c = px[i];
                    float lum = (0.30f * c.r + 0.59f * c.g + 0.11f * c.b) / 255f;
                    // brighten a little and cool it slightly so it reads as polished silver
                    lum = Mathf.Clamp01(lum * 1.12f + 0.04f);
                    px[i] = new Color32((byte)(lum * 236f), (byte)(lum * 242f), (byte)(lum * 252f), c.a);
                }
                tex.SetPixels32(px);
                tex.filterMode = src.filterMode;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.name = src.name + "_BoRSilver";
                tex.Apply(true, true);
                return tex;
            }
            catch (System.Exception e)
            {
                Log.ErrorOnce("[Bank of Rimica] Couldn't make silver bar texture: " + e, 0x5B0F1CB);
                return null;
            }
        }
    }

    /// <summary>
    /// Borrows Odyssey art when it can be found: the passenger shuttle for the bank shuttle, and the gold
    /// stockpile pallet (re-tinted silver) for the bank pallet. Falls back to the mod's own textures.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class OdysseyArt
    {
        static OdysseyArt()
        {
            UseGoldPallet();
            UseShuttle();
        }

        private static void UseGoldPallet()
        {
            ThingDef src = DefDatabase<ThingDef>.AllDefs.FirstOrDefault(d =>
                d.graphicData != null && d.defName.IndexOf("Pallet", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                d.defName.IndexOf("Gold", System.StringComparison.OrdinalIgnoreCase) >= 0 && !d.defName.StartsWith("BoR_"));
            if (src == null)
            {
                Log.Message("[Bank of Rimica] No gold stockpile pallet found; using the bank's own pallet art.");
                return;
            }
            ThingDef def = BoR_DefOf.BoR_BankPallet;
            var data = new GraphicData();
            data.CopyFrom(src.graphicData);
            data.drawSize = Vector2.one;          // the bank pallet is a single cell
            data.color = new Color(0.996f, 0.996f, 0.996f); // keep materials separate from the real gold pallet
            def.graphicData = data;
            def.graphic = data.Graphic;
            foreach (Material mat in new[] { def.graphic.MatSingle, def.graphic.MatNorth, def.graphic.MatEast, def.graphic.MatSouth, def.graphic.MatWest }.Distinct())
            {
                if (mat?.mainTexture is Texture2D tex)
                {
                    Texture2D silver = SilverTint.Make(tex);
                    if (silver != null) mat.mainTexture = silver;
                }
            }
            Log.Message($"[Bank of Rimica] Using {src.defName} art (silvered) for the bank pallet.");
        }

        private static void UseShuttle()
        {
            ThingDef src = DefDatabase<ThingDef>.GetNamedSilentFail("PassengerShuttle")
                           ?? DefDatabase<ThingDef>.AllDefs.FirstOrDefault(d => d.defName.Contains("PassengerShuttle") && d.graphicData != null);
            if (src?.graphicData == null)
            {
                Log.Message("[Bank of Rimica] Odyssey passenger shuttle art not found; using the bank's own shuttle art.");
                return;
            }
            foreach (ThingDef def in new[] { BoR_DefOf.BoR_BankShuttleIncoming, BoR_DefOf.BoR_BankShuttleLanded, BoR_DefOf.BoR_BankShuttleLeaving })
            {
                var data = new GraphicData();
                data.CopyFrom(src.graphicData);
                def.graphicData = data;
                def.graphic = data.Graphic;
            }
        }
    }
}

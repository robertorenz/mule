using Microsoft.Xna.Framework;
using Mule.Core;

namespace Mule.Game;

/// <summary>
/// Central, professional color palette for the whole UI. Deliberately no purple.
/// Dark slate base with cool neutrals and clear resource accent colors.
/// </summary>
public static class Palette
{
    // Base surfaces
    public static readonly Color Background = new(0x14, 0x1A, 0x21); // deep slate
    public static readonly Color Panel      = new(0x1E, 0x27, 0x31);
    public static readonly Color PanelLight = new(0x2A, 0x36, 0x43);
    public static readonly Color Grid       = new(0x39, 0x48, 0x57);

    // Text
    public static readonly Color Text       = new(0xE6, 0xEC, 0xF2);
    public static readonly Color TextMuted  = new(0x8B, 0x9B, 0xAA);

    // Terrain fills
    public static readonly Color Plain      = new(0x3B, 0x4A, 0x3A); // muted olive
    public static readonly Color River      = new(0x24, 0x50, 0x63); // teal water
    public static readonly Color Mountain1  = new(0x4A, 0x44, 0x3A);
    public static readonly Color Mountain2  = new(0x5A, 0x50, 0x42);
    public static readonly Color Mountain3  = new(0x6B, 0x5E, 0x4B);
    public static readonly Color Town       = new(0x33, 0x3E, 0x4B);

    // Resource accents
    public static readonly Color Food       = new(0x27, 0xAE, 0x60); // green
    public static readonly Color Energy     = new(0xE2, 0xB9, 0x3B); // amber
    public static readonly Color Smithore   = new(0x6E, 0x8A, 0x9E); // steel
    public static readonly Color Crystite   = new(0x2F, 0x9E, 0xB0); // cyan

    public static Color TerrainColor(Terrain t) => t switch
    {
        Terrain.Plain     => Plain,
        Terrain.River     => River,
        Terrain.Mountain1 => Mountain1,
        Terrain.Mountain2 => Mountain2,
        Terrain.Mountain3 => Mountain3,
        Terrain.Town      => Town,
        _                 => Plain
    };

    public static Color ResourceColor(Resource r) => r switch
    {
        Resource.Food     => Food,
        Resource.Energy   => Energy,
        Resource.Smithore => Smithore,
        Resource.Crystite => Crystite,
        _                 => Text
    };

    public static Color MuleColor(MuleOutfit m) => m switch
    {
        MuleOutfit.Food     => Food,
        MuleOutfit.Energy   => Energy,
        MuleOutfit.Smithore => Smithore,
        MuleOutfit.Crystite => Crystite,
        _                   => TextMuted
    };

    /// <summary>Converts a Core packed 0xRRGGBBAA color to an XNA Color.</summary>
    public static Color FromPacked(uint rgba)
    {
        byte r = (byte)((rgba >> 24) & 0xFF);
        byte g = (byte)((rgba >> 16) & 0xFF);
        byte b = (byte)((rgba >> 8) & 0xFF);
        byte a = (byte)(rgba & 0xFF);
        return new Color(r, g, b, a);
    }
}

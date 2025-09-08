namespace Mater2026.Rendering
{
    /// <summary>
    /// Centralized asset keys and fallbacks – avoid magic strings all over the code.
    /// </summary>
    public static class AssetKeys
    {
        // UnifiedBitmap core props (multiple known names kept for back-compat)
        public static readonly string[] UB_Bitmap = { "UnifiedBitmap.Bitmap", "unifiedbitmap_Bitmap", "texture_Bitmap" };
        public static readonly string[] UB_Invert = { "UnifiedBitmap.Invert", "unifiedbitmap_Invert" };
        public static readonly string[] UB_ScaleX = { "UnifiedBitmap.RealWorldScaleX", "unifiedbitmap_RealWorldScaleX", "texture_RealWorldScaleX" };
        public static readonly string[] UB_ScaleY = { "UnifiedBitmap.RealWorldScaleY", "unifiedbitmap_RealWorldScaleY", "texture_RealWorldScaleY" };
        public static readonly string[] UB_Rotate = { "UnifiedBitmap.WAngle", "unifiedbitmap_WAngle", "texture_WAngle", "texture_Rotation" };

        // Generic material slots
        public static readonly string[] DiffuseTex = { "generic_diffuse_tex", "Generic_Diffuse", "diffuse_tex", "common_Tint_color_texture" };
        public const string DiffuseOn = "generic_diffuse_on";

        public static readonly string[] GlossTex = { "generic_glossiness_tex", "generic_roughness_tex", "generic_reflect_glossiness_tex" };
        public const string GlossOn = "generic_glossiness_on";

        public static readonly string[] ReflectTex = { "generic_reflectivity_tex", "generic_specular_tex", "generic_reflection_tex" };
        public const string ReflectOn = "generic_reflectivity_on";

        public static readonly string[] MetalTex = { "generic_metalness_tex", "pbr_metalness_tex" };
        public const string MetalOn = "generic_metalness_on";

        public static readonly string[] BumpTex = { "generic_bump_map", "generic_bump_tex", "generic_normalmap_tex", "generic_normaltex" };
        public const string BumpOn = "generic_bump_map_on";

        public static readonly string[] OpacityTex = { "generic_transparency_tex", "generic_opacity_tex", "generic_cutout_tex" };
        public const string OpacityOn = "generic_transparency_on";

        public static readonly string[] EmissiveTex = { "generic_emission_tex", "generic_selfillum_tex", "generic_emissive_tex" };
        public const string EmissiveOn = "generic_emission_on";
    }
}

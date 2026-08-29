namespace Castor.Engine
{
    /// <summary>
    /// Controls how a scene item's source is fitted inside its optional
    /// bounding box.
    /// </summary>
    public enum EngineSceneItemBoundsMode
    {
        /// <summary>Disables bounds; scale controls the rendered size.</summary>
        None = 0,

        /// <summary>Stretches the source to fill the bounds.</summary>
        Stretch = 1,

        /// <summary>Fits the entire source inside the bounds.</summary>
        ScaleInner = 2,

        /// <summary>Fills the bounds while preserving aspect ratio.</summary>
        ScaleOuter = 3,

        /// <summary>Scales to the bounds width while preserving aspect ratio.</summary>
        ScaleToWidth = 4,

        /// <summary>Scales to the bounds height while preserving aspect ratio.</summary>
        ScaleToHeight = 5,

        /// <summary>Uses the bounds only as a maximum size.</summary>
        MaxOnly = 6,
    }

    /// <summary>
    /// A mutable snapshot of a named scene's single visual item transform.
    /// </summary>
    public sealed class EngineSceneItemTransform
    {
        /// <summary>Gets or sets the horizontal position in base-canvas pixels.</summary>
        public float PositionX { get; set; }

        /// <summary>Gets or sets the vertical position in base-canvas pixels.</summary>
        public float PositionY { get; set; }

        /// <summary>Gets or sets the unitless horizontal scale.</summary>
        public float ScaleX { get; set; } = 1.0F;

        /// <summary>Gets or sets the unitless vertical scale.</summary>
        public float ScaleY { get; set; } = 1.0F;

        /// <summary>Gets or sets clockwise rotation in degrees.</summary>
        public float RotationDegrees { get; set; }

        /// <summary>Gets or sets how the source is fitted inside its bounds.</summary>
        public EngineSceneItemBoundsMode BoundsMode { get; set; }

        /// <summary>Gets or sets the bounds width in base-canvas pixels.</summary>
        public float BoundsWidth { get; set; }

        /// <summary>Gets or sets the bounds height in base-canvas pixels.</summary>
        public float BoundsHeight { get; set; }

        /// <summary>Gets or sets source pixels cropped from the left edge.</summary>
        public uint CropLeft { get; set; }

        /// <summary>Gets or sets source pixels cropped from the top edge.</summary>
        public uint CropTop { get; set; }

        /// <summary>Gets or sets source pixels cropped from the right edge.</summary>
        public uint CropRight { get; set; }

        /// <summary>Gets or sets source pixels cropped from the bottom edge.</summary>
        public uint CropBottom { get; set; }
    }
}

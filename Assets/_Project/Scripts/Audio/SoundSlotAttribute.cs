using System;

namespace RiskOfMagic.Audio
{
    /// <summary>
    /// Defaults used by "Create Audio Slots" when it generates the empty SoundEvent asset of a field.
    /// Fields without it get a 3D effects sound.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SoundSlotAttribute : Attribute
    {
        public bool Spatial { get; }
        public SoundCategory Category { get; }
        public float Cooldown { get; set; }
        public int MaxSimultaneous { get; set; } = 6;
        public float MaxDistance { get; set; } = 40f;
        /// <summary>Override fields: no slot is generated (empty means "use the default").</summary>
        public bool Optional { get; set; }

        public SoundSlotAttribute(bool spatial = true, SoundCategory category = SoundCategory.Effects)
        {
            Spatial = spatial;
            Category = category;
        }
    }
}

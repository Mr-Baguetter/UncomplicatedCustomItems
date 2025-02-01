using Exiled.API.Enums;
using UncomplicatedCustomItems.API.Interfaces;

namespace UncomplicatedCustomItems.Elements
{
    public class Effect : IEffect
    {
        /// <summary>
        /// Gets or sets the <see cref="EffectType"/> of the effect
        /// </summary>
        public EffectType EffectType { get; set; } = EffectType.MovementBoost;

        /// <summary>
        /// Gets or sets the duration of the effect
        /// </summary>
        public float Duration { get; set; } = -1;

        /// <summary>
        /// Gets or sets the intensity of the effect
        /// </summary>
        public byte Intensity { get; set; } = 1;

        /// <summary>
        /// Gets or sets whether the effect can be removed by using SCP-500
        /// </summary>
        public bool Removable { get; set; } = false;
    }
}
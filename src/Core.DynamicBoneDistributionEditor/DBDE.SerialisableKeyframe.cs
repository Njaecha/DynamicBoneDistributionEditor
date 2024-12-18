using UnityEngine;
using MessagePack;

namespace DynamicBoneDistributionEditor
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class SerialisableKeyframe
    {
        [Key("Time")]
        public float time;
        [Key("Value")]
        public float value;
        [Key("InTangent")]
        public float inTangent;
        [Key("OutTangent")]
        public float outTangent;
#if KKS
        [Key("InWeight")]
        public float inWeight;
        [Key("OutWeight")]
        public float outWeight;
#endif

        [SerializationConstructor]
        public SerialisableKeyframe(
            float Time,
            float Value,
            float InTangent,
            float OutTangent
#if KKS
            ,float InWeight,
            float OutWeight
#endif
            )
        {
            this.time = Time;
            this.value = Value;
            this.inTangent = InTangent;
            this.outTangent = OutTangent;
#if KKS
            this.inWeight = InWeight;
            this.outWeight = OutWeight;
#endif
        }

        public static implicit operator SerialisableKeyframe(Keyframe k)
        {
#if KK
            return new SerialisableKeyframe(k.time, k.value, k.inTangent, k.outTangent);
#elif KKS
            return new SerialisableKeyframe(k.time, k.value, k.inTangent, k.outTangent, k.inWeight, k.outWeight);
#endif
        }

        public static implicit operator Keyframe(SerialisableKeyframe k)
        {
#if KK
            return new Keyframe(k.time, k.value, k.inTangent, k.outTangent);
#elif KKS
            return new Keyframe(k.time, k.value, k.inTangent, k.outTangent, k.inWeight, k.outWeight);
#endif
        }

    }
}

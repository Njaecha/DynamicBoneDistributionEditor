using System.Collections.Generic;
using System.Linq;
using MessagePack;
using UnityEngine;

namespace DynamicBoneDistributionEditor
{
    [MessagePackObject]
    public struct SerialisedEdit
    {
        // Initial Values
        [Key("DampingDistributionInitial")] public SerialisableKeyframe[] DampingDistributionInitial { get; set; }

        [Key("ElasticityDistributionInitial")]
        public SerialisableKeyframe[] ElasticityDistributionInitial { get; set; }

        [Key("InertiaDistributionInitial")] public SerialisableKeyframe[] InertiaDistributionInitial { get; set; }
        [Key("RadiusDistributionInitial")] public SerialisableKeyframe[] RadiusDistributionInitial { get; set; }

        [Key("StiffnessDistributionInitial")]
        public SerialisableKeyframe[] StiffnessDistributionInitial { get; set; }

        [Key("DampingBaseValueInitial")] public float DampingBaseValueInitial { get; set; }
        [Key("ElasticityBaseValueInitial")] public float ElasticityBaseValueInitial { get; set; }
        [Key("InertiaBaseValueInitial")] public float InertiaBaseValueInitial { get; set; }
        [Key("RadiusBaseValueInitial")] public float RadiusBaseValueInitial { get; set; }
        [Key("StiffnessBaseValueInitial")] public float StiffnessBaseValueInitial { get; set; }

        [Key("GravityInitial")] public Vector3 GravityInitial { get; set; }
        [Key("ForceInitial")] public Vector3 ForceInitial { get; set; }
        [Key("EndOffsetInitial")] public Vector3 EndOffsetInitial { get; set; }

        [Key("FreezeAxisInitial")] public DynamicBone.FreezeAxis FreezeAxisInitial { get; set; }
        [Key("WeightInitial")] public float WeightInitial { get; set; }

        [Key("NotRollsInitial")] public List<string> NotRollsInitial { get; set; }
        [Key("ExclusionsInitial")] public List<string> ExclusionsInitial { get; set; }

        // Actual Values
        [Key("DampingDistribution")] public SerialisableKeyframe[] DampingDistribution { get; set; }
        [Key("ElasticityDistribution")] public SerialisableKeyframe[] ElasticityDistribution { get; set; }
        [Key("InertiaDistribution")] public SerialisableKeyframe[] InertiaDistribution { get; set; }
        [Key("RadiusDistribution")] public SerialisableKeyframe[] RadiusDistribution { get; set; }
        [Key("StiffnessDistribution")] public SerialisableKeyframe[] StiffnessDistribution { get; set; }

        [Key("DampingBaseValue")] public float DampingBaseValue { get; set; }
        [Key("ElasticityBaseValue")] public float ElasticityBaseValue { get; set; }
        [Key("InertiaBaseValue")] public float InertiaBaseValue { get; set; }
        [Key("RadiusBaseValue")] public float RadiusBaseValue { get; set; }
        [Key("StiffnessBaseValue")] public float StiffnessBaseValue { get; set; }

        [Key("Gravity")] public Vector3 Gravity { get; set; }
        [Key("Force")] public Vector3 Force { get; set; }
        [Key("EndOffset")] public Vector3 EndOffset { get; set; }

        [Key("FreezeAxis")] public DynamicBone.FreezeAxis FreezeAxis { get; set; }
        [Key("Weight")] public float Weight { get; set; }

        [Key("NotRolls")] public List<string> NotRolls { get; set; }
        [Key("Exclusions")] public List<string> Exclusions { get; set; }
        
        // general values
        [Key("PrimaryLocation")] public string PrimaryLocation { get; set; }
        [Key("InitialActive")] public bool InitialActive { get; set; }
        [Key("Active")] public bool Active { get; set; }

        public SerialisedEdit(DBDEDynamicBoneEdit edit)
        {
            this.DampingBaseValue = edit.baseValues[0].value;
            this.DampingBaseValueInitial = edit.baseValues[0].initialValue;
            this.DampingDistribution = edit.distributions[0].value.Select(k => (SerialisableKeyframe)k).ToArray();
            this.DampingDistributionInitial = edit.distributions[0].initialValue.Select(k => (SerialisableKeyframe)k).ToArray();
            
            this.ElasticityBaseValue = edit.baseValues[1].value;
            this.ElasticityBaseValueInitial = edit.baseValues[1].initialValue;
            this.ElasticityDistribution = edit.distributions[1].value.Select(k => (SerialisableKeyframe)k).ToArray();
            this.ElasticityDistributionInitial = edit.distributions[1].initialValue.Select(k => (SerialisableKeyframe)k).ToArray();

            this.InertiaBaseValue = edit.baseValues[2].value;
            this.InertiaBaseValueInitial = edit.baseValues[2].initialValue;
            this.InertiaDistribution = edit.distributions[2].value.Select(k => (SerialisableKeyframe)k).ToArray();
            this.InertiaDistributionInitial = edit.distributions[2].initialValue.Select(k => (SerialisableKeyframe)k).ToArray();
            
            this.RadiusBaseValue = edit.baseValues[3].value;
            this.RadiusBaseValueInitial = edit.baseValues[3].initialValue;
            this.RadiusDistribution = edit.distributions[3].value.Select(k => (SerialisableKeyframe)k).ToArray();
            this.RadiusDistributionInitial = edit.distributions[3].initialValue.Select(k => (SerialisableKeyframe)k).ToArray();
            
            this.StiffnessBaseValue = edit.baseValues[4].value;
            this.StiffnessBaseValueInitial = edit.baseValues[4].initialValue;
            this.StiffnessDistribution = edit.distributions[4].value.Select(k => (SerialisableKeyframe)k).ToArray();
            this.StiffnessDistributionInitial = edit.distributions[4].initialValue.Select(k => (SerialisableKeyframe)k).ToArray();

            this.Gravity = edit.gravity.value;
            this.GravityInitial = edit.gravity.initialValue;
            this.Force = edit.force.value;
            this.ForceInitial = edit.force.initialValue;
            this.EndOffset = edit.endOffset.value;
            this.EndOffsetInitial = edit.endOffset.initialValue;
            
            this.FreezeAxis = edit.freezeAxis.value;
            this.FreezeAxisInitial = edit.freezeAxis.initialValue;
            this.Weight = edit.weight.value;
            this.WeightInitial = edit.weight.initialValue;

            this.NotRolls = (List<string>)edit.notRolls;
            this.NotRollsInitial = edit.notRolls.OrgList;
            this.Exclusions = (List<string>)edit.Exclusions;
            this.ExclusionsInitial = edit.Exclusions.OrgList;

            this.PrimaryLocation = edit.PrimaryLocation;
            this.InitialActive = edit.InitialActive;
            this.Active = edit.active;
        }

        [SerializationConstructor]
        public SerialisedEdit(SerialisableKeyframe[] dampingDistributionInitial, SerialisableKeyframe[] elasticityDistributionInitial, SerialisableKeyframe[] inertiaDistributionInitial, SerialisableKeyframe[] radiusDistributionInitial, SerialisableKeyframe[] stiffnessDistributionInitial, float dampingBaseValueInitial, float elasticityBaseValueInitial, float inertiaBaseValueInitial, float radiusBaseValueInitial, float stiffnessBaseValueInitial, Vector3 gravityInitial, Vector3 forceInitial, Vector3 endOffsetInitial, DynamicBone.FreezeAxis freezeAxisInitial, float weightInitial, List<string> notRollsInitial, List<string> exclusionsInitial, SerialisableKeyframe[] dampingDistribution, SerialisableKeyframe[] elasticityDistribution, SerialisableKeyframe[] inertiaDistribution, SerialisableKeyframe[] radiusDistribution, SerialisableKeyframe[] stiffnessDistribution, float dampingBaseValue, float elasticityBaseValue, float inertiaBaseValue, float radiusBaseValue, float stiffnessBaseValue, Vector3 gravity, Vector3 force, Vector3 endOffset, DynamicBone.FreezeAxis freezeAxis, float weight, List<string> notRolls, List<string> exclusions, string primaryLocation, bool initialActive, bool active)
        {
            DampingDistributionInitial = dampingDistributionInitial;
            ElasticityDistributionInitial = elasticityDistributionInitial;
            InertiaDistributionInitial = inertiaDistributionInitial;
            RadiusDistributionInitial = radiusDistributionInitial;
            StiffnessDistributionInitial = stiffnessDistributionInitial;
            DampingBaseValueInitial = dampingBaseValueInitial;
            ElasticityBaseValueInitial = elasticityBaseValueInitial;
            InertiaBaseValueInitial = inertiaBaseValueInitial;
            RadiusBaseValueInitial = radiusBaseValueInitial;
            StiffnessBaseValueInitial = stiffnessBaseValueInitial;
            GravityInitial = gravityInitial;
            ForceInitial = forceInitial;
            EndOffsetInitial = endOffsetInitial;
            FreezeAxisInitial = freezeAxisInitial;
            WeightInitial = weightInitial;
            NotRollsInitial = notRollsInitial;
            ExclusionsInitial = exclusionsInitial;
            DampingDistribution = dampingDistribution;
            ElasticityDistribution = elasticityDistribution;
            InertiaDistribution = inertiaDistribution;
            RadiusDistribution = radiusDistribution;
            StiffnessDistribution = stiffnessDistribution;
            DampingBaseValue = dampingBaseValue;
            ElasticityBaseValue = elasticityBaseValue;
            InertiaBaseValue = inertiaBaseValue;
            RadiusBaseValue = radiusBaseValue;
            StiffnessBaseValue = stiffnessBaseValue;
            Gravity = gravity;
            Force = force;
            EndOffset = endOffset;
            FreezeAxis = freezeAxis;
            Weight = weight;
            NotRolls = notRolls;
            Exclusions = exclusions;
            PrimaryLocation = primaryLocation;
            InitialActive = initialActive;
            Active = active;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MessagePack;

namespace DynamicBoneDistributionEditor
{
	public class DBDEDynamicBoneEdit
	{
		/// <summary>
		/// 0 - Dampening, 1 - Elasticity, 2 - Intertia, 3 - Radius, 4 - Stiffness
		/// </summary>
		public readonly DBDEDistribEdit[] distributions;
		public DynamicBone dynamicBone { get => DynamicBoneAccessor.Invoke(); }
		internal Func<DynamicBone> AccessorFunciton { get => DynamicBoneAccessor; }
		private readonly Func<DynamicBone> DynamicBoneAccessor;

		internal object RedindificiationData;
		

		private Keyframe[] getDefaultCurveKeyframes()
		{
			return new Keyframe[2] { new Keyframe(0f, 1f), new Keyframe(1f, 1f) };
		}

		public DBDEDynamicBoneEdit(Func<DynamicBone> DynamicBoneAccessor, DBDEDynamicBoneEdit copyFrom)
		{
            this.DynamicBoneAccessor = DynamicBoneAccessor;
            this.distributions = new DBDEDistribEdit[]
            {
                new DBDEDistribEdit(copyFrom.distributions[0].GetKeyframes()),
                new DBDEDistribEdit(copyFrom.distributions[1].GetKeyframes()),
                new DBDEDistribEdit(copyFrom.distributions[2].GetKeyframes()),
                new DBDEDistribEdit(copyFrom.distributions[3].GetKeyframes()),
                new DBDEDistribEdit(copyFrom.distributions[4].GetKeyframes())
            };
			Apply();
        }

		public DBDEDynamicBoneEdit(Func<DynamicBone> DynamicBoneAccessor, byte[] serialised = null)
		{
            this.DynamicBoneAccessor = DynamicBoneAccessor;
			this.distributions = new DBDEDistribEdit[]
			{
				new DBDEDistribEdit(dynamicBone.m_DampingDistrib == null ?getDefaultCurveKeyframes() : dynamicBone.m_DampingDistrib.keys.Length >= 2 ? dynamicBone.m_DampingDistrib.keys : getDefaultCurveKeyframes()),
				new DBDEDistribEdit(dynamicBone.m_ElasticityDistrib == null ?getDefaultCurveKeyframes() : dynamicBone.m_ElasticityDistrib.keys.Length >= 2 ? dynamicBone.m_ElasticityDistrib.keys : getDefaultCurveKeyframes()),
				new DBDEDistribEdit(dynamicBone.m_InertDistrib == null ?getDefaultCurveKeyframes() : dynamicBone.m_InertDistrib.keys.Length >= 2 ? dynamicBone.m_InertDistrib.keys : getDefaultCurveKeyframes()),
				new DBDEDistribEdit(dynamicBone.m_RadiusDistrib == null ?getDefaultCurveKeyframes() : dynamicBone.m_RadiusDistrib.keys.Length >= 2 ? dynamicBone.m_RadiusDistrib.keys : getDefaultCurveKeyframes()),
				new DBDEDistribEdit(dynamicBone.m_StiffnessDistrib == null ?getDefaultCurveKeyframes() : dynamicBone.m_StiffnessDistrib.keys.Length >= 2 ? dynamicBone.m_StiffnessDistrib.keys : getDefaultCurveKeyframes())
			};

			if (serialised != null)
			{
				Dictionary<byte, Keyframe[]> edits = MessagePackSerializer.Deserialize<Dictionary<byte, Keyframe[]>>(serialised);
				foreach (byte i in edits.Keys)
				{
					distributions[i].SetKeyframes(edits[i]);
				}
			}
		}

		internal AnimationCurve GetAnimationCurve(byte kind)
		{ 
            return new AnimationCurve(distributions[kind].GetKeyframes());
		}

        public void SetDistribution(int kind, AnimationCurve animationCurve)
		{
			distributions[kind].SetKeyframes(animationCurve.keys);
		}

		public byte[] Sersialise()
		{
            Dictionary<byte, Keyframe[]> edits = distributions
				.Where(t => t.HasEdits)
				.Select((t, i) => new KeyValuePair<byte, Keyframe[]>((byte)i, t.GetKeyframes()))
				.ToDictionary(x => x.Key, x => x.Value);

            return MessagePackSerializer.Serialize(edits);
		}

		public bool IsEdited()
		{
			foreach (var d in distributions)
			{
				if (d.HasEdits) return true;
			}
			return false;
		}

		public void Apply(int? kind = null)
		{
			if (kind.HasValue)
			{
				Keyframe[] keys = distributions[kind.Value].GetKeyframes();
				switch (kind.Value)
				{
					case 0:
						if (dynamicBone.m_DampingDistrib == null) dynamicBone.m_DampingDistrib = new AnimationCurve(keys);
						else dynamicBone.m_DampingDistrib.SetKeys(keys);
						break;
					case 1:
                        if (dynamicBone.m_ElasticityDistrib == null) dynamicBone.m_ElasticityDistrib = new AnimationCurve(keys);
                        else dynamicBone.m_ElasticityDistrib.SetKeys(keys);
                        break;
					case 2:
                        if (dynamicBone.m_InertDistrib == null) dynamicBone.m_InertDistrib = new AnimationCurve(keys);
                        else dynamicBone.m_InertDistrib.SetKeys(keys);
                        break;
					case 3:
                        if (dynamicBone.m_RadiusDistrib == null) dynamicBone.m_RadiusDistrib = new AnimationCurve(keys);
                        else dynamicBone.m_RadiusDistrib.SetKeys(keys);
                        break;
					case 4:
                        if (dynamicBone.m_StiffnessDistrib == null) dynamicBone.m_StiffnessDistrib = new AnimationCurve(keys);
                        else dynamicBone.m_StiffnessDistrib.SetKeys(keys);
                        break;
                }
			}
			else
			{
				if (dynamicBone.m_DampingDistrib == null) dynamicBone.m_DampingDistrib = new AnimationCurve(distributions[0].GetKeyframes());
				else dynamicBone.m_DampingDistrib.SetKeys(distributions[0].GetKeyframes());
                if (dynamicBone.m_ElasticityDistrib == null) dynamicBone.m_ElasticityDistrib = new AnimationCurve(distributions[1].GetKeyframes());
                else dynamicBone.m_ElasticityDistrib.SetKeys(distributions[1].GetKeyframes());
                if (dynamicBone.m_InertDistrib == null) dynamicBone.m_InertDistrib = new AnimationCurve(distributions[2].GetKeyframes());
                else dynamicBone.m_InertDistrib.SetKeys(distributions[2].GetKeyframes());
                if (dynamicBone.m_RadiusDistrib == null) dynamicBone.m_RadiusDistrib = new AnimationCurve(distributions[3].GetKeyframes());
                else dynamicBone.m_RadiusDistrib.SetKeys(distributions[3].GetKeyframes());
                if (dynamicBone.m_StiffnessDistrib == null) dynamicBone.m_StiffnessDistrib = new AnimationCurve(distributions[4].GetKeyframes());
                else dynamicBone.m_StiffnessDistrib.SetKeys(distributions[4].GetKeyframes());
            }
			dynamicBone.SetupParticles();
		}

        public void Reset(int? kind = null)
        {
            if (kind.HasValue)
            {
                Keyframe[] keys = distributions[kind.Value].GetInitialKeyframes();
                switch (kind.Value)
                {
                    case 0:
                        dynamicBone.m_DampingDistrib.SetKeys(keys);
                        break;
                    case 1:
                        dynamicBone.m_ElasticityDistrib.SetKeys(keys);
                        break;
                    case 2:
                        dynamicBone.m_InertDistrib.SetKeys(keys);
                        break;
                    case 3:
                        dynamicBone.m_RadiusDistrib.SetKeys(keys);
                        break;
                    case 4:
                        dynamicBone.m_StiffnessDistrib.SetKeys(keys);
                        break;
                }
				distributions[kind.Value].Reset();
            }
            else
            {
                dynamicBone.m_DampingDistrib.SetKeys(distributions[0].GetInitialKeyframes());
                dynamicBone.m_ElasticityDistrib.SetKeys(distributions[1].GetInitialKeyframes());
                dynamicBone.m_InertDistrib.SetKeys(distributions[2].GetInitialKeyframes());
                dynamicBone.m_RadiusDistrib.SetKeys(distributions[3].GetInitialKeyframes());
                dynamicBone.m_StiffnessDistrib.SetKeys(distributions[4].GetInitialKeyframes());
                foreach (var e in distributions)
                {
					e.Reset();
                }
            }
            dynamicBone.SetupParticles();
        }
    }
}


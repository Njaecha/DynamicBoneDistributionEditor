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
		public readonly DynamicBone dynamicBone;

		public DBDEDynamicBoneEdit(DynamicBone dynamicBone, byte[] serialised = null)
		{
			this.dynamicBone = dynamicBone;
			this.distributions = new DBDEDistribEdit[]
			{
				new DBDEDistribEdit(dynamicBone.m_DampingDistrib.keys),
				new DBDEDistribEdit(dynamicBone.m_ElasticityDistrib.keys),
				new DBDEDistribEdit(dynamicBone.m_InertDistrib.keys),
				new DBDEDistribEdit(dynamicBone.m_RadiusDistrib.keys),
				new DBDEDistribEdit(dynamicBone.m_StiffnessDistrib.keys)
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

        public void setDistribution(int kind, AnimationCurve animationCurve)
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

		public void Apply(int? kind = null)
		{
			if (kind.HasValue)
			{
				Keyframe[] keys = distributions[kind.Value].GetKeyframes();
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
			}
			else
			{
				dynamicBone.m_DampingDistrib.SetKeys(distributions[0].GetKeyframes());
				dynamicBone.m_ElasticityDistrib.SetKeys(distributions[1].GetKeyframes());
				dynamicBone.m_InertDistrib.SetKeys(distributions[2].GetKeyframes());
				dynamicBone.m_RadiusDistrib.SetKeys(distributions[3].GetKeyframes());
				dynamicBone.m_StiffnessDistrib.SetKeys(distributions[4].GetKeyframes());

			}
		}
	}
}


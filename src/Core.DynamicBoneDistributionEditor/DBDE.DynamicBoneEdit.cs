using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MessagePack;
using KKAPI.Utilities;

namespace DynamicBoneDistributionEditor
{
	public class DBDEDynamicBoneEdit
	{
		/// <summary>
        /// Distribution Curves
		/// 0 - Dampening, 1 - Elasticity, 2 - Intertia, 3 - Radius, 4 - Stiffness
		/// </summary>
        public readonly EditableValue<Keyframe[]>[] distributions;
        /// <summary>
        /// Base Values
		/// 0 - Dampening, 1 - Elasticity, 2 - Intertia, 3 - Radius, 4 - Stiffness
		/// </summary>
        public readonly EditableValue<float>[] baseValues;

        public EditableValue<Vector3> gravity;
        public EditableValue<Vector3> force;
        public EditableValue<Vector3> endOffset;
        public EditableValue<DynamicBone.FreezeAxis> freezeAxis;

        private bool _active;
        public bool active {get => _active; set => SetActive(value); }
 
		public DynamicBone dynamicBone { get => DynamicBoneAccessor.Invoke(); }
		internal Func<DynamicBone> AccessorFunciton { get => DynamicBoneAccessor; }
		private readonly Func<DynamicBone> DynamicBoneAccessor;

		internal object ReidentificationData;
		
        internal string GetButtonName()
        {
            string n = "";
            if (ReidentificationData is KeyValuePair<int, string> kvp) n = $"Slot {kvp.Key+1} - ";
            return n + dynamicBone?.m_Root?.name;
        }

		private Keyframe[] getDefaultCurveKeyframes()
		{
			return new Keyframe[2] { new Keyframe(0f, 1f), new Keyframe(1f, 1f) };
		}

        internal void PasteData(DBDEDynamicBoneEdit copyFrom)
        {
            for (int i = 0; i < 5; i++)
            {
                this.distributions[i] = copyFrom.distributions[i];
                this.baseValues[i] = copyFrom.baseValues[i];
            }
            this.freezeAxis = copyFrom.freezeAxis;
            this.gravity = copyFrom.gravity;
            this.force = copyFrom.force;
            this.endOffset = copyFrom.endOffset;

            this.SetActive(copyFrom.active);

            ApplyAll();
        }

		public DBDEDynamicBoneEdit(Func<DynamicBone> DynamicBoneAccessor, DBDEDynamicBoneEdit copyFrom)
		{
            this.DynamicBoneAccessor = DynamicBoneAccessor;
            DynamicBone db = dynamicBone;
            _active = db.enabled;
            this.distributions = new EditableValue<Keyframe[]>[]
            {
                new EditableValue<Keyframe[]>(db?.m_DampingDistrib == null ? getDefaultCurveKeyframes() : db.m_DampingDistrib.keys.Length >= 2 ? db.m_DampingDistrib.keys : getDefaultCurveKeyframes()),
                new EditableValue<Keyframe[]>(db?.m_ElasticityDistrib == null ? getDefaultCurveKeyframes() : db.m_ElasticityDistrib.keys.Length >= 2 ? db.m_ElasticityDistrib.keys : getDefaultCurveKeyframes()),
                new EditableValue<Keyframe[]>(db?.m_InertDistrib == null ? getDefaultCurveKeyframes() : db.m_InertDistrib.keys.Length >= 2 ? db.m_InertDistrib.keys : getDefaultCurveKeyframes()),
                new EditableValue<Keyframe[]>(db?.m_RadiusDistrib == null ? getDefaultCurveKeyframes() : db.m_RadiusDistrib.keys.Length >= 2 ? db.m_RadiusDistrib.keys : getDefaultCurveKeyframes()),
                new EditableValue<Keyframe[]>(db?.m_StiffnessDistrib == null ? getDefaultCurveKeyframes() : db.m_StiffnessDistrib.keys.Length >= 2 ? db.m_StiffnessDistrib.keys : getDefaultCurveKeyframes())
            };
            this.baseValues = new EditableValue<float>[]
            {
                new EditableValue<float>(db.m_Damping),
                new EditableValue<float>(db.m_Elasticity),
                new EditableValue<float>(db.m_Inert),
                new EditableValue<float>(db.m_Radius),
                new EditableValue<float>(db.m_Stiffness)
            };

            gravity = new EditableValue<Vector3>(db.m_Gravity);
            force = new EditableValue<Vector3>(db.m_Force);
            endOffset = new EditableValue<Vector3>(db.m_EndOffset);
            freezeAxis = new EditableValue<DynamicBone.FreezeAxis>(db.m_FreezeAxis);

            PasteData(copyFrom);
        }

		public DBDEDynamicBoneEdit(Func<DynamicBone> DynamicBoneAccessor, byte[] serialised = null)
		{
            this.DynamicBoneAccessor = DynamicBoneAccessor;
			DynamicBone db = dynamicBone;
            _active = db.enabled;
			this.distributions = new EditableValue<Keyframe[]>[]
			{
				new EditableValue<Keyframe[]>(db?.m_DampingDistrib == null ? getDefaultCurveKeyframes() : db.m_DampingDistrib.keys.Length >= 2 ? db.m_DampingDistrib.keys : getDefaultCurveKeyframes()),
				new EditableValue<Keyframe[]>(db?.m_ElasticityDistrib == null ? getDefaultCurveKeyframes() : db.m_ElasticityDistrib.keys.Length >= 2 ? db.m_ElasticityDistrib.keys : getDefaultCurveKeyframes()),
				new EditableValue<Keyframe[]>(db?.m_InertDistrib == null ? getDefaultCurveKeyframes() : db.m_InertDistrib.keys.Length >= 2 ? db.m_InertDistrib.keys : getDefaultCurveKeyframes()),
				new EditableValue<Keyframe[]>(db?.m_RadiusDistrib == null ? getDefaultCurveKeyframes() : db.m_RadiusDistrib.keys.Length >= 2 ? db.m_RadiusDistrib.keys : getDefaultCurveKeyframes()),
				new EditableValue<Keyframe[]>(db?.m_StiffnessDistrib == null ? getDefaultCurveKeyframes() : db.m_StiffnessDistrib.keys.Length >= 2 ? db.m_StiffnessDistrib.keys : getDefaultCurveKeyframes())
			};
			this.baseValues = new EditableValue<float>[]
			{
				new EditableValue<float>(db.m_Damping),
				new EditableValue<float>(db.m_Elasticity),
				new EditableValue<float>(db.m_Inert),
				new EditableValue<float>(db.m_Radius),
				new EditableValue<float>(db.m_Stiffness)
			};

            gravity = new EditableValue<Vector3>(db.m_Gravity);
            force = new EditableValue<Vector3>(db.m_Force);
            endOffset = new EditableValue<Vector3>(db.m_EndOffset);
            freezeAxis = new EditableValue<DynamicBone.FreezeAxis>(db.m_FreezeAxis);

			if (serialised != null)
			{
                //DBDE.Logger.LogInfo($"Deserialising {db.m_Root.name}");
                List<byte[]> edits = MessagePackSerializer.Deserialize<List<byte[]>>(serialised);
                if (edits.Count != 7) return;
                //DBDE.Logger.LogInfo($"{GetButtonName()} - Loading Distribution");
                if (!edits[0].IsNullOrEmpty())
                {
				    var distribs = MessagePackSerializer.Deserialize<Dictionary<byte, Keyframe[]>>(edits[0]);
				    foreach (byte i in distribs.Keys)
				    {
                        if (DBDE.loadSettingsAsDefault.Value) distributions[i] = (EditableValue<Keyframe[]>)distribs[i];
                        else distributions[i].value = distribs[i];
				    }
                }
                //DBDE.Logger.LogInfo($"{GetButtonName()} - Loading BaseValue");
                if (!edits[1].IsNullOrEmpty())
                {
                    var bValue = MessagePackSerializer.Deserialize<Dictionary<byte, float>>(edits[1]);
                    foreach (byte i in bValue.Keys)
                    {
                        if (DBDE.loadSettingsAsDefault.Value) baseValues[i] = (EditableValue<float>)bValue[i];
                        else baseValues[i].value = bValue[i];
                    }
                }
                //DBDE.Logger.LogInfo($"{GetButtonName()} - Loading Gravity");
                if (!edits[2].IsNullOrEmpty()) LoadVector(edits[2], ref gravity);
                //DBDE.Logger.LogInfo($"{GetButtonName()} - Loading Force");
                if (!edits[3].IsNullOrEmpty()) LoadVector(edits[3], ref force);
                //DBDE.Logger.LogInfo($"{GetButtonName()} - Loading EndOffset");
                if (!edits[4].IsNullOrEmpty()) LoadVector(edits[4], ref endOffset);
                //DBDE.Logger.LogInfo($"{GetButtonName()} - Loading FreezeAxis");
                if (!edits[5].IsNullOrEmpty()) 
                { 
                    var sAxis = MessagePackSerializer.Deserialize<byte?>(edits[5]);
                    if (sAxis.HasValue)
                    {
                        if (DBDE.loadSettingsAsDefault.Value) freezeAxis = (EditableValue<DynamicBone.FreezeAxis>)(DynamicBone.FreezeAxis)sAxis;
                        else freezeAxis.value = (DynamicBone.FreezeAxis)sAxis;
                    }
                }
                SetActive(MessagePackSerializer.Deserialize<bool>(edits[6]));
            }
		}

        private void LoadVector(byte[] binary, ref EditableValue<Vector3> editableValue)
        {
            var sValue = MessagePackSerializer.Deserialize<Vector3>(binary);
            if (sValue != null)
            {
                string s = sValue.ToString("F4");
                // DBDE.Logger.LogInfo($"{GetButtonName()} - Loading Vector: {s} (As Default = {DBDE.loadSettingsAsDefault.Value})");
                if (DBDE.loadSettingsAsDefault.Value) editableValue = (EditableValue<Vector3>)sValue;
                else editableValue.value = sValue;
            }
        }

        public void SetActive(bool active)
        {
            dynamicBone.enabled = active;
            _active = active;
        }

		public AnimationCurve GetAnimationCurve(byte kind)
		{ 
            return new AnimationCurve(distributions[kind]);
		}

        public void SetAnimationCurve(int kind, AnimationCurve animationCurve)
		{
			distributions[kind].value = animationCurve.keys;
		}

		public byte[] Sersialise()
		{
            Dictionary<byte, Keyframe[]> distribs = distributions
				.Where(t => t.IsEdited)
				.Select((t, i) => new KeyValuePair<byte, Keyframe[]>((byte)i, t))
				.ToDictionary(x => x.Key, x => x.Value);
            byte[] sDistrib = MessagePackSerializer.Serialize(distribs);
			Dictionary<byte, float> bValues = baseValues
				.Where(v => v.IsEdited)
				.Select((v, i) => new KeyValuePair<byte, float>((byte)i, v))
				.ToDictionary(x => x.Key, x => x.Value);
            byte[] sBaseValues = MessagePackSerializer.Serialize(bValues);

            byte[] sGravtiy = SerialiseEditableVector(gravity);
            byte[] sFroce = SerialiseEditableVector(force);
            byte[] sEndOffset = SerialiseEditableVector(endOffset);
            byte[] sAxis = null;
            if (freezeAxis.IsEdited)
            {
                sAxis = MessagePackSerializer.Serialize(((byte?)freezeAxis.value));
            }
            byte[] sActive = MessagePackSerializer.Serialize(active);

            List<byte[]> edits = new List<byte[]>() {sDistrib, sBaseValues, sGravtiy, sFroce, sEndOffset, sAxis, sActive };
            return MessagePackSerializer.Serialize(edits);
		}

        private byte[] SerialiseEditableVector(EditableValue<Vector3> editableValue)
        {
            byte[] binary = null;
            if (editableValue.IsEdited)
            {
                binary = MessagePackSerializer.Serialize(editableValue.value);
            }
            return binary;
        }

        ///<summary></summary>
        /// <param name="axis">X=0, Y=1, Z=2</param>
        public bool IsGravityEdited(int? axis = null)
        {
            if (axis.HasValue)
            {
                switch (axis.Value)
                {
                    case 0:
                        return gravity.value.x != gravity.initialValue.x;
                    case 1:
                        return gravity.value.y != gravity.initialValue.y;
                    case 2:
                        return gravity.value.z != gravity.initialValue.z;
                    default:
                        return false;
                }
            }
            else return gravity.IsEdited;
        }
        ///<summary></summary>
        /// <param name="axis">X=0, Y=1, Z=2</param>
        public bool IsForceEdited(int? axis = null)
        {
            if (axis.HasValue)
            {
                switch (axis.Value)
                {
                    case 0:
                        return force.value.x != force.initialValue.x;
                    case 1:
                        return force.value.y != force.initialValue.y;
                    case 2:
                        return force.value.z != force.initialValue.z;
                    default:
                        return false;
                }
            }
            else return force.IsEdited;
        }
        ///<summary></summary>
        /// <param name="axis">X=0, Y=1, Z=2</param>
        public bool IsEndOffsetEdited(int? axis = null)
        {
            if (axis.HasValue)
            {
                switch (axis.Value)
                {
                    case 0:
                        return endOffset.value.x != endOffset.initialValue.x;
                    case 1:
                        return endOffset.value.y != endOffset.initialValue.y;
                    case 2:
                        return endOffset.value.z != endOffset.initialValue.z;
                    default:
                        return false;
                }
            }
            else return endOffset.IsEdited;
        }

        public bool IsEdited(int kind)
        {
            if (distributions[kind].IsEdited) return true;
            if (baseValues[kind].IsEdited) return true;
            return false;
        }

		public bool IsEdited()
		{
			foreach (var d in distributions)
			{
				if (d.IsEdited) return true;
			}
            foreach (var d in baseValues)
            {
                if (d.IsEdited) return true;
            }
            if (gravity.IsEdited) return true;
            if (force.IsEdited) return true;
            if (endOffset.IsEdited) return true;
            if (freezeAxis.IsEdited) return true;
			return false;
		}

        public void ApplyAll()
        {
            ApplyDistribution();
            ApplyBaseValues();
            ApplyGravity();
            ApplyForce();
            ApplyEndOffset();
            ApplyFreezeAxis();
        }

		public void ApplyDistribution(int? kind = null)
		{
            DynamicBone db = dynamicBone;
			if (kind.HasValue)
			{
				Keyframe[] keys = distributions[kind.Value];
				switch (kind.Value)
				{
					case 0:
						if (db.m_DampingDistrib == null) dynamicBone.m_DampingDistrib = new AnimationCurve(keys);
						else db.m_DampingDistrib.SetKeys(keys);
						break;
					case 1:
                        if (db.m_ElasticityDistrib == null) dynamicBone.m_ElasticityDistrib = new AnimationCurve(keys);
                        else db.m_ElasticityDistrib.SetKeys(keys);
                        break;
					case 2:
                        if (db.m_InertDistrib == null) dynamicBone.m_InertDistrib = new AnimationCurve(keys);
                        else db.m_InertDistrib.SetKeys(keys);
                        break;
					case 3:
                        if (db.m_RadiusDistrib == null) dynamicBone.m_RadiusDistrib = new AnimationCurve(keys);
                        else db.m_RadiusDistrib.SetKeys(keys);
                        break;
					case 4:
                        if (db.m_StiffnessDistrib == null) dynamicBone.m_StiffnessDistrib = new AnimationCurve(keys);
                        else db.m_StiffnessDistrib.SetKeys(keys);
                        break;
                }
			}
			else
			{
				if (db.m_DampingDistrib == null) dynamicBone.m_DampingDistrib = new AnimationCurve(distributions[0]);
				else db.m_DampingDistrib.SetKeys(distributions[0]);
                if (db.m_ElasticityDistrib == null) dynamicBone.m_ElasticityDistrib = new AnimationCurve(distributions[1]);
                else db.m_ElasticityDistrib.SetKeys(distributions[1]);
                if (db.m_InertDistrib == null) dynamicBone.m_InertDistrib = new AnimationCurve(distributions[2]);
                else db.m_InertDistrib.SetKeys(distributions[2]);
                if (db.m_RadiusDistrib == null) dynamicBone.m_RadiusDistrib = new AnimationCurve(distributions[3]);
                else db.m_RadiusDistrib.SetKeys(distributions[3]);
                if (db.m_StiffnessDistrib == null) dynamicBone.m_StiffnessDistrib = new AnimationCurve(distributions[4]);
                else db.m_StiffnessDistrib.SetKeys(distributions[4]);
            }
            db.UpdateParticles();
		}

        public void ResetDistribution(int? kind = null)
        {
            DynamicBone db = dynamicBone;
            if (kind.HasValue)
            {
                distributions[kind.Value].Reset();
                Keyframe[] keys = distributions[kind.Value].value;
                switch (kind.Value)
                {
                    case 0:
                        db.m_DampingDistrib?.SetKeys(keys);
                        break;
                    case 1:
                        db.m_ElasticityDistrib?.SetKeys(keys);
                        break;
                    case 2:
                        db.m_InertDistrib?.SetKeys(keys);
                        break;
                    case 3:
                        db.m_RadiusDistrib?.SetKeys(keys);
                        break;
                    case 4:
                        db.m_StiffnessDistrib?.SetKeys(keys);
                        break;
                }
            }
            else
            {
                foreach (var d in distributions) d.Reset();
                db.m_DampingDistrib?.SetKeys(distributions[0]);
                db.m_ElasticityDistrib?.SetKeys(distributions[1]);
                db.m_InertDistrib?.SetKeys(distributions[2]);
                db.m_RadiusDistrib?.SetKeys(distributions[3]);
                db.m_StiffnessDistrib?.SetKeys(distributions[4]);
            }
            db.UpdateParticles();
        }

        public void ApplyBaseValues(int? kind = null)
        {
            DynamicBone db = dynamicBone;
            if (kind.HasValue)
            {
                float value = baseValues[kind.Value];
                switch (kind.Value)
                {
                    case 0:
                        db.m_Damping = value;
                        break;
                    case 1:
                        db.m_Elasticity = value;
                        break;
                    case 2:
                        db.m_Inert = value;
                        break;
                    case 3:
                        db.m_Radius = value;
                        break;
                    case 4:
                        db.m_Stiffness = value;
                        break;
                }
            }
            else
            {
                db.m_Damping = baseValues[0];
                db.m_Elasticity = baseValues[1];
                db.m_Inert = baseValues[2];
                db.m_Radius = baseValues[3];
                db.m_Stiffness = baseValues[4];
            }
            db.UpdateParticles();
        }

        public void ResetBaseValues(int? kind = null)
		{
            DynamicBone db = dynamicBone;
            if (kind.HasValue)
            {
                baseValues[kind.Value].Reset();
                float value = baseValues[kind.Value].value;
                switch (kind.Value)
                {
                    case 0:
                        db.m_Damping = value;
                        break;
                    case 1:
                        db.m_Elasticity = value;
                        break;
                    case 2:
                        db.m_Inert = value;
                        break;
                    case 3:
                        db.m_Radius = value;
                        break;
                    case 4:
                        db.m_Stiffness = value;
                        break;
                }
            }
            else
            {
                foreach (var b in baseValues) b.Reset();
                db.m_Damping = baseValues[0];
                db.m_Elasticity = baseValues[1];
                db.m_Inert = baseValues[2];
                db.m_Radius = baseValues[3];
                db.m_Stiffness = baseValues[4];
                foreach (var e in baseValues)
                {
                    e.Reset();
                }
            }
            db.UpdateParticles();
        }

        public void ApplyGravity()
        {
            dynamicBone.m_Gravity = gravity;
        }
        public void ResetGravity()
        {
            gravity.Reset();
            dynamicBone.m_Gravity = gravity;
        }
        public void ApplyForce()
        {
            dynamicBone.m_Force = force;
        }
        public void ResetForce()
        {
            force.Reset();
            dynamicBone.m_Force = force;
        }
        public void ApplyFreezeAxis()
        {
            dynamicBone.m_FreezeAxis = freezeAxis;
        }
        public void ResetFreezeAxis()
        {
            freezeAxis.Reset();
            dynamicBone.m_FreezeAxis = freezeAxis;
        }

        public void ApplyEndOffset()
        {
            dynamicBone.m_EndOffset = endOffset;
            dynamicBone.UpdateParticles();
        }
        public void ResetEndOffset()
        {
            endOffset.Reset();
            dynamicBone.m_EndOffset = endOffset;
            dynamicBone.UpdateParticles();
        }
    }
}


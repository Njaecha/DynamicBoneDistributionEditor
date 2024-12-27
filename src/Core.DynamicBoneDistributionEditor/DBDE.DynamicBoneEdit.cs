using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MessagePack;
using KKAPI.Utilities;
using DBDE.KK_Plugins.DynamicBoneEditor;
using UniRx;
using KKAPI.Maker;
using static AnimationCurveEditor.AnimationCurveEditor.KeyframeEditedArgs;

namespace DynamicBoneDistributionEditor
{
	public class DBDEDynamicBoneEdit
	{
		/// <summary>
        /// Distribution Curves
		/// 0 - Dampening, 1 - Elasticity, 2 - Inertia, 3 - Radius, 4 - Stiffness
		/// </summary>
        public readonly EditableValue<Keyframe[]>[] distributions;
        /// <summary>
        /// Base Values
		/// 0 - Dampening, 1 - Elasticity, 2 - Inertia, 3 - Radius, 4 - Stiffness
		/// </summary>
        public readonly EditableValue<float>[] baseValues;
        
        public EditableValue<Vector3> gravity;
        public EditableValue<Vector3> force;
        public EditableValue<Vector3> endOffset;
        public EditableValue<DynamicBone.FreezeAxis> freezeAxis;
        
        public EditableValue<float> weight;
        
        // uneditable values that are only required for Multi-Bone fix
        private readonly List<Transform> _notRolls;
        private readonly Vector3 _localGravity;
        private readonly List<DynamicBoneCollider> _colliders;
        private readonly List<DynamicBone.Particle> _particles;

        private bool initalActive;
        private bool _active;
        public bool active {get => _active; set => SetActive(value); }

        private DynamicBone _primary;
        public DynamicBone PrimaryDynamicBone { get {
                List<DynamicBone> dbs = DynamicBones;
                if (!_primary || !dbs.Contains(_primary))
                {
                    _primary = dbs.FirstOrDefault();
                } 
                return _primary; 
            } private set => _primary = value; }
        public List<DynamicBone> DynamicBones
        {
            get
            {
                List<DynamicBone> x = AccessorFunction.Invoke();
                return x.IsNullOrEmpty() ? new List<DynamicBone>() : x;
            }
        }

        internal Func<List<DynamicBone>> AccessorFunction { get; private set; }


        internal object ReidentificationData;

        internal string GetButtonName()
        {
            var n = "";
            if (ReidentificationData is KeyValuePair<int, string> kvp) n = $"Slot {kvp.Key + 1} - ";
            return n + PrimaryDynamicBone?.m_Root?.name;
        }

		private static Keyframe[] GetDefaultCurveKeyframes()
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
            this.weight = copyFrom.weight;

            ApplyAll();
        }

		public DBDEDynamicBoneEdit(Func<List<DynamicBone>> accessorFunction, DBDEDynamicBoneEdit copyFrom)
		{
            this.AccessorFunction = accessorFunction;
            List<DynamicBone> dbs = DynamicBones;
            DynamicBone db = PrimaryDynamicBone = dbs.Any(b => b.enabled) ? dbs.FindAll(b => b.enabled).FirstOrDefault() : dbs.FirstOrDefault();
            if (!db)
            {
                DBDE.Logger.LogError("Creating DBDEDynamicBoneEdit failed, Accessor returned zero Dynamic Bones!");
                return;
            }
            
            initalActive = _active = dbs.Any(b => b.enabled);
            
            // values for multi bone fix
            _notRolls = db.m_notRolls;
            _colliders = db.m_Colliders;
            _localGravity = db.m_LocalGravity;
            _particles = db.m_Particles;
            
            this.distributions = new EditableValue<Keyframe[]>[]
            {
                new EditableValue<Keyframe[]>(db.m_DampingDistrib == null ? GetDefaultCurveKeyframes() : db.m_DampingDistrib.keys.Length >= 2 ? db.m_DampingDistrib.keys : GetDefaultCurveKeyframes()),
                new EditableValue<Keyframe[]>(db.m_ElasticityDistrib == null ? GetDefaultCurveKeyframes() : db.m_ElasticityDistrib.keys.Length >= 2 ? db.m_ElasticityDistrib.keys : GetDefaultCurveKeyframes()),
                new EditableValue<Keyframe[]>(db.m_InertDistrib == null ? GetDefaultCurveKeyframes() : db.m_InertDistrib.keys.Length >= 2 ? db.m_InertDistrib.keys : GetDefaultCurveKeyframes()),
                new EditableValue<Keyframe[]>(db.m_RadiusDistrib == null ? GetDefaultCurveKeyframes() : db.m_RadiusDistrib.keys.Length >= 2 ? db.m_RadiusDistrib.keys : GetDefaultCurveKeyframes()),
                new EditableValue<Keyframe[]>(db.m_StiffnessDistrib == null ? GetDefaultCurveKeyframes() : db.m_StiffnessDistrib.keys.Length >= 2 ? db.m_StiffnessDistrib.keys : GetDefaultCurveKeyframes())
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
            weight = new EditableValue<float>(db.m_Weight);

            MultiBoneFix();
            PasteData(copyFrom);
        }

		public DBDEDynamicBoneEdit(Func<List<DynamicBone>> AccessorFunction, byte[] serialised = null, DynamicBoneData DBE = null)
		{
            this.AccessorFunction = AccessorFunction;
            List<DynamicBone> dbs = DynamicBones;
            DynamicBone db = PrimaryDynamicBone = dbs.Any(b => b.enabled) ? dbs.FindAll(b => b.enabled).FirstOrDefault() : dbs.FirstOrDefault();
            if (!db)
            {
                DBDE.Logger.LogError("Creating DBDEDynamicBoneEdit failed, Accessor returned zero Dynamic Bones!");
                return;
            }

            initalActive = _active = dbs.Any(b => b.enabled);
            
            // values for multi bone fix
            _notRolls = db.m_notRolls;
            _colliders = db.m_Colliders;
            _localGravity = db.m_LocalGravity;
            _particles = db.m_Particles;
            
            this.distributions = new EditableValue<Keyframe[]>[]
			{
				new EditableValue<Keyframe[]>(db.m_DampingDistrib == null ? GetDefaultCurveKeyframes() : db.m_DampingDistrib.keys.Length >= 2 ? db.m_DampingDistrib.keys : GetDefaultCurveKeyframes()),
				new EditableValue<Keyframe[]>(db.m_ElasticityDistrib == null ? GetDefaultCurveKeyframes() : db.m_ElasticityDistrib.keys.Length >= 2 ? db.m_ElasticityDistrib.keys : GetDefaultCurveKeyframes()),
				new EditableValue<Keyframe[]>(db.m_InertDistrib == null ? GetDefaultCurveKeyframes() : db.m_InertDistrib.keys.Length >= 2 ? db.m_InertDistrib.keys : GetDefaultCurveKeyframes()),
				new EditableValue<Keyframe[]>(db.m_RadiusDistrib == null ? GetDefaultCurveKeyframes() : db.m_RadiusDistrib.keys.Length >= 2 ? db.m_RadiusDistrib.keys : GetDefaultCurveKeyframes()),
				new EditableValue<Keyframe[]>(db.m_StiffnessDistrib == null ? GetDefaultCurveKeyframes() : db.m_StiffnessDistrib.keys.Length >= 2 ? db.m_StiffnessDistrib.keys : GetDefaultCurveKeyframes())
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
            weight = new EditableValue<float>(db.m_Weight);

			if (serialised != null) // set DBDE specific values
			{
                //DBDE.Logger.LogInfo($"Deserialising {db.m_Root.name}");
                List<byte[]> edits = MessagePackSerializer.Deserialize<List<byte[]>>(serialised);
                //DBDE.Logger.LogInfo($"{GetButtonName()} - Loading Distribution");
                if (!edits[0].IsNullOrEmpty())
                {
                    Dictionary<byte, SerialisableKeyframe[]> distribs = MessagePackSerializer.Deserialize<Dictionary<byte, SerialisableKeyframe[]>>(edits[0]);
				    foreach (byte i in distribs.Keys)
				    {
                        if (DBDE.loadSettingsAsDefault.Value) distributions[i] = (EditableValue<Keyframe[]>)distribs[i].Select(keyframe => (Keyframe)keyframe).ToArray();
                        else distributions[i].value = distribs[i].Select(keyframe => (Keyframe)keyframe).ToArray();
				    }
                }
                //DBDE.Logger.LogInfo($"{GetButtonName()} - Loading Gravity");
                if (!edits[2].IsNullOrEmpty()) LoadVector(edits[2], ref gravity);
                //DBDE.Logger.LogInfo($"{GetButtonName()} - Loading Force");
                if (!edits[3].IsNullOrEmpty()) LoadVector(edits[3], ref force);
                //DBDE.Logger.LogInfo($"{GetButtonName()} - Loading EndOffset");
                if (!edits[4].IsNullOrEmpty()) LoadVector(edits[4], ref endOffset);
                SetActive(MessagePackSerializer.Deserialize<bool>(edits[6]));
            }

            Dictionary<byte, float> DBDEBaseValues = new Dictionary<byte, float>();
            int? DBDEFreezeAxis = null;
            float? DBDEWeight = null;
            if (serialised != null) // load DBDE Data
            {
                List<byte[]> edits = MessagePackSerializer.Deserialize<List<byte[]>>(serialised);
                if (!edits[1].IsNullOrEmpty()) DBDEBaseValues = MessagePackSerializer.Deserialize<Dictionary<byte, float>>(edits[1]);
                if (!edits[5].IsNullOrEmpty()) DBDEFreezeAxis = MessagePackSerializer.Deserialize<byte?>(edits[5]);
                if (edits.Count >= 8 && !edits[7].IsNullOrEmpty()) DBDEWeight = MessagePackSerializer.Deserialize<float>(edits[7]);
            }
            if (DBDE.loadSettingsAsDefault.Value)
            {
                if (DBDEBaseValues.ContainsKey(0)) baseValues[0] = (EditableValue<float>)DBDEBaseValues[0]; // DBDE Data has higher priority
                else if (DBE != null && DBE.Damping.HasValue) baseValues[0] = (EditableValue<float>)DBE.Damping.Value; // Set to DBE Data otherwise if exists
                if (DBDEBaseValues.ContainsKey(1)) baseValues[1] = (EditableValue<float>)DBDEBaseValues[1];
                else if (DBE != null && DBE.Elasticity.HasValue) baseValues[1] = (EditableValue<float>)DBE.Elasticity.Value;
                if (DBDEBaseValues.ContainsKey(2)) baseValues[2] = (EditableValue<float>)DBDEBaseValues[2];
                else if (DBE != null && DBE.Inertia.HasValue) baseValues[2] = (EditableValue<float>)DBE.Inertia.Value;
                if (DBDEBaseValues.ContainsKey(3)) baseValues[3] = (EditableValue<float>)DBDEBaseValues[3];
                else if (DBE != null && DBE.Radius.HasValue) baseValues[3] = (EditableValue<float>)DBE.Radius.Value;
                if (DBDEBaseValues.ContainsKey(4)) baseValues[4] = (EditableValue<float>)DBDEBaseValues[4];
                else if (DBE != null && DBE.Stiffness.HasValue) baseValues[4] = (EditableValue<float>)DBE.Stiffness.Value;

                if (DBDEFreezeAxis.HasValue) freezeAxis = (EditableValue<DynamicBone.FreezeAxis>)(DynamicBone.FreezeAxis)DBDEFreezeAxis.Value;
                else if (DBE != null && DBE.FreezeAxis.HasValue) freezeAxis = (EditableValue<DynamicBone.FreezeAxis>)DBE.FreezeAxis.Value;
                
                if (DBDEWeight.HasValue) weight = (EditableValue<float>)DBDEWeight.Value;
                else if (DBE != null && DBE.Weight.HasValue) weight = (EditableValue<float>)DBE.Weight.Value;
            }
            else
            {
                if (DBDEBaseValues.ContainsKey(0)) baseValues[0].value = DBDEBaseValues[0];
                else if (DBE != null && DBE.Damping.HasValue) baseValues[0].value = DBE.Damping.Value;
                if (DBDEBaseValues.ContainsKey(1)) baseValues[1].value = DBDEBaseValues[1];
                else if (DBE != null && DBE.Elasticity.HasValue) baseValues[1].value = DBE.Elasticity.Value;
                if (DBDEBaseValues.ContainsKey(2)) baseValues[2].value = DBDEBaseValues[2];
                else if (DBE != null && DBE.Inertia.HasValue) baseValues[2].value = DBE.Inertia.Value;
                if (DBDEBaseValues.ContainsKey(3)) baseValues[3].value = DBDEBaseValues[3];
                else if (DBE != null && DBE.Radius.HasValue) baseValues[3].value = DBE.Radius.Value;
                if (DBDEBaseValues.ContainsKey(4)) baseValues[4].value = DBDEBaseValues[4];
                else if (DBE != null && DBE.Stiffness.HasValue) baseValues[4].value = DBE.Stiffness.Value;

                if (DBDEFreezeAxis.HasValue) freezeAxis.value = (DynamicBone.FreezeAxis)DBDEFreezeAxis.Value;
                else if (DBE != null && DBE.FreezeAxis.HasValue) freezeAxis.value = DBE.FreezeAxis.Value;
                
                if (DBDEWeight.HasValue) weight.value = DBDEWeight.Value;
                else if (DBE != null && DBE.Weight.HasValue) weight.value = DBE.Weight.Value;
            }
            
            MultiBoneFix();
            ApplyAll();
        }

        internal void ReferToDynamicBone()
        {
            DynamicBone db = PrimaryDynamicBone;
            if (!db ) return;

            if (MakerAPI.InsideAndLoaded && ReidentificationData is KeyValuePair<int, string> kvp)
            {
                bool noShake = MakerAPI.GetCharacterControl().nowCoordinate.accessory.parts[kvp.Key].noShake;
                initalActive = !noShake;
            }
            _active = DynamicBones.Any(d => d.enabled);

            distributions[0].value = (db?.m_DampingDistrib == null ? GetDefaultCurveKeyframes() : db.m_DampingDistrib.keys.Length >= 2 ? db.m_DampingDistrib.keys : GetDefaultCurveKeyframes());
            distributions[1].value = (db?.m_ElasticityDistrib == null ? GetDefaultCurveKeyframes() : db.m_ElasticityDistrib.keys.Length >= 2 ? db.m_ElasticityDistrib.keys : GetDefaultCurveKeyframes());
            distributions[2].value = (db?.m_InertDistrib == null ? GetDefaultCurveKeyframes() : db.m_InertDistrib.keys.Length >= 2 ? db.m_InertDistrib.keys : GetDefaultCurveKeyframes());
            distributions[3].value = (db?.m_RadiusDistrib == null ? GetDefaultCurveKeyframes() : db.m_RadiusDistrib.keys.Length >= 2 ? db.m_RadiusDistrib.keys : GetDefaultCurveKeyframes());
            distributions[4].value = (db?.m_StiffnessDistrib == null ? GetDefaultCurveKeyframes() : db.m_StiffnessDistrib.keys.Length >= 2 ? db.m_StiffnessDistrib.keys : GetDefaultCurveKeyframes());

            baseValues[0].value = (db.m_Damping);
            baseValues[1].value = (db.m_Elasticity);
            baseValues[2].value = (db.m_Inert);
            baseValues[3].value = (db.m_Radius);
            baseValues[4].value = (db.m_Stiffness);

            gravity.value = (db.m_Gravity);
            force.value = (db.m_Force);
            endOffset.value = (db.m_EndOffset);
            weight.value = (db.m_Weight);
            freezeAxis.value = (db.m_FreezeAxis);

            ApplyAll();
        }

        internal void ReferInitialsToDynamicBone(DynamicBone db)
        {
            Keyframe[] DE0frames = db?.m_DampingDistrib == null ? GetDefaultCurveKeyframes() : db.m_DampingDistrib.keys.Length >= 2 ? db.m_DampingDistrib.keys : GetDefaultCurveKeyframes();
            distributions[0] = new EditableValue<Keyframe[]>(DE0frames, distributions[0].IsEdited ? distributions[0] : DE0frames);
            Keyframe[] DE1frames = db?.m_ElasticityDistrib == null ? GetDefaultCurveKeyframes() : db.m_ElasticityDistrib.keys.Length >= 2 ? db.m_ElasticityDistrib.keys : GetDefaultCurveKeyframes();
            distributions[1] = new EditableValue<Keyframe[]>(DE1frames, distributions[1].IsEdited ? distributions[1] : DE1frames);
            Keyframe[] DE2frames = db?.m_InertDistrib == null ? GetDefaultCurveKeyframes() : db.m_InertDistrib.keys.Length >= 2 ? db.m_InertDistrib.keys : GetDefaultCurveKeyframes();
            distributions[2] = new EditableValue<Keyframe[]>(DE2frames, distributions[2].IsEdited ? distributions[2] : DE2frames);
            Keyframe[] DE3frames = db?.m_RadiusDistrib == null ? GetDefaultCurveKeyframes() : db.m_RadiusDistrib.keys.Length >= 2 ? db.m_RadiusDistrib.keys : GetDefaultCurveKeyframes();
            distributions[3] = new EditableValue<Keyframe[]>(DE3frames, distributions[3].IsEdited ? distributions[3] : DE3frames);
            Keyframe[] DE4frames = db?.m_StiffnessDistrib == null ? GetDefaultCurveKeyframes() : db.m_StiffnessDistrib.keys.Length >= 2 ? db.m_StiffnessDistrib.keys : GetDefaultCurveKeyframes();
            distributions[4] = new EditableValue<Keyframe[]>(DE4frames, distributions[4].IsEdited ? distributions[4] : DE4frames);

            baseValues[0] = new EditableValue<float>(db.m_Damping, baseValues[0].IsEdited ? baseValues[0] : db.m_Damping);
            baseValues[1] = new EditableValue<float>(db.m_Elasticity, baseValues[1].IsEdited ? baseValues[1] : db.m_Elasticity);
            baseValues[2] = new EditableValue<float>(db.m_Inert, baseValues[2].IsEdited ? baseValues[2] : db.m_Inert);
            baseValues[3] = new EditableValue<float>(db.m_Radius, baseValues[3].IsEdited ? baseValues[3] : db.m_Radius);
            baseValues[4] = new EditableValue<float>(db.m_Stiffness, baseValues[4].IsEdited ? baseValues[4] : db.m_Stiffness);

            gravity = new EditableValue<Vector3>(db.m_Gravity, gravity.IsEdited ? gravity : db.m_Gravity);
            force = new EditableValue<Vector3>(db.m_Force, force.IsEdited ? force : db.m_Force);
            endOffset = new EditableValue<Vector3>(db.m_EndOffset, endOffset.IsEdited ? endOffset : db.m_EndOffset);
            weight = new EditableValue<float>(db.m_Weight, weight.IsEdited ? weight : db.m_Weight);
            freezeAxis = new EditableValue<DynamicBone.FreezeAxis>(db.m_FreezeAxis, freezeAxis.IsEdited ? freezeAxis : db.m_FreezeAxis);

            db.enabled = active;
            
            ApplyAll();
        }

        private static void LoadVector(byte[] binary, ref EditableValue<Vector3> editableValue)
        {
            Vector3 sValue = MessagePackSerializer.Deserialize<Vector3>(binary);
            var s = sValue.ToString("F4");
            // DBDE.Logger.LogInfo($"{GetButtonName()} - Loading Vector: {s} (As Default = {DBDE.loadSettingsAsDefault.Value})");
            if (DBDE.loadSettingsAsDefault.Value) editableValue = (EditableValue<Vector3>)sValue;
            else editableValue.value = sValue;
        }

        public void SetActive(bool newActive)
        {
            PrimaryDynamicBone.enabled = newActive;
            _active = newActive;
        }

		public AnimationCurve GetAnimationCurve(byte kind)
		{ 
            return new AnimationCurve(distributions[kind]);
		}

        public void SetAnimationCurve(int kind, AnimationCurve animationCurve)
		{
			distributions[kind].value = animationCurve.keys;
		}

        // currently not used....
        internal static byte[] SerialseFromDBE(DynamicBoneData DBE, byte[] serialsedDBDEData = null)
        {

            byte[] Distributions = MessagePackSerializer.Serialize(new Dictionary<byte, SerialisableKeyframe[]>());

            Dictionary<byte, float> DBDEBaseValues = new Dictionary<byte, float>();

            byte[] Gravity = null;
            byte[] Force = null;
            byte[] EndOffset = null;

            byte[] DBDEFreezeAxis = null;

            if (serialsedDBDEData != null) // load DBDE Data
            {
                List<byte[]> e = MessagePackSerializer.Deserialize<List<byte[]>>(serialsedDBDEData);
                if (!e[0].IsNullOrEmpty() && MessagePackSerializer.Deserialize<Dictionary<byte, SerialisableKeyframe[]>>(e[0]).Count > 0) Distributions = e[0];
                if (!e[1].IsNullOrEmpty()) DBDEBaseValues = MessagePackSerializer.Deserialize<Dictionary<byte, float>>(e[1]); // baseValues
                if (!e[2].IsNullOrEmpty()) Gravity = e[2]; // gravity
                if (!e[3].IsNullOrEmpty()) Force = e[3]; // force
                if (!e[4].IsNullOrEmpty()) EndOffset = e[4]; // endOffset
                if (!e[5].IsNullOrEmpty()) DBDEFreezeAxis = e[5]; // freezeAxis
            }

            Dictionary<byte, float> bValues = new Dictionary<byte, float>();
            if (DBDEBaseValues.ContainsKey(0)) bValues.Add(0, DBDEBaseValues[0]);
            else if (DBE.Damping.HasValue) bValues.Add(0, DBE.Damping.Value);
            if (DBDEBaseValues.ContainsKey(1)) bValues.Add(1, DBDEBaseValues[1]);
            else if (DBE.Elasticity.HasValue) bValues.Add(1, DBE.Elasticity.Value);
            if (DBDEBaseValues.ContainsKey(2)) bValues.Add(2, DBDEBaseValues[2]);
            else if (DBE.Inertia.HasValue) bValues.Add(2, DBE.Inertia.Value);
            if (DBDEBaseValues.ContainsKey(3)) bValues.Add(3, DBDEBaseValues[3]);
            else if (DBE.Radius.HasValue) bValues.Add(3, DBE.Radius.Value);
            if (DBDEBaseValues.ContainsKey(4)) bValues.Add(4, DBDEBaseValues[4]);
            else if (DBE.Stiffness.HasValue) bValues.Add(4, DBE.Stiffness.Value);

            List<byte[]> edits = new List<byte[]>() {
                Distributions, 
                MessagePackSerializer.Serialize(bValues), // baseValues
                Gravity, 
                Force, 
                EndOffset, 
                DBDEFreezeAxis != null ? DBDEFreezeAxis : DBE.FreezeAxis.HasValue ? MessagePackSerializer.Serialize((byte)DBE.FreezeAxis) : null, // freezeAxis as byte or null
                MessagePackSerializer.Serialize(true) // always assumet the bone is enabled because DBE cannot disable it
            };

            return MessagePackSerializer.Serialize(edits);
        }

		public byte[] Serialise()
		{
            Dictionary<byte, SerialisableKeyframe[]> distribs = distributions
				.Select((t, i) => new KeyValuePair<byte, EditableValue<Keyframe[]>>((byte)i, t))
				.Where(kvp => kvp.Value.IsEdited)
				.ToDictionary(x => x.Key, x => ((Keyframe[])x.Value).Select(keyframe => (SerialisableKeyframe)keyframe).ToArray());
            byte[] sDistrib = MessagePackSerializer.Serialize(distribs);
			Dictionary<byte, float> bValues = baseValues
				.Select((v, i) => new KeyValuePair<byte, EditableValue<float>>((byte)i, v))
				.Where(kvp => kvp.Value.IsEdited)
				.ToDictionary(x => x.Key, x => (float)x.Value);
            byte[] sBaseValues = MessagePackSerializer.Serialize(bValues);

            byte[] sGravity = SerialiseEditableVector(gravity);
            byte[] sForce = SerialiseEditableVector(force);
            byte[] sEndOffset = SerialiseEditableVector(endOffset);
            byte[] sAxis = null;
            
            if (freezeAxis.IsEdited)
            {
                sAxis = MessagePackSerializer.Serialize(((byte?)freezeAxis.value));
            }
            byte[] sActive = MessagePackSerializer.Serialize(active);

            byte[] sWeight = MessagePackSerializer.Serialize(weight.value);
            
            var edits = new List<byte[]>() {sDistrib, sBaseValues, sGravity, sForce, sEndOffset, sAxis, sActive, sWeight };
            return MessagePackSerializer.Serialize(edits);
		}

        private static byte[] SerialiseEditableVector(EditableValue<Vector3> editableValue)
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
            return distributions[kind].IsEdited || baseValues[kind].IsEdited;
        }

        public bool isActiveEdited()
        {
            return initalActive != active;
        }

        public bool isWeigthEdited()
        {
            return weight.IsEdited;
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
            if (initalActive != active) return true;
            if (weight.IsEdited) return true;
			return false;
		}

        internal void UpdateActiveStack(bool always = false)
        {
            List<DynamicBone> DBS = DynamicBones;
            if (DBS.IsNullOrEmpty()) return;
            if (active && DBS.FindAll(b => b.enabled == true).Count() == 1) return;
            if (DBS.Count <= 1 && !always) return;
            foreach (DynamicBone db in DBS) db.enabled = false; //disable others
            if (active)
            {
                PrimaryDynamicBone.enabled = true; // enable primary
            }
        }
        /// <summary>
        /// Copies the unchangeable values from the PrimaryDynamicBone to the other DynamicBone components
        /// </summary>
        internal void MultiBoneFix()
        {
            if (DynamicBones.IsNullOrEmpty() || DynamicBones.Count < 2) return;
            foreach (DynamicBone db in DynamicBones)
            {
                db.m_notRolls = _notRolls;
                db.m_Particles = _particles;
                db.m_Colliders = _colliders;
                // I hate this but if I don't delay setting the LocalGravity it gets overwritten right again
                DBDE.Instance.StartCoroutine(ShitHead(db)); 
            }
        }

        private IEnumerator ShitHead(DynamicBone db)
        {
            yield return null;
            db.m_LocalGravity = _localGravity;
            //DBDE.Logger.LogDebug($"Fixed {db.name}, bone count: {db.m_Particles.Count}, gravity: {db.m_LocalGravity.x:0.000}, {db.m_LocalGravity.y:0.000}, {db.m_LocalGravity.z:0.000}");
        }

        public void ApplyAll()
        {
            _ = PrimaryDynamicBone; // make sure PrimaryDynamicBone is always obtained
            ApplyDistribution();
            ApplyBaseValues();
            ApplyGravity();
            ApplyForce();
            ApplyEndOffset();
            ApplyFreezeAxis();
            ApplyWeight();
            // does this create issues?
            SetActive(active);
        }

        public void ResetAll()
        {
            ResetBaseValues();
            ResetDistribution();
            ResetGravity();
            ResetForce();
            ResetEndOffset();
            ResetFreezeAxis();
            ResetWeight();
            SetActive(initalActive);
        }

		public void ApplyDistribution(int? kind = null)
        {
            foreach (DynamicBone db in DynamicBones)
            {
                if (!db) continue;
                if (kind.HasValue)
                {
                    Keyframe[] keys = distributions[kind.Value];
                    switch (kind.Value)
                    {
                        case 0:
                            if (db.m_DampingDistrib == null) db.m_DampingDistrib = new AnimationCurve(keys);
                            else db.m_DampingDistrib.SetKeys(keys);
                            break;
                        case 1:
                            if (db.m_ElasticityDistrib == null) db.m_ElasticityDistrib = new AnimationCurve(keys);
                            else db.m_ElasticityDistrib.SetKeys(keys);
                            break;
                        case 2:
                            if (db.m_InertDistrib == null) db.m_InertDistrib = new AnimationCurve(keys);
                            else db.m_InertDistrib.SetKeys(keys);
                            break;
                        case 3:
                            if (db.m_RadiusDistrib == null) db.m_RadiusDistrib = new AnimationCurve(keys);
                            else db.m_RadiusDistrib.SetKeys(keys);
                            break;
                        case 4:
                            if (db.m_StiffnessDistrib == null) db.m_StiffnessDistrib = new AnimationCurve(keys);
                            else db.m_StiffnessDistrib.SetKeys(keys);
                            break;
                    }
                }
                else
                {
                    if (db.m_DampingDistrib == null) db.m_DampingDistrib = new AnimationCurve(distributions[0]);
                    else db.m_DampingDistrib.SetKeys(distributions[0]);
                    if (db.m_ElasticityDistrib == null) db.m_ElasticityDistrib = new AnimationCurve(distributions[1]);
                    else db.m_ElasticityDistrib.SetKeys(distributions[1]);
                    if (db.m_InertDistrib == null) db.m_InertDistrib = new AnimationCurve(distributions[2]);
                    else db.m_InertDistrib.SetKeys(distributions[2]);
                    if (db.m_RadiusDistrib == null) db.m_RadiusDistrib = new AnimationCurve(distributions[3]);
                    else db.m_RadiusDistrib.SetKeys(distributions[3]);
                    if (db.m_StiffnessDistrib == null) db.m_StiffnessDistrib = new AnimationCurve(distributions[4]);
                    else db.m_StiffnessDistrib.SetKeys(distributions[4]);
                }
                db.UpdateParticles();
            }

            UpdateActiveStack();
        }

        public void ResetDistribution(int? kind = null)
        {
            foreach (DynamicBone db in DynamicBones)
            {
                if (!db) return;
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
                    for (int i = 0; i < distributions.Length; i++) distributions[i].Reset();
                    db.m_DampingDistrib?.SetKeys(distributions[0]);
                    db.m_ElasticityDistrib?.SetKeys(distributions[1]);
                    db.m_InertDistrib?.SetKeys(distributions[2]);
                    db.m_RadiusDistrib?.SetKeys(distributions[3]);
                    db.m_StiffnessDistrib?.SetKeys(distributions[4]);
                }
                db.UpdateParticles();
            }

            UpdateActiveStack();
        }

        public void ApplyBaseValues(int? kind = null)
        {
            foreach (DynamicBone db in DynamicBones)
            {
                if (!db) return;
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

            UpdateActiveStack();
        }

        public void ResetBaseValues(int? kind = null)
        {
            foreach (DynamicBone db in DynamicBones)
            {
                if (!db) return;
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
                    for (int i = 0; i < baseValues.Length; i++) baseValues[i].Reset();
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

            UpdateActiveStack();
        }

        public void ApplyGravity()
        {
            foreach (DynamicBone db in DynamicBones)
            {
                db.m_Gravity = gravity;
            }

            UpdateActiveStack();
        }
        public void ResetGravity()
        {
            gravity.Reset();
            foreach (DynamicBone db in DynamicBones)
            {
                db.m_Gravity = gravity;
            }
            UpdateActiveStack();
        }
        public void ApplyForce()
        {
            foreach (DynamicBone db in DynamicBones)
            {
                db.m_Force = force;
            }

            UpdateActiveStack();
        }
        public void ResetForce()
        {
            force.Reset();
            foreach (DynamicBone db in DynamicBones)
            {
                db.m_Force = force;
            }
            UpdateActiveStack();
        }
        public void ApplyFreezeAxis()
        {
            foreach (DynamicBone db in DynamicBones)
            {
                db.m_FreezeAxis = freezeAxis;
            }

            UpdateActiveStack();
        }
        public void ResetFreezeAxis()
        {
            freezeAxis.Reset();
            foreach (DynamicBone db in DynamicBones)
            {
                db.m_FreezeAxis = freezeAxis;
            }
            UpdateActiveStack();
        }

        public void ApplyEndOffset()
        {
            foreach (DynamicBone db in DynamicBones)
            {
                db.m_EndOffset = endOffset;
                db.UpdateParticles();
            }

            UpdateActiveStack();
        }
        public void ResetEndOffset()
        {
            endOffset.Reset();
            foreach (DynamicBone db in DynamicBones)
            {
                db.m_EndOffset = endOffset;
                db.UpdateParticles();
            }
            UpdateActiveStack();
        }

        public void ApplyWeight()
        {
            foreach (DynamicBone db in DynamicBones)
            {
                db.m_Weight = weight;
                db.UpdateParticles();
            }
            UpdateActiveStack();
        }
        
        public void ResetWeight()
        {
            weight.Reset();
            foreach (DynamicBone db in DynamicBones)
            {
                db.m_Weight = weight;
                db.UpdateParticles();
            }
            UpdateActiveStack();
        }

        public override string ToString()
        {
            if (_primary != null && _primary.m_Root != null) return _primary.m_Root.ToString();
            return "DeadBone " + base.ToString();
        }
    }
}


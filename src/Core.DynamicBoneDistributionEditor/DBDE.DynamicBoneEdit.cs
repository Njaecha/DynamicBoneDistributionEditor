using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MessagePack;
using KKAPI.Utilities;
using DBDE.KK_Plugins.DynamicBoneEditor;
using DynamicBoneDistributionEditor;
using UniRx;
using KKAPI.Maker;
using Studio;
using static AnimationCurveEditor.AnimationCurveEditor.KeyframeEditedArgs;

namespace DynamicBoneDistributionEditor
{
	public class DBDEDynamicBoneEdit
    {

        public class EditHolder
        {
            internal readonly DBDECharaController character = null;
            internal readonly OCIItem item = null;

            public EditHolder(DBDECharaController parentCharacter)
            {
                character = parentCharacter;
            }

            public EditHolder(OCIItem parentItem)
            {
                item = parentItem;
            }

            internal string GetPath()
            {
                return character ? character.transform.GetFullPath() : item.itemComponent.transform.GetFullPath();
            }

            internal Transform GetTransform()
            {
                return character ? character.transform : item.itemComponent.transform;
            }
        }
        
        public readonly EditHolder holder;
        
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

        /// <summary>
        /// List of strings that correspond to the transforms in m_notRolls
        /// </summary>
        internal EditableList<string> notRolls;
        public List<Transform> NotRollsTransforms
        {
            get
            {
                return notRolls.Select(bone => PrimaryDynamicBone.m_Root.Find(bone)).Where(t => t).ToList();
            }
        }
        /// <summary>
        /// List of strings that correspond to the transforms in m_Exclusions
        /// </summary>
        internal EditableList<string> Exclusions;
        public List<Transform> ExclusionsTransforms
        {
            get
            {
                if (Exclusions.Count == 0) return new List<Transform>();
                return Exclusions.Select(t => PrimaryDynamicBone.m_Root.Find(t)).Where(t => t).ToList();
            }
        }
        

        private List<Transform> BoneChainTransforms
        {
            get
            {
                var chainTransforms = new List<Transform> { PrimaryDynamicBone.m_Root };
                Transform child = PrimaryDynamicBone.m_Root.GetChild(0);
                while (child.childCount > 0)
                {
                    chainTransforms.Add(child);
                    child = child.GetChild(0);
                }
                return chainTransforms;
            }
        }
        
        
        // un-editable values that are only required for Multi-Bone fix
        public Vector3 _localGravity { get; private set; }
        public List<DynamicBoneCollider> _colliders { get; private set; }
        public List<DynamicBone.Particle> _particles { get;  private set; }

        public bool InitialActive { get; private set; }
        private bool _active;
        public bool active {get => _active; set => SetActive(value); }

        private bool _primaryUsedBefore = false;
        public String PrimaryLocation { get; private set; } = "NotSetYet"; 
        public DynamicBone PrimaryDynamicBone { get {
                List<DynamicBone> dbs = DynamicBones;
                if (dbs.IsNullOrEmpty())
                {
                    DBDE.Logger.LogVerbose($"Dynamic Bones still null or empty");
                    return null;
                }
                
                DynamicBone primary = dbs.Find(db => holder.GetTransform().GetPathToChild(db.transform) == PrimaryLocation);
                if (!primary)
                {
                    primary = dbs.Any(db => db.enabled) ? dbs.First(db => db.enabled) : dbs.FirstOrDefault();
                }
                if (primary)
                {
                    string oldPrimary = PrimaryLocation;
                    PrimaryLocation = holder.GetTransform().GetPathToChild(primary.transform);
                    if (oldPrimary != PrimaryLocation)
                    {
                        var n = "";
                        if (ReidentificationData is KeyValuePair<int, string> kvp) n = $"Slot {kvp.Key + 1} - ";
                        DBDE.Logger.LogVerbose(
                            $"Primary set: {n + primary.m_Root?.name} | Location: {primary.GetFullPath()}");
                        ReferMultiBoneFixToDynamicBone(primary);
                    }
                }
                else {DBDE.Logger.LogVerbose($"Primary still null");}

                if (_primaryUsedBefore) return primary;
                
                _primaryUsedBefore = true;
                ReferMultiBoneFixToDynamicBone(primary);
                return primary;
            } 
        }
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

            if (copyFrom.notRolls.Count > 0)
            {
                this.notRolls = new EditableList<string>(copyFrom.notRolls);
                ApplyNotRollsAndExclusions();
            }

            if (copyFrom.Exclusions.Count > 0)
            {
                this.Exclusions = new EditableList<string>(copyFrom.Exclusions);
                ApplyNotRollsAndExclusions();
            }
        }

        /// <summary>
        /// Constructor to create a DBDE-Edit from an already existing DBDE-Edit.
        /// </summary>
        /// <param name="accessorFunction"></param>
        /// <param name="copyFrom"></param>
        /// <param name="bonesAvailable"></param>
        /// <param name="belongsTo">What this Edit belongs to</param>
        public DBDEDynamicBoneEdit(Func<List<DynamicBone>> accessorFunction, EditHolder belongsTo, DBDEDynamicBoneEdit copyFrom, bool bonesAvailable)
        {
            this.holder = belongsTo;
            
            this.AccessorFunction = accessorFunction;
            if (bonesAvailable)
            {
                List<DynamicBone> dbs = DynamicBones;
                DynamicBone db = PrimaryDynamicBone;
                if (!db)
                {
                    DBDE.Logger.LogError("Creating DBDEDynamicBoneEdit failed, Accessor returned zero Dynamic Bones!");
                    return;
                }
                
                InitialActive = _active = dbs.Any(b => b.enabled);

                this.distributions = new EditableValue<Keyframe[]>[5];
                this.baseValues = new EditableValue<float>[5];

                ReferInitialsToDynamicBone(db);
                MultiBoneFix();
            }
            else
            {
                InitialActive = copyFrom.InitialActive;
                
                this.distributions = new EditableValue<Keyframe[]>[]
                {
                    new EditableValue<Keyframe[]>(copyFrom.distributions[0].initialValue),
                    new EditableValue<Keyframe[]>(copyFrom.distributions[1].initialValue),
                    new EditableValue<Keyframe[]>(copyFrom.distributions[2].initialValue),
                    new EditableValue<Keyframe[]>(copyFrom.distributions[3].initialValue),
                    new EditableValue<Keyframe[]>(copyFrom.distributions[4].initialValue)
                };
                this.baseValues = new EditableValue<float>[]
                {
                    new EditableValue<float>(copyFrom.baseValues[0].initialValue),
                    new EditableValue<float>(copyFrom.baseValues[1].initialValue),
                    new EditableValue<float>(copyFrom.baseValues[2].initialValue),
                    new EditableValue<float>(copyFrom.baseValues[3].initialValue),
                    new EditableValue<float>(copyFrom.baseValues[4].initialValue)
                };

                gravity = new EditableValue<Vector3>(copyFrom.gravity.initialValue);
                force = new EditableValue<Vector3>(copyFrom.force.initialValue);
                endOffset = new EditableValue<Vector3>(copyFrom.endOffset.initialValue);
                weight = new EditableValue<float>(copyFrom.weight.initialValue);
                freezeAxis = new EditableValue<DynamicBone.FreezeAxis>(copyFrom.freezeAxis.initialValue);
                notRolls = new EditableList<string>(copyFrom.notRolls);
                Exclusions = new EditableList<string>(copyFrom.Exclusions);
            }
            
            PasteData(copyFrom);
        }

        public DBDEDynamicBoneEdit(Func<List<DynamicBone>> accessorFunction, EditHolder belongsTo,
            SerialisedEdit serialised)
        {
            this.holder = belongsTo;
            this.AccessorFunction = accessorFunction;
            
            InitialActive = serialised.InitialActive;
            PrimaryLocation = serialised.PrimaryLocation;
                
            this.distributions = new[]
            {
                new EditableValue<Keyframe[]>(serialised.DampingDistributionInitial.Select(k => (Keyframe)k).ToArray(), serialised.DampingDistribution.Select(k => (Keyframe)k).ToArray()),
                new EditableValue<Keyframe[]>(serialised.ElasticityDistributionInitial.Select(k => (Keyframe)k).ToArray(),serialised.ElasticityDistribution.Select(k => (Keyframe)k).ToArray()),
                new EditableValue<Keyframe[]>(serialised.InertiaDistributionInitial.Select(k => (Keyframe)k).ToArray(),serialised.InertiaDistribution.Select(k => (Keyframe)k).ToArray()),
                new EditableValue<Keyframe[]>(serialised.RadiusDistributionInitial.Select(k => (Keyframe)k).ToArray(),serialised.RadiusDistribution.Select(k => (Keyframe)k).ToArray()),
                new EditableValue<Keyframe[]>(serialised.StiffnessDistributionInitial.Select(k => (Keyframe)k).ToArray(),serialised.StiffnessDistribution.Select(k => (Keyframe)k).ToArray())
            };
            this.baseValues = new[]
            {
                new EditableValue<float>(serialised.DampingBaseValueInitial, serialised.DampingBaseValue),
                new EditableValue<float>(serialised.ElasticityBaseValueInitial, serialised.ElasticityBaseValue),
                new EditableValue<float>(serialised.InertiaBaseValueInitial, serialised.InertiaBaseValue),
                new EditableValue<float>(serialised.RadiusBaseValueInitial, serialised.RadiusBaseValue),
                new EditableValue<float>(serialised.StiffnessBaseValueInitial, serialised.StiffnessBaseValue),
            };

            gravity = new EditableValue<Vector3>(serialised.GravityInitial, serialised.Gravity);
            force = new EditableValue<Vector3>(serialised.ForceInitial, serialised.Force);
            endOffset = new EditableValue<Vector3>(serialised.EndOffsetInitial, serialised.EndOffset);
            weight = new EditableValue<float>(serialised.WeightInitial, serialised.Weight);
            freezeAxis = new EditableValue<DynamicBone.FreezeAxis>(serialised.FreezeAxisInitial, serialised.FreezeAxis);
            notRolls = new EditableList<string>(serialised.NotRollsInitial, serialised.NotRolls);
            Exclusions = new EditableList<string>(serialised.ExclusionsInitial, serialised.Exclusions);
            
            this.SetActive(serialised.Active);
            this.ApplyAll();
            ApplyNotRollsAndExclusions();
        }

        /// <summary>
        /// Constructor to create a new Edit from scratch, OLD seriliased data and/or DBE data.
        /// </summary>
        /// <param name="accessorFunction">How to get access to the dynamic bones</param>
        /// <param name="belongsTo"></param>
        /// <param name="serialised">Serialised DBDE Edit data</param>
        /// <param name="DBE">DBE Data</param>
        public DBDEDynamicBoneEdit(Func<List<DynamicBone>> accessorFunction, EditHolder belongsTo, byte[] serialised = null, DynamicBoneData DBE = null)
        {
            DBDE.Logger.LogVerbose($"Creating Edit (Default) | from serialised: {serialised != null}; from DBE {DBE != null}");
            this.holder = belongsTo;
            
            this.AccessorFunction = accessorFunction;
            List<DynamicBone> dbs = DynamicBones;
            DynamicBone db = PrimaryDynamicBone;
            if (!db)
            {
                DBDE.Logger.LogError("Creating DBDEDynamicBoneEdit failed, Accessor returned zero Dynamic Bones!");
                return;
            }

            InitialActive = _active = dbs.Any(b => b.enabled);

            this.distributions = new EditableValue<Keyframe[]>[5];
            this.baseValues = new EditableValue<float>[5];

            ReferInitialsToDynamicBone(db);

            var notRollsEcxlusionsLoaded = false;
            
			if (serialised != null) // set DBDE specific values
			{
                DBDE.Logger.LogVerbose($"Deserialising {db.m_Root.name} | OLD FORMAT");
                List<byte[]> edits = MessagePackSerializer.Deserialize<List<byte[]>>(serialised);
                DBDE.Logger.LogVerbose($"{GetButtonName()} - Loading Distribution | OLD FORMAT");
                if (!edits[0].IsNullOrEmpty())
                {
                    Dictionary<byte, SerialisableKeyframe[]> distribs = MessagePackSerializer.Deserialize<Dictionary<byte, SerialisableKeyframe[]>>(edits[0]);
				    foreach (byte i in distribs.Keys)
				    {
                        distributions[i].value = distribs[i].Select(keyframe => (Keyframe)keyframe).ToArray();
				    }
                }
                DBDE.Logger.LogVerbose($"{GetButtonName()} - Loading Gravity | OLD FORMAT");
                if (!edits[2].IsNullOrEmpty()) LoadVector(edits[2], ref gravity);
                DBDE.Logger.LogVerbose($"{GetButtonName()} - Loading Force | OLD FORMAT");
                if (!edits[3].IsNullOrEmpty()) LoadVector(edits[3], ref force);
                DBDE.Logger.LogVerbose($"{GetButtonName()} - Loading EndOffset | OLD FORMAT");
                if (!edits[4].IsNullOrEmpty()) LoadVector(edits[4], ref endOffset);
                SetActive(MessagePackSerializer.Deserialize<bool>(edits[6]));

                if (edits.Count > 9)
                {
                    if (!edits[8].IsNullOrEmpty())
                    {
                        DBDE.Logger.LogVerbose($"{GetButtonName()} - Loading NotRolls | OLD FORMAT");
                        var nr = MessagePackSerializer.Deserialize<List<string>>(edits[8]);
                        if (!nr.IsNullOrEmpty()) notRolls = new EditableList<string>(nr);
                        notRollsEcxlusionsLoaded = true;
                    }

                    if (!edits[9].IsNullOrEmpty())
                    {
                        DBDE.Logger.LogVerbose($"{GetButtonName()} - Loading Exclusions | OLD FORMAT");
                        var exc = MessagePackSerializer.Deserialize<List<string>>(edits[9]);
                        if (!exc.IsNullOrEmpty()) Exclusions = new EditableList<string>(exc);
                        notRollsEcxlusionsLoaded = true;
                    }
                }
            }

            Dictionary<byte, float> DBDEBaseValues = new Dictionary<byte, float>();
            int? DBDEFreezeAxis = null;
            float? DBDEWeight = null;
            if (serialised != null) // load DBDE Data
            {
                List<byte[]> edits = MessagePackSerializer.Deserialize<List<byte[]>>(serialised);
                if (!edits[1].IsNullOrEmpty()) DBDEBaseValues = MessagePackSerializer.Deserialize<Dictionary<byte, float>>(edits[1]);
                if (!edits[5].IsNullOrEmpty()) DBDEFreezeAxis = MessagePackSerializer.Deserialize<byte?>(edits[5]);
                if (edits.Count > 7 && !edits[7].IsNullOrEmpty()) DBDEWeight = MessagePackSerializer.Deserialize<float>(edits[7]);
            }
            
            // Load Serialised Data
            if (DBDEBaseValues.ContainsKey(0)) baseValues[0].value = DBDEBaseValues[0]; // DBDE has higher priority
            else if (DBE != null && DBE.Damping.HasValue) baseValues[0].value = DBE.Damping.Value; // else load DBE if exists
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
            
            MultiBoneFix();
            ApplyAll();
            if (notRollsEcxlusionsLoaded) ApplyNotRollsAndExclusions();
        }

        private void ReferMultiBoneFixToDynamicBone(DynamicBone db)
        {
            DBDE.Logger.LogVerbose($"Referring Colliders, LocalGravity and Particles to Dynamic Bone | Location: {db.transform.GetFullPath()} Root: {db.m_Root.GetFullPath()}");
            _colliders = db.m_Colliders;
            _localGravity = db.m_LocalGravity;
            _particles = db.m_Particles;
        }

        /// <summary>
        /// Refers the edited (not initial) values to the passed dynamic bone or the values of the current primary dynamic bone
        /// </summary>
        /// <param name="db"></param>
        /// <param name="fromUI"></param>
        internal void ReferToDynamicBone(DynamicBone db = null, bool fromUI = false)
        {
            if (fromUI && DBDE.verboseStopUiSpam.Value) Extension.DoVerboseLoggingOverride = false;
            
            if (!db) db = PrimaryDynamicBone;
            
            DBDE.Logger.LogVerbose($"Referring values to Dynamic Bone | Location: {db.transform.GetFullPath()} Root: {db.m_Root.GetFullPath()}");
            
            if (!db ) return;

            if (MakerAPI.InsideAndLoaded && ReidentificationData is KeyValuePair<int, string> kvp)
            {
                bool noShake = MakerAPI.GetCharacterControl().nowCoordinate.accessory.parts[kvp.Key].noShake;
                InitialActive = !noShake;
            }
            _active = DynamicBones.Any(d => d.enabled);

            distributions[0].value = (db?.m_DampingDistrib == null ? GetDefaultCurveKeyframes() : db.m_DampingDistrib.keys.Length >= 2 ? db.m_DampingDistrib.keys : GetDefaultCurveKeyframes());
            DBDE.Logger.LogVerbose($"  > Damping distribution: {distributions[0].value}");
            distributions[1].value = (db?.m_ElasticityDistrib == null ? GetDefaultCurveKeyframes() : db.m_ElasticityDistrib.keys.Length >= 2 ? db.m_ElasticityDistrib.keys : GetDefaultCurveKeyframes());
            DBDE.Logger.LogVerbose($"  > Elasticity distribution: {distributions[1].value}");
            distributions[2].value = (db?.m_InertDistrib == null ? GetDefaultCurveKeyframes() : db.m_InertDistrib.keys.Length >= 2 ? db.m_InertDistrib.keys : GetDefaultCurveKeyframes());
            DBDE.Logger.LogVerbose($"  > Inertia distribution: {distributions[2].value}");
            distributions[3].value = (db?.m_RadiusDistrib == null ? GetDefaultCurveKeyframes() : db.m_RadiusDistrib.keys.Length >= 2 ? db.m_RadiusDistrib.keys : GetDefaultCurveKeyframes());
            DBDE.Logger.LogVerbose($"  > Stiffness distribution: {distributions[3].value}");
            distributions[4].value = (db?.m_StiffnessDistrib == null ? GetDefaultCurveKeyframes() : db.m_StiffnessDistrib.keys.Length >= 2 ? db.m_StiffnessDistrib.keys : GetDefaultCurveKeyframes());
            DBDE.Logger.LogVerbose($"  > Stiffness distribution: {distributions[4].value}");

            baseValues[0].value = (db.m_Damping);
            DBDE.Logger.LogVerbose($"  > Damping: {baseValues[0].value}");
            baseValues[1].value = (db.m_Elasticity);
            DBDE.Logger.LogVerbose($"  > Elasticity: {baseValues[1].value}");
            baseValues[2].value = (db.m_Inert);
            DBDE.Logger.LogVerbose($"  > Inertia: {baseValues[2].value}");
            baseValues[3].value = (db.m_Radius);
            DBDE.Logger.LogVerbose($"  > Radius: {baseValues[3].value}");
            baseValues[4].value = (db.m_Stiffness);
            DBDE.Logger.LogVerbose($"  > Stiffness: {baseValues[4].value}");

            gravity.value = (db.m_Gravity);
            DBDE.Logger.LogVerbose($"  > Gravity: {gravity.value}");
            force.value = (db.m_Force);
            DBDE.Logger.LogVerbose($"  > Force: {force.value}");
            endOffset.value = (db.m_EndOffset);
            DBDE.Logger.LogVerbose($"  > endOffset: {endOffset.value}");
            weight.value = (db.m_Weight);
            DBDE.Logger.LogVerbose($"  > Weight: {weight.value}");
            freezeAxis.value = (db.m_FreezeAxis);
            DBDE.Logger.LogVerbose($"  > FreezeAxis: {freezeAxis.value}");

            ApplyAll();
            
            Extension.DoVerboseLoggingOverride = true;
        }

        internal void ReferInitialsToDynamicBone(DynamicBone db)
        {
            DBDE.Logger.LogVerbose($"Referring initials to Dynamic Bone | Location: {db.transform.GetFullPath()} Root: {db.m_Root.GetFullPath()}");
            
            Keyframe[] DE0frames = db?.m_DampingDistrib == null ? GetDefaultCurveKeyframes() : db.m_DampingDistrib.keys.Length >= 2 ? db.m_DampingDistrib.keys : GetDefaultCurveKeyframes();
            distributions[0] = new EditableValue<Keyframe[]>(DE0frames, distributions[0].IsEdited ? distributions[0] : DE0frames);
            DBDE.Logger.LogVerbose($"  > Damping distribution: {distributions[0].initialValue}");
            Keyframe[] DE1frames = db?.m_ElasticityDistrib == null ? GetDefaultCurveKeyframes() : db.m_ElasticityDistrib.keys.Length >= 2 ? db.m_ElasticityDistrib.keys : GetDefaultCurveKeyframes();
            distributions[1] = new EditableValue<Keyframe[]>(DE1frames, distributions[1].IsEdited ? distributions[1] : DE1frames);
            DBDE.Logger.LogVerbose($"  > Elasticity distribution: {distributions[1].initialValue}");
            Keyframe[] DE2frames = db?.m_InertDistrib == null ? GetDefaultCurveKeyframes() : db.m_InertDistrib.keys.Length >= 2 ? db.m_InertDistrib.keys : GetDefaultCurveKeyframes();
            distributions[2] = new EditableValue<Keyframe[]>(DE2frames, distributions[2].IsEdited ? distributions[2] : DE2frames);
            DBDE.Logger.LogVerbose($"  > Inertia distribution: {distributions[2].initialValue}");
            Keyframe[] DE3frames = db?.m_RadiusDistrib == null ? GetDefaultCurveKeyframes() : db.m_RadiusDistrib.keys.Length >= 2 ? db.m_RadiusDistrib.keys : GetDefaultCurveKeyframes();
            distributions[3] = new EditableValue<Keyframe[]>(DE3frames, distributions[3].IsEdited ? distributions[3] : DE3frames);
            DBDE.Logger.LogVerbose($"  > Stiffness distribution: {distributions[3].initialValue}");
            Keyframe[] DE4frames = db?.m_StiffnessDistrib == null ? GetDefaultCurveKeyframes() : db.m_StiffnessDistrib.keys.Length >= 2 ? db.m_StiffnessDistrib.keys : GetDefaultCurveKeyframes();
            distributions[4] = new EditableValue<Keyframe[]>(DE4frames, distributions[4].IsEdited ? distributions[4] : DE4frames);
            DBDE.Logger.LogVerbose($"  > Stiffness distribution: {distributions[4].initialValue}");

            baseValues[0] = new EditableValue<float>(db.m_Damping, baseValues[0].IsEdited ? baseValues[0] : db.m_Damping);
            DBDE.Logger.LogVerbose($"  > Damping: {baseValues[0].initialValue}");
            baseValues[1] = new EditableValue<float>(db.m_Elasticity, baseValues[1].IsEdited ? baseValues[1] : db.m_Elasticity);
            DBDE.Logger.LogVerbose($"  > Elasticity: {baseValues[1].initialValue}");
            baseValues[2] = new EditableValue<float>(db.m_Inert, baseValues[2].IsEdited ? baseValues[2] : db.m_Inert);
            DBDE.Logger.LogVerbose($"  > Inertia: {baseValues[2].initialValue}");
            baseValues[3] = new EditableValue<float>(db.m_Radius, baseValues[3].IsEdited ? baseValues[3] : db.m_Radius);
            DBDE.Logger.LogVerbose($"  > Radius: {baseValues[3].initialValue}");
            baseValues[4] = new EditableValue<float>(db.m_Stiffness, baseValues[4].IsEdited ? baseValues[4] : db.m_Stiffness);
            DBDE.Logger.LogVerbose($"  > Stiffness: {baseValues[4].initialValue}");

            gravity = new EditableValue<Vector3>(db.m_Gravity, gravity.IsEdited ? gravity : db.m_Gravity);
            DBDE.Logger.LogVerbose($"  > Gravity: {gravity.initialValue}");
            force = new EditableValue<Vector3>(db.m_Force, force.IsEdited ? force : db.m_Force);
            DBDE.Logger.LogVerbose($"  > Force: {force.initialValue}");
            endOffset = new EditableValue<Vector3>(db.m_EndOffset, endOffset.IsEdited ? endOffset : db.m_EndOffset);
            DBDE.Logger.LogVerbose($"  > endOffset: {endOffset.initialValue}");
            weight = new EditableValue<float>(db.m_Weight, weight.IsEdited ? weight : db.m_Weight);
            DBDE.Logger.LogVerbose($"  > Weight: {weight.initialValue}");
            freezeAxis = new EditableValue<DynamicBone.FreezeAxis>(db.m_FreezeAxis, freezeAxis.IsEdited ? freezeAxis : db.m_FreezeAxis);
            DBDE.Logger.LogVerbose($"  > FreezeAxis: {freezeAxis.initialValue}");
            
            notRolls = new EditableList<string>(db.m_notRolls.IsNullOrEmpty() ? new List<string>() : db.m_notRolls.Select(t => db.m_Root.GetPathToChild(t)).Where(t => !t.IsNullOrEmpty()));
            Exclusions = new EditableList<string>(db.m_Exclusions.IsNullOrEmpty() ? new List<string>() : db.m_Exclusions.Select(t => db.m_Root.GetPathToChild(t)).Where(t => !t.IsNullOrEmpty()));
            
            _colliders = db.m_Colliders;
            _localGravity = db.m_LocalGravity;
            _particles = db.m_Particles;

            db.enabled = active;
            
        }

        private static void LoadVector(byte[] binary, ref EditableValue<Vector3> editableValue)
        {
            Vector3 sValue = MessagePackSerializer.Deserialize<Vector3>(binary);
            var s = sValue.ToString("F4");
            // DBDE.Logger.LogInfo($"{GetButtonName()} - Loading Vector: {s} (As Default = {DBDE.loadSettingsAsDefault.Value})");
            editableValue.value = sValue;
        }

        public void SetActive(bool newActive)
        {
            if (PrimaryDynamicBone) PrimaryDynamicBone.enabled = newActive;
            _active = newActive;
        }

        public void BakeActive()
        {
            InitialActive = active;
        }

		public AnimationCurve GetAnimationCurve(byte kind)
		{ 
            return new AnimationCurve(distributions[kind]);
		}

        public void SetAnimationCurve(int kind, AnimationCurve animationCurve)
		{
			distributions[kind].value = animationCurve.keys;
		}

        [Obsolete("Old Serialisation Method. Use SerialiseAll() instead.")]
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
                sAxis = MessagePackSerializer.Serialize((byte?)freezeAxis.value);
            }
            byte[] sActive = MessagePackSerializer.Serialize(active);

            byte[] sWeight = MessagePackSerializer.Serialize(weight.value);
            
            byte[] sNotRolls = MessagePackSerializer.Serialize((List<string>)notRolls);

            byte[] sExclusions = MessagePackSerializer.Serialize((List<string>)Exclusions);
            
            var edits = new List<byte[]>() {sDistrib, sBaseValues, sGravity, sForce, sEndOffset, sAxis, sActive, sWeight, sNotRolls, sExclusions };
            
            return MessagePackSerializer.Serialize(edits);
		}

        public byte[] SerialiseAll()
        {
            // still serialise as List<byte[]> put only add the Serialised edit.
            return MessagePackSerializer.Serialize(new List<byte[]>{MessagePackSerializer.Serialize(new SerialisedEdit(this))});
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

        public bool IsEdited(int kind)
        {
            return distributions[kind].IsEdited || baseValues[kind].IsEdited;
        }

        public bool IsActiveEdited()
        {
            return InitialActive != active;
        }

        public bool IsEdited()
		{
			if (distributions.Any(d => d.IsEdited))
            {
                return true;
            }
            if (baseValues.Any(d => d.IsEdited))
            {
                return true;
            }
            if (gravity.IsEdited) return true;
            if (force.IsEdited) return true;
            if (endOffset.IsEdited) return true;
            if (freezeAxis.IsEdited) return true;
            if (IsActiveEdited()) return true;
            if (weight.IsEdited) return true;
            if (notRolls.IsEdited) return true;
            if (Exclusions.IsEdited) return true;
			return false;
		}

        internal void UpdateActiveStack(bool always = false)
        {
            List<DynamicBone> dbs = DynamicBones;
            if (dbs.IsNullOrEmpty()) return;
            if (active && dbs.FindAll(b => b.enabled == true).Count() == 1) return;
            if (dbs.Count <= 1 && !always) return;
            foreach (DynamicBone db in dbs) db.enabled = false; //disable others
            if (active)
            {
                PrimaryDynamicBone.enabled = true; // enable primary
            }
        }

        internal void ReSetup()
        {
            DBDE.Instance.StartCoroutine(ReSetupContinuer());
        }
        
        private IEnumerator ReSetupContinuer()
        {
            DBDE.Logger.LogVerbose($"[{GetButtonName()}] Starting ReSetup");
            MultiBoneFix();
            yield return null; // wait for MultiBoneFix to finish
            DynamicBone db = PrimaryDynamicBone;
            db.ResetParticlesPosition(); // reset particle positions so that all particles are in their initial positions
            db.ApplyParticlesToTransforms(); // apply the reset particle positions to all transforms
            db.SetupParticles(); // ReSetup; this also sets the initial positions of the particles, hence the above
            ReferMultiBoneFixToDynamicBone(db);
            MultiBoneFix(); // copy new values to other DynamicBones.
            DBDE.Logger.LogVerbose($"[{GetButtonName()}] Finished ReSetup");
        }
        
        /// <summary>
        /// Copies the unchangeable values from the PrimaryDynamicBone to the other DynamicBone components
        /// </summary>
        internal void MultiBoneFix()
        {
            IEnumerator ShitHead(DynamicBone db)
            {
                yield return null;
                db.m_LocalGravity = _localGravity;
                //DBDE.Logger.LogDebug($"Fixed {db.name}, bone count: {db.m_Particles.Count}, gravity: {db.m_LocalGravity.x:0.000}, {db.m_LocalGravity.y:0.000}, {db.m_LocalGravity.z:0.000}");
            }
            if (DynamicBones.IsNullOrEmpty()) return;
            
            DBDE.Logger.LogVerbose($"[{GetButtonName()}] MultiBoneFix for {DynamicBones.Count} Dynamic Bones");
            // particle fix
            if (_particles.Count > 0 && !_particles[0].m_Transform && _particles.All(p => !p.m_Transform))
            {
                ReferMultiBoneFixToDynamicBone(PrimaryDynamicBone);
            }
            
            foreach (DynamicBone db in DynamicBones)
            {
                db.m_notRolls = NotRollsTransforms;
                db.m_Exclusions = ExclusionsTransforms;
                db.m_Particles = _particles;
                db.m_Colliders = _colliders;
                // I hate this but if I don't delay setting the LocalGravity it gets overwritten right again
                DBDE.Instance.StartCoroutine(ShitHead(db)); 
            }
        }

        #region Apply/Reset

        

        public void ApplyAll()
        {
            if (!PrimaryDynamicBone) return;
            ApplyDistribution();
            ApplyBaseValues();
            ApplyGravity();
            ApplyForce();
            ApplyEndOffset();
            ApplyFreezeAxis();
            ApplyWeight();
            
            SetActive(active);
        }
        
        /// <summary>
        /// Resets all values except NotRolls and Exclusions
        /// </summary>
        public void ResetAll()
        {
            ResetBaseValues();
            ResetDistribution();
            ResetGravity();
            ResetForce();
            ResetEndOffset();
            ResetFreezeAxis();
            ResetWeight();
            SetActive(InitialActive);
        }

        /// <summary>
        /// Bakes all values except NotRolls and Exclusions
        /// </summary>
        public void BakeAll()
        {
            BakeValues();
            BakeDistributions();
            BakeGravity();  
            BakeForce();
            BakeEndOffset();
            BakeFreezeAxis();
            BakeWeight();
            BakeNotRolls();
            BakeExclusions();
        }

		public void ApplyDistribution(int? kind = null)
        {
            DBDE.Logger.LogVerbose($"[{GetButtonName()}] Applying Distribution(s) {kind}");
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
            DBDE.Logger.LogVerbose($"[{GetButtonName()}] Resetting Distribution(s)  {kind}");
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
        
        public void BakeDistributions(int? kind = null)
        {
            DBDE.Logger.LogVerbose($"[{GetButtonName()}] Baking Distribution(s)  {kind}");
            foreach (DynamicBone db in DynamicBones)
            {
                if (!db) return;
                if (kind.HasValue)
                {
                    distributions[kind.Value].SetCurrentAsInitial();
                }
                else
                {
                    for (int i = 0; i < distributions.Length; i++) distributions[i].SetCurrentAsInitial();
                }
            }
        }

        public void ApplyBaseValues(int? kind = null)
        {
            DBDE.Logger.LogVerbose($"[{GetButtonName()}] Applying BaseValues(s)  {kind}");
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
            DBDE.Logger.LogVerbose($"[{GetButtonName()}] Resetting BaseValues(s)  {kind}");
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
        
        public void BakeValues(int? kind = null)
        {
            DBDE.Logger.LogVerbose($"[{GetButtonName()}] Baking BaseValues(s)  {kind}");
            foreach (DynamicBone db in DynamicBones)
            {
                if (!db) return;
                if (kind.HasValue)
                {
                    baseValues[kind.Value].SetCurrentAsInitial();
                    
                }
                else
                {
                    for (int i = 0; i < baseValues.Length; i++) baseValues[i].SetCurrentAsInitial();
                }
            }
        }

        public void ApplyGravity()
        {
            DBDE.Logger.LogVerbose($"[{GetButtonName()}] Applying Gravity");
            foreach (DynamicBone db in DynamicBones)
            {
                db.m_Gravity = gravity;
            }

            UpdateActiveStack();
        }
        public void ResetGravity()
        {
            DBDE.Logger.LogVerbose($"[{GetButtonName()}] Resetting Gravity");
            gravity.Reset();
            foreach (DynamicBone db in DynamicBones)
            {
                db.m_Gravity = gravity;
            }
            UpdateActiveStack();
        }
        
        public void BakeGravity()
        {
            DBDE.Logger.LogVerbose($"[{GetButtonName()}] Baking Gravity");
            gravity.SetCurrentAsInitial();
        }
        
        public void ApplyForce()
        {
            DBDE.Logger.LogVerbose($"[{GetButtonName()}] Applying Force");
            foreach (DynamicBone db in DynamicBones)
            {
                db.m_Force = force;
            }

            UpdateActiveStack();
        }
        public void ResetForce()
        {
            DBDE.Logger.LogVerbose($"[{GetButtonName()}] Resetting Force");
            force.Reset();
            foreach (DynamicBone db in DynamicBones)
            {
                db.m_Force = force;
            }
            UpdateActiveStack();
        }
        public void BakeForce()
        {
            DBDE.Logger.LogVerbose($"[{GetButtonName()}] Baking Force");
            force.SetCurrentAsInitial();
        }
        public void ApplyFreezeAxis()
        {
            DBDE.Logger.LogVerbose($"[{GetButtonName()}] Applying Freeze Axis");
            foreach (DynamicBone db in DynamicBones)
            {
                db.m_FreezeAxis = freezeAxis;
            }

            UpdateActiveStack();
        }
        public void ResetFreezeAxis()
        {
            DBDE.Logger.LogVerbose($"[{GetButtonName()}] Resetting Freeze Axis");
            freezeAxis.Reset();
            foreach (DynamicBone db in DynamicBones)
            {
                db.m_FreezeAxis = freezeAxis;
            }
            UpdateActiveStack();
        }
        public void BakeFreezeAxis()
        {
            DBDE.Logger.LogVerbose($"[{GetButtonName()}] Baking Freeze Axis");
            freezeAxis.SetCurrentAsInitial();
        }

        public void ApplyEndOffset()
        {
            DBDE.Logger.LogVerbose($"[{GetButtonName()}] Applying EndOffset");
            foreach (DynamicBone db in DynamicBones)
            {
                db.m_EndOffset = endOffset;
                db.UpdateParticles();
            }

            UpdateActiveStack();
        }
        public void ResetEndOffset()
        {
            DBDE.Logger.LogVerbose($"[{GetButtonName()}] Resetting End Offset");
            endOffset.Reset();
            foreach (DynamicBone db in DynamicBones)
            {
                db.m_EndOffset = endOffset;
                db.UpdateParticles();
            }
            UpdateActiveStack();
        }
        
        public void BakeEndOffset()
        {
            DBDE.Logger.LogVerbose($"[{GetButtonName()}] Baking EndOffset");
            endOffset.SetCurrentAsInitial();
        }

        public void ApplyWeight()
        {
            DBDE.Logger.LogVerbose($"[{GetButtonName()}] Applying Weight");
            foreach (DynamicBone db in DynamicBones)
            {
                db.m_Weight = weight;
                db.UpdateParticles();
            }
            UpdateActiveStack();
        }
        
        public void ResetWeight()
        {
            DBDE.Logger.LogVerbose($"[{GetButtonName()}] Resetting Weight");
            weight.Reset();
            foreach (DynamicBone db in DynamicBones)
            {
                db.m_Weight = weight;
                db.UpdateParticles();
            }
            UpdateActiveStack();
        }
        
        public void BakeWeight()
        {
            DBDE.Logger.LogVerbose($"[{GetButtonName()}] Baking Weight");
            weight.SetCurrentAsInitial();
        }

        public void ApplyNotRollsAndExclusions()
        {
            if (!PrimaryDynamicBone) return;
            DBDE.Logger.LogVerbose($"[{GetButtonName()}] Applying NotRolls and Exclusions");
            ReSetup();
        }

        public void ResetNotRolls()
        {
            DBDE.Logger.LogVerbose($"[{GetButtonName()}] Resetting NotRolls");
            notRolls.Reset();
            ApplyNotRollsAndExclusions();
        }
        
        public void BakeNotRolls()
        {
            DBDE.Logger.LogVerbose($"[{GetButtonName()}] Baking NotRolls");
            notRolls.SetCurrentAsInitial();
        }
        

        public void ResetExclusions()
        {
            DBDE.Logger.LogVerbose($"[{GetButtonName()}] Applying Exclusions");
            Exclusions.Reset();
            ApplyNotRollsAndExclusions();
        }
        
        public void BakeExclusions()
        {
            DBDE.Logger.LogVerbose($"[{GetButtonName()}] Baking Exclusions");
            Exclusions.SetCurrentAsInitial();
        }
        

        public void AddNotRoll(Transform transform)
        {
            DBDE.Logger.LogVerbose($"[{GetButtonName()}] Adding NotRoll: {transform.GetFullPath()}");
            if (NotRollsTransforms.Contains(transform)) return;

            string pathToChild = PrimaryDynamicBone.m_Root.GetPathToChild(transform);
            if (pathToChild != null) notRolls.Add(pathToChild);
            
            ApplyNotRollsAndExclusions();
        }
        
        public void RemoveNotRoll(Transform transform)
        {
            DBDE.Logger.LogVerbose($"[{GetButtonName()}] Removing NotRoll: {transform.GetFullPath()}");
            if (!NotRollsTransforms.Contains(transform)) return;
            
            string pathToChild = PrimaryDynamicBone.m_Root.GetPathToChild(transform);
            if (pathToChild != null) notRolls.Remove(pathToChild);
            
            ApplyNotRollsAndExclusions();
        }

        public void AddExclusion(Transform transform)
        {
            DBDE.Logger.LogVerbose($"[{GetButtonName()}] Adding Exclusion: {transform.GetFullPath()}");
            if (ExclusionsTransforms.Contains(transform)) return;

            string pathToChild = PrimaryDynamicBone.m_Root.GetPathToChild(transform);
            if (pathToChild != null) Exclusions.Add(pathToChild);
            
            ApplyNotRollsAndExclusions();
        }

        public void RemoveExclusion(Transform transform)
        {
            DBDE.Logger.LogVerbose($"[{GetButtonName()}] Removing Exclusion: {transform.GetFullPath()}");
            if (!ExclusionsTransforms.Contains(transform)) return;

            string pathToChild = PrimaryDynamicBone.m_Root.GetPathToChild(transform);
            if (pathToChild != null) Exclusions.Remove(pathToChild);
            
            ApplyNotRollsAndExclusions();
        }

        #endregion
        
        public override string ToString()
        {
            if (PrimaryDynamicBone && PrimaryDynamicBone.m_Root) return PrimaryDynamicBone.m_Root.ToString();
            return "DeadBone " + base.ToString();
        }
    }
}


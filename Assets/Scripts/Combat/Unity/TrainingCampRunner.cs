using System;
using System.Collections.Generic;
using Combat.Config;
using Combat.Core;
using Combat.Unity;
using UnityEngine;

namespace Combat.TrainingCamp
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TrainingCampController), typeof(TrainingCampPanel), typeof(TrainingCampVisuals))]
    public sealed class TrainingCampRunner : MonoBehaviour
    {
        [SerializeField] CombatDatabaseAsset _database;
        [SerializeField] bool _useSoDatabase = true;
        [SerializeField] float _logicHz = 50f;
        [SerializeField] bool _showPanel = true;

        CombatWorld _world;
        InputAdapter _input;
        EntityId _playerId, _dummyId;
        readonly List<string> _eventStream = new List<string>(12);
        readonly List<string> _eventScratch = new List<string>(12);
        string _lastOperation = "Ready - choose a check";
        bool _dummyAi;
        bool _paused;

        public CombatWorld World => _world;
        public EntityId PlayerId => _playerId;
        public EntityId DummyId => _dummyId;
        public bool DummyAiEnabled => _dummyAi;
        public bool PanelVisible { get => _showPanel; set => _showPanel = value; }
        public string LastOperation => _lastOperation;
        public IReadOnlyList<string> EventStream => _eventStream;
        public float LogicHz => _logicHz;

        void Awake() => ResetWorld();

        public void ResetWorld()
        {
            _world = null;
            var content = _useSoDatabase && _database != null
                ? (ICombatContent)new SoCombatContent(_database)
                : new CodeCombatContent();
            var baked = content.Bake();
            _world = new CombatWorld(new FighterActorFactory(baked), new IntentQueue(), new EventBus(), new CombatTime(), new SeededRandom(7), baked.Cues, baked.Motor);
            baked.Install(_world);
            BindEvents();
            _input = new InputAdapter { EnableCombatKeys = true };
            _dummyAi = false;
            _playerId = Spawn("fighter", new SimVec3(-2.2f, 0f, 0f));
            _dummyId = Spawn("melee_ai", new SimVec3(1.2f, 0f, 0f));
            if (_world.TryGetActor(_dummyId, out var dummy))
            {
                var attr = dummy.GetComp<AttributeSet>();
                attr.SetBase(AttrId.MaxHp, 1000000000f);
                attr.SetBase(AttrId.Hp, 1000000000f);
                dummy.GetComp<TeamComp>().SetTeam(2); // targetable while passive; AI is disabled below
                dummy.GetComp<PerceptionComp>().Enabled = false;
                dummy.GetComp<BehaviorTreeComp>().Enabled = false;
                dummy.GetComp<BehaviorTreeComp>().Board.LeashRange = 9f;
            }
            ClearEvents();
            _lastOperation = "World reset | Passive Dummy | infinite HP";
        }

        EntityId Spawn(string blueprint, SimVec3 position)
        {
            var id = _world.SpawnActor(new ActorSpawnSpec(blueprint));
            if (_world.TryGetActor(id, out var actor)) actor.GetComp<TransformComp>().Position = position;
            return id;
        }

        void BindEvents()
        {
            _world.Events.Subscribe<EvDamage>(e => AddEvent("Damage " + e.Source + " -> " + e.Target + "  " + e.Amount.ToString("F1")));
            _world.Events.Subscribe<EvImmune>(e => AddEvent("Immune " + e.Source + " -> " + e.Target));
            _world.Events.Subscribe<EvHitstop>(e => AddEvent("Hitstop " + e.Frames + "f"));
            _world.Events.Subscribe<EvCue>(e => AddEvent("Cue " + e.Name + " (#" + e.CueId + ")"));
            _world.Events.Subscribe<EvHeal>(e => AddEvent("Heal " + e.Target + " +" + e.Amount.ToString("F1")));
            _world.Events.Subscribe<EvEntitySpawn>(e => AddEvent("Spawn " + e.BlueprintId + " owner=" + e.Owner));
            _world.Events.Subscribe<EvEntityCleanup>(e => AddEvent("Cleanup " + e.Id + " " + e.Reason));
        }
        void AddEvent(string value)
        {
            _eventStream.Insert(0, "F" + (_world != null ? _world.Time.Frame : 0) + "  " + value);
            if (_eventStream.Count > 10) _eventStream.RemoveAt(_eventStream.Count - 1);
        }
        void ClearEvents() => _eventStream.Clear();

        public void ToggleDummyAI()
        {
            _dummyAi = !_dummyAi;
            if (_world.TryGetActor(_dummyId, out var dummy))
            {
                // Team 2 keeps the dummy targetable in both modes; the component
                // switches control whether it actively perceives and attacks.
                dummy.GetComp<TeamComp>().SetTeam(2);
                var perception = dummy.GetComp<PerceptionComp>();
                perception.Enabled = _dummyAi;
                dummy.GetComp<BehaviorTreeComp>().Enabled = _dummyAi;
                if (!_dummyAi)
                {
                    perception.ClearAlert();
                    dummy.GetComp<LocomotionComp>().RequestMoveIntent(0f, 0f);
                }
            }
            _lastOperation = _dummyAi ? "PASS: AI Dummy enabled" : "PASS: Passive Dummy enabled";
        }
        public void Attack() { Queue(InputToken.Attack); _lastOperation = "Attack queued"; }
        public void Dodge() { Queue(Season2Tokens.Dodge); _lastOperation = "Dodge queued"; }
        public void Jump() { Queue(InputToken.Jump); _lastOperation = "Jump queued"; }
        void Queue(InputToken token) => _input?.Queue(token);
        Actor Player() { return _world != null && _world.TryGetActor(_playerId, out var a) ? a : null; }
        Actor Dummy() { return _world != null && _world.TryGetActor(_dummyId, out var a) ? a : null; }
        float Atk(Actor a) => a != null && a.TryGetComp<AttributeSet>(out var attr) ? attr.GetFinal(AttrId.Atk) : 0f;

        public void SpawnFireball() { var a = Player(); if (a == null) return; _world.Deliver(new IEffect[] { new SpawnProjectileEffect(CombatIds.Fireball) }, a, null, Atk(a)); _lastOperation = "Fireball spawn requested"; }
        public void SpawnHomingProjectile() { var a = Player(); var d = Dummy(); if (a == null || d == null) return; _world.Deliver(new IEffect[] { new SpawnProjectileEffect(CombatIds.HomingBolt) }, a, d, Atk(a)); _lastOperation = "Homing bolt locked to dummy"; }
        public void SpawnFireGround() { var a = Player(); var d = Dummy(); if (a == null || d == null) return; _world.Deliver(new IEffect[] { new SpawnAoeEffect(CombatIds.FireGround) }, a, null, Atk(a), d.GetComp<TransformComp>().Position); _lastOperation = "Fire Ground spawned"; }
        public void SpawnAura() { var a = Player(); if (a == null) return; _world.Deliver(new IEffect[] { new SpawnAoeEffect(CombatIds.AuraField) }, a, null, Atk(a), a.GetComp<TransformComp>().Position); _lastOperation = "Aura occupancy field spawned"; }
        public void Summon() { var a = Player(); if (a == null) return; _world.Deliver(new IEffect[] { new SpawnSummonEffect(CombatIds.MeleeSummon) }, a, null, Atk(a)); _lastOperation = "Summon spawned with OwnerId"; }
        public void ApplyBuff() { var a = Player(); var d = Dummy(); if (a == null || d == null) return; _world.Deliver(new IEffect[] { new ApplyDurationEffect(CombatCatalog.Burn(), 1) }, a, d, Atk(a)); _lastOperation = "Burn +1 applied (max 3)"; }
        public void DispelBuff() { var a = Player(); var d = Dummy(); if (a == null || d == null) return; _world.Deliver(new IEffect[] { new DispelEffect(DispelMode.ByBuffId, CombatIds.Burn) }, a, d, 0); _lastOperation = "Dispel Burn"; }
        public void KnockdownDummy() { var a = Player(); var d = Dummy(); if (a == null || d == null) return; _world.Deliver(new IEffect[] { new KnockdownEffect() }, a, d, 0); _lastOperation = "Knockdown Dummy"; }
        public void KillRespawnPlayer()
        {
            var a = Player();
            if (a == null || !a.GetComp<TagComp>().Has(CommonTags.Dead)) a?.GetComp<StateMachineComp>().TryEnter(ActivityId.Dead, new ActivityEnterArgs { Reason = "Validation" });
            else ResetWorld();
            _lastOperation = "Kill / Respawn Player";
        }
        public void ClearRuntimeObjects()
        {
            if (_world == null) return;
            var actors = _world.RegistryActive();
            for (int i = 0; i < actors.Count; i++)
                if (actors[i].TryGetComp<ProjectileComp>(out _) || actors[i].TryGetComp<AoeComp>(out _) || actors[i].TryGetComp<SummonComp>(out _))
                { _world.RequestDespawn(actors[i].Id); actors[i].SetActive(false); }
            _lastOperation = "Runtime objects cleared";
        }
        public bool RunCurrentCheck() { bool ok = TrainingCampProbe.Check(_world, _playerId, _dummyId); _lastOperation = (ok ? "PASS" : "FAIL") + " current world contract"; return ok; }
        public bool RunAllChecks() { bool ok = TrainingCampVerificationDemo.Run(s => AddEvent(s)); _lastOperation = ok ? "PASS all pure C# checks" : "FAIL pure C# checks"; return ok; }
        public void Step(float dt)
        {
            if (_world == null || _paused) return;
            _input.SampleUnity();
            _world.TryGetActor(_playerId, out var player);
            _input.PumpLogic(player);
            _world.Tick(dt);
        }
        public void SetPaused(bool value) => _paused = value;

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab)) PanelVisible = !PanelVisible;
            if (Input.GetKeyDown(KeyCode.Q)) ToggleDummyAI();
            if (Input.GetKeyDown(KeyCode.R)) ResetWorld();
            if (Input.GetKeyDown(KeyCode.F1)) ClearRuntimeObjects();
            if (Input.GetKeyDown(KeyCode.F2)) RunCurrentCheck();
            if (Input.GetKeyDown(KeyCode.F3)) RunAllChecks();
            if (Input.GetKeyDown(KeyCode.Alpha1)) SpawnFireball();
            if (Input.GetKeyDown(KeyCode.Alpha2)) SpawnHomingProjectile();
            if (Input.GetKeyDown(KeyCode.Alpha3)) SpawnFireGround();
            if (Input.GetKeyDown(KeyCode.Alpha4)) SpawnAura();
            if (Input.GetKeyDown(KeyCode.Alpha5)) Summon();
            if (Input.GetKeyDown(KeyCode.Alpha6)) ApplyBuff();
            if (Input.GetKeyDown(KeyCode.Alpha7)) DispelBuff();
            if (Input.GetKeyDown(KeyCode.Alpha8)) KnockdownDummy();
            Step(1f / Mathf.Max(1f, _logicHz));
        }
    }
}

using UnityEngine;

namespace Combat.TrainingCamp
{
    public sealed class TrainingCampController : MonoBehaviour
    {
        TrainingCampRunner _runner;
        void Awake() => _runner = GetComponent<TrainingCampRunner>();
        public void ResetWorld() => _runner.ResetWorld();
        public void ToggleDummyAI() => _runner.ToggleDummyAI();
        public void Attack() => _runner.Attack();
        public void Dodge() => _runner.Dodge();
        public void Jump() => _runner.Jump();
        public void SpawnFireball() => _runner.SpawnFireball();
        public void SpawnHomingProjectile() => _runner.SpawnHomingProjectile();
        public void SpawnFireGround() => _runner.SpawnFireGround();
        public void SpawnAura() => _runner.SpawnAura();
        public void Summon() => _runner.Summon();
        public void ApplyBuff() => _runner.ApplyBuff();
        public void DispelBuff() => _runner.DispelBuff();
        public void KnockdownDummy() => _runner.KnockdownDummy();
        public void KillRespawnPlayer() => _runner.KillRespawnPlayer();
        public void ClearRuntimeObjects() => _runner.ClearRuntimeObjects();
        public void RunCurrentCheck() => _runner.RunCurrentCheck();
        public void RunAllChecks() => _runner.RunAllChecks();
    }
}

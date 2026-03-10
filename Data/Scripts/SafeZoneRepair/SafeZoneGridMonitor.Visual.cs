using Sandbox.ModAPI;
using Sandbox.Game;
using Sandbox.Game.Entities;
using System;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace SafeZoneRepair
{
    public partial class SafeZoneGridMonitor
    {
        // Эффект сварки
        public void SpawnWeldingFX(IMySlimBlock block, bool finished = false)
        {
            if (block == null) return;
            if (effect != null)
            {
                if (finished)
                {
                    if (!effect.IsStopped) effect.Stop();
                    StopWeldingSound();
                    return;
                }
                if (effect.IsStopped)
                    CreateWeldingEffect(block);
                else
                    UpdateWeldingEffectPosition(block);
            }
            else if (!finished)
            {
                CreateWeldingEffect(block);
            }
        }

        private void CreateWeldingEffect(IMySlimBlock block)
        {
            MatrixD matrix = block.CubeGrid.WorldMatrix;
            Vector3D pos = block.CubeGrid.GridIntegerToWorld(block.Position);
            MyParticlesManager.TryCreateParticleEffect(MyParticleEffectsNameEnum.ShipWelderArc, ref matrix, ref pos, uint.MaxValue, out effect);
            if (effect != null)
                effect.UserScale = 3f;
            PlayWeldingSound(block);
        }

        private void UpdateWeldingEffectPosition(IMySlimBlock block)
        {
            Vector3D pos = block.CubeGrid.GridIntegerToWorld(block.Position);
            effect?.SetTranslation(ref pos);
        }

        private void PlayWeldingSound(IMySlimBlock block)
        {
            if (soundEmitter == null)
                soundEmitter = new MyEntity3DSoundEmitter((MyEntity)block.CubeGrid);
            soundEmitter.PlaySound(new MySoundPair("ToolLrgWeldMetal"), true);
        }

        private void StopWeldingSound()
        {
            soundEmitter?.StopSound(true);
        }
    }
}
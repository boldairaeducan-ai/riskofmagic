using System;
using RiskOfMagic.Core;
using UnityEngine;
using UnityEngine.VFX;

namespace RiskOfMagic.Vfx
{
    /// <summary>
    /// Local effect that follows a channelling caster (Souffle enflammé), then stops emitting and lets its particles
    /// fade before going back to the pool (TimedEffect). Added at runtime on the spawned effect.
    /// </summary>
    public sealed class ChannelEffect : MonoBehaviour
    {
        [Tooltip("Time left to the particles after the channel stops, before the effect is released.")]
        [SerializeField, Min(0f)] private float fadeTime = 2f;

        private Transform anchor;
        private Vector3 localOffset;
        private Func<Quaternion> rotation;
        private Func<bool> isActive;
        private float stopTime;
        private bool stopped;
        private VisualEffect[] graphs = Array.Empty<VisualEffect>();
        private ParticleSystem[] systems = Array.Empty<ParticleSystem>();

        /// <summary>
        /// Follows <paramref name="followAnchor"/> (position + <paramref name="offset"/> in its space) with
        /// <paramref name="worldRotation"/> for <paramref name="duration"/>, or until <paramref name="active"/> returns false.
        /// </summary>
        public static ChannelEffect Begin(GameObject effect, Transform followAnchor, Vector3 offset, Func<Quaternion> worldRotation,
            Func<bool> active, float duration)
        {
            if (effect == null || followAnchor == null)
                return null;
            if (!effect.TryGetComponent(out ChannelEffect channel))
                channel = effect.AddComponent<ChannelEffect>();
            channel.anchor = followAnchor;
            channel.localOffset = offset;
            channel.rotation = worldRotation;
            channel.isActive = active;
            channel.stopTime = Time.time + duration;
            channel.stopped = false;
            channel.graphs = effect.GetComponentsInChildren<VisualEffect>(true);
            channel.systems = effect.GetComponentsInChildren<ParticleSystem>(true);
            if (effect.TryGetComponent(out TimedEffect timed))
                timed.ExtendLifetime(duration + channel.fadeTime);
            channel.Follow();
            // Pooled instances were stopped last time: start over.
            foreach (var graph in channel.graphs)
                if (graph != null)
                {
                    graph.Reinit();
                    graph.Play();
                }
            foreach (var system in channel.systems)
                if (system != null)
                {
                    system.Clear(false);
                    system.Play(false);
                }
            return channel;
        }

        private void LateUpdate()
        {
            if (stopped)
                return;
            if (anchor == null || Time.time >= stopTime || (isActive != null && !isActive()))
            {
                Stop();
                return;
            }
            Follow();
        }

        private void Follow()
        {
            if (anchor == null)
                return;
            transform.SetPositionAndRotation(anchor.TransformPoint(localOffset), rotation != null ? rotation() : anchor.rotation);
        }

        /// <summary>Stops emitting; particles already emitted finish their life.</summary>
        public void Stop()
        {
            if (stopped)
                return;
            stopped = true;
            anchor = null;
            rotation = null;
            isActive = null;
            if (graphs.Length == 0 && systems.Length == 0)
            {
                GameObjectPool.Release(gameObject); // placeholder only: nothing to fade
                return;
            }
            foreach (var graph in graphs)
                if (graph != null)
                    graph.Stop();
            foreach (var system in systems)
                if (system != null)
                    system.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            // TimedEffect releases it at duration + fadeTime after the spawn.
        }
    }
}

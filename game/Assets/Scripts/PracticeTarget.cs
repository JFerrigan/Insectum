using UnityEngine;

namespace Insectum.Game
{
    internal sealed class PracticeTarget : MonoBehaviour
    {
        private static readonly Color32 IdleTint = new(217, 141, 78, 255);
        private Transform visual;
        private SpriteRenderer shell;
        private SpriteRenderer[] pips;
        private DamageReceiver receiver;
        private float hitUntil;

        public static void Attach(Transform parent, Sprite pixel, CombatArena combat, Vector2 anchor)
        {
            var target = new GameObject("Practice husk");
            target.transform.SetParent(parent, false);
            target.transform.localPosition = anchor;
            var view = target.AddComponent<PracticeTarget>();
            view.visual = new GameObject("Husk graphics").transform;
            view.visual.SetParent(target.transform, false);
            ForestRoute.CreateMarker(view.visual, "Husk", Vector2.zero, new Vector2(0.72f, 0.64f), pixel, IdleTint, 2);
            view.shell = view.visual.GetChild(0).GetComponent<SpriteRenderer>();
            var health = new HealthPool(3);
            view.pips = new SpriteRenderer[health.Maximum];
            for (int i = 0; i < health.Maximum; i++)
            {
                ForestRoute.CreateMarker(view.visual, $"Heart {i + 1}", new Vector2((i - 1) * 0.16f, 0.4f),
                    new Vector2(0.10f, 0.08f), pixel, new Color32(255, 211, 145, 255), 3);
                view.pips[i] = view.visual.GetChild(i + 1).GetComponent<SpriteRenderer>();
            }

            view.receiver = new DamageReceiver(health, 0.36f, () => target.transform.position, 8, view.Kick);
            view.receiver.Resolved += view.ShowHit;
            combat.AddTarget(view.receiver);
        }

        private void Kick(Vector2 velocity) => visual.localPosition += (Vector3)(velocity * 0.056f);

        private void ShowHit(DamageHit hit, DamageOutcome outcome)
        {
            for (int i = 0; i < pips.Length; i++)
                pips[i].enabled = i < receiver.Health.Current;

            shell.color = Color.white;
            hitUntil = Time.time + 0.095f;
            if (!receiver.Health.Alive)
                gameObject.SetActive(false);
        }

        private void Update()
        {
            visual.localPosition = Vector2.MoveTowards(visual.localPosition, Vector2.zero, Time.deltaTime * 5.3f);
            if (Time.time >= hitUntil)
                shell.color = IdleTint;
        }
    }
}

using UnityEngine;

namespace Insectum.Game
{
    internal sealed class CombatPractice : MonoBehaviour
    {
        internal static readonly Vector2 FrontTarget = new(-4.7f, 1.42f);
        internal static readonly Vector2 CrushTarget = new(-0.1f, -4.61f);

        private Transform strike;
        private float strikeUntil;
        private CombatArena combat;

        public static void Attach(Transform parent, Sprite pixel, CombatArena combat)
        {
            var root = new GameObject("Combat practice");
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<CombatPractice>();
            view.combat = combat;
            ForestRoute.CreateMarker(root.transform, "Strike", Vector2.zero, Vector2.one, pixel,
                new Color32(255, 229, 123, 116), 4);
            view.strike = root.transform.GetChild(0);
            view.strike.gameObject.SetActive(false);
            combat.MeleePerformed += view.ShowStrike;

            PracticeTarget.Attach(root.transform, pixel, combat, FrontTarget);
            PracticeTarget.Attach(root.transform, pixel, combat, CrushTarget);
        }

        private void ShowStrike(MeleeStrike swing)
        {
            strike.localPosition = swing.Origin + swing.Direction * swing.Reach * 0.5f;
            strike.localScale = new Vector3(swing.Reach, swing.HalfWidth * 2f, 1f);
            float angle = Mathf.Atan2(swing.Direction.y, swing.Direction.x) * Mathf.Rad2Deg;
            strike.localRotation = Quaternion.Euler(0f, 0f, angle);
            strike.gameObject.SetActive(true);
            strikeUntil = Time.time + 0.10f;
        }

        private void Update()
        {
            if (strike.gameObject.activeSelf && Time.time >= strikeUntil)
                strike.gameObject.SetActive(false);
        }

        private void OnDestroy() => combat.MeleePerformed -= ShowStrike;
    }
}

using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Sistema de efectos visuales para ataques.
    /// Dibuja línea/rayo entre attacker y defender.
    /// </summary>
    public class AttackEffectSystem : MonoBehaviour
    {
        public static void PlayAttackEffect(Card3DView attacker, Card3DView defender)
        {
            if (attacker == null || defender == null)
                return;

            // Crear línea visual
            DrawAttackLine(attacker.transform.position, defender.transform.position);

            // Flash rojo en defender
            defender.SetColor(Color.red);

            // Volver a color normal después
            MonoBehaviour.Instantiate(new GameObject()).AddComponent<MonoBehaviour>().StartCoroutine(
                ResetColorCoroutine(defender, 0.2f)
            );
        }

        private static void DrawAttackLine(Vector3 from, Vector3 to)
        {
            // Crear line renderer temporal
            var lineGo = new GameObject("AttackLine");
            var lineRenderer = lineGo.AddComponent<LineRenderer>();

            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, from);
            lineRenderer.SetPosition(1, to);

            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = Color.yellow;
            lineRenderer.endColor = Color.red;
            lineRenderer.startWidth = 0.1f;
            lineRenderer.endWidth = 0.1f;

            // Destruir después de 0.3 segundos
            Object.Destroy(lineGo, 0.3f);
        }

        private static System.Collections.IEnumerator ResetColorCoroutine(Card3DView card, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (card != null)
                card.SetColor(new Color(0.1f, 0.1f, 0.15f, 0.9f));
        }
    }
}

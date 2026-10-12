using UnityEngine;

namespace Soulslike
{
    /// <summary>The health bar, the boss bar, prompts and the banner (IMGUI: a prototype).</summary>
    public class Hud : MonoBehaviour
    {
        GUIStyle banner;
        GUIStyle label;
        GUIStyle hint;
        Texture2D white;

        void Bar(Rect rect, float fraction, Color fill)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4), white);
            GUI.color = new Color(0.15f, 0.12f, 0.1f, 0.9f);
            GUI.DrawTexture(rect, white);
            GUI.color = fill;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fraction), rect.height), white);
            GUI.color = Color.white;
        }

        static string WeaponName(Training.Weapon weapon) =>
            weapon == Training.Weapon.Sword ? "sword" : weapon == Training.Weapon.Hammer ? "hammer" : "hands";

        void OnGUI()
        {
            var game = GameManager.Instance;
            if (game == null || game.player == null) return;
            if (white == null)
            {
                white = Texture2D.whiteTexture;
                banner = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 64, fontStyle = FontStyle.Normal };
                label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerLeft, fontSize = 18 };
                hint = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
            }
            float scale = Screen.height / 720f;
            var player = game.player;
            Bar(new Rect(30 * scale, 28 * scale, 280 * scale, 12 * scale), player.Health.Current / player.Health.max, new Color(0.62f, 0.1f, 0.08f));
            label.fontSize = Mathf.RoundToInt(16 * scale);
            GUI.Label(new Rect(30 * scale, 44 * scale, 600 * scale, 24 * scale), $"[R] Flask ×{player.Flasks}   [1] sword  [2] hammer  [3] hands: {WeaponName(player.weapon)}", label);

            var boss = game.BossInFight;
            if (boss != null)
            {
                Rect bar = new Rect(Screen.width * 0.2f, Screen.height - 70 * scale, Screen.width * 0.6f, 10 * scale);
                label.fontSize = Mathf.RoundToInt(18 * scale);
                GUI.Label(new Rect(bar.x, bar.y - 30 * scale, bar.width, 26 * scale), boss.title, label);
                Bar(bar, boss.Health.Current / boss.Health.max, new Color(0.62f, 0.1f, 0.08f));
            }

            if (PauseMenu.Open) return;
            if (game.bonfire != null && game.bonfire.PlayerNear && game.BannerAge > 2f)
            {
                hint.fontSize = Mathf.RoundToInt(20 * scale);
                GUI.Label(new Rect(0, Screen.height * 0.72f, Screen.width, 40 * scale), "[E] Rest at the bonfire", hint);
            }

            if (game.BannerAge < 5f)
            {
                // Fades in, holds, fades out, over a dark band, as in the genre.
                float alpha = Mathf.Clamp01(game.BannerAge / 0.8f) * Mathf.Clamp01((5f - game.BannerAge) / 1f);
                GUI.color = new Color(0f, 0f, 0f, 0.55f * alpha);
                GUI.DrawTexture(new Rect(0, Screen.height * 0.42f, Screen.width, Screen.height * 0.16f), white);
                banner.fontSize = Mathf.RoundToInt(64 * scale);
                banner.normal.textColor = new Color(game.BannerColor.r, game.BannerColor.g, game.BannerColor.b, alpha);
                GUI.color = Color.white;
                GUI.Label(new Rect(0, Screen.height * 0.42f, Screen.width, Screen.height * 0.16f), game.Banner, banner);
            }
        }
    }
}

using UnityEngine;
using UnityEditor;
using System.IO;

namespace Flippy.CardDuelMobile.Editor
{
    public static class SkillIconGenerator
    {
        private const string ICON_OUTPUT_PATH = "Assets/Runtime/Icons/Skills";
        private const int ICON_SIZE = 64;

        [MenuItem("Tools/Icons/Create or Refresh All")]
        public static void GenerateAllSkillIcons()
        {
            EnsureDirectoryExists(ICON_OUTPUT_PATH);

            // Defensive skills
            GenerateShieldIcon();
            GenerateRegenerateIcon();
            GenerateTauntIcon();
            GenerateDodgeIcon();
            GenerateEvasionIcon();
            GenerateReflectionIcon();
            GenerateFlyIcon();

            // Offensive skills
            GenerateTrampleIcon();
            GenerateCleaveIcon();
            GeneratePoisonIcon();
            GenerateStunIcon();
            GenerateExecuteIcon();
            GenerateRicochetIcon();
            GenerateLeechIcon();
            GenerateEnrageIcon();
            GenerateManaBurnIcon();

            // Synergy
            GenerateLastStandIcon();
            GenerateChargeIcon();

            // Utility
            GenerateHasteIcon();
            GenerateLinkelinkIcon();

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Success", "Generated 20 skill icons with symbols!", "OK");
        }

        [MenuItem("Tools/Icons/Create SkillIconDefinition")]
        public static void CreateSkillIconDefinition()
        {
            Debug.Log("[SkillIconGenerator] Creating SkillIconDefinition asset...");

            // Create or get existing asset
            var assetPath = "Assets/Runtime/Data/SkillIconDefinition.asset";
            Flippy.CardDuelMobile.Data.SkillIconDefinition definition =
                AssetDatabase.LoadAssetAtPath<Flippy.CardDuelMobile.Data.SkillIconDefinition>(assetPath);

            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<Flippy.CardDuelMobile.Data.SkillIconDefinition>();
                AssetDatabase.CreateAsset(definition, assetPath);
            }

            // Clear existing entries
            definition.skillIcons.Clear();

            // Load all icon textures and assign
            var skillIds = new[]
            {
                "regenerate", "shield", "taunt", "dodge", "evasion", "reflection", "fly",
                "trample", "cleave", "poison", "stun", "execute", "ricochet", "leech",
                "enrage", "mana_burn", "last_stand", "charge", "haste", "lifelink"
            };

            foreach (var skillId in skillIds)
            {
                var iconPath = $"{ICON_OUTPUT_PATH}/Icon_{skillId}.png";
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);

                if (texture != null)
                {
                    var entry = new Flippy.CardDuelMobile.Data.SkillIconDefinition.SkillIconEntry
                    {
                        skillId = skillId,
                        icon = texture
                    };
                    definition.skillIcons.Add(entry);
                    Debug.Log($"✓ Assigned: {skillId}");
                }
                else
                {
                    Debug.LogWarning($"✗ Missing icon: Icon_{skillId}.png");
                }
            }

            // Save
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();

            Debug.Log($"[SkillIconGenerator] Created SkillIconDefinition at {assetPath} with {definition.skillIcons.Count}/20 icons");
            EditorUtility.DisplayDialog("Success",
                $"SkillIconDefinition created with {definition.skillIcons.Count}/20 icons!\n\nAsset: {assetPath}",
                "OK");
        }

        [MenuItem("Tools/Icons/Clear All")]
        public static void ClearAllSkillIcons()
        {
            var directory = new DirectoryInfo(Path.Combine(Application.dataPath, "../", ICON_OUTPUT_PATH));
            if (directory.Exists)
            {
                foreach (var file in directory.GetFiles("*.png"))
                {
                    file.Delete();
                }
                foreach (var file in directory.GetFiles("*.meta"))
                {
                    file.Delete();
                }
            }

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Success", "Cleared all skill icons!", "OK");
        }

        private static void GenerateShieldIcon()
        {
            var texture = CreateIconBase("shield", new Color(0.3f, 0.6f, 1f)); // Light Blue
            DrawShield(texture, Color.white);
            SaveIcon(texture, "shield");
        }

        private static void GenerateRegenerateIcon()
        {
            var texture = CreateIconBase("regenerate", new Color(0.2f, 0.8f, 0.2f)); // Green
            DrawHeart(texture, Color.white);
            SaveIcon(texture, "regenerate");
        }

        private static void GenerateTauntIcon()
        {
            var texture = CreateIconBase("taunt", new Color(1f, 0.4f, 0.2f)); // Orange
            DrawExclamation(texture, Color.white);
            SaveIcon(texture, "taunt");
        }

        private static void GenerateDodgeIcon()
        {
            var texture = CreateIconBase("dodge", new Color(0.8f, 0.8f, 0.2f)); // Yellow
            DrawArrow(texture, Color.white);
            SaveIcon(texture, "dodge");
        }

        private static void GenerateEvasionIcon()
        {
            var texture = CreateIconBase("evasion", new Color(0.9f, 0.9f, 0.9f)); // White
            DrawWave(texture, Color.black);
            SaveIcon(texture, "evasion");
        }

        private static void GenerateReflectionIcon()
        {
            var texture = CreateIconBase("reflection", new Color(0.8f, 0.2f, 0.8f)); // Magenta
            DrawMirror(texture, Color.white);
            SaveIcon(texture, "reflection");
        }

        private static void GenerateFlyIcon()
        {
            var texture = CreateIconBase("fly", new Color(0.5f, 0.7f, 1f)); // Sky Blue
            DrawWing(texture, Color.white);
            SaveIcon(texture, "fly");
        }

        private static void GenerateTrampleIcon()
        {
            var texture = CreateIconBase("trample", new Color(1f, 0.2f, 0.2f)); // Red
            DrawHoof(texture, Color.white);
            SaveIcon(texture, "trample");
        }

        private static void GenerateCleaveIcon()
        {
            var texture = CreateIconBase("cleave", new Color(1f, 0.5f, 0f)); // Orange-Red
            DrawDoubleSlash(texture, Color.white);
            SaveIcon(texture, "cleave");
        }

        private static void GeneratePoisonIcon()
        {
            var texture = CreateIconBase("poison", new Color(0.4f, 1f, 0.4f)); // Lime Green
            DrawSkull(texture, Color.white);
            SaveIcon(texture, "poison");
        }

        private static void GenerateStunIcon()
        {
            var texture = CreateIconBase("stun", new Color(1f, 1f, 0.2f)); // Bright Yellow
            DrawLightning(texture, Color.white);
            SaveIcon(texture, "stun");
        }

        private static void GenerateExecuteIcon()
        {
            var texture = CreateIconBase("execute", new Color(0.8f, 0f, 0f)); // Dark Red
            DrawSword(texture, Color.white);
            SaveIcon(texture, "execute");
        }

        private static void GenerateRicochetIcon()
        {
            var texture = CreateIconBase("ricochet", new Color(1f, 0.7f, 0.2f)); // Gold
            DrawBounce(texture, Color.white);
            SaveIcon(texture, "ricochet");
        }

        private static void GenerateLeechIcon()
        {
            var texture = CreateIconBase("leech", new Color(0.8f, 0.2f, 0.4f)); // Pink
            DrawFang(texture, Color.white);
            SaveIcon(texture, "leech");
        }

        private static void GenerateEnrageIcon()
        {
            var texture = CreateIconBase("enrage", new Color(1f, 0f, 0f)); // Pure Red
            DrawFlame(texture, Color.white);
            SaveIcon(texture, "enrage");
        }

        private static void GenerateManaBurnIcon()
        {
            var texture = CreateIconBase("mana_burn", new Color(0.4f, 0.2f, 0.8f)); // Purple
            DrawMana(texture, Color.white);
            SaveIcon(texture, "mana_burn");
        }

        private static void GenerateLastStandIcon()
        {
            var texture = CreateIconBase("last_stand", new Color(0.8f, 0.6f, 0.2f)); // Brown-Gold
            DrawFist(texture, Color.white);
            SaveIcon(texture, "last_stand");
        }

        private static void GenerateChargeIcon()
        {
            var texture = CreateIconBase("charge", new Color(1f, 0.8f, 0.2f)); // Bright Gold
            DrawSpear(texture, Color.white);
            SaveIcon(texture, "charge");
        }

        private static void GenerateHasteIcon()
        {
            var texture = CreateIconBase("haste", new Color(0.2f, 1f, 0.8f)); // Cyan
            DrawSpeed(texture, Color.white);
            SaveIcon(texture, "haste");
        }

        private static void GenerateLinkelinkIcon()
        {
            var texture = CreateIconBase("lifelink", new Color(1f, 0.4f, 0.6f)); // Rose
            DrawLink(texture, Color.white);
            SaveIcon(texture, "lifelink");
        }

        // Icon drawing helpers
        private static Texture2D CreateIconBase(string skillId, Color baseColor)
        {
            var texture = new Texture2D(ICON_SIZE, ICON_SIZE, TextureFormat.RGBA32, false);
            var radius = ICON_SIZE * 0.4f;
            var center = ICON_SIZE / 2f;

            for (int y = 0; y < ICON_SIZE; y++)
            {
                for (int x = 0; x < ICON_SIZE; x++)
                {
                    var dx = x - center;
                    var dy = y - center;
                    var distSq = dx * dx + dy * dy;
                    var radiusSq = radius * radius;

                    Color pixelColor;
                    if (distSq <= radiusSq)
                    {
                        pixelColor = baseColor;
                    }
                    else if (distSq <= (radius + 2) * (radius + 2))
                    {
                        pixelColor = new Color(baseColor.r * 0.6f, baseColor.g * 0.6f, baseColor.b * 0.6f, 1f);
                    }
                    else
                    {
                        pixelColor = new Color(0, 0, 0, 0);
                    }
                    texture.SetPixel(x, y, pixelColor);
                }
            }
            return texture;
        }

        // Simple symbol drawing functions
        private static void DrawShield(Texture2D texture, Color color)
        {
            int center = ICON_SIZE / 2;
            // Simple shield outline
            for (int i = center - 10; i < center + 10; i++)
            {
                texture.SetPixel(i, center - 12, color);
                texture.SetPixel(center - 12, i - 5, color);
                texture.SetPixel(center + 12, i - 5, color);
            }
        }

        private static void DrawHeart(Texture2D texture, Color color)
        {
            int center = ICON_SIZE / 2;
            // Simple heart shape
            for (int i = 0; i < 8; i++)
            {
                texture.SetPixel(center - 5 + i, center - 5, color);
                texture.SetPixel(center - 5 + i, center + 5, color);
                texture.SetPixel(center - 8, center - 2 + i/2, color);
                texture.SetPixel(center + 8, center - 2 + i/2, color);
            }
        }

        private static void DrawExclamation(Texture2D texture, Color color)
        {
            int center = ICON_SIZE / 2;
            // Exclamation mark
            for (int i = 0; i < 10; i++)
            {
                texture.SetPixel(center, center - 8 + i, color);
            }
            texture.SetPixel(center, center + 5, color);
        }

        private static void DrawArrow(Texture2D texture, Color color)
        {
            int center = ICON_SIZE / 2;
            // Arrow pointing right
            for (int i = 0; i < 10; i++)
            {
                texture.SetPixel(center - 5 + i, center, color);
            }
            texture.SetPixel(center + 5, center - 3, color);
            texture.SetPixel(center + 5, center + 3, color);
        }

        private static void DrawWave(Texture2D texture, Color color)
        {
            int center = ICON_SIZE / 2;
            // Wave pattern
            for (int i = -8; i <= 8; i++)
            {
                texture.SetPixel(center + i, center + (int)(3 * Mathf.Sin(i * 0.3f)), color);
            }
        }

        private static void DrawMirror(Texture2D texture, Color color)
        {
            int center = ICON_SIZE / 2;
            // Mirror/reflect symbol
            for (int i = -8; i <= 8; i++)
            {
                texture.SetPixel(center, center - 8 + i, color);
                texture.SetPixel(center - 3 - i/4, center - 8 + i, color);
            }
        }

        private static void DrawWing(Texture2D texture, Color color)
        {
            int center = ICON_SIZE / 2;
            // Simple wing shape
            for (int i = 0; i < 8; i++)
            {
                texture.SetPixel(center - 8 + i, center - 3 - i/2, color);
                texture.SetPixel(center + i, center - 3 - i/2, color);
            }
        }

        private static void DrawHoof(Texture2D texture, Color color)
        {
            int center = ICON_SIZE / 2;
            // Hoof print - 4 circles
            DrawCircle(texture, center - 5, center - 5, 2, color);
            DrawCircle(texture, center + 5, center - 5, 2, color);
            DrawCircle(texture, center - 3, center + 5, 2, color);
            DrawCircle(texture, center + 3, center + 5, 2, color);
        }

        private static void DrawDoubleSlash(Texture2D texture, Color color)
        {
            int center = ICON_SIZE / 2;
            // Two diagonal lines
            for (int i = -8; i <= 8; i++)
            {
                texture.SetPixel(center - 6 + i, center - 2 + i, color);
                texture.SetPixel(center + i, center + 2 - i/2, color);
            }
        }

        private static void DrawSkull(Texture2D texture, Color color)
        {
            int center = ICON_SIZE / 2;
            // Simple skull
            DrawCircle(texture, center, center - 3, 5, color);
            DrawCircle(texture, center - 3, center - 5, 1, color);
            DrawCircle(texture, center + 3, center - 5, 1, color);
        }

        private static void DrawLightning(Texture2D texture, Color color)
        {
            int center = ICON_SIZE / 2;
            // Lightning bolt
            texture.SetPixel(center, center - 8, color);
            texture.SetPixel(center + 2, center - 6, color);
            texture.SetPixel(center + 2, center - 2, color);
            texture.SetPixel(center, center, color);
            texture.SetPixel(center - 2, center + 4, color);
            texture.SetPixel(center - 2, center + 8, color);
        }

        private static void DrawSword(Texture2D texture, Color color)
        {
            int center = ICON_SIZE / 2;
            // Simple sword
            for (int i = -10; i <= 10; i++)
            {
                texture.SetPixel(center, center - 10 + i, color);
                if (i < -6) texture.SetPixel(center - 2, center - 10 + i, color);
                if (i < -6) texture.SetPixel(center + 2, center - 10 + i, color);
            }
        }

        private static void DrawBounce(Texture2D texture, Color color)
        {
            int center = ICON_SIZE / 2;
            // Bounce pattern
            DrawCircle(texture, center - 4, center - 4, 2, color);
            DrawCircle(texture, center + 4, center + 4, 2, color);
            texture.SetPixel(center, center, color);
        }

        private static void DrawFang(Texture2D texture, Color color)
        {
            int center = ICON_SIZE / 2;
            // Fang shape
            for (int i = 0; i < 10; i++)
            {
                texture.SetPixel(center - 1 + i/5, center - 5 + i, color);
            }
        }

        private static void DrawFlame(Texture2D texture, Color color)
        {
            int center = ICON_SIZE / 2;
            // Flame shape
            DrawCircle(texture, center, center, 3, color);
            DrawCircle(texture, center - 2, center + 3, 2, color);
            DrawCircle(texture, center + 2, center + 3, 2, color);
        }

        private static void DrawMana(Texture2D texture, Color color)
        {
            int center = ICON_SIZE / 2;
            // Mana star
            for (int i = 0; i < 5; i++)
            {
                float angle = i * Mathf.PI * 2 / 5;
                int x = center + (int)(5 * Mathf.Cos(angle));
                int y = center + (int)(5 * Mathf.Sin(angle));
                texture.SetPixel(x, y, color);
            }
        }

        private static void DrawFist(Texture2D texture, Color color)
        {
            int center = ICON_SIZE / 2;
            // Fist shape
            DrawCircle(texture, center, center, 4, color);
            texture.SetPixel(center - 4, center - 2, color);
        }

        private static void DrawSpear(Texture2D texture, Color color)
        {
            int center = ICON_SIZE / 2;
            // Spear head
            for (int i = 0; i < 6; i++)
            {
                texture.SetPixel(center - 2 + i, center - 8, color);
                texture.SetPixel(center, center - 8 + i, color);
            }
        }

        private static void DrawSpeed(Texture2D texture, Color color)
        {
            int center = ICON_SIZE / 2;
            // Speed lines
            for (int j = 0; j < 3; j++)
            {
                for (int i = 0; i < 8; i++)
                {
                    texture.SetPixel(center - 8 + i, center - 5 + j * 3, color);
                }
            }
        }

        private static void DrawLink(Texture2D texture, Color color)
        {
            int center = ICON_SIZE / 2;
            // Chain link
            DrawCircle(texture, center - 4, center, 2, color);
            DrawCircle(texture, center + 4, center, 2, color);
            texture.SetPixel(center - 2, center, color);
            texture.SetPixel(center, center, color);
            texture.SetPixel(center + 2, center, color);
        }

        private static void DrawCircle(Texture2D texture, int cx, int cy, int radius, Color color)
        {
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    if (x * x + y * y <= radius * radius)
                    {
                        int px = cx + x;
                        int py = cy + y;
                        if (px >= 0 && px < ICON_SIZE && py >= 0 && py < ICON_SIZE)
                        {
                            texture.SetPixel(px, py, color);
                        }
                    }
                }
            }
        }

        private static void SaveIcon(Texture2D texture, string skillId)
        {
            texture.Apply();
            texture.name = $"Icon_{skillId}";
            var path = Path.Combine(ICON_OUTPUT_PATH, $"Icon_{skillId}.png");
            var pngData = texture.EncodeToPNG();
            var fullPath = Path.Combine(Application.dataPath, "../", path);
            File.WriteAllBytes(fullPath, pngData);
            Debug.Log($"Generated icon: {path}");
            Object.DestroyImmediate(texture);
        }

        private static void EnsureDirectoryExists(string path)
        {
            var fullPath = Path.Combine(Application.dataPath, "../", path);
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
            }
        }
    }
}

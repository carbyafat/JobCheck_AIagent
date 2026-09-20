using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace JobCheck.Ui.Editor.Tests
{
    public sealed class UiThemeAssetTests
    {
        private const string ThemePath = "Assets/JobCheckUiTheme.asset";

        [Test]
        public void Theme_DefinesFontsAndOrderedTypeScale()
        {
            SerializedObject theme = LoadTheme();

            Assert.That(theme.FindProperty("displayFont").objectReferenceValue, Is.Not.Null);
            Assert.That(theme.FindProperty("bodyFont").objectReferenceValue, Is.Not.Null);
            Assert.That(Float(theme, "pageTitleSize"), Is.GreaterThan(Float(theme, "sectionTitleSize")));
            Assert.That(Float(theme, "sectionTitleSize"), Is.GreaterThan(Float(theme, "bodySize")));
            Assert.That(Float(theme, "bodySize"), Is.GreaterThan(Float(theme, "supportingTextSize")));
        }

        [Test]
        public void Theme_DefinesAscendingSpacingAndReusableComponentMetrics()
        {
            SerializedObject theme = LoadTheme();

            float[] spaces =
            {
                Float(theme, "spaceXs"),
                Float(theme, "spaceSm"),
                Float(theme, "spaceMd"),
                Float(theme, "spaceLg"),
                Float(theme, "spaceXl")
            };

            for (int index = 1; index < spaces.Length; index++)
            {
                Assert.That(spaces[index], Is.GreaterThan(spaces[index - 1]));
            }

            Assert.That(Float(theme, "sidebarWidth"), Is.EqualTo(240f));
            Assert.That(Float(theme, "compactButtonHeight"), Is.LessThan(Float(theme, "buttonHeight")));
            Assert.That(Float(theme, "controlCornerRadius"), Is.LessThanOrEqualTo(Float(theme, "cardCornerRadius")));
            Assert.That(Float(theme, "modalMaxWidth"), Is.LessThanOrEqualTo(1400f));
        }

        [Test]
        public void Theme_CoreTextCombinationsMeetAccessibleContrast()
        {
            SerializedObject theme = LoadTheme();

            Assert.That(Contrast(Color(theme, "textPrimary"), Color(theme, "appBackground")),
                Is.GreaterThanOrEqualTo(4.5f));
            Assert.That(Contrast(Color(theme, "textSecondary"), Color(theme, "appBackground")),
                Is.GreaterThanOrEqualTo(4.5f));
            Assert.That(Contrast(Color(theme, "textOnPrimary"), Color(theme, "primary")),
                Is.GreaterThanOrEqualTo(4.5f));
        }

        private static SerializedObject LoadTheme()
        {
            Object asset = AssetDatabase.LoadMainAssetAtPath(ThemePath);
            Assert.That(asset, Is.Not.Null, "Missing UI theme asset: " + ThemePath);
            return new SerializedObject(asset);
        }

        private static float Float(SerializedObject theme, string name)
        {
            SerializedProperty property = theme.FindProperty(name);
            Assert.That(property, Is.Not.Null, "Missing theme value: " + name);
            return property.floatValue;
        }

        private static Color Color(SerializedObject theme, string name)
        {
            SerializedProperty property = theme.FindProperty(name);
            Assert.That(property, Is.Not.Null, "Missing theme color: " + name);
            return property.colorValue;
        }

        private static float Contrast(Color first, Color second)
        {
            float firstLuminance = Luminance(first);
            float secondLuminance = Luminance(second);
            float light = Mathf.Max(firstLuminance, secondLuminance);
            float dark = Mathf.Min(firstLuminance, secondLuminance);
            return (light + 0.05f) / (dark + 0.05f);
        }

        private static float Luminance(Color color)
        {
            return 0.2126f * Linear(color.r) + 0.7152f * Linear(color.g) + 0.0722f * Linear(color.b);
        }

        private static float Linear(float channel)
        {
            return channel <= 0.03928f
                ? channel / 12.92f
                : Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f);
        }
    }
}

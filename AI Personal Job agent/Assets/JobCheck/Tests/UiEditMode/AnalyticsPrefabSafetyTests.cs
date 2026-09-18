using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace JobCheck.Ui.Editor.Tests
{
    /// <summary>只讀取 Prefab 資產；不進入 Play Mode，也不產生動態字型資料。</summary>
    public sealed class AnalyticsPrefabSafetyTests
    {
        private const string HostPath = "Assets/Prefab/AllJobPage.prefab";
        private const string PanelPath = "Assets/Prefab/Panel_Analytics.prefab";

        [Test]
        public void OneTimeBuilder_RejectsExistingAnalyticsPrefab()
        {
            Type builder = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("BuildAnalyticsPrefab", false))
                .FirstOrDefault(type => type != null);
            Assert.That(builder, Is.Not.Null);
            MethodInfo canBuild = builder.GetMethod("CanBuild", BindingFlags.Public | BindingFlags.Static);
            Assert.That(canBuild, Is.Not.Null);
            Assert.That((bool)canBuild.Invoke(null, null), Is.False);
        }

        [Test]
        public void HostPrefab_HasInactiveAnalyticsPanelAtFrontOfDisplayOrder()
        {
            GameObject host = AssetDatabase.LoadAssetAtPath<GameObject>(HostPath);
            Assert.That(host, Is.Not.Null);
            Transform entry = host.transform.Find("Button_ShowAnalytics");
            Transform panel = host.transform.Find("Panel_Analytics");
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.GetComponent("Button"), Is.Not.Null);
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.gameObject.activeSelf, Is.False);
            Assert.That(panel.GetSiblingIndex(), Is.GreaterThan(entry.GetSiblingIndex()));
        }

        [Test]
        public void AnalyticsReferences_AreSerializedInBothPrefabs()
        {
            GameObject host = AssetDatabase.LoadAssetAtPath<GameObject>(HostPath);
            Assert.That(host, Is.Not.Null);
            MonoBehaviour page = host.GetComponents<MonoBehaviour>()
                .FirstOrDefault(component => component != null && component.GetType().Name == "AllJobPage");
            Assert.That(page, Is.Not.Null);
            var pageData = new SerializedObject(page);
            Assert.That(pageData.FindProperty("buttonShowAnalytics").objectReferenceValue, Is.Not.Null);
            Assert.That(pageData.FindProperty("analyticsPanel").objectReferenceValue, Is.Not.Null);

            GameObject panel = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            Assert.That(panel, Is.Not.Null);
            MonoBehaviour controller = panel.GetComponents<MonoBehaviour>()
                .FirstOrDefault(component => component != null && component.GetType().Name == "Panel_Analytics");
            Assert.That(controller, Is.Not.Null);
            var panelData = new SerializedObject(controller);
            foreach (string field in new[] { "textProfile", "textReport", "buttonClose", "scrollRect" })
                Assert.That(panelData.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
        }
    }
}

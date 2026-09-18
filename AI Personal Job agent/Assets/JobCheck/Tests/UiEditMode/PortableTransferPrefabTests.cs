using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace JobCheck.Ui.Editor.Tests
{
    public sealed class PortableTransferPrefabTests
    {
        [Test]
        public void HostPrefab_HasBoundInactiveTransferPanel()
        {
            GameObject host = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefab/AllJobPage.prefab");
            Assert.That(host, Is.Not.Null);
            Transform entry = host.transform.Find("Button_PortableTransfer");
            Transform panel = host.transform.Find("Panel_PortableTransfer");
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.GetComponent<UnityEngine.UI.Button>(), Is.Not.Null);
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.gameObject.activeSelf, Is.False);
            Assert.That(panel.GetSiblingIndex(), Is.EqualTo(host.transform.childCount - 1));

            MonoBehaviour page = host.GetComponents<MonoBehaviour>()
                .First(component => component != null && component.GetType().Name == "AllJobPage");
            var pageData = new SerializedObject(page);
            Assert.That(pageData.FindProperty("buttonPortableTransfer").objectReferenceValue,
                Is.Not.Null);
            Assert.That(pageData.FindProperty("portableTransferPanel").objectReferenceValue,
                Is.Not.Null);

            MonoBehaviour controller = panel.GetComponents<MonoBehaviour>()
                .First(component => component != null && component.GetType().Name == "Panel_PortableTransfer");
            var panelData = new SerializedObject(controller);
            foreach (string field in new[] {
                "textMessage", "inputPackagePath", "buttonExport", "buttonPreview",
                "buttonImport", "buttonClose" })
                Assert.That(panelData.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
        }
    }
}

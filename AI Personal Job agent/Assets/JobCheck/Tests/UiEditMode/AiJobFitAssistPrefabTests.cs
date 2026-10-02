using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace JobCheck.Ui.Editor.Tests
{
    public sealed class AiJobFitAssistPrefabTests
    {
        [Test]
        public void JobDetailPrefab_HasBoundInactiveAiAssistModal()
        {
            GameObject host = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefab/SinglePanel_Detail.prefab");
            Assert.That(host, Is.Not.Null);

            Transform entry = Find(host.transform, "Button_ShowAiAssist_V029");
            Transform panel = Find(host.transform, "Panel_AiJobFitAssist_V029");
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.GetComponent<Button>(), Is.Not.Null);
            Assert.That(entry.GetComponentInChildren<TMP_Text>(true).text,
                Is.EqualTo("AI 輔助"));
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.gameObject.activeSelf, Is.False);
            Assert.That(panel.GetSiblingIndex(), Is.EqualTo(host.transform.childCount - 1));

            MonoBehaviour detailController = FindController(host, "Panel_JobDetail");
            Assert.That(detailController, Is.Not.Null);
            var detail = new SerializedObject(detailController);
            Assert.That(detail.FindProperty("buttonShowAiAssist").objectReferenceValue,
                Is.Not.Null);
            Assert.That(detail.FindProperty("aiAssistPanel").objectReferenceValue,
                Is.Not.Null);

            MonoBehaviour panelController = FindController(
                panel.gameObject,
                "Panel_AiJobFitAssist");
            Assert.That(panelController, Is.Not.Null);
            var controller = new SerializedObject(panelController);
            foreach (string field in new[]
            {
                "textDataPreview", "textStatus", "inputResultJson", "textResultPreview",
                "scrollRect", "buttonExportRequest", "buttonCopyInstructions",
                "buttonChooseResult", "buttonValidateResult", "buttonSaveResult", "buttonClose"
            })
            {
                Assert.That(controller.FindProperty(field).objectReferenceValue,
                    Is.Not.Null, field);
            }

            ScrollRect scroll = panel.GetComponentInChildren<ScrollRect>(true);
            Assert.That(scroll, Is.Not.Null);
            Assert.That(scroll.horizontal, Is.False);
            Assert.That(scroll.vertical, Is.True);
            Assert.That(scroll.verticalScrollbar, Is.Not.Null);
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }

        private static MonoBehaviour FindController(GameObject root, string typeName)
        {
            foreach (MonoBehaviour component in root.GetComponents<MonoBehaviour>())
            {
                if (component != null && component.GetType().Name == typeName)
                    return component;
            }
            return null;
        }
    }
}

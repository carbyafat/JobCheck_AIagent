using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JobCheck.Ui.Editor.Tests
{
    public sealed class AppShellSceneTests
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string AllJobsPrefabPath = "Assets/Prefab/AllJobPage.prefab";
        private const string JobDetailPrefabPath = "Assets/Prefab/SinglePanel_Detail.prefab";

        [Test]
        public void JobsPage_ContainsExistingListAndDetailPrefabs()
        {
            WithScene(scene =>
            {
                Transform canvas = FindRoot(scene, "Canvas").transform;
                Transform appShell = RequireChild(canvas, "AppShell");
                Transform contentRoot = RequireChild(appShell, "ContentRoot");
                Transform jobsPage = RequireChild(contentRoot, "Page_Jobs");
                Transform allJobPage = RequireChild(jobsPage, "AllJobPage");
                Transform detailPage = RequireChild(jobsPage, "SinglePanel_Detail");

                Assert.That(allJobPage.gameObject.activeSelf, Is.True);
                Assert.That(detailPage.gameObject.activeSelf, Is.False);

                MonoBehaviour listController = FindComponent(allJobPage, "AllJobPage");
                var listData = new SerializedObject(listController);
                Component detailReference = listData.FindProperty("jobDetailPanel").objectReferenceValue as Component;
                Assert.That(detailReference, Is.Not.Null);
                Assert.That(detailReference.transform, Is.EqualTo(detailPage));

                MonoBehaviour detailController = FindComponent(detailPage, "Panel_JobDetail");
                var detailData = new SerializedObject(detailController);
                Assert.That(detailData.FindProperty("buttonBack").objectReferenceValue, Is.Not.Null);
            });
        }

        [Test]
        public void Navigator_SwitchesOnlyTopLevelPages()
        {
            var host = new GameObject("NavigatorHost");
            var home = new GameObject("Page_Home");
            var jobs = new GameObject("Page_Jobs");
            var resume = new GameObject("Page_Resume");
            home.transform.SetParent(host.transform);
            jobs.transform.SetParent(host.transform);
            resume.transform.SetParent(host.transform);

            try
            {
                Type navigatorType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType("AppPageNavigator", false))
                    .FirstOrDefault(type => type != null);
                Assert.That(navigatorType, Is.Not.Null);
                MonoBehaviour navigator = host.AddComponent(navigatorType) as MonoBehaviour;
                Assert.That(navigator, Is.Not.Null);

                var data = new SerializedObject(navigator);
                data.FindProperty("pageHome").objectReferenceValue = home;
                data.FindProperty("pageJobs").objectReferenceValue = jobs;
                data.FindProperty("pageResume").objectReferenceValue = resume;
                data.ApplyModifiedPropertiesWithoutUndo();

                Invoke(navigator, "ShowResume");
                Assert.That(home.activeSelf, Is.False);
                Assert.That(jobs.activeSelf, Is.False);
                Assert.That(resume.activeSelf, Is.True);

                Invoke(navigator, "ShowJobs");
                Assert.That(home.activeSelf, Is.False);
                Assert.That(jobs.activeSelf, Is.True);
                Assert.That(resume.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Scene_ReservesTwoHundredFortyPixelsForSidebar()
        {
            WithScene(scene =>
            {
                Transform appShell = RequireChild(FindRoot(scene, "Canvas").transform, "AppShell");
                RectTransform sidebar = RequireChild(appShell, "Sidebar") as RectTransform;
                RectTransform contentRoot = RequireChild(appShell, "ContentRoot") as RectTransform;

                Assert.That(sidebar, Is.Not.Null);
                Assert.That(contentRoot, Is.Not.Null);
                Assert.That(sidebar.sizeDelta.x, Is.EqualTo(240f));
                Assert.That(contentRoot.sizeDelta.x, Is.EqualTo(-240f));
                Assert.That(contentRoot.anchoredPosition.x, Is.EqualTo(120f));
            });
        }

        [Test]
        public void JobsPagePrimaryControls_FitInsideAvailableWidth()
        {
            GameObject page = AssetDatabase.LoadAssetAtPath<GameObject>(AllJobsPrefabPath);
            Assert.That(page, Is.Not.Null);

            foreach (string name in new[]
                     {
                         "Button_TrashManagement",
                         "Button_PortableTransfer",
                         "Button_ShowAnalytics",
                         "Button_AddJobPosting",
                         "Button_ShowFilter",
                         "Button_Load",
                         "Button_LastPage",
                         "Button_NextPage",
                         "TMP_Date",
                         "JobPanels"
                     })
            {
                RectTransform item = page.transform.Find(name) as RectTransform;
                Assert.That(item, Is.Not.Null, name);
                float left = item.anchoredPosition.x - item.sizeDelta.x * item.pivot.x;
                float right = item.anchoredPosition.x + item.sizeDelta.x * (1f - item.pivot.x);
                Assert.That(left, Is.GreaterThanOrEqualTo(-840f), name + " left");
                Assert.That(right, Is.LessThanOrEqualTo(840f), name + " right");
            }

            GameObject detail = AssetDatabase.LoadAssetAtPath<GameObject>(JobDetailPrefabPath);
            Assert.That(detail, Is.Not.Null);
            RectTransform scrollView = detail.transform.Find("Scroll View") as RectTransform;
            Assert.That(scrollView, Is.Not.Null);
            Assert.That(scrollView.sizeDelta.x, Is.LessThanOrEqualTo(1600f));
        }

        [Test]
        public void ResumePage_HasBoundProfileDisplayController()
        {
            WithScene(scene =>
            {
                Transform appShell = RequireChild(FindRoot(scene, "Canvas").transform, "AppShell");
                Transform resume = RequireChild(RequireChild(appShell, "ContentRoot"), "Page_Resume");
                Transform display = RequireChild(resume, "ResumeProfileDisplay");
                MonoBehaviour controller = FindComponent(resume, "CareerProfilePage");
                var data = new SerializedObject(controller);

                Assert.That(data.FindProperty("displayText").objectReferenceValue, Is.Not.Null);
                Assert.That(data.FindProperty("displayText").objectReferenceValue, Is.EqualTo(
                    display.GetComponent<UnityEngine.UI.Text>()));
                Assert.That(controller.GetType().GetMethod("OpenEditor"), Is.Not.Null);
                Assert.That(controller.GetType().GetMethod("SaveEditor"), Is.Not.Null);
                Assert.That(controller.GetType().GetMethod("CancelEditor"), Is.Not.Null);
            });
        }

        private static void WithScene(Action<Scene> assertion)
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedForTest = !scene.IsValid() || !scene.isLoaded;
            if (openedForTest)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            try
            {
                assertion(scene);
            }
            finally
            {
                if (openedForTest)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            GameObject root = scene.GetRootGameObjects().FirstOrDefault(item => item.name == name);
            Assert.That(root, Is.Not.Null, "Missing root: " + name);
            return root;
        }

        private static Transform RequireChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            Assert.That(child, Is.Not.Null, "Missing child: " + parent.name + "/" + name);
            return child;
        }

        private static MonoBehaviour FindComponent(Transform root, string typeName)
        {
            MonoBehaviour component = root.GetComponents<MonoBehaviour>()
                .FirstOrDefault(item => item != null && item.GetType().Name == typeName);
            Assert.That(component, Is.Not.Null, "Missing component: " + typeName);
            return component;
        }

        private static void Invoke(MonoBehaviour target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, "Missing method: " + methodName);
            method.Invoke(target, arguments);
        }
    }
}

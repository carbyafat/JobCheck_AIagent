using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace JobCheck.Ui.Editor.Tests
{
    public sealed class AppShellSceneTests
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string AllJobsPrefabPath = "Assets/Prefab/AllJobPage.prefab";
        private const string JobDetailPrefabPath = "Assets/Prefab/SinglePanel_Detail.prefab";
        private const string FilterPanelPrefabPath = "Assets/Prefab/FilterPanels.prefab";
        private const string AnalyticsPanelPrefabPath = "Assets/Prefab/Panel_Analytics.prefab";
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;
        private const float TargetWindowWidth = 1600f;
        private const float TargetWindowHeight = 900f;

        [Test]
        public void Player_DefaultsToFixedSixteenHundredByNineHundredWindow()
        {
            Assert.That(PlayerSettings.defaultScreenWidth, Is.EqualTo((int)TargetWindowWidth));
            Assert.That(PlayerSettings.defaultScreenHeight, Is.EqualTo((int)TargetWindowHeight));
            Assert.That(PlayerSettings.fullScreenMode, Is.EqualTo(FullScreenMode.Windowed));
            Assert.That(PlayerSettings.resizableWindow, Is.False);
        }

        [Test]
        public void Canvas_ScalesUniformlyFromReferenceToTargetWindow()
        {
            WithScene(scene =>
            {
                CanvasScaler scaler = FindRoot(scene, "Canvas").GetComponent<CanvasScaler>();
                Assert.That(scaler, Is.Not.Null);
                Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
                Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(ReferenceWidth, ReferenceHeight)));
                Assert.That(scaler.screenMatchMode, Is.EqualTo(CanvasScaler.ScreenMatchMode.MatchWidthOrHeight));
                Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(0f));

                float widthScale = TargetWindowWidth / ReferenceWidth;
                float heightScale = TargetWindowHeight / ReferenceHeight;
                Assert.That(widthScale, Is.EqualTo(heightScale).Within(0.0001f));
                Assert.That(ReferenceHeight * widthScale, Is.EqualTo(TargetWindowHeight).Within(0.01f));
            });
        }

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
        public void AppShell_HasFinishedSidebarNavigation()
        {
            WithScene(scene =>
            {
                Transform appShell = RequireChild(FindRoot(scene, "Canvas").transform, "AppShell");
                MonoBehaviour presenter = FindComponent(appShell, "AppShellThemePresenter");
                var data = new SerializedObject(presenter);

                Assert.That(data.FindProperty("theme").objectReferenceValue, Is.Not.Null);
                Assert.That(data.FindProperty("appBackground").objectReferenceValue, Is.Not.Null);
                Assert.That(data.FindProperty("sidebarBackground").objectReferenceValue, Is.Not.Null);
                Assert.That(data.FindProperty("versionText").objectReferenceValue, Is.Not.Null);
                Assert.That(data.FindProperty("activeNavigationIndicator").objectReferenceValue, Is.Not.Null);
                Assert.That(data.FindProperty("buttonHome").objectReferenceValue, Is.Not.Null);
                Assert.That(data.FindProperty("buttonJobs").objectReferenceValue, Is.Not.Null);
                Assert.That(data.FindProperty("buttonResume").objectReferenceValue, Is.Not.Null);

                Transform sidebar = RequireChild(appShell, "Sidebar");
                Assert.That(sidebar.Find("Text_PrototypeNote"), Is.Null);
                Text version = RequireChild(sidebar, "Text_Version").GetComponent<Text>();
                Assert.That(version, Is.Not.Null);
                Assert.That(version.text, Is.EqualTo("v0.2.6"));
                Assert.That(version.gameObject.activeSelf, Is.True);
                Assert.That(RequireChild(sidebar, "Navigation_ActiveIndicator").GetComponent<Image>(),
                    Is.Not.Null);

                Assert.That(RequireChild(RequireChild(sidebar, "Button_Home"), "Text").GetComponent<Text>().text,
                    Does.StartWith("⌂"));
                Assert.That(RequireChild(RequireChild(sidebar, "Button_Jobs"), "Text").GetComponent<Text>().text,
                    Does.StartWith("▤"));
                Assert.That(RequireChild(RequireChild(sidebar, "Button_Resume"), "Text").GetComponent<Text>().text,
                    Does.StartWith("▣"));
            });
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

            float scale = TargetWindowWidth / ReferenceWidth;
            float sidebarWidth = 240f * scale;
            float contentCenter = sidebarWidth + (TargetWindowWidth - sidebarWidth) * 0.5f;

            foreach (string name in new[]
                     {
                         "Button_AddJobPosting",
                         "Button_ShowFilter",
                         "Button_Load",
                         "Button_MoreActions",
                         "Button_LastPage",
                         "Button_NextPage",
                         "TMP_Date",
                         "JobPanels"
                     })
            {
                RectTransform item = FindDescendant(page.transform, name) as RectTransform;
                Assert.That(item, Is.Not.Null, name);
                float left = item.anchoredPosition.x - item.sizeDelta.x * item.pivot.x;
                float right = item.anchoredPosition.x + item.sizeDelta.x * (1f - item.pivot.x);
                Assert.That(left, Is.GreaterThanOrEqualTo(-840f), name + " left");
                Assert.That(right, Is.LessThanOrEqualTo(840f), name + " right");

                float screenLeft = contentCenter + left * scale;
                float screenRight = contentCenter + right * scale;
                Assert.That(screenLeft, Is.GreaterThanOrEqualTo(sidebarWidth - 0.01f), name + " 1600x900 left");
                Assert.That(screenRight, Is.LessThanOrEqualTo(TargetWindowWidth + 0.01f), name + " 1600x900 right");
            }

            Transform morePanel = FindDescendant(page.transform, "Panel_MoreActions");
            Assert.That(morePanel, Is.Not.Null);
            Assert.That(morePanel.gameObject.activeSelf, Is.False,
                "Low-frequency actions should be hidden until requested.");
            foreach (string name in new[]
                     {
                         "Button_ShowAnalytics",
                         "Button_PortableTransfer",
                         "Button_TrashManagement"
                     })
            {
                Assert.That(FindDescendant(morePanel, name), Is.Not.Null, name);
            }

            GameObject detail = AssetDatabase.LoadAssetAtPath<GameObject>(JobDetailPrefabPath);
            Assert.That(detail, Is.Not.Null);
            RectTransform scrollView = detail.transform.Find("Scroll View") as RectTransform;
            Assert.That(scrollView, Is.Not.Null);
            Assert.That(scrollView.sizeDelta.x, Is.LessThanOrEqualTo(1600f));
        }

        [Test]
        public void JobsPageAndCards_HaveBoundThemeAssets()
        {
            GameObject page = AssetDatabase.LoadAssetAtPath<GameObject>(AllJobsPrefabPath);
            Assert.That(page, Is.Not.Null);
            MonoBehaviour pagePresenter = FindComponent(page.transform, "JobsPageThemePresenter");
            Assert.That(new SerializedObject(pagePresenter).FindProperty("theme").objectReferenceValue, Is.Not.Null);

            MonoBehaviour moreActions = FindComponent(page.transform, "MoreActionsMenu");
            var moreActionsData = new SerializedObject(moreActions);
            Assert.That(moreActionsData.FindProperty("buttonMoreActions").objectReferenceValue, Is.Not.Null);
            Assert.That(moreActionsData.FindProperty("panelMoreActions").objectReferenceValue, Is.Not.Null);
            Assert.That(moreActionsData.FindProperty("menuActionButtons").arraySize, Is.EqualTo(3));

            const string cardPath = "Assets/Prefab/Panel_SingleJob.prefab";
            GameObject card = AssetDatabase.LoadAssetAtPath<GameObject>(cardPath);
            Assert.That(card, Is.Not.Null);
            MonoBehaviour cardController = FindComponent(card.transform, "Panel_SingleJob");
            Assert.That(new SerializedObject(cardController).FindProperty("theme").objectReferenceValue, Is.Not.Null);
        }

        [Test]
        public void FilterPanel_StretchesToJobsPageAndKeepsActionsInsideContent()
        {
            GameObject panel = AssetDatabase.LoadAssetAtPath<GameObject>(FilterPanelPrefabPath);
            Assert.That(panel, Is.Not.Null);

            RectTransform root = panel.transform as RectTransform;
            Assert.That(root, Is.Not.Null);
            Assert.That(root.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(root.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(root.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(root.sizeDelta, Is.EqualTo(Vector2.zero));

            foreach (string name in new[] { "Button_ClearAll", "Button_AppliedFilter" })
            {
                RectTransform button = panel.transform.Find(name) as RectTransform;
                Assert.That(button, Is.Not.Null, name);
                float left = button.anchoredPosition.x - button.sizeDelta.x * button.pivot.x;
                float right = button.anchoredPosition.x + button.sizeDelta.x * (1f - button.pivot.x);
                Assert.That(left, Is.GreaterThanOrEqualTo(-840f), name + " left");
                Assert.That(right, Is.LessThanOrEqualTo(840f), name + " right");
            }
        }

        [Test]
        public void JobModals_UseOneOverlayCardAndFixedChromePattern()
        {
            GameObject jobsPage = AssetDatabase.LoadAssetAtPath<GameObject>(AllJobsPrefabPath);
            GameObject filterPanel = AssetDatabase.LoadAssetAtPath<GameObject>(FilterPanelPrefabPath);
            GameObject analyticsPanel = AssetDatabase.LoadAssetAtPath<GameObject>(AnalyticsPanelPrefabPath);
            Assert.That(jobsPage, Is.Not.Null);
            Assert.That(filterPanel, Is.Not.Null);
            Assert.That(analyticsPanel, Is.Not.Null);

            Transform[] modalRoots =
            {
                filterPanel.transform,
                FindDescendant(jobsPage.transform, "Panel_TrashManagement"),
                FindDescendant(jobsPage.transform, "Panel_PortableTransfer"),
                analyticsPanel.transform,
                FindDescendant(jobsPage.transform, "Panel_JobPostingCreate")
            };

            foreach (Transform modal in modalRoots)
            {
                Assert.That(modal, Is.Not.Null);
                Image overlay = modal.GetComponent<Image>();
                Assert.That(overlay, Is.Not.Null, modal.name + " overlay");
                Assert.That(overlay.color.a, Is.EqualTo(0.65f).Within(0.001f), modal.name + " overlay alpha");

                RectTransform card = FindDescendant(modal, "Card") as RectTransform;
                Assert.That(card, Is.Not.Null, modal.name + " card");
                Assert.That(card.sizeDelta.x, Is.LessThanOrEqualTo(1400f), modal.name + " width");
                Assert.That(card.sizeDelta.y, Is.LessThanOrEqualTo(900f), modal.name + " height");
                Assert.That(FindDescendant(modal, "Button_CloseTop"), Is.Not.Null,
                    modal.name + " top-right close");
                Assert.That(FindDescendant(modal, "Title") ?? FindDescendant(modal, "Text_Title"), Is.Not.Null,
                    modal.name + " fixed title");
            }

            Assert.That(FindDescendant(analyticsPanel.transform, "Viewport").GetComponent<ScrollRect>(), Is.Not.Null);
            Assert.That(FindDescendant(modalRoots[4], "Scroll_Form").GetComponent<ScrollRect>(), Is.Not.Null);

            RectTransform analyticsProfile = FindDescendant(analyticsPanel.transform, "Text_Profile") as RectTransform;
            RectTransform analyticsClose = FindDescendant(analyticsPanel.transform, "Button_CloseTop") as RectTransform;
            Assert.That(analyticsProfile, Is.Not.Null);
            Assert.That(analyticsClose, Is.Not.Null);
            float profileRight = analyticsProfile.anchoredPosition.x + analyticsProfile.sizeDelta.x * 0.5f;
            float closeLeft = analyticsClose.anchoredPosition.x - analyticsClose.sizeDelta.x * 0.5f;
            Assert.That(profileRight, Is.LessThanOrEqualTo(closeLeft - 24f),
                "Analytics profile label must reserve space for the top-right close button.");
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
                    display.GetComponent<TMPro.TMP_Text>()));
                Assert.That(data.FindProperty("fontAsset").objectReferenceValue, Is.Not.Null);
                Assert.That(controller.GetType().GetMethod("OpenEditor"), Is.Not.Null);
                Assert.That(controller.GetType().GetMethod("SaveEditor"), Is.Not.Null);
                Assert.That(controller.GetType().GetMethod("CancelEditor"), Is.Not.Null);
                Assert.That(controller.GetType().GetMethod("OpenTransfer"), Is.Not.Null);
                Assert.That(controller.GetType().GetMethod("CloseTransfer"), Is.Not.Null);
                Assert.That(controller.GetType().GetMethod("ChooseExportPath"), Is.Not.Null);
                Assert.That(controller.GetType().GetMethod("ChooseImportFile"), Is.Not.Null);
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

        private static Transform FindDescendant(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item.name == name);
        }

        private static void Invoke(MonoBehaviour target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, "Missing method: " + methodName);
            method.Invoke(target, arguments);
        }
    }
}

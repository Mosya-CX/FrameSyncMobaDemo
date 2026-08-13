using FrameSyncMoba.LuaBridge;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FrameSyncMoba.Bootstrap.Tests
{
    /// <summary>
    /// Cell prefab refs must resolve to the components Lua expects: Button
    /// refs point at Button components (the buttons live on the icon child),
    /// and the SelectTip/OwnedMask nodes added for selection/owned state are
    /// exposed to cell Lua.
    /// </summary>
    [TestFixture]
    public sealed class CellPrefabRefTests
    {
        [Test]
        public void HeroSelectCell_ButtonRefIsButtonAndSelectTipBound()
        {
            Validate(
                "Assets/Resources/Prefab/UI/HeroSelectCell.prefab",
                "UI.HeroCell",
                "SelectTip");
        }

        [Test]
        public void EquipmentShopCell_ButtonRefIsButtonAndStateRefsBound()
        {
            const string path =
                "Assets/Resources/Prefab/UI/EquipmentShopCell.prefab";
            Validate(
                path,
                "UI.ShopCell",
                "Icon",
                "SelectTip",
                "OwnedMask");
            GameObject root =
                AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Image icon = root.transform.Find("Icon")
                ?.GetComponent<Image>();
            Assert.That(
                icon,
                Is.Not.Null,
                path + " must expose an Image icon.");
            Image ownedMask = root.transform
                .Find("OwnedMask")
                ?.GetComponent<Image>();
            Assert.That(ownedMask, Is.Not.Null);
            Assert.That(
                ownedMask.raycastTarget,
                Is.False,
                "OwnedMask is visual-only and must not block cell selection.");
            Graphic[] ownedGraphics = ownedMask
                .GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < ownedGraphics.Length; i++)
            {
                Assert.That(
                    ownedGraphics[i].raycastTarget,
                    Is.False,
                    ownedGraphics[i].name +
                    " is inside OwnedMask and must not block cell selection.");
            }
        }

        private static void Validate(
            string path,
            string module,
            params string[] extraRefs)
        {
            GameObject root =
                AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(root, Is.Not.Null, path);
            UICell cell = root.GetComponent<UICell>();
            Assert.That(cell, Is.Not.Null, path);

            var serialized = new SerializedObject(cell);
            SerializedProperty refs =
                serialized.FindProperty("refs");
            Assert.That(refs, Is.Not.Null);

            UnityEngine.Object buttonRef = null;
            bool buttonFound = false;
            for (int i = 0; i < refs.arraySize; i++)
            {
                SerializedProperty entry =
                    refs.GetArrayElementAtIndex(i);
                string name =
                    entry.FindPropertyRelative("Name")
                        .stringValue;
                UnityEngine.Object value =
                    entry.FindPropertyRelative("Value")
                        .objectReferenceValue;
                if (name == "Button")
                {
                    buttonFound = true;
                    buttonRef = value;
                }
            }
            Assert.That(
                buttonFound,
                Is.True,
                path + " must expose a Button ref.");
            Assert.That(
                buttonRef,
                Is.InstanceOf<Button>(),
                path + " Button ref must resolve to a Button component.");

            for (int i = 0; i < extraRefs.Length; i++)
            {
                bool found = false;
                for (int j = 0; j < refs.arraySize; j++)
                {
                    SerializedProperty entry =
                        refs.GetArrayElementAtIndex(j);
                    if (entry.FindPropertyRelative("Name")
                            .stringValue == extraRefs[i])
                    {
                        found = true;
                        break;
                    }
                }
                Assert.That(
                    found,
                    Is.True,
                    path + " must expose a '" +
                    extraRefs[i] + "' ref.");
            }
        }
    }
}

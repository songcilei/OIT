using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FXLodTools.Editor
{
    public static class FXLodSelfTests
    {
        private static int assertions;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("Tools/FXLod/运行自检")]
        public static void RunMenu()
        {
            try { RunAll(); EditorUtility.DisplayDialog("FXLod 自检", $"通过 {assertions} 项断言。详情见 Console。", "确定"); }
            catch (Exception exception) { Debug.LogException(exception); EditorUtility.DisplayDialog("FXLod 自检失败", exception.Message, "确定"); }
        }

        public static void RunBatch()
        {
            try { RunAll(); TestPrefabStageSave(); Debug.Log($"[FXLod Tests] FINAL PASS: {assertions} assertions."); EditorApplication.Exit(0); }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }

        public static void RunAll()
        {
            assertions = 0;
            string testRoot = "Assets/__FXLodSelfTest_" + Guid.NewGuid().ToString("N");
            string sourceRoot = testRoot + "/Effects/High";
            var roots = new List<GameObject>();
            try
            {
                FXLodExporter.EnsureFolder(sourceRoot + "/Skills/Fire");
                var root = new GameObject("Fire"); roots.Add(root);
                var smoke = Child(root, "Smoke");
                var sparks = Child(smoke, "Sparks");
                var core = Child(root, "Core");
                var inactive = Child(root, "InitiallyInactive"); inactive.SetActive(false);
                var lod = root.AddComponent<FXLod>(); lod.SynchronizeObjects();
                Check(lod.Objects.Count == 4, "同步全部后代（包括禁用物体）");
                Check(Enum.GetValues(typeof(FXLodLevel)).Length == 2 && (int)FXLodLevel.Middle == 1 && (int)FXLodLevel.Low == 2, "仅保留 Middle / Low 且保留旧枚举序列化值");
                using (var serialized = new SerializedObject(lod))
                {
                    Check(serialized.FindProperty("defaultLevel") == null && serialized.FindProperty("applyOnEnable") == null, "没有默认等级和自动应用字段");
                    Check(serialized.FindProperty("objects").GetArrayElementAtIndex(0).FindPropertyRelative("high") == null, "没有 High 配置字段");
                }
                Check(typeof(FXLod).GetMethod("OnEnable", Private) == null, "没有自动应用 OnEnable");
                Check(!Config(lod, inactive).IsEnabled(FXLodLevel.Middle), "新节点沿用原始 activeSelf");
                Config(lod, smoke).SetEnabled(FXLodLevel.Middle, false);
                Config(lod, smoke).SetEnabled(FXLodLevel.Low, false);
                Config(lod, core).SetEnabled(FXLodLevel.Low, false);
                lod.enabled = false; lod.enabled = true; root.SetActive(false); root.SetActive(true);
                Check(smoke.activeSelf && core.activeSelf, "启用组件和根节点保持原完整版本状态");
                Check(!lod.IsIncluded(sparks, FXLodLevel.Middle), "排除父节点会排除已勾选子节点");
                ExpectThrow(() => lod.ApplyLevel((FXLodLevel)0), "拒绝已删除的旧 High 枚举值");
                lod.ApplyLevel(FXLodLevel.Low);
                Check(!smoke.activeSelf && !core.activeSelf && root.activeSelf, "Low 应用只改变子物体");
                lod.ApplyLevel(FXLodLevel.Middle);
                Check(!smoke.activeSelf && core.activeSelf, "Middle 可恢复对应节点");
                smoke.SetActive(true); // Source remains the full-quality prefab, not a baked Middle/Low.
                var sameName = Child(root, "Core"); lod.SynchronizeObjects();
                Check(!Config(lod, core).IsEnabled(FXLodLevel.Low) && Config(lod, sameName).IsEnabled(FXLodLevel.Low), "同名节点独立配置");
                Object.DestroyImmediate(sameName); lod.SynchronizeObjects();
                Check(lod.Objects.Count == 4 && !Config(lod, smoke).IsEnabled(FXLodLevel.Middle), "同步保留既有 Middle / Low 配置");
                var migrated = JsonUtility.FromJson<FXLodObjectConfig>("{\"high\":false,\"middle\":false,\"low\":true}");
                Check(!migrated.IsEnabled(FXLodLevel.Middle) && migrated.IsEnabled(FXLodLevel.Low), "旧数据删除 high 字段后保留 middle / low 值");

                string sourcePath = sourceRoot + "/Skills/Fire/Fire.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, sourcePath);
                string sourceBefore = File.ReadAllText(sourcePath);
                string sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
                Check(FXLodExporter.GetSourcePrefabPath(AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath)) == sourcePath, "正确解析当前 Prefab 资源路径");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath)); roots.Add(instance);
                Check(FXLodExporter.GetSourcePrefabPath(instance) == sourcePath, "正确解析场景 Prefab 实例源路径");
                Check(FXLodExporter.TryGetSourceLayout(sourcePath, out string folder, out string relative, out _) && folder == sourceRoot && relative == "Skills/Fire/Fire.prefab", "跨多层父目录定位 High 和相对路径");
                string middlePath = FXLodExporter.ExportPrefab(sourcePath, FXLodLevel.Middle, FXLodExportMode.RemoveExcluded, false);
                string lowPath = FXLodExporter.ExportPrefab(sourcePath, FXLodLevel.Low, FXLodExportMode.RemoveExcluded, false);
                Check(middlePath == testRoot + "/Effects/Middle/Skills/Fire/Fire.prefab" && lowPath == testRoot + "/Effects/Low/Skills/Fire/Fire.prefab", "Middle / Low 创建于 High 同级并保留深层相对路径");
                var middle = AssetDatabase.LoadAssetAtPath<GameObject>(middlePath);
                var low = AssetDatabase.LoadAssetAtPath<GameObject>(lowPath);
                Check(middle != null && low != null, "两个等级文件成功生成");
                Check(middle.transform.Find("Smoke") == null && middle.transform.Find("Core") != null, "Middle 删除排除子树并保留选中节点");
                Check(low.transform.childCount == 0, "Low 可仅保留根节点");
                Check(middle.GetComponent<FXLod>() == null && low.GetComponent<FXLod>() == null, "产物剥离配置组件");
                Check(File.ReadAllText(sourcePath) == sourceBefore && AssetDatabase.AssetPathToGUID(sourcePath) == sourceGuid, "源完整版本文件内容及 GUID 均不修改");
                Check(AssetDatabase.FindAssets("t:Prefab", new[] { sourceRoot }).Length == 1, "High 内仅保留原源 Prefab，不额外导出完整版本");
                Check(FXLodExporter.FindConfiguredPrefabs(testRoot).SequenceEqual(new[] { sourcePath }), "重复扫描不包含生成产物");
                string guid = AssetDatabase.AssetPathToGUID(middlePath);
                FXLodExporter.ExportPrefab(sourcePath, FXLodLevel.Middle, FXLodExportMode.DisableExcluded, true);
                middle = AssetDatabase.LoadAssetAtPath<GameObject>(middlePath);
                Check(middle.transform.Find("Smoke/Sparks") != null && !middle.transform.Find("Smoke").gameObject.activeSelf, "仅禁用模式保留引用节点");
                Check(AssetDatabase.AssetPathToGUID(middlePath) == guid, "覆盖保留目标 GUID");
                ExpectThrow(() => FXLodExporter.ExportPrefab(sourcePath, FXLodLevel.Middle, FXLodExportMode.RemoveExcluded, false), "默认拒绝覆盖已有文件");
                TestPaths(testRoot, sourceRoot, sourcePath, root);
                TestNestedAndVariant(testRoot, sourceRoot, roots);
                TestReferences(roots);
                TestAutoApply(sourceRoot, roots);
                TestAlignment(testRoot, roots);
                TestInspector(lod, smoke, sparks, core);
                TestPreview(root, lod);
                Check(File.ReadAllText(sourcePath) == sourceBefore, "全部测试结束后源 Prefab 仍保持不变");
                Debug.Log($"[FXLod Tests] PASS: {assertions} assertions.");
            }
            finally
            {
                foreach (var root in roots) if (root != null) Object.DestroyImmediate(root);
                if (testRoot.StartsWith("Assets/__FXLodSelfTest_", StringComparison.Ordinal) && AssetDatabase.IsValidFolder(testRoot))
                    AssetDatabase.DeleteAsset(testRoot);
            }
        }

        private static void TestAutoApply(string sourceRoot, List<GameObject> roots)
        {
            Check(FXLodExportSettings.DefaultSourceFolder == "Assets/Res/Effect/male_01", "默认扫描目录为指定角色特效目录");
            var original = new GameObject("AutoApply"); roots.Add(original);
            Child(original, "Keep"); Child(original, "Remove");
            var originalLight = original.AddComponent<Light>(); originalLight.intensity = 1;
            original.AddComponent<FXLod>().SynchronizeObjects();
            string path = sourceRoot + "/AutoApply.prefab";
            PrefabUtility.SaveAsPrefabAsset(original, path);
            string guid = AssetDatabase.AssetPathToGUID(path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path)); roots.Add(instance);
            var light = instance.GetComponent<Light>();
            light.intensity = 7; PrefabUtility.RecordPrefabInstancePropertyModifications(light);
            var keep = instance.transform.Find("Keep"); keep.localScale = new Vector3(2, 3, 4);
            PrefabUtility.RecordPrefabInstancePropertyModifications(keep);
            Child(instance, "Added").AddComponent<AudioSource>();
            Object.DestroyImmediate(instance.transform.Find("Remove").gameObject);
            var lod = instance.GetComponent<FXLod>(); lod.SynchronizeObjects();
            Config(lod, keep.gameObject).SetEnabled(FXLodLevel.Low, false);
            EditorUtility.SetDirty(lod); PrefabUtility.RecordPrefabInstancePropertyModifications(lod);
            Check(PrefabUtility.HasPrefabInstanceAnyOverrides(instance, false), "测试实例含未应用的属性和结构修改");
            Check(FXLodExporter.ApplyCurrentPrefabChanges(instance) == path, "当前实例自动应用到正确源路径");
            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Check(Mathf.Approximately(saved.GetComponent<Light>().intensity, 7), "自动应用不限于 FXLod，包含其他组件属性");
            Check(saved.transform.Find("Added").GetComponent<AudioSource>() != null && saved.transform.Find("Remove") == null, "新增组件及新增删除子节点一并应用");
            Check(saved.transform.Find("Keep").localScale == new Vector3(2, 3, 4), "子节点 Transform 修改被保存");
            Check(!Config(saved.GetComponent<FXLod>(), saved.transform.Find("Keep").gameObject).IsEnabled(FXLodLevel.Low), "LOD 勾选修改被保存");
            Check(!PrefabUtility.HasPrefabInstanceAnyOverrides(instance, false), "自动应用后无残留可应用 Overrides");
            Check(AssetDatabase.AssetPathToGUID(path) == guid, "自动应用保留源 Prefab GUID");
            string bakedPath = FXLodExporter.ExportPrefab(path, FXLodLevel.Low, FXLodExportMode.RemoveExcluded, false);
            var baked = AssetDatabase.LoadAssetAtPath<GameObject>(bakedPath);
            Check(baked.transform.Find("Keep") == null && baked.transform.Find("Added") != null && baked.GetComponent<Light>().intensity == 7, "导出读取最新应用的完整修改而不是旧源内容");
            Check(FXLodExporter.ApplyCurrentPrefabChanges(instance) == path, "无修改实例可重复准备导出");
            Check(FXLodExporter.ApplyCurrentPrefabChanges(saved) == path, "Project 中的 Prefab 资产可直接保存准备导出");
            ExpectThrow(() => FXLodExporter.ApplyCurrentPrefabChanges(original), "普通未连接 Prefab 场景对象不自动创建或覆盖资产");

            string variantPath = sourceRoot + "/AutoApplyVariant.prefab";
            PrefabUtility.SaveAsPrefabAsset(instance, variantPath);
            var variantInstance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(variantPath)); roots.Add(variantInstance);
            var variantLight = variantInstance.GetComponent<Light>(); variantLight.intensity = 11;
            PrefabUtility.RecordPrefabInstancePropertyModifications(variantLight);
            string baseBefore = File.ReadAllText(path);
            FXLodExporter.ApplyCurrentPrefabChanges(variantInstance);
            Check(AssetDatabase.LoadAssetAtPath<GameObject>(variantPath).GetComponent<Light>().intensity == 11 && File.ReadAllText(path) == baseBefore,
                "Variant 实例自动应用到 Variant，不误改基础 Prefab");
        }

        // Batch-only: opening/closing stages must never disrupt a user's active stage in menu self-tests.
        private static void TestPrefabStageSave()
        {
            string testRoot = "Assets/__FXLodSelfTest_" + Guid.NewGuid().ToString("N");
            string path = testRoot + "/High/StageSave.prefab";
            GameObject root = null;
            try
            {
                FXLodExporter.EnsureFolder(testRoot + "/High");
                root = new GameObject("StageSave"); root.AddComponent<FXLod>();
                PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root); root = null;
                var stage = PrefabStageUtility.OpenPrefab(path);
                var stageRoot = stage.prefabContentsRoot;
                Child(stageRoot, "UnsavedChild").AddComponent<Light>().intensity = 9;
                stageRoot.GetComponent<FXLod>().SynchronizeObjects();
                EditorSceneManager.MarkSceneDirty(stage.scene);
                Check(stage.scene.isDirty, "Prefab Mode 测试存在未保存编辑");
                Check(FXLodExporter.ApplyCurrentPrefabChanges(stageRoot) == path && !stage.scene.isDirty, "自动保存整个 Prefab Mode 并清除脏状态");
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Check(asset.transform.Find("UnsavedChild").GetComponent<Light>().intensity == 9, "Prefab Mode 新增节点和组件参数写入源资产");
                var baked = AssetDatabase.LoadAssetAtPath<GameObject>(FXLodExporter.ExportPrefab(path, FXLodLevel.Middle, FXLodExportMode.RemoveExcluded, false));
                Check(baked.transform.Find("UnsavedChild") != null, "Prefab Mode 保存后导出包含最新节点");
            }
            finally
            {
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null && stage.assetPath == path) { stage.ClearDirtiness(); StageUtility.GoToMainStage(); }
                if (root != null) Object.DestroyImmediate(root);
                if (AssetDatabase.IsValidFolder(testRoot)) AssetDatabase.DeleteAsset(testRoot);
            }
        }
        private static void TestAlignment(string testRoot, List<GameObject> roots)
        {
            string scope = testRoot + "/Alignment";
            var plain = new GameObject("NoLodComponent"); roots.Add(plain);
            Action<string> save = path =>
            {
                FXLodExporter.EnsureFolder(Path.GetDirectoryName(path));
                PrefabUtility.SaveAsPrefabAsset(plain, path);
            };
            string validHigh = scope + "/High/Skills/Keep.prefab";
            string keepMiddle = scope + "/Middle/Skills/Keep.prefab";
            string keepLow = scope + "/Low/Skills/Keep.prefab";
            string orphanMiddle = scope + "/Middle/Skills/Gone.prefab";
            string orphanLow = scope + "/Low/Skills/Gone.prefab";
            save(validHigh); save(keepMiddle); save(keepLow); save(orphanMiddle); save(orphanLow);
            save(scope + "/High/Elsewhere/Gone.prefab"); // Same name at another relative path must not keep it.
            string nestedSource = scope + "/Low/Other/High/Source.prefab"; save(nestedSource);
            string nonLevel = scope + "/MyLow/Ignore.prefab"; save(nonLevel);
            string noHighTree = scope + "/Second/Low/Deep/NoSource.prefab"; save(noHighTree);
            string materialPath = scope + "/Low/Skills/Keep.txt";
            File.WriteAllText(materialPath, "do not delete"); AssetDatabase.ImportAsset(materialPath);
            var results = FXLodAlignment.Scan(scope);
            Check(results.Count == 3, "对齐检测找到两个无源产物及整个 High 缺失树的孤立 Prefab");
            Check(results.Any(item => item.AssetPath == orphanMiddle) && results.Any(item => item.AssetPath == orphanLow), "没有 FXLod 组件的导出产物也参与检测");
            Check(results.All(item => item.AssetPath != keepMiddle && item.AssetPath != keepLow), "High 源文件存在时保留两个等级，不要求源挂有 FXLod");
            Check(results.All(item => item.AssetPath != nestedSource && item.AssetPath != nonLevel), "不清理 High 源及非精确等级目录");
            Check(results.Single(item => item.AssetPath == noHighTree).HighFolderMissing, "High 文件夹整体缺失时显式标记风险");
            Check(results.Single(item => item.AssetPath == orphanLow).ExpectedHighPath == scope + "/High/Skills/Gone.prefab", "以完整相对路径对齐，同名但不同目录不匹配");
            Check(FXLodAlignment.Scan(scope + "/High/Skills").Count == 2, "选择 High 子目录仅检测对应 Middle / Low 子树");
            Check(FXLodAlignment.Scan(scope + "/Low").Count == 1, "选择 Low 时不扩大到未选择的 Middle 范围");
            Check(!FXLodAlignment.TryGetHighCounterpart("Assets/Low/../High/A.prefab", out _, out _), "拒绝对齐路径穿越");
            Check(FXLodAlignment.TryGetHighCounterpart("Assets/Effects/low/A.prefab", out string lower, out _) && lower == "Assets/Effects/High/A.prefab", "等级目录匹配不区分大小写");
            Check(File.Exists(orphanMiddle) && File.Exists(orphanLow), "检测只读，不删除资源");
            var middle = results.Single(item => item.AssetPath == orphanMiddle);
            var low = results.Single(item => item.AssetPath == orphanLow);
            Check(!FXLodAlignment.CanDelete(scope + "/Second", middle, out _), "拒绝删除检测范围外文件");
            save(scope + "/High/Skills/Gone.prefab");
            Check(!FXLodAlignment.MoveToTrash(scope, middle, out _) && File.Exists(orphanMiddle), "检测后恢复 High 源时删除前重新检查并跳过");
            AssetDatabase.DeleteAsset(scope + "/High/Skills/Gone.prefab");
            Check(FXLodAlignment.Scan(scope).Any(item => item.AssetPath == orphanMiddle), "High 刚删除后的缓存 GUID 不阻止检测到孤立产物");
            string moveError = AssetDatabase.MoveAsset(orphanLow, scope + "/ReplacedOld.prefab");
            if (!string.IsNullOrEmpty(moveError)) throw new Exception(moveError);
            save(orphanLow);
            Check(AssetDatabase.AssetPathToGUID(orphanLow) != low.Guid, "同路径替换测试确实产生不同 GUID");
            Check(!FXLodAlignment.MoveToTrash(scope, low, out string replacedReason) && replacedReason.Contains("替换") && File.Exists(orphanLow), "同路径文件已替换且 GUID 改变时不删除旧检测记录");
            File.SetAttributes(orphanMiddle, File.GetAttributes(orphanMiddle) | FileAttributes.ReadOnly);
            try { Check(!FXLodAlignment.CanDelete(scope, middle, out string readOnlyReason) && readOnlyReason.Contains("只读"), "只读目标跳过清理"); }
            finally { File.SetAttributes(orphanMiddle, File.GetAttributes(orphanMiddle) & ~FileAttributes.ReadOnly); }
            string highBefore = File.ReadAllText(validHigh);
            Check(FXLodAlignment.MoveToTrash(scope, middle, out string failure), "确认后的孤立 Prefab 可移入回收站：" + failure);
            Check(!File.Exists(orphanMiddle) && !File.Exists(orphanMiddle + ".meta"), "Prefab 及 meta 一起移出项目");
            Check(File.Exists(orphanLow) && File.Exists(keepMiddle) && File.Exists(keepLow) && File.Exists(materialPath), "只处理指定目标，其他 Prefab 和非 Prefab 文件保留");
            Check(Directory.Exists(scope + "/Middle/Skills") && File.ReadAllText(validHigh) == highBefore, "不删除文件夹也不修改 High 源");
            Check(!FXLodAlignment.MoveToTrash(scope, middle, out _), "已清理记录重复调用安全跳过");
            Check(FXLodAlignment.Scan(scope).Count == 2, "清理后重新检测更新剩余结果");
        }
        private static void TestPaths(string testRoot, string sourceRoot, string sourcePath, GameObject root)
        {
            Check(FXLodExporter.GetDestinationPath("Assets/High/A.prefab", FXLodLevel.Low) == "Assets/Low/A.prefab", "High 位于 Assets 直属目录时有效");
            Check(FXLodExporter.GetDestinationPath(sourceRoot + "/Direct.prefab", FXLodLevel.Middle) == testRoot + "/Effects/Middle/Direct.prefab", "Prefab 直接位于 High 时有效");
            Check(FXLodExporter.GetDestinationPath("Assets/Effects/high/A.prefab", FXLodLevel.Low) == "Assets/Effects/Low/A.prefab", "High 名称不区分大小写");
            Check(FXLodExporter.GetDestinationPath(sourceRoot + "/Other/High/Deep/A.prefab", FXLodLevel.Middle) == sourceRoot + "/Other/Middle/Deep/A.prefab", "存在多个 High 祖先时选择最近的一个");
            ExpectThrow(() => FXLodExporter.GetDestinationPath("Assets/Effects/Hight/A.prefab", FXLodLevel.Middle), "拼写错误的 Hight 不被识别为 High 文件夹");
            ExpectThrow(() => FXLodExporter.GetDestinationPath("Assets/Effects/MyHigh/A.prefab", FXLodLevel.Middle), "匹配整个文件夹名而非子串");
            ExpectThrow(() => FXLodExporter.GetDestinationPath("Assets/Effects/High.prefab", FXLodLevel.Low), "文件名不充当 High 目录");
            ExpectThrow(() => FXLodExporter.GetDestinationPath(sourceRoot + "/../A.prefab", FXLodLevel.Middle), "拒绝路径穿越");
            ExpectThrow(() => FXLodExporter.GetDestinationPath("Packages/High/A.prefab", FXLodLevel.Middle), "拒绝 Assets 外资源");
            ExpectThrow(() => FXLodExporter.GetDestinationPath(sourcePath, (FXLodLevel)0), "拒绝导出旧 High 枚举值");
            ExpectThrow(() => FXLodExporter.NormalizeAssetFolder("../OutsideAssets"), "拒绝扫描 Assets 外目录");
            Check(!FXLodExporter.TryGetSourceLayout("", out _, out _, out _), "未保存的对象不可导出");
            string invalid = testRoot + "/NotInHigh.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, invalid);
            var rejected = new List<string>();
            Check(FXLodExporter.FindConfiguredPrefabs(testRoot, rejected).SequenceEqual(new[] { sourcePath }) && rejected.Count == 1, "扫描跳过并报告不在 High 内的配置源文件");
            ExpectThrow(() => FXLodExporter.ExportPrefab(invalid, FXLodLevel.Middle, FXLodExportMode.RemoveExcluded, false), "非法源目录导出前即被拒绝");
            string second = testRoot + "/Second/High/Skills/Fire/Fire.prefab";
            FXLodExporter.EnsureFolder(Path.GetDirectoryName(second));
            PrefabUtility.SaveAsPrefabAsset(root, second);
            Check(FXLodExporter.ExportPrefab(second, FXLodLevel.Middle, FXLodExportMode.RemoveExcluded, false) == testRoot + "/Second/Middle/Skills/Fire/Fire.prefab", "不同 High 树各自导出到其同级目录");
            Check(FXLodExporter.FindConfiguredPrefabs(testRoot).Count == 2, "批量扫描可覆盖多个 High 根");
            string other = sourceRoot + "/Other/Fire.prefab";
            FXLodExporter.EnsureFolder(Path.GetDirectoryName(other)); PrefabUtility.SaveAsPrefabAsset(root, other);
            string otherDestination = FXLodExporter.ExportPrefab(other, FXLodLevel.Middle, FXLodExportMode.RemoveExcluded, false);
            Check(otherDestination.EndsWith("/Middle/Other/Fire.prefab") && File.Exists(otherDestination), "同名不同目录资源无冲突");
            string protectedPath = FXLodExporter.GetDestinationPath(other, FXLodLevel.Low);
            FXLodExporter.EnsureFolder(Path.GetDirectoryName(protectedPath));
            PrefabUtility.SaveAsPrefabAsset(root, protectedPath);
            string before = File.ReadAllText(protectedPath);
            ExpectThrow(() => FXLodExporter.ExportPrefab(other, FXLodLevel.Low, FXLodExportMode.RemoveExcluded, true), "拒绝覆盖带 FXLod 的其他配置源资源");
            Check(File.ReadAllText(protectedPath) == before, "受保护目标保持原样");
        }

        private static void TestNestedAndVariant(string testRoot, string sourceRoot, List<GameObject> roots)
        {
            var nested = new GameObject("Nested"); roots.Add(nested); Child(nested, "NestedChild");
            string nestedPath = testRoot + "/Nested.prefab"; PrefabUtility.SaveAsPrefabAsset(nested, nestedPath);
            var outer = new GameObject("Outer"); roots.Add(outer);
            var nestedInstance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(nestedPath));
            nestedInstance.transform.SetParent(outer.transform, false);
            var lod = outer.AddComponent<FXLod>(); lod.SynchronizeObjects();
            Config(lod, nestedInstance.transform.GetChild(0).gameObject).SetEnabled(FXLodLevel.Low, false);
            string path = sourceRoot + "/Outer.prefab"; PrefabUtility.SaveAsPrefabAsset(outer, path);
            string nestedBefore = File.ReadAllText(nestedPath);
            var baked = AssetDatabase.LoadAssetAtPath<GameObject>(FXLodExporter.ExportPrefab(path, FXLodLevel.Low, FXLodExportMode.RemoveExcluded, false));
            Check(baked.transform.Find("Nested") != null && baked.transform.Find("Nested/NestedChild") == null, "嵌套 Prefab 安全裁剪");
            Check(File.ReadAllText(nestedPath) == nestedBefore, "嵌套源资源不修改");
            var variant = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path)); roots.Add(variant);
            string variantPath = sourceRoot + "/Variant.prefab"; PrefabUtility.SaveAsPrefabAsset(variant, variantPath);
            Check(PrefabUtility.GetPrefabAssetType(AssetDatabase.LoadAssetAtPath<GameObject>(variantPath)) == PrefabAssetType.Variant, "创建测试 Variant");
            string before = File.ReadAllText(variantPath);
            var output = AssetDatabase.LoadAssetAtPath<GameObject>(FXLodExporter.ExportPrefab(variantPath, FXLodLevel.Low, FXLodExportMode.RemoveExcluded, false));
            Check(PrefabUtility.GetPrefabAssetType(output) == PrefabAssetType.Regular && output.transform.Find("Nested/NestedChild") == null, "Variant 导出独立普通 Prefab");
            Check(File.ReadAllText(variantPath) == before, "Variant 源文件不变");
            nestedInstance.AddComponent<FXLod>();
            ExpectThrow(() => FXLodExporter.BakeHierarchy(outer, FXLodLevel.Middle, FXLodExportMode.RemoveExcluded), "拒绝多个嵌套控制器");
        }

        private static void TestReferences(List<GameObject> roots)
        {
            var root = new GameObject("Linked"); roots.Add(root);
            var emitter = Child(root, "Emitter").AddComponent<ParticleSystem>();
            var space = Child(root, "CustomSpace");
            var main = emitter.main; main.simulationSpace = ParticleSystemSimulationSpace.Custom; main.customSimulationSpace = space.transform;
            var lod = root.AddComponent<FXLod>(); lod.SynchronizeObjects(); Config(lod, space).SetEnabled(FXLodLevel.Low, false);
            var warnings = new List<string>(); FXLodExporter.BakeHierarchy(root, FXLodLevel.Low, FXLodExportMode.RemoveExcluded, warnings);
            Check(warnings.Count > 0, "报告删除引起的序列化引用风险");
            var child = Child(root, "SubEmitter").AddComponent<ParticleSystem>();
            var sub = emitter.subEmitters; sub.AddSubEmitter(child, ParticleSystemSubEmitterType.Birth, ParticleSystemSubEmitterProperties.InheritNothing);
            sub.enabled = true; Check(FXLod.GetSubEmitters(new[] { emitter, child }).Contains(child), "避免重复播放启用的子发射器");
            sub.enabled = false; Check(!FXLod.GetSubEmitters(new[] { emitter, child }).Contains(child), "禁用子发射模块不影响普通粒子播放");
        }

        private static void TestInspector(FXLod lod, GameObject smoke, GameObject sparks, GameObject core)
        {
            var inspector = UnityEditor.Editor.CreateEditor(lod, typeof(FXLodInspector));
            try
            {
                inspector.serializedObject.Update();
                var entries = inspector.serializedObject.FindProperty("objects");
                var build = typeof(FXLodInspector).GetMethod("BuildIndex", Private); build.Invoke(inspector, new object[] { entries });
                var counts = (int[])typeof(FXLodInspector).GetField("counts", Private).GetValue(inspector);
                Check(counts.Length == 2 && counts[0] == 1 && counts[1] == 0, "Inspector 只统计 Middle / Low");
                var mask = typeof(FXLodInspector).GetMethod("IncludedMask", Private);
                Check((int)mask.Invoke(inspector, new object[] { sparks, entries }) == 0, "生效状态继承父节点排除规则");
                var collapsed = (HashSet<int>)typeof(FXLodInspector).GetField("collapsed", Private).GetValue(inspector); collapsed.Add(smoke.GetInstanceID());
                var hidden = typeof(FXLodInspector).GetMethod("HiddenByFold", Private);
                Check((bool)hidden.Invoke(inspector, new object[] { sparks }) && !(bool)hidden.Invoke(inspector, new object[] { core }), "折叠只隐藏对应后代");
                entries.GetArrayElementAtIndex(0).FindPropertyRelative("middle").boolValue = true;
                build.Invoke(inspector, new object[] { entries });
                Check((int)mask.Invoke(inspector, new object[] { sparks, entries }) == 1, "待提交 Middle 勾选即时更新子节点状态");
                Check(!Config(lod, smoke).IsEnabled(FXLodLevel.Middle), "统计不意外提交配置"); inspector.serializedObject.Update();
                entries = inspector.serializedObject.FindProperty("objects");
                build.Invoke(inspector, new object[] { entries });
                var visibleMethod = typeof(FXLodInspector).GetMethod("GetVisibleRows", Private);
                Func<List<int>> visible = () => (List<int>)visibleMethod.Invoke(inspector, new object[] { entries });
                Check(visible().Count == 3, "折叠后高度按可见子物体行数收缩");
                collapsed.Clear();
                Check(visible().Count == 4, "展开后恢复全部子物体行数");
                typeof(FXLodInspector).GetField("search", Private).SetValue(inspector, "Core");
                Check(visible().Count == 1, "搜索后列表高度仅包含匹配行");
                typeof(FXLodInspector).GetField("search", Private).SetValue(inspector, "NoMatch");
                Check(visible().Count == 0, "无匹配项使用空状态高度");
                typeof(FXLodInspector).GetField("search", Private).SetValue(inspector, "");
                typeof(FXLodInspector).GetField("differencesOnly", Private).SetValue(inspector, true);
                Check(visible().Count == 1, "差异筛选后高度按实际显示行数计算");
                TestTableLayout();
            }
            finally { Object.DestroyImmediate(inspector); }
        }

        private static void TestTableLayout()
        {
            var type = typeof(FXLodInspector);
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            var height = type.GetMethod("TableHeight", flags);
            var width = type.GetMethod("ColumnWidth", flags);
            var cellMethod = type.GetMethod("Cell", flags);
            var toggleMethod = type.GetMethod("ToggleRect", flags);
            Func<int, float> getHeight = count => (float)height.Invoke(null, new object[] { count });
            Check(getHeight(1) == 69 && getHeight(10) == 294, "列表高度等于表头加行数乘行高");
            Check(getHeight(100) == 2544 && getHeight(100) > 440, "大量子物体完整展开，不存在旧 440px 高度上限");
            Check(getHeight(0) == 110, "空列表保留紧凑提示区域");
            foreach (float tableWidth in new[] { 240f, 320f, 520f, 800f })
            {
                float column = (float)width.Invoke(null, new object[] { tableWidth });
                bool aligned = true;
                for (int level = 0; level < 2; ++level)
                {
                    var header = (Rect)cellMethod.Invoke(null, new object[] { new Rect(17, 20, tableWidth, 44), column, level });
                    var headerToggle = (Rect)toggleMethod.Invoke(null, new object[] { new Rect(header.x, header.y + 21, header.width, 23) });
                    for (int row = 0; row < 100; ++row)
                    {
                        var cell = (Rect)cellMethod.Invoke(null, new object[] { new Rect(17, 64 + row * 25, tableWidth, 25), column, level });
                        var toggle = (Rect)toggleMethod.Invoke(null, new object[] { cell });
                        aligned &= Mathf.Approximately(header.center.x, toggle.center.x) && Mathf.Approximately(headerToggle.x, toggle.x);
                    }
                }
                Check(aligned, $"{tableWidth}px 宽度下表头、整列 Toggle、100 行 Toggle 严格对齐");
            }
        }
        private static void TestPreview(GameObject root, FXLod lod)
        {
            var ps = root.transform.Find("Core").gameObject.AddComponent<ParticleSystem>();
            var main = ps.main; main.startLifetime = 3; main.startSpeed = 1;
            var emission = ps.emission; emission.rateOverTime = 30;
            var window = ScriptableObject.CreateInstance<FXLodPreviewWindow>();
            try
            {
                var type = typeof(FXLodPreviewWindow);
                type.GetField("source", Private).SetValue(window, lod);
                type.GetField("level", Private).SetValue(window, FXLodLevel.Low);
                type.GetMethod("BuildPreview", Private).Invoke(window, null);
                var clone = (FXLod)type.GetField("clone", Private).GetValue(window);
                Check(clone != null && clone.gameObject.scene != root.scene, "预览使用隔离场景");
                var advance = type.GetMethod("Advance", Private); advance.Invoke(window, new object[] { 0.5f });
                Check(clone.GetComponentInChildren<ParticleSystem>(true).particleCount == 0, "Low 排除节点不发射粒子");
                Check(root.transform.Find("Smoke").gameObject.activeSelf && root.transform.Find("Core").gameObject.activeSelf, "预览不改变原完整版本激活状态");
                type.GetField("level", Private).SetValue(window, FXLodLevel.Middle); type.GetMethod("BuildPreview", Private).Invoke(window, null);
                Check(clone == null, "切换等级释放旧副本");
                clone = (FXLod)type.GetField("clone", Private).GetValue(window); advance.Invoke(window, new object[] { 0.5f });
                Check(clone.GetComponentInChildren<ParticleSystem>().particleCount > 0, "Middle 实际发射粒子");
                type.GetMethod("ReleasePreview", Private).Invoke(window, null); Check(clone == null, "停止释放预览副本");
            }
            finally { Object.DestroyImmediate(window); }
        }

        private static GameObject Child(GameObject parent, string name)
        { var child = new GameObject(name); child.transform.SetParent(parent.transform, false); return child; }
        private static FXLodObjectConfig Config(FXLod lod, GameObject target) => lod.Objects.First(item => item.Target == target);
        private static void Check(bool condition, string description)
        {
            if (!condition) throw new Exception("[FXLod Tests] FAIL: " + description);
            ++assertions; Debug.Log("[FXLod Tests] PASS: " + description);
        }
        private static void ExpectThrow(Action action, string description)
        {
            bool threw = false; try { action(); } catch { threw = true; }
            Check(threw, description);
        }
    }
}







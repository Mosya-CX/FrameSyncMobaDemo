using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Collections.Generic;

public class CodeMergerWindow : EditorWindow
{
    private string inputDirectory = "Assets";      // C# 脚本目录
    private string luaDirectory = "";               // Lua 脚本目录（可选）
    private string outputFilePath = "Assets/merged_code.md";

    [MenuItem("Tools/Code Merger")]
    public static void ShowWindow()
    {
        GetWindow<CodeMergerWindow>("Code Merger");
    }

    private void OnGUI()
    {
        GUILayout.Label("Code Merger Settings", EditorStyles.boldLabel);

        // C# 目录
        EditorGUILayout.BeginHorizontal();
        inputDirectory = EditorGUILayout.TextField("C# Directory", inputDirectory);
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string selected = EditorUtility.OpenFolderPanel("Select C# Directory", inputDirectory, "");
            if (!string.IsNullOrEmpty(selected))
                inputDirectory = GetRelativePath(selected);
        }
        EditorGUILayout.EndHorizontal();

        // Lua 目录
        EditorGUILayout.BeginHorizontal();
        luaDirectory = EditorGUILayout.TextField("Lua Directory", luaDirectory);
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string selected = EditorUtility.OpenFolderPanel("Select Lua Directory", luaDirectory, "");
            if (!string.IsNullOrEmpty(selected))
                luaDirectory = GetRelativePath(selected);
        }
        EditorGUILayout.EndHorizontal();

        // 输出文件
        EditorGUILayout.BeginHorizontal();
        outputFilePath = EditorGUILayout.TextField("Output File", outputFilePath);
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string selected = EditorUtility.SaveFilePanel("Save merged file",
                Path.GetDirectoryName(outputFilePath),
                Path.GetFileNameWithoutExtension(outputFilePath),
                "md");
            if (!string.IsNullOrEmpty(selected))
                outputFilePath = GetRelativePath(selected);
        }
        EditorGUILayout.EndHorizontal();

        // 执行按钮
        if (GUILayout.Button("Merge Code Files", GUILayout.Height(30)))
        {
            MergeCodeFiles();
        }
    }

    /// <summary>
    /// 将绝对路径转换为相对于项目的路径（用于显示和存储）
    /// </summary>
    private string GetRelativePath(string absolutePath)
    {
        string dataPath = Application.dataPath;
        string projectPath = Path.GetDirectoryName(dataPath);

        if (absolutePath.StartsWith(projectPath))
        {
            return "Assets" + absolutePath.Substring(projectPath.Length);
        }
        else
        {
            return absolutePath;
        }
    }

    private void MergeCodeFiles()
    {
        // 收集所有文件
        List<string> allFiles = new List<string>();

        if (!string.IsNullOrEmpty(inputDirectory) && Directory.Exists(inputDirectory))
        {
            allFiles.AddRange(Directory.GetFiles(inputDirectory, "*.cs", SearchOption.AllDirectories));
        }

        if (!string.IsNullOrEmpty(luaDirectory) && Directory.Exists(luaDirectory))
        {
            allFiles.AddRange(Directory.GetFiles(luaDirectory, "*.lua", SearchOption.AllDirectories));
        }

        if (allFiles.Count == 0)
        {
            EditorUtility.DisplayDialog("Info", "No .cs or .lua files found in specified directories.", "OK");
            return;
        }

        // 如果输出文件已存在，询问是否覆盖
        if (File.Exists(outputFilePath))
        {
            if (!EditorUtility.DisplayDialog("File Exists", "Output file already exists. Overwrite?", "Yes", "No"))
                return;
        }

        // 确保输出目录存在
        string outputDir = Path.GetDirectoryName(outputFilePath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        // 写入 Markdown 文件
        using (StreamWriter writer = new StreamWriter(outputFilePath, false, Encoding.UTF8))
        {
            // 文件头
            writer.WriteLine("# Merged Code Files");
            writer.WriteLine();
            writer.WriteLine($"Generated on: {System.DateTime.Now}");
            writer.WriteLine();

            for (int i = 0; i < allFiles.Count; i++)
            {
                string file = allFiles[i];
                EditorUtility.DisplayProgressBar("Merging Code Files",
                    $"Processing {Path.GetFileName(file)}",
                    (float)i / allFiles.Count);

                // 确定语言标识
                string ext = Path.GetExtension(file).ToLower();
                string language = ext == ".cs" ? "csharp" : "lua";

                // 三级标题：文件名
                writer.WriteLine($"### {Path.GetFileName(file)}");
                writer.WriteLine();

                // 代码块
                writer.WriteLine($"```{language}");
                writer.Write(File.ReadAllText(file));
                writer.WriteLine(); // 确保代码块结束换行
                writer.WriteLine("```");
                writer.WriteLine();
            }
        }

        EditorUtility.ClearProgressBar();
        EditorUtility.DisplayDialog("Success",
            $"Merged {allFiles.Count} code files into:\n{outputFilePath}",
            "OK");

        AssetDatabase.Refresh();
    }
}

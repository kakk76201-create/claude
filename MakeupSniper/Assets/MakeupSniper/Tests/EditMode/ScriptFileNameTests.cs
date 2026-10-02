using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MakeupSniper.Tests
{
    /// <summary>
    /// Каждый компонент и ассет-тип должен лежать в файле со своим именем, иначе Unity не может
    /// сохранить ссылку на скрипт и сборка игры падает при загрузке сцены («level0 is corrupted»).
    /// </summary>
    public class ScriptFileNameTests
    {
        [Test]
        public void EveryComponentHasItsOwnScriptFile()
        {
            var problems = new List<string>();
            foreach (Type t in typeof(World).Assembly.GetTypes())
            {
                if (t.IsAbstract || t.IsGenericType) continue;
                if (!typeof(MonoBehaviour).IsAssignableFrom(t) && !typeof(ScriptableObject).IsAssignableFrom(t)) continue;
                bool found = false;
                foreach (string guid in AssetDatabase.FindAssets(t.Name + " t:MonoScript"))
                {
                    var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                    if (script != null && script.GetClass() == t) { found = true; break; }
                }
                if (!found) problems.Add(t.FullName);
            }
            Assert.IsEmpty(problems, "Классы без своего файла: " + string.Join(", ", problems));
        }
    }
}

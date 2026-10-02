using System;
using System.IO;
using UnityEngine;

namespace MakeupSniper
{
    /// <summary>
    /// Прогресс бригады: звёзды по местам. Хранится на компьютере хоста (у кого игра создана).
    /// Четыре звезды в месте открывают следующее.
    /// </summary>
    [Serializable]
    public class Progression
    {
        public const int UnlockStars = 4;

        public int[] stars = new int[0];

        /// <summary>Для автотестов: свой файл прогресса, чтобы не трогать настоящий.</summary>
        public static string OverridePath;

        public static string DefaultPath { get { return OverridePath ?? Path.Combine(Application.persistentDataPath, "progress.json"); } }

        public int StarsAt(int location)
        {
            return location >= 0 && location < stars.Length ? stars[location] : 0;
        }

        public bool IsUnlocked(int location)
        {
            if (location <= 0) return true;
            return StarsAt(location - 1) >= UnlockStars;
        }

        /// <summary>Добавить звёзды. Возвращает индекс места, которое только что открылось, или −1.</summary>
        public int AddStars(int location, int count, int locationCount)
        {
            if (location < 0) return -1;
            if (stars.Length < locationCount) Array.Resize(ref stars, locationCount);
            bool wasUnlocked = location + 1 < locationCount && IsUnlocked(location + 1);
            stars[location] += Mathf.Max(0, count);
            bool nowUnlocked = location + 1 < locationCount && IsUnlocked(location + 1);
            return !wasUnlocked && nowUnlocked ? location + 1 : -1;
        }

        public static Progression Load(string path = null)
        {
            try
            {
                string p = path ?? DefaultPath;
                if (File.Exists(p))
                {
                    var loaded = JsonUtility.FromJson<Progression>(File.ReadAllText(p));
                    if (loaded != null && loaded.stars != null) return loaded;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MakeupSniper] Не удалось прочитать прогресс: " + e.Message);
            }
            return new Progression();
        }

        public void Save(string path = null)
        {
            try
            {
                string p = path ?? DefaultPath;
                Directory.CreateDirectory(Path.GetDirectoryName(p));
                File.WriteAllText(p, JsonUtility.ToJson(this, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MakeupSniper] Не удалось сохранить прогресс: " + e.Message);
            }
        }
    }
}

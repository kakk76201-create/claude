using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MakeupSniper.EditorTools
{
    /// <summary>
    /// Сборка игры для Windows: папка Builds/MakeupSniper_Windows в корне репозитория.
    /// Меню Unity: Makeup Sniper → Собрать игру для Windows. Эту папку (или zip) можно отправить другу.
    /// </summary>
    public static class BuildTools
    {
        public static string OutputDir
        {
            get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Builds", "MakeupSniper_Windows")); }
        }

        [MenuItem("Makeup Sniper/Собрать игру для Windows")]
        public static void BuildWindows()
        {
            string dir = OutputDir;
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { SceneBuilder.ScenePath },
                locationPathName = Path.Combine(dir, "MakeupSniper.exe"),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("[MakeupSniper] Сборка не удалась: " + report.summary.result + ", ошибок " + report.summary.totalErrors);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            // тестовый номер Steam (Spacewar), пока у игры нет своей страницы
            File.WriteAllText(Path.Combine(dir, "steam_appid.txt"), "480");
            File.WriteAllText(Path.Combine(dir, "КАК ИГРАТЬ.txt"), HowToPlay(), new UTF8Encoding(true));
            Debug.Log("[MakeupSniper] Игра собрана: " + dir + " (" + (report.summary.totalSize / (1024 * 1024)) + " МБ)");
        }

        static string HowToPlay()
        {
            return
@"MAKEUP SNIPER — прототип

ЗАПУСК
Открой MakeupSniper.exe. Если Windows пишет «Защитник Windows предотвратил запуск» —
нажми «Подробнее» → «Выполнить в любом случае» (игра не подписана, это нормально для прототипа).
Если Windows спросит про доступ к сети — разреши (нужно, чтобы друзья могли подключиться).

КАК ИГРАТЬ С ДРУГОМ
1. Один нажимает «Создать игру». В лобби появятся коды.
2. Код из 6 букв — через Steam: работает через интернет, если у обоих запущен Steam.
   Код из 8 знаков — по одной сети: дома по Wi-Fi или через Radmin VPN (radmin-vpn.com):
   оба вступаете в одну сеть Radmin, хост жмёт «Другая сеть», пока не увидит «Radmin VPN».
3. Друг вводит код в меню и жмёт «Войти».
4. Хост выбирает место и жмёт «НАЧАТЬ». Голос — через Discord.

РОЛИ
Модель сидит в кресле, видит образ на «телефоне» и описывает его голосом,
но без запретных слов. Мышь — голова, ПКМ — ладонь, Пробел — уворот, E — размазать.
Стрелки красят лицо: ЛКМ — выстрел, ПКМ — зум, 1 помада, 2 тушь, 3 румяна, 4 тональник-ластик,
WASD — ходить, T — «Модель сказала запретное слово» (минус 5 секунд).
Чем дальше линия (3 / 8 / 15 м), тем больше очков.

Тренировка одному: Модель — бот, образ откроется только на раскрытии.
";
        }
    }
}

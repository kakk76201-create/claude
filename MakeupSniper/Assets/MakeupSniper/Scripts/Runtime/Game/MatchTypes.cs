using UnityEngine;

namespace MakeupSniper
{
    public enum MatchState : byte { Lobby = 0, Pick = 1, Countdown = 2, Shoot = 3, Reveal = 4, Summary = 5 }

    /// <summary>Действия Модели в кресле.</summary>
    public enum ModelAction : byte { Palm = 1, Dodge = 2, Smear = 3 }

    /// <summary>Команды хоста в лобби.</summary>
    public enum HostCommand : byte { Start = 1, SetLocation = 2, SetRounds = 3, Skip = 4, ToLobby = 5 }

    /// <summary>Строка чеклиста зон на полароиде.</summary>
    public struct ZoneLine
    {
        public byte face;
        public string name;
        public byte color;
        public byte coverage;   // проценты
        public bool ok;
        public byte blame;      // номер игрока, чьего неправильного цвета больше всего
    }

    /// <summary>Итог раунда одного игрока.</summary>
    public struct PlayerLine
    {
        public byte slot;
        public string name;
        public int points;
        public int coinsTotal;
        public string note;
    }

    /// <summary>Всё, что нужно для экрана раскрытия.</summary>
    public struct RevealPacket
    {
        public byte location;
        public byte option;
        public byte modelSlot;
        public string modelName;
        public int match;
        public byte stars;
        public int[] faceMatch;
        public ZoneLine[] zones;
        public PlayerLine[] players;
        public int modelCoins;
        public int bet;
        public bool betWon;
        public bool noticed;
        public bool perfect;
        public string headline;
        public string unlock;
    }

    public struct RoundRecord
    {
        public byte modelSlot;
        public string modelName;
        public string title;
        public int match;
        public byte location;
        public byte option;
    }

    /// <summary>Итоги вечера.</summary>
    public struct SummaryPacket
    {
        public PlayerLine[] players;
        public string bestArtist;
        public string bestFace;
        public string worstFace;
        public RoundRecord[] rounds;
    }

    /// <summary>Цвета фартуков игроков — по ним видно, чей мазок и кто виноват.</summary>
    public static class PlayerColors
    {
        static readonly Color[] colors =
        {
            new Color(0.62f, 0.60f, 0.64f),  // 0 — никто / бот
            new Color(0.36f, 0.62f, 0.95f),  // 1 синий
            new Color(0.40f, 0.80f, 0.45f),  // 2 зелёный
            new Color(0.98f, 0.78f, 0.25f),  // 3 жёлтый
            new Color(0.70f, 0.45f, 0.90f),  // 4 фиолетовый
        };
        static readonly string[] names = { "НИКТО", "СИНИЙ", "ЗЕЛЁНЫЙ", "ЖЁЛТЫЙ", "ФИОЛЕТОВЫЙ" };

        public static Color Of(byte slot)
        {
            if (slot == FaceGrid.ModelOwner) return new Color(0.96f, 0.65f, 0.75f);
            return colors[Mathf.Clamp(slot, 0, colors.Length - 1)];
        }

        public static string NameOf(byte slot)
        {
            if (slot == FaceGrid.ModelOwner) return "МОДЕЛЬ";
            return names[Mathf.Clamp(slot, 0, names.Length - 1)];
        }

        public static string Hex(byte slot)
        {
            return ColorUtility.ToHtmlStringRGB(Of(slot));
        }
    }
}

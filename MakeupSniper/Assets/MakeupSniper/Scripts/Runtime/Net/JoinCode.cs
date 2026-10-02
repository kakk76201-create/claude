using System;
using System.Net;
using System.Text;

namespace MakeupSniper.Net
{
    public enum CodeKind { Invalid, Direct, Steam }

    /// <summary>
    /// Коды входа в игру.
    /// Прямой код (8 символов, «K7QX-2M9B») — это зашифрованный адрес компьютера хоста и порт:
    /// работает в одной сети, через Radmin VPN / ZeroTier / Hamachi или с пробросом порта.
    /// Код Steam (6 букв, «KQX-MBR») — случайное имя комнаты, которое ищется через лобби Steam:
    /// работает через интернет без настроек, если у обоих запущен Steam.
    /// </summary>
    public static class JoinCode
    {
        /// <summary>Алфавит Crockford base32: без I, L, O, U, чтобы не путать буквы с цифрами.</summary>
        public const string Crockford = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        /// <summary>Буквы для кода Steam: без I и O.</summary>
        public const string SteamAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        public const ushort BasePort = 7770;
        public const int PortRange = 32;
        public const int DirectLength = 8;
        public const int SteamLength = 6;

        /// <summary>Убирает пробелы и дефисы, переводит в верхний регистр.</summary>
        public static string Normalize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            var sb = new StringBuilder(raw.Length);
            foreach (char ch in raw)
            {
                char c = char.ToUpperInvariant(ch);
                if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) sb.Append(c);
                // кириллица, похожая на латиницу (если раскладка не та): А В Е К М Н О Р С Т Х
                else
                {
                    int i = "АВЕКМНОРСТХ".IndexOf(c);
                    if (i >= 0) sb.Append("ABEKMHOPCTX"[i]);
                }
            }
            return sb.ToString();
        }

        public static CodeKind Classify(string raw)
        {
            string n = Normalize(raw);
            if (n.Length == DirectLength)
            {
                string ip; ushort port;
                return TryDecodeDirect(n, out ip, out port) ? CodeKind.Direct : CodeKind.Invalid;
            }
            if (n.Length == SteamLength && IsSteamCode(n)) return CodeKind.Steam;
            return CodeKind.Invalid;
        }

        public static bool IsSteamCode(string normalized)
        {
            if (normalized == null || normalized.Length != SteamLength) return false;
            foreach (char c in normalized) if (SteamAlphabet.IndexOf(c) < 0) return false;
            return true;
        }

        static int Checksum(byte[] ip, int portOffset)
        {
            return (ip[0] * 1 + ip[1] * 3 + ip[2] * 5 + ip[3] * 7 + portOffset * 11) % 8;
        }

        /// <summary>Адрес IPv4 и порт (7770..7801) → код из 8 символов.</summary>
        public static string EncodeDirect(string ipv4, ushort port)
        {
            IPAddress addr;
            if (!IPAddress.TryParse(ipv4, out addr) || addr.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                throw new ArgumentException("Нужен адрес IPv4: " + ipv4);
            int offset = port - BasePort;
            if (offset < 0 || offset >= PortRange)
                throw new ArgumentOutOfRangeException("port", "Порт должен быть от " + BasePort + " до " + (BasePort + PortRange - 1));
            byte[] b = addr.GetAddressBytes();
            ulong bits = ((ulong)b[0] << 32) | ((ulong)b[1] << 24) | ((ulong)b[2] << 16) | ((ulong)b[3] << 8)
                         | ((ulong)(uint)offset << 3) | (ulong)(uint)Checksum(b, offset);
            var chars = new char[DirectLength];
            for (int i = DirectLength - 1; i >= 0; i--)
            {
                chars[i] = Crockford[(int)(bits & 31UL)];
                bits >>= 5;
            }
            return new string(chars);
        }

        /// <summary>Код из 8 символов → адрес и порт. false, если код набран с ошибкой.</summary>
        public static bool TryDecodeDirect(string raw, out string ipv4, out ushort port)
        {
            ipv4 = null; port = 0;
            string n = Normalize(raw);
            if (n.Length != DirectLength) return false;
            ulong bits = 0;
            foreach (char ch in n)
            {
                char c = ch;
                if (c == 'O') c = '0';
                else if (c == 'I' || c == 'L') c = '1';
                int v = Crockford.IndexOf(c);
                if (v < 0) return false;
                bits = (bits << 5) | (uint)v;
            }
            var b = new byte[4];
            b[0] = (byte)((bits >> 32) & 0xFF);
            b[1] = (byte)((bits >> 24) & 0xFF);
            b[2] = (byte)((bits >> 16) & 0xFF);
            b[3] = (byte)((bits >> 8) & 0xFF);
            int offset = (int)((bits >> 3) & 31UL);
            int check = (int)(bits & 7UL);
            if (Checksum(b, offset) != check) return false;
            if (b[0] == 0) return false;
            ipv4 = b[0] + "." + b[1] + "." + b[2] + "." + b[3];
            port = (ushort)(BasePort + offset);
            return true;
        }

        public static string NewSteamCode(Random rng)
        {
            var chars = new char[SteamLength];
            for (int i = 0; i < SteamLength; i++) chars[i] = SteamAlphabet[rng.Next(SteamAlphabet.Length)];
            return new string(chars);
        }

        /// <summary>Код для показа на экране: с дефисом посередине.</summary>
        public static string Pretty(string code)
        {
            string n = Normalize(code);
            if (n.Length < 6) return n;
            int half = n.Length / 2;
            return n.Substring(0, half) + "-" + n.Substring(half);
        }
    }
}

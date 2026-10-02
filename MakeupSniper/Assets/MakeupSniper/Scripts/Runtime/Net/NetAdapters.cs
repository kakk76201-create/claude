using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MakeupSniper.Net
{
    /// <summary>Сетевой адрес этого компьютера, по которому к нему можно подключиться.</summary>
    public struct NetAdapter
    {
        public string Address;   // IPv4
        public string Label;     // «Radmin VPN», «Локальная сеть»…
        public int Priority;     // меньше — лучше
    }

    /// <summary>
    /// Находит адреса компьютера и сортирует их: сначала VPN для игр (Radmin, ZeroTier, Hamachi, Tailscale),
    /// потом домашняя сеть. Код для друга строится из первого адреса.
    /// </summary>
    public static class NetAdapters
    {
        public static List<NetAdapter> List()
        {
            var result = new List<NetAdapter>();
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    IPInterfaceProperties props;
                    try { props = ni.GetIPProperties(); } catch { continue; }
                    foreach (UnicastIPAddressInformation ua in props.UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        if (IPAddress.IsLoopback(ua.Address)) continue;
                        byte[] b = ua.Address.GetAddressBytes();
                        if (b[0] == 169 && b[1] == 254) continue; // адрес без DHCP, бесполезен
                        string name = (ni.Name + " " + ni.Description).ToLowerInvariant();
                        result.Add(Classify(ua.Address.ToString(), b, name));
                    }
                }
            }
            catch (Exception)
            {
                // на некоторых системах список адаптеров недоступен — вернём то, что есть
            }
            result.Sort((a, b) => a.Priority != b.Priority ? a.Priority.CompareTo(b.Priority) : string.CompareOrdinal(a.Address, b.Address));
            // убрать дубликаты адресов
            for (int i = result.Count - 1; i > 0; i--)
                if (result[i].Address == result[i - 1].Address) result.RemoveAt(i);
            return result;
        }

        public static NetAdapter Classify(string address, byte[] b, string lowerName)
        {
            if (lowerName.Contains("radmin") || b[0] == 26) return new NetAdapter { Address = address, Label = "Radmin VPN", Priority = 0 };
            if (lowerName.Contains("zerotier")) return new NetAdapter { Address = address, Label = "ZeroTier", Priority = 1 };
            if (lowerName.Contains("hamachi") || b[0] == 25) return new NetAdapter { Address = address, Label = "Hamachi", Priority = 2 };
            if (lowerName.Contains("tailscale") || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)) return new NetAdapter { Address = address, Label = "Tailscale", Priority = 3 };
            bool lan = b[0] == 10 || (b[0] == 192 && b[1] == 168) || (b[0] == 172 && b[1] >= 16 && b[1] <= 31);
            if (lan) return new NetAdapter { Address = address, Label = "Локальная сеть", Priority = 5 };
            return new NetAdapter { Address = address, Label = "Интернет (нужен проброс порта)", Priority = 8 };
        }
    }
}

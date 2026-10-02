using MakeupSniper.Net;
using NUnit.Framework;

namespace MakeupSniper.Tests
{
    /// <summary>Коды входа: туда-обратно, опечатки, разные виды кодов.</summary>
    public class JoinCodeTests
    {
        [TestCase("192.168.1.42", (ushort)7770)]
        [TestCase("26.11.200.5", (ushort)7771)]
        [TestCase("10.0.0.1", (ushort)7801)]
        [TestCase("255.255.255.254", (ushort)7790)]
        public void Direct_RoundTrip(string ip, ushort port)
        {
            string code = JoinCode.EncodeDirect(ip, port);
            Assert.AreEqual(JoinCode.DirectLength, code.Length);
            string ip2; ushort port2;
            Assert.IsTrue(JoinCode.TryDecodeDirect(code, out ip2, out port2), code);
            Assert.AreEqual(ip, ip2);
            Assert.AreEqual(port, port2);
            Assert.AreEqual(CodeKind.Direct, JoinCode.Classify(code));
        }

        [Test]
        public void Direct_AcceptsPrettyLowercaseAndConfusables()
        {
            string code = JoinCode.EncodeDirect("192.168.0.17", 7772);
            string pretty = JoinCode.Pretty(code).ToLowerInvariant();
            StringAssert.Contains("-", pretty);
            string ip; ushort port;
            Assert.IsTrue(JoinCode.TryDecodeDirect(" " + pretty + " ", out ip, out port));
            Assert.AreEqual("192.168.0.17", ip);
            // буква O вместо нуля тоже понимается
            Assert.IsTrue(JoinCode.TryDecodeDirect(code.Replace('0', 'O'), out ip, out port));
        }

        [Test]
        public void Direct_MostTyposAreCaught()
        {
            string code = JoinCode.EncodeDirect("192.168.1.42", 7770);
            int caught = 0, total = 0;
            for (int pos = 0; pos < code.Length; pos++)
            {
                foreach (char c in JoinCode.Crockford)
                {
                    if (c == code[pos]) continue;
                    total++;
                    string typo = code.Substring(0, pos) + c + code.Substring(pos + 1);
                    string ip; ushort port;
                    if (!JoinCode.TryDecodeDirect(typo, out ip, out port) || ip != "192.168.1.42" || port != 7770) caught++;
                }
            }
            // опечатка никогда не даёт тот же адрес, а большинство опечаток не проходит проверку
            Assert.AreEqual(total, caught);
        }

        [Test]
        public void Steam_CodesAreSixLettersAndDistinctFromDirect()
        {
            var rng = new System.Random(5);
            for (int i = 0; i < 200; i++)
            {
                string code = JoinCode.NewSteamCode(rng);
                Assert.AreEqual(6, code.Length);
                Assert.AreEqual(CodeKind.Steam, JoinCode.Classify(code), code);
                Assert.AreEqual(CodeKind.Steam, JoinCode.Classify(JoinCode.Pretty(code).ToLowerInvariant()));
            }
        }

        [Test]
        public void Classify_RejectsGarbage()
        {
            Assert.AreEqual(CodeKind.Invalid, JoinCode.Classify(""));
            Assert.AreEqual(CodeKind.Invalid, JoinCode.Classify("123"));
            Assert.AreEqual(CodeKind.Invalid, JoinCode.Classify("ABC12"));
            Assert.AreEqual(CodeKind.Invalid, JoinCode.Classify("ABCDEFGHIJ"));
        }

        [Test]
        public void Normalize_MapsCyrillicLookalikes()
        {
            // набрали в русской раскладке похожие буквы
            Assert.AreEqual("KMHPCX", JoinCode.Normalize("кмнрсх"));
        }

        [Test]
        public void Adapters_PreferGamingVpn()
        {
            var radmin = NetAdapters.Classify("26.1.2.3", new byte[] { 26, 1, 2, 3 }, "radmin vpn");
            var lan = NetAdapters.Classify("192.168.1.5", new byte[] { 192, 168, 1, 5 }, "ethernet");
            Assert.Less(radmin.Priority, lan.Priority);
            Assert.AreEqual("Radmin VPN", radmin.Label);
        }
    }
}

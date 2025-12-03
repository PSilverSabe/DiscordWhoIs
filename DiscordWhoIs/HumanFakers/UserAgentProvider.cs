using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace DiscordWhoIs.HumanFakers
{
    public static class UserAgentProvider
    {
        private static readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();
        private static readonly Random _rand = new();

        public static BrowserIdentity GetRandomIdentity()
        {
            var profile = WeightedPick(Profiles);
            var (locale, acceptLang, tz) =
                UserAgentProfileMapper.MapFromUserAgent(profile.UA);

            var identity = new BrowserIdentity
            {
                UserAgent = profile.UA,
                Platform = profile.Platform,
                OSVersion = profile.OSVersion,
                Architecture = profile.Arch,
                DeviceType = profile.DeviceType,
                Locale = locale,
                AcceptLanguage = acceptLang,
                Timezone = tz,
                ScreenResolution = RandomResolution(profile.DeviceType),
                ColorDepth = 24,
                HardwareConcurrency = RandomCpuCores(profile.DeviceType),
                DeviceMemoryGb = RandomDeviceMemory(profile.DeviceType),
                MaxTouchPoints = profile.DeviceType == "mobile" ? _rand.Next(1, 6) : 0,
                Gpu = RandomGpu(profile.Platform, profile.DeviceType)
            };

            return identity;
        }

        // --------------------------------------
        // PROFILES (User-Agent + Platform family)
        // --------------------------------------

        private static readonly List<FingerprintProfile> Profiles =
        [
            new("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0 Safari/537.36",
                "Windows", "10", "x64", "desktop", 35),

            new("Mozilla/5.0 (Macintosh; Intel Mac OS X 13_3) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.3 Safari/605.1.15",
                "macOS", "13.3", "x64", "desktop", 20),

            new("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0 Safari/537.36",
                "Linux", "5.15", "x64", "desktop", 5),

            new("Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1",
                "iOS", "17.0", "arm64", "mobile", 20),

            new("Mozilla/5.0 (Linux; Android 14; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0 Mobile Safari/537.36",
                "Android", "14", "arm64", "mobile", 20),

            new("Mozilla/5.0 (Linux; Android 13; SM-S908B) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0 Mobile Safari/537.36",
                "Android", "13", "arm64", "mobile", 15)
        ];

        // ---------------------
        // RANDOMIZATION HELPERS
        // ---------------------

        private static T WeightedPick<T>(List<T> list) where T : IWeighted
        {
            int total = 0;
            foreach (var item in list) total += item.Weight;

            int roll = SecureInt(0, total);
            int running = 0;

            foreach (var item in list)
            {
                running += item.Weight;
                if (roll < running)
                    return item;
            }

            return list[0];
        }

        private static int SecureInt(int min, int max)
        {
            if (min >= max) return min;
            byte[] b = new byte[4];
            _rng.GetBytes(b);
            uint v = BitConverter.ToUInt32(b, 0);
            return (int)(v % (max - min)) + min;
        }

        private static string RandomResolution(string deviceType)
        {
            if (deviceType == "mobile")
            {
                var mobileRes =
                    new List<string> { "1080x2340", "1170x2532", "1125x2436", "1440x3120" };
                return mobileRes[_rand.Next(mobileRes.Count)];
            }

            var desktopRes =
                new List<string> { "1920x1080", "1366x768", "2560x1440", "1680x1050", "1536x864" };
            return desktopRes[_rand.Next(desktopRes.Count)];
        }

        private static int RandomCpuCores(string deviceType)
        {
            if (deviceType == "mobile")
                return _rand.Next(6, 9);  // 6–8 cores typical

            return _rand.Next(4, 17); // desktop 4–16 cores
        }

        private static int RandomDeviceMemory(string deviceType)
        {
            if (deviceType == "mobile")
            {
                int[] mobile = [4, 6, 8, 12];
                return mobile[_rand.Next(mobile.Length)];
            }

            int[] desktop = [8, 16, 32];
            return desktop[_rand.Next(desktop.Length)];
        }

        private static string RandomGpu(string platform, string deviceType)
        {
            if (deviceType == "mobile")
            {
                string[] gpus =
                [
                    "Adreno 660",
                    "Adreno 730",
                    "Apple A16 GPU",
                    "Mali-G78",
                    "Mali-G710"
                ];
                return gpus[_rand.Next(gpus.Length)];
            }

            if (platform == "Windows")
            {
                string[] winGpu =
                {
                    "NVIDIA GeForce RTX 3060",
                    "NVIDIA GeForce GTX 1650",
                    "AMD Radeon RX 580",
                    "Intel UHD Graphics 730"
                };
                return winGpu[_rand.Next(winGpu.Length)];
            }

            if (platform == "macOS")
            {
                string[] macGpu =
                {
                    "Apple M1 GPU",
                    "Apple M2 GPU",
                    "Intel Iris Plus Graphics 655"
                };
                return macGpu[_rand.Next(macGpu.Length)];
            }

            // Linux
            string[] linuxGpu =
            {
                "NVIDIA GeForce GTX 1050",
                "Intel Iris Graphics 6100",
                "AMD Radeon RX 570"
            };
            return linuxGpu[_rand.Next(linuxGpu.Length)];
        }
    }

    // -----------------------
    // INTERFACES + DATA TYPES
    // -----------------------

    public interface IWeighted
    {
        int Weight { get; }
    }

    public record FingerprintProfile(
        string UA,
        string Platform,
        string OSVersion,
        string Arch,
        string DeviceType,
        int Weight
    ) : IWeighted;

    public class BrowserIdentity
    {
        public string UserAgent { get; set; } = null!;
        public string Platform { get; set; } = null!;
        public string OSVersion { get; set; } = null!;
        public string Architecture { get; set; } = null!;
        public string DeviceType { get; set; } = null!;
        public string ScreenResolution { get; set; } = null!;
        public int ColorDepth { get; set; }
        public int HardwareConcurrency { get; set; }
        public int DeviceMemoryGb { get; set; }
        public string Gpu { get; set; } = null!;
        public int MaxTouchPoints { get; set; }

        public string Locale { get; set; } = null!;
        public string AcceptLanguage { get; set; } = null!;
        public string Timezone { get; set; } = null!;

        public override string ToString() =>
            $"{UserAgent}\n" +
            $"{Platform} {OSVersion} {Architecture}\n" +
            $"{DeviceType} {ScreenResolution} GPU={Gpu} " +
            $"Cores={HardwareConcurrency} RAM={DeviceMemoryGb}GB";
    }
}

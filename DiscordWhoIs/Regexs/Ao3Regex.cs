namespace DiscordWhoIs.Regexs
{
    using System.Text.RegularExpressions;

    public static partial class Ao3Regex
    {
        // Compile the regex at build-time
        [GeneratedRegex("page=([0-9]+)", RegexOptions.Compiled)]
        public static partial Regex PageCountRegex();
    }
}

using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace Crash.Helper.Updates
{
    [DataContract]
    internal sealed class UpdateRelease
    {
        [DataMember(Name = "tag_name")] internal string Tag { get; set; }
        [DataMember(Name = "draft")] internal bool Draft { get; set; }
        [DataMember(Name = "prerelease")] internal bool Prerelease { get; set; }
        [DataMember(Name = "assets")] internal UpdateAsset[] Assets { get; set; }

        internal static bool TryVersion(string tag, out Version version)
        {
            version = null;
            if (tag == null || !Regex.IsMatch(tag, @"\Av?[0-9]+\.[0-9]+\.[0-9]+\z")) return false;
            Version parsed;
            if (!Version.TryParse(tag.TrimStart('v'), out parsed)) return false;
            version = new Version(parsed.Major, parsed.Minor, parsed.Build, 0);
            return true;
        }

        internal UpdateAsset GetNewerAsset(Version current)
        {
            Version version;
            if (Draft || Prerelease || !TryVersion(Tag, out version) || version <= current) return null;
            var candidates = (Assets ?? new UpdateAsset[0]).Where(a => a != null && a.Name == "Crash.Helper.exe").ToArray();
            if (candidates.Length != 1) throw new InvalidOperationException("The latest release must contain exactly one Crash.Helper.exe asset.");
            var asset = candidates[0];
            Uri uri;
            string path = "/oimobatake/Crash.Helper/releases/download/" + Tag + "/Crash.Helper.exe";
            if (!Uri.TryCreate(asset.Url, UriKind.Absolute, out uri) || uri.Scheme != "https" ||
                !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) || !uri.IsDefaultPort ||
                uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 || Uri.UnescapeDataString(uri.AbsolutePath) != path)
                throw new InvalidOperationException("The update URL does not belong to the expected release.");
            if (asset.Size <= 0 || asset.Size > 128 * 1024 * 1024 || asset.Digest == null ||
                !Regex.IsMatch(asset.Digest, @"\Asha256:[0-9a-fA-F]{64}\z"))
                throw new InvalidOperationException("The update has no valid size or SHA-256 digest.");
            return asset;
        }
    }

    [DataContract]
    internal sealed class UpdateAsset
    {
        [DataMember(Name = "name")] internal string Name { get; set; }
        [DataMember(Name = "browser_download_url")] internal string Url { get; set; }
        [DataMember(Name = "size")] internal long Size { get; set; }
        [DataMember(Name = "digest")] internal string Digest { get; set; }
    }
}

using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;

namespace Crash.Helper.Updates
{
    [DataContract]
    internal sealed class UpdatePackage
    {
        [DataMember] internal string TargetPath;
        [DataMember] internal string Tag;
        [DataMember] internal string Sha256;
        [DataMember] internal string PreviousSha256;
        [DataMember] internal long Size;
        [DataMember] internal int ParentId;
        [DataMember] internal long ParentStartTicks;
        internal string DirectoryPath;
        internal string PayloadPath => Path.Combine(DirectoryPath, "package.exe");
        internal string ManifestPath => Path.Combine(DirectoryPath, "update.json");

        internal static string Hash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        internal void Validate(string path)
        {
            Version version;
            if (!UpdateRelease.TryVersion(Tag, out version) || new FileInfo(path).Length != Size ||
                !string.Equals(Hash(path), Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The update does not match its release size or SHA-256 digest.");
            var assembly = AssemblyName.GetAssemblyName(path);
            if (assembly.Name != "Crash.Helper" || assembly.Version != version || assembly.ProcessorArchitecture == ProcessorArchitecture.X86)
                throw new InvalidDataException("The executable identity, version or architecture does not match the release.");
        }

        internal void Save()
        {
            using (var stream = File.Create(ManifestPath))
                new DataContractJsonSerializer(typeof(UpdatePackage)).WriteObject(stream, this);
        }

        internal static UpdatePackage Load(string directory)
        {
            using (var stream = File.OpenRead(Path.Combine(directory, "update.json")))
            {
                var package = (UpdatePackage)new DataContractJsonSerializer(typeof(UpdatePackage)).ReadObject(stream);
                if (package == null) throw new InvalidDataException("The update manifest is empty.");
                package.DirectoryPath = directory;
                return package;
            }
        }
    }
}

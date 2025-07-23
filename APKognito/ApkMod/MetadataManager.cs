using System.IO;
using System.Text;
using APKognito.Utilities;

namespace APKognito.ApkMod;

public static class MetadataManager
{
    private const string APKOGNITO_MAGIC = "apkgn_meta";

    public static void WriteMetadata(string metaPath, RenamedPackageMetadata metadata)
    {
        using FileStream file = File.OpenWrite(metaPath);

        file.Write(Encoding.UTF8.GetBytes(APKOGNITO_MAGIC));

        byte[] metaBson = Bson.Serialize(metadata);
        file.Write(metaBson);

        file.Close();
    }

    public static RenamedPackageMetadata? LoadMetadata(string metaPath)
    {
        using FileStream file = File.OpenRead(metaPath);
        using BinaryReader reader = new(file);

        Span<char> loadedMagic = stackalloc char[APKOGNITO_MAGIC.Length];
        _ = reader.Read(loadedMagic);

        if (loadedMagic.ToString() != APKOGNITO_MAGIC)
        {
            return null;
        }

        byte[] bsonData = reader.ReadBytes((int)(file.Length - file.Position));

        return Bson.Deserialize<RenamedPackageMetadata>(bsonData);
    }
}

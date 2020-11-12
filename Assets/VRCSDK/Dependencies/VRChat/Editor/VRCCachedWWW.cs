using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Networking;

public static class VRCCachedWWW {
    public const float DefaultCacheTimeHours = 24 * 7;

    public static void ClearOld(float cacheLimitHours = DefaultCacheTimeHours)
    {
        string cacheDir = CacheDir;
        if (System.IO.Directory.Exists(cacheDir))
        {
            foreach (string fileName in System.IO.Directory.GetFiles(cacheDir))
            {
                if (GetAge(fileName) > cacheLimitHours)
                {
                    Debug.Log($"Deleting {fileName}");
                    System.IO.File.Delete(fileName);   
                }
            }
        }
    }

    private static string CacheDir
    {
        get
        {
            return Application.temporaryCachePath;
        }
    }

    public static IEnumerator Get(string url, System.Action<Texture2D> onDone, float cacheLimitHours = DefaultCacheTimeHours)
    {
        string cacheDir = CacheDir;
        if (!System.IO.Directory.Exists(cacheDir))
            System.IO.Directory.CreateDirectory(cacheDir);

        string hash = CreateHash(url);
        string cache = cacheDir + "/www_" + hash;

        if (File.Exists(cache))
        {
            // Use cached file if it exists
            
            if (GetAge(cache) > cacheLimitHours)
                File.Delete(cache);
            else
            {
                var texture = new Texture2D(2, 2);
                if (texture.LoadImage(File.ReadAllBytes(cache)))
                {
                    // load texture from disk and exit if we successfully read it
                    texture.Apply();
                    onDone(texture);
                    yield break;   
                }
            }
        }
        
        else
        {
            // No cached file, load it from url
            using (UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(url))
            {
                // Wait until request and download are complete
                yield return uwr.SendWebRequest();
                while (!uwr.isDone || !uwr.downloadHandler.isDone)
                {
                    yield return null;
                }

                var texture = DownloadHandlerTexture.GetContent(uwr);
            
                if(string.IsNullOrEmpty(uwr.error))
                    File.WriteAllBytes(cache, uwr.downloadHandler.data);
                
                onDone(texture);
                yield break;
            }   
        }
        
    }

    private static string CreateHash(string _string)
    {
        SHA256 hash = SHA256.Create();
        byte[] computed_hash = hash.ComputeHash(System.Text.Encoding.Default.GetBytes(_string));
        return System.Uri.EscapeDataString(System.Convert.ToBase64String(computed_hash));
    }

    private static double GetAge(string file)
    {
        if (!System.IO.File.Exists(file))
            return 0;

        System.DateTime writeTime = System.IO.File.GetLastWriteTimeUtc(file);
        return System.DateTime.UtcNow.Subtract(writeTime).TotalHours;
    }
}

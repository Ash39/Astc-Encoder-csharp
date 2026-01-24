using Castle.Components.DictionaryAdapter.Xml;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace Astc_Encoder_CSharp_Generator
{
    public class AstcGeneratorTask : Task
    {
        [Required]
        public string ASTCVersion { get; set; }
        [Required]
        public string ASTCSourceFilePath { get; set; }
        [Required]
        public string CacheFile { get; set; }
        [Required]
        public string MethodExportName { get; set; }
        [Required]
        public string GeneraedFilesNamespace { get; set; }
        [Required]
        public string ProjectPath { get; set; }
        public string InternalTypes { get; set; }
        public string Preprocessers { get; set; } = string.Empty;
        public bool DisableXmlCache { get; set; }

        private HttpClient client;

        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, "Starting ASTC C# Binding Generation Task...");
            try
            {
                client = new HttpClient();
                client.BaseAddress = new Uri("https://api.github.com/");
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Astc-Encoder-CSharp", "1.0"));
                client.DefaultRequestHeaders.Add("accept", "application/vnd.github.raw");

                Directory.CreateDirectory(Path.GetDirectoryName(CacheFile));
            }
            catch (Exception e)
            {
                Log.LogError($"Failed to create necessary directories: {e.Message}");
                return false;
            }

            string versionCommit = GetLastestReleaseCommit();

            if (File.Exists(CacheFile))
            {
                Log.LogMessage(MessageImportance.High, "Found cached ASTC source download. Validating...");
                string cachedJson = File.ReadAllText(CacheFile);

                AstcSourceDownload sourceDownload = JsonSerializer.Deserialize<AstcSourceDownload>(cachedJson);


                if (!string.IsNullOrEmpty(versionCommit) && sourceDownload != null)
                {
                    if (sourceDownload.Commit == versionCommit)
                    {
                        Log.LogMessage(MessageImportance.High, "Cached ASTC source download is valid. Using cached version.");
                        if (string.IsNullOrEmpty(sourceDownload.Xml) || GetType().Assembly.ManifestModule.ModuleVersionId.ToString() != sourceDownload.ModuleVersionId || DisableXmlCache)
                        {
                            Log.LogMessage(MessageImportance.High, "Cached ASTC source XML is missing or outdated. Re-generating XML from source code.");

                            CTokensToXml.ConvertTokensToXml(sourceDownload, Preprocessers.Split(';'), MethodExportName);

                            sourceDownload.ModuleVersionId = GetType().Assembly.ManifestModule.ModuleVersionId.ToString();

                            if (string.IsNullOrEmpty(sourceDownload.Xml))
                            {
                                this.Log.LogError("Failed to convert ASTC source code to XML.");
                                return false;
                            }
                            try
                            {
                                Log.LogMessage(MessageImportance.High, "Updating cache file with new XML data.");
                                File.WriteAllText(CacheFile, JsonSerializer.Serialize<AstcSourceDownload>(sourceDownload));
                            }
                            catch (Exception e)
                            {
                                Log.LogErrorFromException(e);
                            }
                        }
                        Log.LogMessage(MessageImportance.High, "Generating C# bindings from cached ASTC source XML.");
                        CSharpConvertion.CreateBindings(Log, sourceDownload, ASTCSourceFilePath, GeneraedFilesNamespace, ProjectPath, InternalTypes.Split(';'));

                        Log.LogCommandLine(MessageImportance.High, "Extracting ASTC runtime libraries from cached assets...");

                        string destinationPath = new FileInfo(CacheFile).Directory.FullName;

                        string[] assets = Directory.GetFiles(destinationPath, "*.zip");

                        if (assets.Length == 0)
                        {
                            assets = DownloadReleaseAssets();
                        }

                        ExtractAssetsZipFiles(assets);

                        return true;
                    }
                }
            }
            Log.LogMessage(MessageImportance.High, "No valid cached ASTC source download found. Downloading from GitHub...");


            return DownLoadSourceCodeAndCreateBindings(versionCommit);
        }

        private bool DownLoadSourceCodeAndCreateBindings(string versionCommit)
        {

            if (string.IsNullOrEmpty(versionCommit))
            {
                Log.LogError("Failed to retrieve ASTC version commit from GitHub.");
                return false;
            }

            AstcSourceDownload sourceDownload = new AstcSourceDownload();
            sourceDownload.Commit = versionCommit;

            try
            {
                string sourceFile = System.Threading.Tasks.Task.Run<string>(
                    async () => await client.GetStringAsync($"repos/Ash39/astc-encoder/contents/{ASTCSourceFilePath}?ref={sourceDownload.Commit}")).Result;

                if (string.IsNullOrEmpty(sourceFile))
                {
                    //TODO: Log Error
                    Log.LogError("Failed to retrieve ASTC source code from GitHub.");
                    return false;
                }

                Log.LogMessage(MessageImportance.High, "Successfully retrieved ASTC source code from GitHub.");

                sourceDownload.Code = sourceFile;

                Log.LogMessage(MessageImportance.High, "Converting ASTC source code to XML...");

                CTokensToXml.ConvertTokensToXml(sourceDownload, Preprocessers.Split(';'), MethodExportName);

                Log.LogMessage(MessageImportance.High, "Caching ASTC source download...");

                File.WriteAllText(CacheFile, JsonSerializer.Serialize<AstcSourceDownload>(sourceDownload));

            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e);
                return false;
            }

            Log.LogMessage(MessageImportance.High, "Generating C# bindings from ASTC source XML.");
            CSharpConvertion.CreateBindings(Log, sourceDownload, ASTCSourceFilePath, GeneraedFilesNamespace, ProjectPath, InternalTypes.Split(';'));

            string[] assets = DownloadReleaseAssets();
            try
            {
                if (assets.Length > 0)
                {
                    ExtractAssetsZipFiles(assets);
                }
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e);
                return false;
            }
            

            return true;
        }

        private void ExtractAssetsZipFiles(string[] assets)
        {
            foreach (var assetPath in assets)
            {
                string libraryPlatform = assetPath.ToLower().Contains("windows") ? "windows" :
                                          assetPath.ToLower().Contains("linux") ? "linux" :
                                          assetPath.ToLower().Contains("macos") ? "osx" : string.Empty;

                string libraryArchitecture = assetPath.ToLower().Contains("x64") ? "x64" :
                                          assetPath.ToLower().Contains("x86") ? "x86" :
                                          assetPath.ToLower().Contains("arm64") ? "arm64" : string.Empty;

                Log.LogMessage(MessageImportance.High, $"Extracting ASTC runtime libraries for platform: {libraryPlatform}...");

                string destinationPath = Path.Combine(ProjectPath, "runtimes", libraryPlatform, libraryArchitecture);

                using (ZipArchive archive = ZipFile.OpenRead(assetPath))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string entryDirectory = Path.GetDirectoryName(entry.FullName);

                        FileInfo fileInfo = new FileInfo(entry.FullName);

                        if (!fileInfo.IsDirectory())
                        {
                            if (string.IsNullOrEmpty(entry.Name))
                                continue;

                            string destinationAssetPath = Path.Combine(destinationPath, entry.Name);

                            Log.LogCommandLine(MessageImportance.High, $"Extracting file: {entry.Name} to {destinationAssetPath}...");

                            Directory.CreateDirectory(destinationPath);

                            entry.ExtractToFile(destinationAssetPath, overwrite: true);
                        }
                    }
                }
            }
        }

        private JsonDocument DownloadReleaseInformation()
        {
            Log.LogMessage(MessageImportance.High, $"Retrieving ASTC version '{ASTCVersion}' from GitHub...");

            string jsonReleaseResponse = System.Threading.Tasks.Task.Run<string>(
            async () =>
            {
                HttpResponseMessage httpResponse;

                if (ASTCVersion == "latest")
                    httpResponse = await client.GetAsync($"repos/Ash39/astc-encoder/releases/latest");
                else
                    httpResponse = await client.GetAsync($"repos/Ash39/astc-encoder/releases/tags/{ASTCVersion}");

                if (!httpResponse.IsSuccessStatusCode)
                {
                    //TODO: Log Error
                    Log.LogError($"Failed to retrieve ASTC releases from GitHub: {httpResponse.ReasonPhrase}");
                    return string.Empty;
                }

                return await httpResponse.Content.ReadAsStringAsync();

            }).Result;

            if (string.IsNullOrEmpty(jsonReleaseResponse))
                return null;

            Log.LogMessage(MessageImportance.High, "Parsing ASTC release information...");

            return JsonDocument.Parse(jsonReleaseResponse);
        }

        private string GetLastestReleaseCommit()
        {
            JsonDocument doc = DownloadReleaseInformation();

            if (doc == null)
                return string.Empty;

            JsonElement element = doc.RootElement.GetProperty("target_commitish");

            return element.GetString();
        }

        private string[] DownloadReleaseAssets()
        {
            JsonDocument doc = DownloadReleaseInformation();

            if (doc == null)
                return Array.Empty<string>();

            JsonElement assestsInfo = doc.RootElement.GetProperty("assets");

            List<System.Threading.Tasks.Task<string>> tasks = new List<System.Threading.Tasks.Task<string>>(assestsInfo.GetArrayLength());

            string destinationPath = new FileInfo(CacheFile).Directory.FullName;

            foreach (JsonElement asset in assestsInfo.EnumerateArray())
            {
                string assetUrl = asset.GetProperty("browser_download_url").GetString();
                string assetName = asset.GetProperty("name").GetString();
                string assetSavePath = Path.Combine(destinationPath, assetName);

                tasks.Add(FileDownload());

                async System.Threading.Tasks.Task<string> FileDownload()
                {
                    try
                    {
                        if (Path.GetExtension(assetUrl).ToLower() != ".zip")
                            return string.Empty;

                        Log.LogMessage(MessageImportance.High, $"Downloading asset: {assetName}...");

                        using (Stream contentStream = await client.GetStreamAsync(assetUrl))
                        {
                            using (FileStream fileStream = new FileStream(assetSavePath, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                await contentStream.CopyToAsync(fileStream);
                            }
                        }
                        Log.LogMessage($"Download complete. File saved to: {destinationPath}");
                        return assetSavePath;
                    }
                    catch (Exception e)
                    {
                        Log.LogErrorFromException(e);
                        return string.Empty;
                    }
                }
            }

            Log.LogMessage(MessageImportance.High, "Downloading ASTC release assets...");

            System.Threading.Tasks.Task.WaitAll(tasks.ToArray());

            Log.LogMessage(MessageImportance.High, "All ASTC release assets downloaded.");

            return tasks.Select(t => t.Result).Where(s => !string.IsNullOrEmpty(s)).ToArray();
        }
    }
}
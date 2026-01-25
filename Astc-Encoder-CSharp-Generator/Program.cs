using Castle.Components.DictionaryAdapter.Xml;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;
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
    public class Program
    {
        private static HttpClient client;

        public static int Main(string[] args)
        {
            RootCommand rootCommand = new ("Generate ASTC c# Bindings.");

            Option<string> ASTCVersionOption = new("--Version")
            {
                Required = true,
                Description = ""
            };

            Option<string> ASTCSourceFilePathOption = new("--SourceFilePath")
            {
                Required = true,
                Description = ""
            };
            Option<string> CacheFilePathOption = new("--CacheFilePath")
            {
                Required = true,
                Description = ""
            };

            Option<string> MethodExportNameOption = new("--MethodExportName")
            {
                Required = true,
                Description = ""
            };

            Option<string> GeneratedFilesNamespaceOption = new("--GeneratedFilesNamespace")
            {
                Required = true,
                Description = ""
            };

            Option<string> ProjectPathOption = new("--ProjectPath")
            {
                Required = true,
                Description = ""
            };

            Option<string> InternalTypesOption = new("--InternalTypes")
            {
                Required = false,
                Description = ""
            };

            Option<string> PreprocessersOption = new("--Preprocessers")
            {
                Required = false,
                Description = ""
            };

            Option<bool> DisableXmlCacheOption = new("--DisableXmlCache")
            {
                Required = false,
                Description = ""
            };

            rootCommand.Add(ASTCVersionOption);
            rootCommand.Add(CacheFilePathOption);
            rootCommand.Add(ASTCSourceFilePathOption);
            rootCommand.Add(MethodExportNameOption);
            rootCommand.Add(GeneratedFilesNamespaceOption);
            rootCommand.Add(ProjectPathOption);
            rootCommand.Add(InternalTypesOption);
            rootCommand.Add(PreprocessersOption);
            rootCommand.Add(DisableXmlCacheOption);

            rootCommand.SetAction((result) =>
            {
                if (result.Errors.Count > 0)
                {
                    return;
                }

                Generate(result.GetValue(ASTCVersionOption), result.GetValue(ASTCSourceFilePathOption), result.GetValue(CacheFilePathOption), result.GetValue(MethodExportNameOption), result.GetValue(GeneratedFilesNamespaceOption), 
                    result.GetValue(ProjectPathOption), result.GetValue(InternalTypesOption), result.GetValue(PreprocessersOption), result.GetValue(DisableXmlCacheOption));
            });

            return rootCommand.Parse(args).Invoke();
        }

        public static bool Generate(string ASTCVersion, string ASTCSourceFilePath, string CacheFile, string MethodExportName, string GeneraedFilesNamespace,
            string ProjectPath, string InternalTypes, string Preprocessers, bool DisableXmlCache) 
        {
            Console.WriteLine("Starting ASTC C# Binding Generation Task...");
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
                Console.Error.WriteLine($"Failed to create necessary directories: {e.Message}");
                return false;
            }

            string versionCommit = GetLastestReleaseCommit(ASTCVersion);

            if (File.Exists(CacheFile))
            {
                Console.WriteLine("Found cached ASTC source download. Validating...");
                string cachedJson = File.ReadAllText(CacheFile);

                AstcSourceDownload sourceDownload = JsonSerializer.Deserialize<AstcSourceDownload>(cachedJson);


                if (!string.IsNullOrEmpty(versionCommit) && sourceDownload != null)
                {
                    if (sourceDownload.Commit == versionCommit)
                    {
                        Console.WriteLine("Cached ASTC source download is valid. Using cached version.");
                        if (string.IsNullOrEmpty(sourceDownload.Xml) || typeof(Program).Assembly.ManifestModule.ModuleVersionId.ToString() != sourceDownload.ModuleVersionId || DisableXmlCache)
                        {
                            Console.WriteLine("Cached ASTC source XML is missing or outdated. Re-generating XML from source code.");

                            CTokensToXml.ConvertTokensToXml(sourceDownload, Preprocessers.Split(';'), MethodExportName);

                            sourceDownload.ModuleVersionId = typeof(Program).Assembly.ManifestModule.ModuleVersionId.ToString();

                            if (string.IsNullOrEmpty(sourceDownload.Xml))
                            {
                                Console.Error.WriteLine("Failed to convert ASTC source code to XML.");
                                return false;
                            }
                            try
                            {
                                Console.WriteLine("Updating cache file with new XML data.");
                                File.WriteAllText(CacheFile, JsonSerializer.Serialize<AstcSourceDownload>(sourceDownload));
                            }
                            catch (Exception e)
                            {
                                Console.Error.WriteLine(e.Message);
                                return false;
                            }
                        }
                        Console.WriteLine("Generating C# bindings from cached ASTC source XML.");
                        CSharpConvertion.CreateBindings(sourceDownload, ASTCSourceFilePath, GeneraedFilesNamespace, ProjectPath, InternalTypes.Split(';'));

                        Console.WriteLine( "Extracting ASTC runtime libraries from cached assets...");

                        string destinationPath = new FileInfo(CacheFile).Directory.FullName;

                        string[] assets = Directory.GetFiles(destinationPath, "*.zip");

                        if (assets.Length == 0)
                        {
                            assets = DownloadReleaseAssets(ASTCVersion, CacheFile);
                        }

                        ExtractAssetsZipFiles(ProjectPath, assets);

                        return true;
                    }
                }
            }
            Console.WriteLine("No valid cached ASTC source download found. Downloading from GitHub...");


            return DownLoadSourceCodeAndCreateBindings(ProjectPath,InternalTypes, Preprocessers, MethodExportName, GeneraedFilesNamespace, CacheFile, ASTCVersion, ASTCSourceFilePath,versionCommit);
        }

        private static bool DownLoadSourceCodeAndCreateBindings(string projectPath, string internalTypes, string preprocessers, string methodExportName, string generaedFilesNamespace, string cacheFile, string ASTCVersion, string ASTCSourceFilePath, string versionCommit)
        {

            if (string.IsNullOrEmpty(versionCommit))
            {
                Console.Error.WriteLine("Failed to retrieve ASTC version commit from GitHub.");
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
                    Console.Error.WriteLine("Failed to retrieve ASTC source code from GitHub.");
                    return false;
                }

                Console.WriteLine("Successfully retrieved ASTC source code from GitHub.");

                sourceDownload.Code = sourceFile;

                Console.WriteLine("Converting ASTC source code to XML...");

                CTokensToXml.ConvertTokensToXml(sourceDownload, preprocessers.Split(';'), methodExportName);

                Console.WriteLine("Caching ASTC source download...");

                File.WriteAllText(cacheFile, JsonSerializer.Serialize<AstcSourceDownload>(sourceDownload));

            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                return false;
            }

            Console.WriteLine("Generating C# bindings from ASTC source XML.");
            CSharpConvertion.CreateBindings(sourceDownload, ASTCSourceFilePath, generaedFilesNamespace, projectPath, internalTypes.Split(','));

            string[] assets = DownloadReleaseAssets(ASTCVersion, projectPath);
            try
            {
                if (assets.Length > 0)
                {
                    ExtractAssetsZipFiles(projectPath, assets);
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                return false;
            }


            return true;

        }

        private static void ExtractAssetsZipFiles(string projectPath, string[] assets)
        {
            foreach (var assetPath in assets)
            {
                string libraryPlatform = assetPath.ToLower().Contains("windows") ? "windows" :
                                          assetPath.ToLower().Contains("linux") ? "linux" :
                                          assetPath.ToLower().Contains("macos") ? "osx" : string.Empty;

                string libraryArchitecture = assetPath.ToLower().Contains("x64") ? "x64" :
                                          assetPath.ToLower().Contains("x86") ? "x86" :
                                          assetPath.ToLower().Contains("arm64") ? "arm64" : string.Empty;

               Console.WriteLine( $"Extracting ASTC runtime libraries for platform: {libraryPlatform}...");

                string destinationPath = Path.Combine(projectPath, "runtimes", libraryPlatform, libraryArchitecture);

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

                            Console.WriteLine( $"Extracting file: {entry.Name} to {destinationAssetPath}...");

                            Directory.CreateDirectory(destinationPath);

                            entry.ExtractToFile(destinationAssetPath, overwrite: true);
                        }
                    }
                }
            }
        }

        private static JsonDocument DownloadReleaseInformation(string ASTCVersion)
        {
           Console.WriteLine( $"Retrieving ASTC version '{ASTCVersion}' from GitHub...");

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
                    Console.Error.WriteLine($"Failed to retrieve ASTC releases from GitHub: {httpResponse.ReasonPhrase}");
                    return string.Empty;
                }

                return await httpResponse.Content.ReadAsStringAsync();

            }).Result;

            if (string.IsNullOrEmpty(jsonReleaseResponse))
                return null;

           Console.WriteLine( "Parsing ASTC release information...");

            return JsonDocument.Parse(jsonReleaseResponse);
        }

        private static string GetLastestReleaseCommit(string ASTCVersion)
        {
            JsonDocument doc = DownloadReleaseInformation(ASTCVersion);

            if (doc == null)
                return string.Empty;

            JsonElement element = doc.RootElement.GetProperty("target_commitish");

            return element.GetString();
        }

        private static string[] DownloadReleaseAssets(string ASTCVersion, string cacheFile)
        {
            JsonDocument doc = DownloadReleaseInformation(ASTCVersion);

            if (doc == null)
                return Array.Empty<string>();

            JsonElement assestsInfo = doc.RootElement.GetProperty("assets");

            List<System.Threading.Tasks.Task<string>> tasks = new List<System.Threading.Tasks.Task<string>>(assestsInfo.GetArrayLength());

            string destinationPath = new FileInfo(cacheFile).Directory.FullName;

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

                       Console.WriteLine( $"Downloading asset: {assetName}...");

                        using (Stream contentStream = await client.GetStreamAsync(assetUrl))
                        {
                            using (FileStream fileStream = new FileStream(assetSavePath, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                await contentStream.CopyToAsync(fileStream);
                            }
                        }
                        Console.WriteLine($"Download complete. File saved to: {destinationPath}");
                        return assetSavePath;
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine(e.Message);
                        return string.Empty;
                    }
                }
            }

           Console.WriteLine( "Downloading ASTC release assets...");

            System.Threading.Tasks.Task.WaitAll(tasks.ToArray());

           Console.WriteLine( "All ASTC release assets downloaded.");

            return tasks.Select(t => t.Result).Where(s => !string.IsNullOrEmpty(s)).ToArray();
        }
    }
}
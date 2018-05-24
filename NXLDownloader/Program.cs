using API;
using MoreLinq;
using Newtonsoft.Json;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Readers.GZip;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NXLDownloader
{
    public class Program
    {
        static ConcurrentQueue<string> consoleMessages = new ConcurrentQueue<string>();
        static Product[] Products;
        public static string UserAgent = "NXLDownloader";
        public static Login AuthInfo;

        public static void Log(string message) => consoleMessages.Enqueue(message);
        static void Main(string[] args) => GetStuff(args.Select(c => c.ToLower()).ToArray());

        static void GetStuff(string[] args)
        {
            string hash = GetFlagValue(args, "-h");
            string productSelected = GetFlagValue(args, "-p");
            bool autoYes = args.Any(c => c.StartsWith("-y"));
            bool verbose = args.Any(c => c.StartsWith("-v"));
            bool quiet = args.Any(c => c.StartsWith("-q"));

            Initialize(verbose);
            InteractiveDownload(productSelected, hash, autoYes, verbose, quiet);
        }

        public static void Initialize(bool verbose = true)
        {
            // Set the log handler for when hash mismatches or wrong sizes
            FileEntry.Log = Log;

            using (HttpClient client = new HttpClient())
            {
                // Declare for analytics that we are downloading from NXLDownloader
                client.DefaultRequestHeaders.Add("User-Agent", UserAgent);
                if (verbose) Console.WriteLine("Getting list of products");
                // If there's an exception here, we can't recover
                ProductEntriesList productList = JsonConvert.DeserializeObject<ProductEntriesList>(client.GetStringAsync("http://nexon.ws/api/gms/products").Result);
                if (verbose) Console.WriteLine($"Found {productList.Products.Length} products, downloading info");
                Products = productList.Products.Select(c =>
                {
                    try
                    {
                        string productDetails;
                        if (AuthInfo == null)
                            productDetails = client.GetStringAsync(c.ProductLink).Result;
                        else
                        {
                            client.DefaultRequestHeaders.Add("Authorization", $"bearer {Convert.ToBase64String(Encoding.UTF8.GetBytes(AuthInfo.access_token))}");
                            productDetails = client.GetStringAsync($"https://api.nexon.io/products/{c.ProductId}").Result;
                        }
                        return JsonConvert.DeserializeObject<Product>(productDetails);
                    }
                    catch (Exception ex)
                    {
                        if (verbose) Console.WriteLine($"Error downloading product details for {c.ProductId}, skipping");
                        return null;
                    }
                }).Where(c => c != null).ToArray();
            }
        }

        public static void InteractiveDownload(string productSelected = null, string hash = null, bool autoYes = true, bool verbose = true, bool quiet = false)
        {
            Product selected = null;
            Manifest manifest = null;

            using (HttpClient client = new HttpClient())
            {
                while (selected == null || hash == null)
                {
                    while (selected == null)
                    {
                        for (int i = 0; i < Products.Length; ++i)
                        {
                            Product product = Products[i];
                            if (!quiet) Console.WriteLine($"[{i}] {product.ProductId} {product.FriendlyProductName ?? product.ProductName}");
                        }

                        if (string.IsNullOrEmpty(productSelected) && !quiet)
                        {
                            if (Products.Length != 0) Console.Write($"Enter product number (0-{Products.Length - 1}): ");
                            else Console.WriteLine("Enter product ID: ");
                        }

                        if (string.IsNullOrWhiteSpace(productSelected)) productSelected = Console.ReadLine().Trim('\r', '\n', ' ');

                        if (int.TryParse(productSelected, out int productId))
                        {
                            if (productId < Products.Length) selected = Products[productId];
                            else selected = Products.FirstOrDefault(c => c.ProductId.Equals(productId.ToString()));
                            if (selected == null)
                            {
                                if (!quiet && verbose) Console.WriteLine("Unknown product ID, attempting to download info");
                                try
                                {
                                    string details = client.GetStringAsync($"http://nexon.ws/api/gms/products/{productSelected}").Result;
                                    selected = JsonConvert.DeserializeObject<Product>(details);
                                }
                                catch (Exception ex)
                                {
                                    if (!quiet)
                                    {
                                        Console.WriteLine("Couldn't get product info");
                                        Console.WriteLine(ex.Message);
                                    }
                                }
                            }
                        }
                        productSelected = null;

                        if (!quiet)
                        {
                            if (selected == null) Console.WriteLine("Invalid product selected.");
                            else Console.WriteLine($"{selected.FriendlyProductName ?? selected.ProductName} selected.");
                        }
                    }

                    if (verbose) Console.WriteLine("Downloading possible manifest hashes");
                    KeyValuePair<string, Tuple<string, string>>[] hashes = selected.Details.Branches
                        .SelectMany(c => c.Value)
                        .Append(new KeyValuePair<string, string>("Main", selected.Details.ManifestURL))
                        .Where(c => c.Value != null)
                        .Select(c =>
                        {
                            string manifestHash = null;
                            try
                            {
                                manifestHash = client.GetStringAsync(c.Value).Result;
                            }
                            catch (Exception ex)
                            {
                                if (!quiet) Console.WriteLine("Error downloading manifest's hash, skipping");
                                manifestHash = null;
                            }
                            return new KeyValuePair<string, Tuple<string, string>>(c.Key, new Tuple<string, string>(manifestHash, c.Value));
                        })
                        .Where(c => c.Value.Item1 != null)
                        .ToArray();

                    while (hash == null)
                    {
                        for (int i = 0; i < hashes.Length; ++i)
                        {
                            KeyValuePair<string, Tuple<string, string>> manifestHash = hashes[i];
                            if (!quiet) Console.WriteLine($"[{i}] {manifestHash.Key} {manifestHash.Value.Item1} @ {manifestHash.Value.Item2}");
                        }

                        if (!quiet)
                        {
                            if (hashes.Length == 0) Console.Write("No manifests found, enter manifest hash to attempt to download: ");
                            else Console.Write($"Select manifest (0-{hashes.Length - 1}): ");
                        }

                        hash = Console.ReadLine().Trim('\r', '\n', ' ');

                        if (int.TryParse(hash, out int hashIndex))
                            hash = hashes[hashIndex].Value.Item1;
                        else if (string.IsNullOrEmpty(hash))
                        {
                            hash = null;
                            selected = null;
                            if (!quiet) Console.WriteLine("Returning to product selection");
                            break;
                        }
                        else if (hash.Length != 40) hash = null;

                        if (hash == null)
                        {
                            if (!quiet) Console.WriteLine("Invalid hash selected");
                        }
                        else
                        {
                            if (!quiet) Console.WriteLine("Downloading manifest");
                            try
                            {
                                byte[] ManifestCompressed = client.GetByteArrayAsync($"https://download2.nexon.net/Game/nxl/games/{selected.ProductId}/{hash}").Result;
                                // Parse the manifest
                                manifest = Manifest.Parse(ManifestCompressed);
                            }
                            catch (Exception ex)
                            {
                                if (!quiet)
                                {
                                    Console.WriteLine("Error getting manifest");
                                    Console.WriteLine(ex.Message);
                                }
                                hash = null;
                            }
                        }
                    }

                    if (manifest != null && !autoYes && !quiet)
                    {
                        Console.WriteLine($"Selected {manifest.BuiltAt} of {manifest.Product}");
                        Console.WriteLine($"This download will be {manifest.TotalCompressedSize} and will take up {manifest.TotalUncompressedSize} disk space");
                        Console.Write("Continue? [Y]/N: ");
                        if (Console.ReadLine().Trim('\r', '\n', ' ').Equals("N", StringComparison.CurrentCultureIgnoreCase))
                        {
                            hash = null;
                            manifest = null;
                            Console.WriteLine("Returning to hash selection");
                        }
                    }
                }
            }

            Download(manifest, selected, hash, quiet);
        }

        static Dictionary<string, Product> productCache = new Dictionary<string, Product>();

        public static Product GetProduct(string productId)
        {
            if (productCache.ContainsKey(productId)) return productCache[productId];
            using (HttpClient client = new HttpClient())
            {
                string productDetails;
                if (AuthInfo == null)
                    productDetails = client.GetStringAsync($"http://nexon.ws/api/gms/products/{productId}").Result;
                else
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"bearer {Convert.ToBase64String(Encoding.UTF8.GetBytes(AuthInfo.access_token))}");
                    productDetails = client.GetStringAsync($"https://api.nexon.io/products/{productId}").Result;
                }
                Product p = JsonConvert.DeserializeObject<Product>(productDetails);
                if (Products == null) Products = new Product[] { p };
                else {
                    Product[] oldProducts = Products;
                    Products = new Product[Products.Length + 1];
                    for(var i = 0; i < oldProducts.Length; ++i)
                        Products[i] = oldProducts[i];
                    Products[oldProducts.Length] = p;
                }

                productCache.Add(productId, p);
                return p;
            }
        }

        static string GetFlagValue(string[] args, string flag)
        {
            if (args.Any(c => c.StartsWith(flag)))
            {
                string pFlag = args.FirstOrDefault(c => c.StartsWith("-p"));
                string afterFlag = args.ElementAtOrDefault(Array.IndexOf(args, pFlag) + 1);
                if (pFlag.Length > 2) return pFlag.Substring(2);
                else if (afterFlag != null && !afterFlag.StartsWith("-")) return afterFlag;
            }

            return null;
        }

        public static void Download(string branchName, string productId, bool quiet = false)
        {
            using (HttpClient client = new HttpClient())
            {
                Product selected = Products.First(c => c.ProductId.Equals(productId, StringComparison.CurrentCultureIgnoreCase));
                string hash = selected.Details.Branches["win32"][branchName];
                byte[] ManifestCompressed = null;

                if (hash.StartsWith("http")) {
                    ManifestCompressed = client.GetByteArrayAsync(hash).Result;
                    hash = Encoding.ASCII.GetString(ManifestCompressed);
                }

                ManifestCompressed = client.GetByteArrayAsync($"https://download2.nexon.net/Game/nxl/games/{selected.ProductId}/{hash}").Result;
                // Parse the manifest
                Manifest manifest = Manifest.Parse(ManifestCompressed);
                Download(manifest, selected, hash);
            }
        }

        public static void Download(Manifest manifest, Product selected, string selectedHash, bool quiet = false)
        {
            bool running = true;
            // Handle the console messages in its own thread so as to prevent any locking or messages being written at the same time
            Thread consoleQueue = new Thread(() =>
            {
                string message = null;
                while (running || consoleMessages.TryDequeue(out message))
                {
                    do
                    {
                        if (message != null) Console.WriteLine(message);
                        Thread.Sleep(1);
                    } while (consoleMessages.TryDequeue(out message));
                }
            });

            consoleQueue.Start();

            string output = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), selected.ProductId, selectedHash);

            if (!Directory.Exists(output)) Directory.CreateDirectory(output);

            Dictionary<string, FileEntry> FileNames = manifest.RealFileNames;
            // Build the directory tree before we start downloading
            KeyValuePair<string, FileEntry>[] directories = FileNames.Where(c => c.Value.ChunkHashes.Count > 0 && c.Value.ChunkHashes.First().Equals("__DIR__")).ToArray();

            foreach (KeyValuePair<string, FileEntry> directory in directories)
            {
                string subDirectory = Path.Combine(output, directory.Key);
                if (File.Exists(subDirectory)) File.Delete(subDirectory);
                if (!Directory.Exists(subDirectory)) Directory.CreateDirectory(subDirectory);
            }

            long toDownload = manifest.TotalUncompressedSize;
            long downloaded = 0;

            foreach (KeyValuePair<string, FileEntry> file in FileNames.Where(c => !directories.Contains(c) && c.Value.ChunkHashes.Count > 0))
            {
                string filePath = Path.Combine(output, file.Key);
                if (!quiet) Log($"Starting download of {file.Key}");

                long position = 0;
                long existingFileSize = 0;
                long firstChunkSize = file.Value.ChunkSizes.First();
                // Get all of the chunks in their own threads
                long writtenSize = file.Value.ChunkHashes.Select(hash =>
                {
                    Tuple<long, byte[]> chunk = null;
                    int index = file.Value.ChunkHashes.IndexOf(hash); // Which chunk are we downloading
                    long size = file.Value.ChunkSizes[index]; // How big is the chunk
                    long realSize = 0;
                    do
                    {
                        byte[] existing;
                        SHA1 sha1 = SHA1.Create();
                        string sha1Hash;

                        if (!File.Exists(filePath)) File.Create(filePath).Dispose();
                        else // Otherwise check the hashes
                        {
                            using (FileStream fileOut = File.OpenRead(filePath)) // Reusing the same FileStream seems to cause memory issues, so make a new one
                            {
                                existingFileSize = fileOut.Length;
                                if (!quiet) Log($"Verifying existing data at {position} ({size} / {firstChunkSize})");
                                if (fileOut.Length >= position + size)
                                {
                                    existing = new byte[size];
                                    fileOut.Position = position;
                                    int read = 0;
                                    while ((read += fileOut.Read(existing, read, (int)(size - read))) != size) ; // Usually less than int.max so this should be okay
                                    sha1Hash = string.Join("", sha1.ComputeHash(existing).Select(c => c.ToString("x2")));
                                    if (sha1Hash.Equals(hash))
                                    {
                                        if (!quiet) Log("Hash check passed, skipping downloading");
                                        realSize = size;
                                        break;
                                    }
                                    else if (fileOut.Length >= position + firstChunkSize && firstChunkSize != size)
                                    {
                                        existing = new byte[firstChunkSize];
                                        fileOut.Position = position;
                                        read = 0;
                                        while ((read += fileOut.Read(existing, read, (int)(firstChunkSize - read))) != firstChunkSize) ; // Usually less than int.max so this should be okay
                                        sha1Hash = string.Join("", sha1.ComputeHash(existing).Select(c => c.ToString("x2")));
                                        if (sha1Hash.Equals(hash))
                                        {
                                            if (!quiet) Log("Hash check passed, skipping downloading");
                                            realSize = firstChunkSize;
                                            break;
                                        }
                                    }
                                    else if (!quiet) Log("Chunk didn't match hash");
                                }
                                if (file.Value.ChunkHashes.Count == 1)
                                {
                                    existing = new byte[fileOut.Length];
                                    int read = 0;
                                    fileOut.Position = 0;
                                    while ((read += fileOut.Read(existing, read, (int)(fileOut.Length - read))) != fileOut.Length) ; // Usually less than int.max so this should be okay
                                    sha1Hash = string.Join("", sha1.ComputeHash(existing).Select(c => c.ToString("x2")));
                                    if (sha1Hash.Equals(hash))
                                    {
                                        if (!quiet) Log("Hash check passed, skipping downloading");
                                        realSize = fileOut.Length;
                                        break;
                                    }
                                    else
                                        if (!quiet) Log("File didn't match hash");

                                    byte[] compressed = Program.Compress(existing);
                                    sha1Hash = string.Join("", sha1.ComputeHash(compressed).Select(c => c.ToString("x2")));
                                    if (sha1Hash.Equals(hash))
                                    {
                                        if (!quiet) Log("Hash check passed, skipping downloading");
                                        realSize = compressed.Length;
                                        break;
                                    }
                                    else
                                        if (!quiet) Log("Compressed file didn't match hash");
                                }
                            }
                        }

                        using (FileStream fileOut = File.OpenWrite(filePath))
                        {
                            if (!quiet) Log($"Downloading {hash}");
                            chunk = FileEntry.DownloadChunk(manifest.Product, hash, size, position); // Download the chunk
                            realSize = chunk.Item2.Length;

                            fileOut.Position = chunk.Item1; // The chunk's offset
                            fileOut.Write(chunk.Item2, 0, chunk.Item2.Length); // Write the chunk data to the file
                            if (!quiet) Log($"Wrote 0x{chunk.Item2.Length.ToString("X")} at 0x{chunk.Item1.ToString("X")} to {file.Key}");
                            fileOut.Flush(); // Flush it out and dispose of the FileStream
                        }
                    } while (chunk == null);

                    position += realSize;
                    downloaded += realSize;
                    if (!quiet) Log($"Downloaded: {((((position * 100f) / file.Value.FileSize))).ToString("0.00")}% {position} / {file.Value.FileSize} total: {((((downloaded * 100f) / toDownload))).ToString("0.00")}% {downloaded} / {toDownload}");

                    chunk = null;
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true); // Try to GC if possible

                    return realSize;
                }).Sum(); // Get the sum of the chunked data

                if (writtenSize != file.Value.FileSize)
                    if (!quiet) Log($"ERROR, mismatch written and expected size");
                if (file.Value.FileSize != existingFileSize && file.Value.FileSize != writtenSize)
                {
                    if (!quiet) Log($"Existing file size does not match expected size, trimming to {file.Value.FileSize}");
                    using (FileStream fileOut = File.OpenWrite(filePath)) // Ensure no trailing excess data
                        fileOut.SetLength(file.Value.FileSize);
                }
                if (!quiet) Log($"{file.Key} Total: {writtenSize} Expected: {file.Value.FileSize}");
            }

            // Exit out of the console message processor
            running = false;
            consoleQueue.Join(); // Wait for console processor to exit
        }

        public static byte[] Decompress(byte[] data)
        {
            using (MemoryStream str = new MemoryStream(data))
                return Decompress(str);
        }
        public static byte[] Compress(byte[] data)
        {
            using (MemoryStream str = new MemoryStream(data))
                return Compress(str);
        }

        public static byte[] Decompress(Stream str)
        {
            using (MemoryStream result = new MemoryStream())
            using (ZlibStream inflate = new ZlibStream(str, SharpCompress.Compressors.CompressionMode.Decompress, true))
            {
                inflate.CopyTo(result);

                result.Position = 0;
                return result.ToArray();
            }
        }

        public static byte[] Compress(Stream str)
        {
            using (MemoryStream result = new MemoryStream())
            using (ZlibStream deflate = new ZlibStream(str, SharpCompress.Compressors.CompressionMode.Compress, SharpCompress.Compressors.Deflate.CompressionLevel.Level0, true, Encoding.ASCII))
            {
                deflate.CopyTo(result);

                result.Position = 0;
                return result.ToArray();
            }
        }
    }
}

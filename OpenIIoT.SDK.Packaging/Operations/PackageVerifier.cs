using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenIIoT.SDK.Package.Manifest;
using Utility.PGPSignatureTools;
using OpenIIoT.SDK.Packaging.Properties;
using OpenIIoT.SDK.Common;

namespace OpenIIoT.SDK.Package.Packaging.Operations
{
    public static class PackageVerifier
    {
        #region Private Fields

        /// <summary>
        ///     Raises the <see cref="Updated"/> event with a message of type <see cref="PackagingUpdateType.Info"/>.
        /// </summary>
        private static Action<string> Info = message => OnUpdated(PackagingUpdateType.Info, message);

        /// <summary>
        ///     Raises the <see cref="Updated"/> event with a message of type <see cref="PackagingUpdateType.Success"/>.
        /// </summary>
        private static Action<string> Success = message => OnUpdated(PackagingUpdateType.Success, message);

        /// <summary>
        ///     Raises the <see cref="Updated"/> event with a message of type <see cref="PackagingUpdateType.Verbose"/>.
        /// </summary>
        private static Action<string> Verbose = message => OnUpdated(PackagingUpdateType.Verbose, message);

        #endregion Private Fields

        #region Public Events

        public static event EventHandler<PackagingUpdateEventArgs> Updated;

        #endregion Public Events

        #region Public Methods

        /// <summary>
        ///     Verifies the specified Package.
        /// </summary>
        /// <param name="packageFile">The Package to verify.</param>
        /// <param name="publicKeyFile">The filename of the file containing the ASCII armored PGP private key.</param>
        public static void VerifyPackage(string packageFile, string publicKeyFile = "")
        {
            ArgumentValidator.ValidatePackageFileArgumentForReading(packageFile);

            if (!string.IsNullOrEmpty(publicKeyFile))
            {
                ArgumentValidator.ValidatePublicKeyArgument(publicKeyFile);
            }

            Info($"Verifying Package '{Path.GetFileName(packageFile)}'...");

            Exception deferredException = default(Exception);

            // looks like: temp\OpenIIoT.SDK\<Guid>\
            string tempDirectory = Path.Combine(Path.GetTempPath(), System.Reflection.Assembly.GetEntryAssembly().GetName().Name, Guid.NewGuid().ToString());

            try
            {
                Verbose($"Extracting package '{Path.GetFileName(packageFile)}' to temp directory '{tempDirectory}'");
                ZipFile.ExtractToDirectory(packageFile, tempDirectory);
                Verbose("Package extracted successfully.");

                Verbose("Checking extracted files...");
                string manifestFilename = Path.Combine(tempDirectory, Package.Constants.ManifestFilename);

                if (!File.Exists(manifestFilename))
                {
                    throw new FileNotFoundException("it does not contain a manifest.");
                }

                string payloadFilename = Path.Combine(tempDirectory, Package.Constants.PayloadArchiveName);

                if (!File.Exists(payloadFilename))
                {
                    throw new FileNotFoundException("it does not contain a payload archive.");
                }
                Verbose("Manifest and Payload Archive extracted successfully.");

                Verbose("Extracting Payload Archive...");
                ZipFile.ExtractToDirectory(payloadFilename, Path.Combine(tempDirectory, Package.Constants.PayloadDirectoryName));
                Verbose("Payload Archive extracted successfully.");

                Verbose("Checking extracted files...");
                string payloadDirectory = Path.Combine(tempDirectory, Package.Constants.PayloadDirectoryName);

                if (Directory.GetFiles(payloadDirectory).Length == 0)
                {
                    throw new FileNotFoundException("the payload directory does not contain any files.");
                }
                Verbose("Extracted files validated successfully.");

                Verbose($"Fetching manifest from '{manifestFilename}'...");
                PackageManifest manifest = ReadManifest(manifestFilename);
                Verbose("Manifest fetched successfully.");

                string verifiedTrust = string.Empty;

                // verify Trust
                if (!string.IsNullOrEmpty(manifest.Signature.Trust))
                {
                    Verbose("Verifying the Manifest Trust...");

                    if (string.IsNullOrEmpty(manifest.Signature.Digest))
                    {
                        throw new InvalidDataException("the Manifest is Trusted but it contains no Digest to trust.");
                    }

                    byte[] trustBytes = Encoding.ASCII.GetBytes(manifest.Signature.Trust);
                    byte[] verifiedTrustBytes;

                    try
                    {
                        verifiedTrustBytes = PGPSignature.Verify(trustBytes, Resources.PGPPublicKey);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidDataException($"an Exception was thrown while verifying the Trust: {ex.GetType().Name}: {ex.Message}");
                    }

                    verifiedTrust = Encoding.ASCII.GetString(verifiedTrustBytes);

                    if (manifest.Signature.Digest != verifiedTrust)
                    {
                        throw new InvalidDataException("the Manifest Trust is not valid; the Trusted Digest does not match the Digest in the Manifest.");
                    }

                    Verbose("Trust verified successfully.");
                }

                // verify Signature. start by determining the public key to use.
                string publicKey = string.Empty;
                string verifiedDigest = string.Empty;

                if (!string.IsNullOrEmpty(manifest.Signature.Digest))
                {
                    if (!string.IsNullOrEmpty(publicKeyFile))
                    {
                        publicKey = File.ReadAllText(publicKeyFile);
                    }
                    else
                    {
                        publicKey = FetchPublicKeyForUser(manifest.Signature.Subject);
                    }

                    byte[] digestBytes = Encoding.ASCII.GetBytes(manifest.Signature.Digest);
                    byte[] verifiedDigestBytes;

                    try
                    {
                        verifiedDigestBytes = PGPSignature.Verify(digestBytes, publicKey);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidDataException($"an Exception was thrown while verifying the Digest: {ex.GetType().Name}: {ex.Message}");
                    }

                    verifiedDigest = Encoding.ASCII.GetString(verifiedDigestBytes);

                    // remove the digest and trust from the manifest, then serialize it and compare it to the verified digest.
                    manifest.Signature.Digest = default(string);
                    manifest.Signature.Trust = default(string);

                    // if the scrubbed manifest and verified digest don't match, something was tampered with.
                    if (manifest.ToJson() != verifiedDigest)
                    {
                        throw new InvalidDataException("the Manifest Digest is not valid; the verified Digest does not match the Manifest.");
                    }

                    Verbose("Digest verified successfully.");
                }

                // TODO: validate files.

                Success("Package verified successfully.");
            }
            catch (Exception ex)
            {
                deferredException = new Exception($"Package '{packageFile}' is invalid: {ex.Message}");
            }
            finally
            {
                Verbose("Deleting temporary files...");
                Directory.Delete(tempDirectory, true);
                Verbose("Temporary files deleted successfully.");

                if (deferredException != default(Exception))
                {
                    throw deferredException;
                }
            }
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        ///     Fetches the PGP public key for the specified keybase.io username from the keybase.io API.
        /// </summary>
        /// <param name="username">The keybase.io username of the user for which the PGP public key is to be fetched..</param>
        /// <returns>The fetched PGP public key.</returns>
        /// <exception cref="WebException">Thrown when an error occurs fetching the key.</exception>
        public static string FetchPublicKeyForUser(string username)
        {
            string url = Constants.KeyUrlBase.Replace(Constants.KeyUrlPlaceholder, username);

            Verbose($"Fetching PGP key information from {url}...");

            try
            {
                using (WebClient client = new WebClient())
                {
                    string content = client.DownloadString(url);

                    Verbose($"Key information fetched.  Parsing primary public key...");

                    JObject key = JObject.Parse(content);
                    string publicKey = key["them"]["public_keys"]["primary"]["bundle"].ToString();

                    if (publicKey.Length < Constants.KeyMinimumLength)
                    {
                        throw new InvalidDataException($"The length of the retrieved key was not long enough (expected: >= {Constants.KeyMinimumLength}, actual: {publicKey.Length}) to be a valid PGP public key.");
                    }

                    Verbose($"Public key fetched successfully.");

                    return publicKey;
                }
            }
            catch (Exception ex)
            {
                throw new WebException($"Failed to retrieve the PGP Public Key for the package: '{url}': {ex.Message}");
            }
        }

        /// <summary>
        ///     Raises the <see cref="Updated"/> event with the specified message.
        /// </summary>
        /// <param name="message">The message to send.</param>
        private static void OnUpdated(PackagingUpdateType type, string message)
        {
            if (Updated != null)
            {
                Updated(null, new PackagingUpdateEventArgs(PackagingOperationType.ManifestExtraction, type, message));
            }
        }

        /// <summary>
        ///     Reads and deserializes the <see cref="PackageManifest"/> contains within the specified file.
        /// </summary>
        /// <param name="manifestFilename">the file from which to read and deserialize the Manifest.</param>
        /// <returns>The deserialized Manifest.</returns>
        private static PackageManifest ReadManifest(string manifestFilename)
        {
            try
            {
                return JsonConvert.DeserializeObject<PackageManifest>(File.ReadAllText(manifestFilename));
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"The contents of manifest file '{manifestFilename}' could not be read and deserialized: {ex.Message}");
            }
        }

        #endregion Private Methods
    }
}
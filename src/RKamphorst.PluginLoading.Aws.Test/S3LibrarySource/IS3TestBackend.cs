using Amazon.S3.Model;

namespace RKamphorst.PluginLoading.Aws.Test.S3LibrarySource;

using S3LibrarySource = Aws.S3LibrarySource;

public interface IS3TestBackend
{
    /// <summary>
    /// Setup: remove all versions under given prefix and define given object versions.
    /// </summary>
    /// <param name="prefix">Prefix to clean up</param>
    /// <param name="referenceDateTime">
    /// Reference datetime.
    ///
    /// Since the objects are newly created in a real bucket, the timestamps will
    /// not be the same as in <paramref name="versionsAndContent"/>.
    ///
    /// The datetime returned by this method has roughly the same offset with
    /// respect to <paramref name="versionsAndContent"/> as last-modified times in
    /// <paramref name="referenceDateTime"/>.
    /// 
    /// In particular, if a last-modified time in <paramref name="versionsAndContent"/>
    /// is before <paramref name="referenceDateTime"/>, the created object will have a
    /// last-modified timestamp before the returned datetime.
    /// </param>
    /// <param name="versionsAndContent">The list of object versions to set up.</param>
    /// <returns></returns>
    Task<DateTimeOffset> Setup(string prefix, DateTimeOffset referenceDateTime, List<(S3ObjectVersion Version, string? Content)> versionsAndContent);

    /// <summary>
    /// Create system under test
    /// </summary>
    /// <param name="prefix">The prefix relative to which the S3LibrarySource operates</param>
    /// <param name="dateTimeOffset">The "version at date" setting (optional)</param>
    /// <returns>An S3LibrarySource instance to test with</returns>
    S3LibrarySource CreateSystemUnderTest(string prefix, DateTimeOffset? versionAtDate = null);

    /// <summary>
    /// Verify invocations
    /// </summary>
    void VerifyAll();
}
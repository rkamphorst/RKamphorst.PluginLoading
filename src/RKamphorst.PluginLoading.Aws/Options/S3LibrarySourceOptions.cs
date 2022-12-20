namespace RKamphorst.PluginLoading.Aws.Options;

/// <summary>
/// Configuration options for <see cref="S3LibrarySource"/>
/// </summary>
public class S3LibrarySourceOptions
{
    /// <summary>
    /// S3 bucket where plugins are stored
    /// </summary>
    public string? S3Bucket { get; set; }
    
    /// <summary>
    /// Prefix under which plugins are stored in the S3 bucket
    /// </summary>
    public string? S3Prefix { get; set; }
    
    /// <summary>
    /// Operate on the plugin versions in S3 up to this date
    /// </summary>
    /// <remarks>
    /// This setting will make the s3 library source only consider object versions
    /// in S3 up to given datetime.
    /// If any versions appeared in the S3 bucket after this time, they will be
    /// disregarded.
    ///
    /// This is useful for rollbacks: if a badly behaving plugin
    /// is added to the bucket, a service can still be started with a VersionAtDate
    /// before the badly behaving plugin was added.
    ///
    /// If this option is not set, an attempt will be made to parse
    /// <see cref="VersionAtDateEnvironmentVariable"/>.
    ///
    /// If no VersionAtDate can be established, all versions (up to the newest)
    /// will be taken into account.
    /// </remarks>
    public DateTimeOffset? VersionAtDate { get; set; }

    /// <summary>
    /// Use environment variable as fallback if <see cref="VersionAtDate" is not set/>
    /// </summary>
    /// <remarks>
    /// If <see cref="VersionAtDate"/> is empty, attempt to parse the contents of this environment
    /// variable instead.
    /// 
    /// Default value: <see cref="Constants.DefaultVersionAtDateEnvironmentVariable"/>
    /// </remarks>
    public string? VersionAtDateEnvironmentVariable { get; set; } = Constants.DefaultVersionAtDateEnvironmentVariable;
}
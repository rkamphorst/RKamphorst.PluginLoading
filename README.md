# Plugin Loading

A plugin is a class that implements a contract,  is distributed in 
a "plugin library" (a zip), which is loadable at run time into an 
application that knows of that same contract.

This repository contains libraries for the loading of plugins in .NET 6. 

How to load a plugin you can see in the [sample program](./src//Sample.Api/Program.cs).

A plugin library can be loaded from any source that implements 
[IPluginStore](./src/RKamphorst.PluginLoading/IPluginStore.cs). 
[S3Librarysource](./src/RKamphorst.PluginLoading.Aws/S3LibrarySource.cs)
implements the loading of plugins in zip packages from an AWS S3 
bucket.


This repository publishes the following NuGet packages:

* [RKamphorst.PluginLoading](./src/RKamphorst.PluginLoading),
  [RKamphorst.PluginLoading.Contract](./src/RKamphorst.PluginLoading)    
  Core plugin loading code and contract
* [RKamphorst.PluginLoading.DependencyInjection](./src/RKamphorst.PluginLoading.DependencyInjection/)    
  Support for plugin loading with Microsoft Dependency Injection
* [RKamphorst.PluginConfiguration](./src/RKamphorst.PluginConfiguration/), 
  [RKamphorst.PluginConfiguration.Contract](./src/RKamphorst.PluginConfiguration.Contract/)    
  Support for per-plugin configuration
* [RKamphorst.PluginLoading.Aws](./src/RKamphorst.PluginLoading.Aws), 
  [Rkamphorst.PluginLoading.Aws.DependencyInjection](./src/Rkamphorst.PluginLoading.Aws.DependencyInjection)    
  Implementations for plugin loading on AWS infrastructure, e.g. loading plugins from S3

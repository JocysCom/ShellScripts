# Transform script for XML configuration files

Visual Studio provides good way to manage configuration files for different environments for Web Application Projects. Visual Studio creates 3 files:
1. _Web.config_ file, which contains shared properties for all environments.
2. _Web.Debug.config_ file for use in developing environment (contains only transformation changes).
3. _Web.Release.config_ file for use in production environment (contains only transformation changes).

![https://raw.githubusercontent.com/wiki/JocysCom/XmlTransform/Images/WebConfig.png](https://raw.githubusercontent.com/wiki/JocysCom/XmlTransform/Images/WebConfig.png)

You can add more environments and there are extensions, which will provide similar functionality for other project types:
https://marketplace.visualstudio.com/items?itemName=GolanAvraham.ConfigurationTransform

![https://raw.githubusercontent.com/wiki/JocysCom/XmlTransform/Images/VsExtension.jpg](https://raw.githubusercontent.com/wiki/JocysCom/XmlTransform/Images/VsExtension.jpg)

Good news is that with some extra steps, you can reuse configuration file transform feature in other project types and without using any third party Visual Studio extension.

For example, you can reorganize a litte bit and create:

1. _App.Transform.Source.config_ file which will contain configuration shared between DEV, TEST and LIVE environments.
2. And put all differences in _App.[Dev|Live|Test].config_ files:
Note: You can use File Nesting extension to put files as children of other file.
https://marketplace.visualstudio.com/items?itemName=MadsKristensen.FileNesting

![https://raw.githubusercontent.com/wiki/JocysCom/XmlTransform/Images/AppConfig.png](https://raw.githubusercontent.com/wiki/JocysCom/XmlTransform/Images/AppConfig.png)

3. Then, add configuration block into _*.csproj_ file, which will create final configuration _App.Transform.Destination.[Dev|Test|Live].config_ files for each environments during project builds:

```
<Project>
...
  <UsingTask TaskName="TransformXml" AssemblyFile="$(MSBuildExtensionsPath)\Microsoft\VisualStudio\v$(MSBuildToolsVersion)\Web\Microsoft.Web.Publishing.Tasks.dll" />
  <!-- To modify your build process, add your task inside one of the targets below and uncomment it. 
       Other similar extension points exist, see Microsoft.Common.targets. -->
  <Target Name="BeforeCompile">
    <!-- Happens after PreBuildEvent but BeforeCompile -->
    <TransformXml Source="App.Transform.Source.config" Transform="App.Dev.config" Destination="App.Transform.Destination.Dev.config" />
    <TransformXml Source="App.Transform.Source.config" Transform="App.Test.config" Destination="App.Transform.Destination.Test.config" />
    <TransformXml Source="App.Transform.Source.config" Transform="App.Live.config" Destination="App.Transform.Destination.Live.config" />
  </Target>
</Project>
```

You can also exclude _App.config_ from source control:

![https://raw.githubusercontent.com/wiki/JocysCom/XmlTransform/Images/AppConfigSourceControl.png](https://raw.githubusercontent.com/wiki/JocysCom/XmlTransform/Images/AppConfigSourceControl.png) 

…and add one line to Pre-build event on Project properties, which will restore missing App.config file after you acquire clean Project from the Source Control:

        if NOT EXIST "$(ProjectDir)App.config" copy "$(ProjectDir)App.Transform.Destination.Dev.config" "$(ProjectDir)App.config"

![https://raw.githubusercontent.com/wiki/JocysCom/XmlTransform/Images/BuildEventScript.png](https://raw.githubusercontent.com/wiki/JocysCom/XmlTransform/Images/BuildEventScript.png) 

---
## What are benefits of this system?

a)	Organized and smaller configuration files.
b)	Source control client won’t give you warnings about changed _Web.config_ or _App.Config_ file after you switch project environment.

---
## How to use it in Visual Studio project Publishing Scripts?

This is example of publishing script line, which will copy correct configuration file into specific environment:

For example to _MyProject (TEST).pubxml_:

```
<Target Name="CustomAfterPublish" AfterTargets="GatherAllFilesToPublish">
  <Message Text="********************************** GatherAllFilesToPublish ***********************************" Importance="high"/>
  <Exec Command="copy /y &quot;$(ProjectDir)App.Transform.Destination.Test.config&quot; &quot;$(ProjectDir)obj\$(ConfigurationName)\Package\PackageTmp\Web.config&quot;" />
</Target>
```

---
## How transform configuration files outside the Visual Studio?

You can drop _XmlTransfrom.bat_ and _XmlTransform.cs_ script files into any folder, which contains projects and solutions.

![https://raw.githubusercontent.com/wiki/JocysCom/XmlTransform/Images/FolderList.png](https://raw.githubusercontent.com/wiki/JocysCom/XmlTransform/Images/FolderList.png) 

If you start _XmlTransform.bat_ then it will execute _XmlTransform.cs_ C# script, which will:

1. Search for all configuration files it can find by using pattern _[App|Web].<Environment>.config_
2. Offer you the choice to which environment you would like to switch your projects.
3. After your choice, it will transform all configuration files for all environments and update _App.config_ or _Web.config_ to environment of your choice.

![https://raw.githubusercontent.com/wiki/JocysCom/XmlTransform/Images/Console.png](https://raw.githubusercontent.com/wiki/JocysCom/XmlTransform/Images/Console.png) 

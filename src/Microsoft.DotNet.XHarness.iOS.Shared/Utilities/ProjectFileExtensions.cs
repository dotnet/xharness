// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.DotNet.XHarness.Common.Utilities;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Utilities
{
    public static class ProjectFileExtensions
    {
        private const string MSBuild_Namespace = "http://schemas.microsoft.com/developer/msbuild/2003";

        public static void SetProjectTypeGuids(this XmlDocument csproj, string value) => SetNode(csproj, "ProjectTypeGuids", value);

        public static string GetProjectGuid(this XmlDocument csproj) => csproj.SelectSingleNode("/*/*/*[local-name() = 'ProjectGuid']").InnerText;

        public static void SetProjectGuid(this XmlDocument csproj, string value) => csproj.SelectSingleNode("/*/*/*[local-name() = 'ProjectGuid']").InnerText = value;

        public static string GetOutputType(this XmlDocument csproj) => csproj.SelectSingleNode("/*/*/*[local-name() = 'OutputType']").InnerText;

        public static void SetOutputType(this XmlDocument csproj, string value) => csproj.SelectSingleNode("/*/*/*[local-name() = 'OutputType']").InnerText = value;

        private static readonly string[] s_eqsplitter = new string[] { "==" };
        private static readonly string[] s_orsplitter = new string[] { " Or " };
        private static readonly char[] s_pipesplitter = new char[] { '|' };
        private static readonly char[] s_trimchars = new char[] { '\'', ' ' };

        private static void ParseConditions(this XmlNode node, out string platform, out string configuration)
        {
            // This parses the platform/configuration out of conditions like this:
            //
            // Condition=" '$(Configuration)|$(Platform)' == 'Debug|iPhoneSimulator' "
            //
            platform = "Any CPU";
            configuration = "Debug";
            while (node != null)
            {
                if (node.Attributes != null)
                {
                    XmlAttribute conditionAttribute = node.Attributes["Condition"];
                    if (conditionAttribute != null)
                    {
                        string condition = conditionAttribute.Value;
                        string[] eqsplit = condition.Split(s_eqsplitter, StringSplitOptions.None);
                        if (eqsplit.Length == 2)
                        {
                            string[] left = eqsplit[0].Trim(s_trimchars).Split(s_pipesplitter);
                            string[] right = eqsplit[1].Trim(s_trimchars).Split(s_pipesplitter);
                            if (left.Length == right.Length)
                            {
                                for (int i = 0; i < left.Length; i++)
                                {
                                    switch (left[i])
                                    {
                                        case "$(Configuration)":
                                            configuration = right[i];
                                            break;
                                        case "$(Platform)":
                                            platform = right[i];
                                            break;
                                        default:
                                            throw new Exception(string.Format("Unknown condition logic: {0}", left[i]));
                                    }
                                }
                            }
                        }

                        if (string.IsNullOrEmpty(platform) || string.IsNullOrEmpty(configuration))
                            throw new Exception(string.Format("Could not parse the condition: {0}", conditionAttribute.Value));
                    }
                }
                node = node.ParentNode;
            }

            if (string.IsNullOrEmpty(platform) || string.IsNullOrEmpty(configuration))
                throw new Exception("Could not find a condition attribute.");
        }

        public static void SetOutputPath(this XmlDocument csproj, string value, bool expand = true)
        {
            XmlNodeList nodes = csproj.SelectNodes("/*/*/*[local-name() = 'OutputPath']");
            if (nodes.Count == 0)
                throw new Exception("Could not find node OutputPath");
            foreach (XmlNode n in nodes)
            {
                if (expand)
                {
                    // OutputPath needs to be expanded, otherwise Xamarin Studio isn't able to launch the project.
                    ParseConditions(n, out string platform, out string configuration);
                    n.InnerText = value.Replace("$(Platform)", platform).Replace("$(Configuration)", configuration);
                }
                else
                {
                    n.InnerText = value;
                }
            }
        }

        private static bool IsNodeApplicable(XmlNode node, string platform, string configuration)
        {
            while (node != null)
            {
                if (!EvaluateCondition(node, platform, configuration))
                    return false;
                node = node.ParentNode;
            }
            return true;
        }

        private static bool EvaluateCondition(XmlNode node, string platform, string configuration)
        {
            if (node.Attributes == null)
                return true;

            XmlAttribute condition = node.Attributes["Condition"];
            if (condition == null)
                return true;

            string conditionValue = condition.Value;
            if (configuration != null)
                conditionValue = conditionValue.Replace("$(Configuration)", configuration);
            if (platform != null)
                conditionValue = conditionValue.Replace("$(Platform)", platform);

            string[] orsplits = conditionValue.Split(s_orsplitter, StringSplitOptions.None);
            foreach (string orsplit in orsplits)
            {
                string[] eqsplit = orsplit.Split(s_eqsplitter, StringSplitOptions.None);
                if (eqsplit.Length != 2)
                {
                    Console.WriteLine("Could not parse condition; {0}", conditionValue);
                    return false;
                }

                string left = eqsplit[0].Trim(s_trimchars);
                string right = eqsplit[1].Trim(s_trimchars);
                if (left == right)
                    return true;
            }

            return false;
        }

        public static string GetOutputPath(this XmlDocument csproj, string platform, string configuration) => GetElementValue(csproj, platform, configuration, "OutputPath");

        public static string GetMtouchArch(this XmlDocument csproj, string platform, string configuration) => GetElementValue(csproj, platform, configuration, "MtouchArch");

        private static string GetElementValue(this XmlDocument csproj, string platform, string configuration, string elementName)
        {
            XmlNodeList nodes = csproj.SelectNodes($"/*/*/*[local-name() = '{elementName}']");
            if (nodes.Count == 0)
                throw new Exception($"Could not find node {elementName}");
            foreach (XmlNode n in nodes)
            {
                if (IsNodeApplicable(n, platform, configuration))
                    return n.InnerText.Replace("$(Platform)", platform).Replace("$(Configuration)", configuration);
            }
            throw new Exception($"Could not find {elementName}");
        }

        public static string GetOutputAssemblyPath(this XmlDocument csproj, string platform, string configuration)
        {
            string outputPath = GetOutputPath(csproj, platform, configuration);
            string assemblyName = GetElementValue(csproj, platform, configuration, "AssemblyName");
            string outputType = GetElementValue(csproj, platform, configuration, "OutputType");
            string extension = (outputType.ToLowerInvariant()) switch
            {
                "library" => "dll",
                "exe" => "exe",
                _ => throw new NotImplementedException(outputType),
            };
            return outputPath + "\\" + assemblyName + "." + extension; // MSBuild-style paths.
        }

        public static void SetIntermediateOutputPath(this XmlDocument csproj, string value)
        {
            // Set any existing IntermediateOutputPath
            XmlNodeList nodes = csproj.SelectNodes("/*/*/*[local-name() = 'IntermediateOutputPath']");
            bool hasToplevel = false;
            if (nodes.Count != 0)
            {
                foreach (XmlNode n in nodes)
                {
                    n.InnerText = value;
                    hasToplevel |= n.Attributes["Condition"] == null;
                }
            }

            if (hasToplevel)
                return;

            // Make sure there's a top-level version too.
            XmlNode property_group = csproj.SelectSingleNode("/*/*[local-name() = 'PropertyGroup' and not(@Condition)]");

            XmlElement intermediateOutputPath = csproj.CreateElement("IntermediateOutputPath", MSBuild_Namespace);
            intermediateOutputPath.InnerText = value;
            property_group.AppendChild(intermediateOutputPath);
        }

        public static void SetTargetFrameworkIdentifier(this XmlDocument csproj, string value) => SetTopLevelPropertyGroupValue(csproj, "TargetFrameworkIdentifier", value);

        public static void SetTopLevelPropertyGroupValue(this XmlDocument csproj, string key, string value)
        {
            XmlNode firstPropertyGroups = csproj.SelectNodes("//*[local-name() = 'PropertyGroup']")[0];
            XmlNode targetFrameworkIdentifierNode = firstPropertyGroups.SelectSingleNode(string.Format("//*[local-name() = '{0}']", key));
            if (targetFrameworkIdentifierNode != null)
            {
                SetNode(csproj, key, value);
            }
            else
            {
                XmlElement mea = csproj.CreateElement(key, MSBuild_Namespace);
                mea.InnerText = value;
                firstPropertyGroups.AppendChild(mea);
            }
        }

        public static void RemoveTargetFrameworkIdentifier(this XmlDocument csproj)
        {
            try
            {
                RemoveNode(csproj, "TargetFrameworkIdentifier");
            }
            catch
            {
                // ignore exceptions, if not present, we are not worried
            }
        }

        public static void SetAssemblyName(this XmlDocument csproj, string value) => SetNode(csproj, "AssemblyName", value);

        public static string GetAssemblyName(this XmlDocument csproj) => csproj.SelectSingleNode("/*/*/*[local-name() = 'AssemblyName']").InnerText;

        public static void SetPlatformAssembly(this XmlDocument csproj, string value) => SetAssemblyReference(csproj, "Xamarin.iOS", value);

        public static void SetAssemblyReference(this XmlDocument csproj, string current, string value)
        {
            XmlNode project = csproj.ChildNodes[1];
            XmlNode reference = csproj.SelectSingleNode("/*/*/*[local-name() = 'Reference' and @Include = '" + current + "']");
            if (reference != null)
                reference.Attributes["Include"].Value = value;
        }

        public static void RemoveReferences(this XmlDocument csproj, string projectName)
        {
            XmlNode reference = csproj.SelectSingleNode("/*/*/*[local-name() = 'Reference' and @Include = '" + projectName + "']");
            if (reference != null)
                reference.ParentNode.RemoveChild(reference);
        }

        public static void AddCompileInclude(this XmlDocument csproj, string link, string include, bool prepend = false)
        {
            XmlNode compile_node = csproj.SelectSingleNode("//*[local-name() = 'Compile']");
            XmlNode item_group = compile_node.ParentNode;

            XmlElement node = csproj.CreateElement("Compile", MSBuild_Namespace);
            XmlAttribute include_attribute = csproj.CreateAttribute("Include");
            include_attribute.Value = include;
            node.Attributes.Append(include_attribute);
            XmlElement linkElement = csproj.CreateElement("Link", MSBuild_Namespace);
            linkElement.InnerText = link;
            node.AppendChild(linkElement);
            if (prepend)
                item_group.PrependChild(node);
            else
                item_group.AppendChild(node);
        }

        public static void FixCompileInclude(this XmlDocument csproj, string include, string newInclude) => csproj.SelectSingleNode($"//*[local-name() = 'Compile' and @Include = '{include}']").Attributes["Include"].Value = newInclude;

        public static void AddInterfaceDefinition(this XmlDocument csproj, string include)
        {
            XmlNode itemGroup = csproj.CreateItemGroup();
            XmlElement id = csproj.CreateElement("InterfaceDefinition", MSBuild_Namespace);
            XmlAttribute attrib = csproj.CreateAttribute("Include");
            attrib.Value = include;
            id.Attributes.Append(attrib);
            itemGroup.AppendChild(id);
        }

        public static void SetImport(this XmlDocument csproj, string value)
        {
            XmlNodeList imports = csproj.SelectNodes("/*/*[local-name() = 'Import'][not(@Condition)]");
            if (imports.Count != 1)
                throw new Exception("More than one import");
            imports[0].Attributes["Project"].Value = value;
        }

        public static void SetExtraLinkerDefs(this XmlDocument csproj, string value)
        {
            XmlNodeList mtouchExtraArgs = csproj.SelectNodes("//*[local-name() = 'MtouchExtraArgs']");
            foreach (XmlNode mea in mtouchExtraArgs)
                mea.InnerText = mea.InnerText.Replace("extra-linker-defs.xml", value);
            XmlNodeList nones = csproj.SelectNodes("//*[local-name() = 'None' and @Include = 'extra-linker-defs.xml']");
            foreach (XmlNode none in nones)
                none.Attributes["Include"].Value = value;
        }

        public static void AddExtraMtouchArgs(this XmlDocument csproj, string value, string platform, string configuration) => AddToNode(csproj, "MtouchExtraArgs", value, platform, configuration);

        public static void AddMonoBundlingExtraArgs(this XmlDocument csproj, string value, string platform, string configuration) => AddToNode(csproj, "MonoBundlingExtraArgs", value, platform, configuration);

        public static void AddToNode(this XmlDocument csproj, string node, string value, string platform, string configuration)
        {
            XmlNodeList nodes = csproj.SelectNodes($"//*[local-name() = '{node}']");
            bool found = false;
            foreach (XmlNode mea in nodes)
            {
                if (!IsNodeApplicable(mea, platform, configuration))
                    continue;

                if (mea.InnerText.Length > 0 && mea.InnerText[mea.InnerText.Length - 1] != ' ')
                    mea.InnerText += " ";
                mea.InnerText += value;
                found = true;
            }

            if (found)
                return;

            // The project might not have this node, so create one of none was found.
            XmlNodeList propertyGroups = csproj.SelectNodes("//*[local-name() = 'PropertyGroup' and @Condition]");
            foreach (XmlNode pg in propertyGroups)
            {
                if (!EvaluateCondition(pg, platform, configuration))
                    continue;

                XmlElement mea = csproj.CreateElement(node, MSBuild_Namespace);
                mea.InnerText = value;
                pg.AppendChild(mea);
            }
        }

        public static string GetMtouchLink(this XmlDocument csproj, string platform, string configuration) => GetNode(csproj, "MtouchLink", platform, configuration);

        public static void SetMtouchUseLlvm(this XmlDocument csproj, bool value, string platform, string configuration) => SetNode(csproj, "MtouchUseLlvm", true ? "true" : "false", platform, configuration);

        public static void SetMtouchUseBitcode(this XmlDocument csproj, bool value, string platform, string configuration) => SetNode(csproj, "MtouchEnableBitcode", true ? "true" : "false", platform, configuration);

        public static IEnumerable<XmlNode> GetPropertyGroups(this XmlDocument csproj, string platform, string configuration)
        {
            XmlNodeList propertyGroups = csproj.SelectNodes("//*[local-name() = 'PropertyGroup' and @Condition]");
            foreach (XmlNode node in propertyGroups)
            {
                if (!EvaluateCondition(node, platform, configuration))
                    continue;

                yield return node;
            }
        }

        public static void SetNode(this XmlDocument csproj, string node, string value, string platform, string configuration)
        {
            IEnumerable<XmlNode> projnode = csproj.SelectElementNodes(node);
            bool found = false;
            foreach (XmlNode xmlnode in projnode)
            {
                if (!IsNodeApplicable(xmlnode, platform, configuration))
                    continue;

                xmlnode.InnerText = value;
                found = true;
            }

            if (found)
                return;

            // Not all projects have a MtouchExtraArgs node, so create one of none was found.
            XmlNodeList propertyGroups = csproj.SelectNodes("//*[local-name() = 'PropertyGroup' and @Condition]");
            foreach (XmlNode pg in propertyGroups)
            {
                if (!EvaluateCondition(pg, platform, configuration))
                    continue;

                XmlElement mea = csproj.CreateElement(node, MSBuild_Namespace);
                mea.InnerText = value;
                pg.AppendChild(mea);
            }
        }

        private static string GetNode(this XmlDocument csproj, string name, string platform, string configuration)
        {
            foreach (XmlNode pg in GetPropertyGroups(csproj, platform, configuration))
            {
                foreach (XmlNode node in pg.ChildNodes)
                    if (node.Name == name)
                        return node.InnerText;
            }

            return null;
        }

        public static string GetImport(this XmlDocument csproj)
        {
            XmlNodeList imports = csproj.SelectNodes("/*/*[local-name() = 'Import'][not(@Condition)]");
            if (imports.Count != 1)
                throw new Exception("More than one import");
            return imports[0].Attributes["Project"].Value;
        }

        public delegate bool FixReferenceDelegate(string reference, out string fixed_reference);
        public static void FixProjectReferences(this XmlDocument csproj, string suffix, FixReferenceDelegate fixCallback = null)
        {
            XmlNodeList nodes = csproj.SelectNodes("/*/*/*[local-name() = 'ProjectReference']");
            if (nodes.Count == 0)
                return;
            foreach (XmlNode n in nodes)
            {
                string name = n["Name"].InnerText;
                string fixed_name = null;
                if (fixCallback != null && !fixCallback(name, out fixed_name))
                    continue;
                XmlAttribute include = n.Attributes["Include"];
                string fixed_include;
                if (fixed_name == null)
                {
                    fixed_include = include.Value;
                    fixed_include = fixed_include.Replace(".csproj", suffix + ".csproj");
                    fixed_include = fixed_include.Replace(".fsproj", suffix + ".fsproj");
                }
                else
                {
                    string unix_path = include.Value.Replace('\\', '/');
                    string unix_dir = System.IO.Path.GetDirectoryName(unix_path);
                    fixed_include = System.IO.Path.Combine(unix_dir, fixed_name + System.IO.Path.GetExtension(unix_path));
                    fixed_include = fixed_include.Replace('/', '\\');
                }
                n.Attributes["Include"].Value = fixed_include;
                XmlElement nameElement = n["Name"];
                name = System.IO.Path.GetFileNameWithoutExtension(fixed_include.Replace('\\', '/'));
                nameElement.InnerText = name;
            }
        }

        public static void FixTestLibrariesReferences(this XmlDocument csproj, string platform)
        {
            XmlNodeList nodes = csproj.SelectNodes("//*[local-name() = 'ObjcBindingNativeLibrary' or local-name() = 'ObjcBindingNativeFramework']");
            string[] test_libraries = new string[] {
                "libtest.a",
                "libtest2.a",
                "XTest.framework",
                "XStaticArTest.framework",
                "XStaticObjectTest.framework"
            };
            foreach (XmlNode node in nodes)
            {
                XmlAttribute includeAttribute = node.Attributes["Include"];
                if (includeAttribute != null)
                {
                    foreach (string tl in test_libraries)
                        includeAttribute.Value = includeAttribute.Value.Replace($"test-libraries\\.libs\\ios-fat\\{tl}", $"test-libraries\\.libs\\{platform}-fat\\{tl}");
                }
            }
            nodes = csproj.SelectNodes("//*[local-name() = 'Target' and @Name = 'BeforeBuild']");
            foreach (XmlNode node in nodes)
            {
                XmlAttribute outputsAttribute = node.Attributes["Outputs"];
                if (outputsAttribute != null)
                {
                    foreach (string tl in test_libraries)
                        outputsAttribute.Value = outputsAttribute.Value.Replace($"test-libraries\\.libs\\ios-fat\\${tl}", $"test-libraries\\.libs\\{platform}-fat\\${tl}");
                }
            }
        }

        public static void FixArchitectures(this XmlDocument csproj, string simulator_arch, string device_arch, string platform = null, string configuration = null)
        {
            XmlNodeList nodes = csproj.SelectNodes("/*/*/*[local-name() = 'MtouchArch']");
            if (nodes.Count == 0)
                throw new Exception(string.Format("Could not find MtouchArch at all"));
            foreach (XmlNode n in nodes)
            {
                if (platform != null && configuration != null && !IsNodeApplicable(n, platform, configuration))
                    continue;
                switch (n.InnerText.ToLower())
                {
                    case "i386":
                    case "x86_64":
                    case "i386, x86_64":
                        n.InnerText = simulator_arch;
                        break;
                    case "armv7":
                    case "armv7s":
                    case "arm64":
                    case "arm64_32":
                    case "armv7k":
                    case "armv7, arm64":
                    case "armv7k, arm64_32":
                        n.InnerText = device_arch;
                        break;
                    default:
                        throw new NotImplementedException(string.Format("Unhandled architecture: {0}", n.InnerText));

                }
            }
        }

        public static void FindAndReplace(this XmlDocument csproj, string find, string replace) => FindAndReplace(csproj.ChildNodes, find, replace);

        private static void FindAndReplace(XmlNode node, string find, string replace)
        {
            if (node.HasChildNodes)
            {
                FindAndReplace(node.ChildNodes, find, replace);
            }
            else
            {
                if (node.NodeType == XmlNodeType.Text)
                    node.InnerText = node.InnerText.Replace(find, replace);
            }
            if (node.Attributes != null)
            {
                foreach (XmlAttribute attrib in node.Attributes)
                    attrib.Value = attrib.Value.Replace(find, replace);
            }
        }

        private static void FindAndReplace(XmlNodeList nodes, string find, string replace)
        {
            foreach (XmlNode node in nodes)
                FindAndReplace(node, find, replace);
        }

        public static void FixInfoPListInclude(this XmlDocument csproj, string suffix, string fullPath = null, string newName = null)
        {
            XmlNode import = GetInfoPListNode(csproj, false);
            if (import != null)
            {
                string value = import.Attributes["Include"].Value;
                string unixValue = value.Replace('\\', '/');
                string fname = Path.GetFileName(unixValue);
                if (newName == null)
                {
                    if (string.IsNullOrEmpty(fullPath))
                        newName = value.Replace(fname, $"Info{suffix}.plist");
                    else
                        newName = value.Replace(fname, $"{fullPath}\\Info{suffix}.plist");
                }
                import.Attributes["Include"].Value = (!Path.IsPathRooted(unixValue)) ? value.Replace(fname, newName) : newName;

                XmlNode logicalName = import.SelectSingleNode("./*[local-name() = 'LogicalName']");
                if (logicalName == null)
                {
                    logicalName = csproj.CreateElement("LogicalName", MSBuild_Namespace);
                    import.AppendChild(logicalName);
                }
                logicalName.InnerText = "Info.plist";
            }
        }

        private static XmlNode GetInfoPListNode(this XmlDocument csproj, bool throw_if_not_found = false)
        {
            XmlNodeList logicalNames = csproj.SelectNodes("//*[local-name() = 'LogicalName']");
            foreach (XmlNode ln in logicalNames)
            {
                if (!ln.InnerText.Contains("Info.plist"))
                    continue;
                return ln.ParentNode;
            }
            XmlNodeList nodes = csproj.SelectNodes("//*[local-name() = 'None' and contains(@Include ,'Info.plist')]");
            if (nodes.Count > 0)
            {
                return nodes[0]; // return the value, which could be Info.plist or a full path (linked).
            }
            nodes = csproj.SelectNodes("//*[local-name() = 'None' and contains(@Include ,'Info-tv.plist')]");
            if (nodes.Count > 0)
            {
                return nodes[0]; // return the value, which could be Info.plist or a full path (linked).
            }
            if (throw_if_not_found)
                throw new Exception($"Could not find Info.plist include.");
            return null;
        }

        public static string GetInfoPListInclude(this XmlDocument csproj) => GetInfoPListNode(csproj).Attributes["Include"].Value;

        public static IEnumerable<string> GetProjectReferences(this XmlDocument csproj)
        {
            XmlNodeList nodes = csproj.SelectNodes("//*[local-name() = 'ProjectReference']");
            foreach (XmlNode node in nodes)
                yield return node.Attributes["Include"].Value;
        }

        public static IEnumerable<string> GetExtensionProjectReferences(this XmlDocument csproj)
        {
            XmlNodeList nodes = csproj.SelectNodes("//*[local-name() = 'ProjectReference']");
            foreach (XmlNode node in nodes)
            {
                if (node.SelectSingleNode("./*[local-name () = 'IsAppExtension']") != null)
                    yield return node.Attributes["Include"].Value;
            }
        }

        public static IEnumerable<string> GetNunitAndXunitTestReferences(this XmlDocument csproj)
        {
            XmlNodeList nodes = csproj.SelectNodes("//*[local-name() = 'Reference']");
            foreach (XmlNode node in nodes)
            {
                string includeValue = node.Attributes["Include"].Value;
                if (includeValue.EndsWith("_test.dll", StringComparison.Ordinal) || includeValue.EndsWith("_xunit-test.dll", StringComparison.Ordinal))
                    yield return includeValue;
            }
        }

        public static void SetProjectReferenceValue(this XmlDocument csproj, string projectInclude, string node, string value)
        {
            XmlNode nameNode = csproj.SelectSingleNode("//*[local-name() = 'ProjectReference' and @Include = '" + projectInclude + "']/*[local-name() = '" + node + "']");
            nameNode.InnerText = value;
        }

        public static void SetProjectReferenceInclude(this XmlDocument csproj, string projectInclude, string value)
        {
            IEnumerable<XmlNode> elements = csproj.SelectElementNodes("ProjectReference");
            elements
                  .Where((v) =>
                   {
                       XmlAttribute attrib = v.Attributes["Include"];
                       if (attrib == null)
                           return false;
                       return attrib.Value == projectInclude;
                   })
                  .Single()
                  .Attributes["Include"].Value = value;
        }

        public static void CreateProjectReferenceValue(this XmlDocument csproj, string existingInclude, string path, string guid, string name)
        {
            XmlNode referenceNode = csproj.SelectSingleNode("//*[local-name() = 'Reference' and @Include = '" + existingInclude + "']");
            XmlElement projectReferenceNode = csproj.CreateElement("ProjectReference", MSBuild_Namespace);
            XmlAttribute includeAttribute = csproj.CreateAttribute("Include");
            includeAttribute.Value = path.Replace('/', '\\');
            projectReferenceNode.Attributes.Append(includeAttribute);
            XmlElement projectNode = csproj.CreateElement("Project", MSBuild_Namespace);
            projectNode.InnerText = guid;
            projectReferenceNode.AppendChild(projectNode);
            XmlElement nameNode = csproj.CreateElement("Name", MSBuild_Namespace);
            nameNode.InnerText = name;
            projectReferenceNode.AppendChild(nameNode);

            XmlNode itemGroup;
            if (referenceNode != null)
            {
                itemGroup = referenceNode.ParentNode;
                referenceNode.ParentNode.RemoveChild(referenceNode);
            }
            else
            {
                itemGroup = csproj.CreateElement("ItemGroup", MSBuild_Namespace);
                csproj.SelectSingleNode("//*[local-name() = 'Project']").AppendChild(itemGroup);
            }
            itemGroup.AppendChild(projectReferenceNode);
        }

        private static XmlNode CreateItemGroup(this XmlDocument csproj)
        {
            XmlNode lastItemGroup = csproj.SelectSingleNode("//*[local-name() = 'ItemGroup'][last()]");
            XmlElement newItemGroup = csproj.CreateElement("ItemGroup", MSBuild_Namespace);
            lastItemGroup.ParentNode.InsertAfter(newItemGroup, lastItemGroup);
            return newItemGroup;
        }

        public static void AddAdditionalDefines(this XmlDocument csproj, string value)
        {
            XmlNode mainPropertyGroup = csproj.SelectSingleNode("//*[local-name() = 'PropertyGroup' and not(@Condition)]");
            XmlNode mainDefine = mainPropertyGroup.SelectSingleNode("*[local-name() = 'DefineConstants']");
            if (mainDefine == null)
            {
                mainDefine = csproj.CreateElement("DefineConstants", MSBuild_Namespace);
                mainDefine.InnerText = value;
                mainPropertyGroup.AppendChild(mainDefine);
            }
            else
            {
                mainDefine.InnerText = mainDefine.InnerText + ";" + value;
            }

            // make sure all other DefineConstants include the main one
            XmlNodeList otherDefines = csproj.SelectNodes("//*[local-name() = 'PropertyGroup' and @Condition]/*[local-name() = 'DefineConstants']");
            foreach (XmlNode def in otherDefines)
            {
                if (!def.InnerText.Contains("$(DefineConstants"))
                    def.InnerText += ";$(DefineConstants)";
            }
        }

        public static void RemoveDefines(this XmlDocument csproj, string defines, string platform = null, string configuration = null)
        {
            char[] separator = new char[] { ';' };
            string[] defs = defines.Split(separator, StringSplitOptions.RemoveEmptyEntries);
            XmlNodeList projnode = csproj.SelectNodes("//*[local-name() = 'PropertyGroup']/*[local-name() = 'DefineConstants']");
            foreach (XmlNode xmlnode in projnode)
            {
                if (string.IsNullOrEmpty(xmlnode.InnerText))
                    continue;

                XmlNode parent = xmlnode.ParentNode;
                if (!IsNodeApplicable(parent, platform, configuration))
                    continue;

                string[] existing = xmlnode.InnerText.Split(separator, StringSplitOptions.RemoveEmptyEntries);
                bool any = false;
                foreach (string def in defs)
                {
                    for (int i = 0; i < existing.Length; i++)
                    {
                        if (existing[i] == def)
                        {
                            existing[i] = null;
                            any = true;
                        }
                    }
                }
                if (!any)
                    continue;
                xmlnode.InnerText = string.Join(separator[0].ToString(), existing.Where((v) => !string.IsNullOrEmpty(v)));
            }
        }

        public static void AddAdditionalDefines(this XmlDocument csproj, string value, string platform, string configuration)
        {
            XmlNodeList projnode = csproj.SelectNodes("//*[local-name() = 'PropertyGroup' and @Condition]/*[local-name() = 'DefineConstants']");
            foreach (XmlNode xmlnode in projnode)
            {
                XmlNode parent = xmlnode.ParentNode;
                if (parent.Attributes["Condition"] == null)
                    continue;
                if (!IsNodeApplicable(parent, platform, configuration))
                    continue;

                if (string.IsNullOrEmpty(xmlnode.InnerText))
                {
                    xmlnode.InnerText = value;
                }
                else
                {
                    xmlnode.InnerText += ";" + value;
                }
                return;
            }

            projnode = csproj.SelectNodes("//*[local-name() = 'PropertyGroup' and @Condition]");
            foreach (XmlNode xmlnode in projnode)
            {
                if (xmlnode.Attributes["Condition"] == null)
                    continue;
                if (!IsNodeApplicable(xmlnode, platform, configuration))
                    continue;

                XmlElement defines = csproj.CreateElement("DefineConstants", MSBuild_Namespace);
                defines.InnerText = "$(DefineConstants);" + value;
                xmlnode.AppendChild(defines);
                return;
            }

            throw new Exception("Could not find where to add a new DefineConstants node");
        }

        public static void SetNode(this XmlDocument csproj, string node, string value)
        {
            XmlNodeList nodes = csproj.SelectNodes("/*/*/*[local-name() = '" + node + "']");
            if (nodes.Count == 0)
                throw new Exception(string.Format("Could not find node {0}", node));
            foreach (XmlNode n in nodes)
            {
                n.InnerText = value;
            }
        }

        public static void RemoveNode(this XmlDocument csproj, string node, bool throwOnInexistentNode = true)
        {
            XmlNodeList nodes = csproj.SelectNodes("/*/*/*[local-name() = '" + node + "']");
            if (throwOnInexistentNode && nodes.Count == 0)
                throw new Exception(string.Format("Could not find node {0}", node));
            foreach (XmlNode n in nodes)
            {
                n.ParentNode.RemoveChild(n);
            }
        }

        public static void CloneConfiguration(this XmlDocument csproj, string platform, string configuration, string new_configuration)
        {
            IEnumerable<XmlNode> projnode = csproj.GetPropertyGroups(platform, configuration);
            foreach (XmlNode xmlnode in projnode)
            {
                XmlNode clone = xmlnode.Clone();
                XmlAttribute condition = clone.Attributes["Condition"];
                condition.InnerText = condition.InnerText.Replace(configuration, new_configuration);
                xmlnode.ParentNode.InsertAfter(clone, xmlnode);
                return;
            }

            throw new Exception($"Configuration {platform}|{configuration} not found.");
        }

        public static void DeleteConfiguration(this XmlDocument csproj, string platform, string configuration)
        {
            IEnumerable<XmlNode> projnode = csproj.GetPropertyGroups(platform, configuration);
            foreach (XmlNode xmlnode in projnode)
                xmlnode.ParentNode.RemoveChild(xmlnode);
        }

        private static IEnumerable<XmlNode> SelectElementNodes(this XmlNode node, string name)
        {
            foreach (XmlNode child in node.ChildNodes)
            {
                if (child.NodeType == XmlNodeType.Element && child.Name == name)
                    yield return child;

                if (!child.HasChildNodes)
                    continue;

                foreach (XmlNode descendent in child.SelectElementNodes(name))
                    yield return descendent;
            }
        }

        public static void ResolveAllPaths(this XmlDocument csproj, string project_path)
        {
            string dir = System.IO.Path.GetDirectoryName(project_path);
            string[] nodes_with_paths = new string[]
            {
                "AssemblyOriginatorKeyFile",
                "CodesignEntitlements",
                "TestLibrariesDirectory",
                "HintPath",
            };
            string[][] attributes_with_paths = new string[][]
            {
                new string [] { "None", "Include" },
                new string [] { "Compile", "Include" },
                new string [] { "Compile", "Exclude" },
                new string [] { "ProjectReference", "Include" },
                new string [] { "InterfaceDefinition", "Include" },
                new string [] { "BundleResource", "Include" },
                new string [] { "EmbeddedResource", "Include" },
                new string [] { "ImageAsset", "Include" },
                new string [] { "GeneratedTestInput", "Include" },
                new string [] { "GeneratedTestOutput", "Include" },
                new string [] { "TestLibrariesInput", "Include" },
                new string [] { "TestLibrariesOutput", "Include" },
                new string [] { "Content", "Include" },
                new string [] { "ObjcBindingApiDefinition", "Include" },
                new string [] { "ObjcBindingCoreSource", "Include" },
                new string [] { "ObjcBindingNativeLibrary", "Include" },
                new string [] { "ObjcBindingNativeFramework", "Include" },
                new string [] { "Import", "Project", "CustomBuildActions.targets" },
                new string [] { "FilesToCopy", "Include" },
                new string [] { "FilesToCopyFoo", "Include" },
                new string [] { "FilesToCopyFooBar", "Include" },
                new string [] { "FilesToCopyEncryptedXml", "Include" },
                new string [] { "FilesToCopyCryptographyPkcs", "Include" },
                new string [] { "FilesToCopyResources", "Include" },
                new string [] { "FilesToCopyXMLFiles", "Include" },
                new string [] { "FilesToCopyChannels", "Include" },
                new string [] { "CustomMetalSmeltingInput", "Include" },
                new string [] { "Metal", "Include" },
            };
            string[] nodes_with_variables = new string[]
            {
                "MtouchExtraArgs",
            };
            Func<string, string> convert = (input) =>
            {
                if (input[0] == '/')
                    return input; // This is already a full path.
                if (input.StartsWith("$(MSBuildExtensionsPath)", StringComparison.Ordinal))
                    return input; // This is already a full path.
                if (input.StartsWith("$(MSBuildBinPath)", StringComparison.Ordinal))
                    return input; // This is already a full path.
                input = input.Replace('\\', '/'); // make unix-style
                input = System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, input));
                input = input.Replace('/', '\\'); // make windows-style again
                return input;
            };

            foreach (string key in nodes_with_paths)
            {

                IEnumerable<XmlNode> nodes = csproj.SelectElementNodes(key);
                foreach (XmlNode node in nodes)
                    node.InnerText = convert(node.InnerText);
            }
            foreach (string key in nodes_with_variables)
            {
                IEnumerable<XmlNode> nodes = csproj.SelectElementNodes(key);
                foreach (XmlNode node in nodes)
                {
                    node.InnerText = node.InnerText.Replace("${ProjectDir}", StringUtils.Quote(System.IO.Path.GetDirectoryName(project_path)));
                }
            }
            foreach (string[] kvp in attributes_with_paths)
            {
                string element = kvp[0];
                string attrib = kvp[1];
                IEnumerable<XmlNode> nodes = csproj.SelectElementNodes(element);
                foreach (XmlNode node in nodes)
                {
                    XmlAttribute a = node.Attributes[attrib];
                    if (a == null)
                        continue;

                    // entries after index 2 is a list of values to filter the attribute value against.
                    bool found = kvp.Length == 2;
                    bool skipLogicalName = kvp.Length > 2;
                    for (int i = 2; i < kvp.Length; i++)
                        found |= a.Value == kvp[i];
                    if (!found)
                        continue;

                    // Fix any default LogicalName values (but don't change existing ones).
                    XmlNode ln = node.SelectElementNodes("LogicalName")?.SingleOrDefault();
                    IEnumerable<XmlNode> links = node.SelectElementNodes("Link");
                    if (!skipLogicalName && ln == null && !links.Any())
                    {
                        ln = csproj.CreateElement("LogicalName", MSBuild_Namespace);
                        node.AppendChild(ln);

                        string logicalName = a.Value;
                        switch (element)
                        {
                            case "BundleResource":
                                if (logicalName.StartsWith("Resources\\", StringComparison.Ordinal))
                                    logicalName = logicalName.Substring("Resources\\".Length);
                                break;
                            default:
                                break;
                        }
                        ln.InnerText = logicalName;
                    }

                    a.Value = convert(a.Value);
                }
            }
        }
    }
}


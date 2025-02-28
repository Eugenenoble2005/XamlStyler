﻿// © Xavalon. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Xavalon.XamlStyler.Options;

namespace Xavalon.XamlStyler.Console
{
    public sealed class XamlStylerConsole
    {
        private static readonly string[] SupportedPatterns = { "*.xaml", "*.axaml" };
        private static readonly string[] SupportedExtensions = { ".xaml", ".axaml" };

        private readonly CommandLineOptions options;
        private readonly Logger logger;
        private readonly StylerService stylerService;

        public XamlStylerConsole(CommandLineOptions options, Logger logger)
        {
            this.options = options;
            this.logger = logger;

            IStylerOptions stylerOptions = new StylerOptions();

            if (this.options.Configuration != null)
            {
                stylerOptions = this.LoadConfiguration(this.options.Configuration);
            }

            this.ApplyOptionOverrides(options, stylerOptions);

            this.stylerService = new StylerService(stylerOptions, new XamlLanguageOptions
            {
                IsFormatable = true
            });
        }

        private void ApplyOptionOverrides(CommandLineOptions options, IStylerOptions stylerOptions)
        {
            if (options.IndentSize != null)
            {
                stylerOptions.IndentSize = options.IndentSize.Value;
            }

            if (options.IndentWithTabs != null)
            {
                stylerOptions.IndentWithTabs = options.IndentWithTabs.Value;
            }

            if (options.AttributesTolerance != null)
            {
                stylerOptions.AttributesTolerance = options.AttributesTolerance.Value;
            }

            if (options.KeepFirstAttributeOnSameLine != null)
            {
                stylerOptions.KeepFirstAttributeOnSameLine = options.KeepFirstAttributeOnSameLine.Value;
            }

            if (options.MaxAttributeCharactersPerLine != null)
            {
                stylerOptions.MaxAttributeCharactersPerLine = options.MaxAttributeCharactersPerLine.Value;
            }

            if (options.MaxAttributesPerLine != null)
            {
                stylerOptions.MaxAttributesPerLine = options.MaxAttributesPerLine.Value;
            }

            if (options.NoNewLineElements != null)
            {
                stylerOptions.NoNewLineElements = options.NoNewLineElements;
            }

            if (options.PutAttributeOrderRuleGroupsOnSeparateLines != null)
            {
                stylerOptions.PutAttributeOrderRuleGroupsOnSeparateLines = options.PutAttributeOrderRuleGroupsOnSeparateLines.Value;
            }

            if (options.AttributeIndentation != null)
            {
                stylerOptions.AttributeIndentation = options.AttributeIndentation.Value;
            }

            if (options.AttributeIndentationStyle != null)
            {
                stylerOptions.AttributeIndentationStyle = options.AttributeIndentationStyle.Value;
            }

            if (options.RemoveDesignTimeReferences != null)
            {
                stylerOptions.RemoveDesignTimeReferences = options.RemoveDesignTimeReferences.Value;
            }

            if (options.EnableAttributeReordering != null)
            {
                stylerOptions.EnableAttributeReordering = options.EnableAttributeReordering.Value;
            }

            if (options.FirstLineAttributes != null)
            {
                stylerOptions.FirstLineAttributes = options.FirstLineAttributes;
            }

            if (options.OrderAttributesByName != null)
            {
                stylerOptions.OrderAttributesByName = options.OrderAttributesByName.Value;
            }

            if (options.PutEndingBracketOnNewLine != null)
            {
                stylerOptions.PutEndingBracketOnNewLine = options.PutEndingBracketOnNewLine.Value;
            }

            if (options.RemoveEndingTagOfEmptyElement != null)
            {
                stylerOptions.RemoveEndingTagOfEmptyElement = options.RemoveEndingTagOfEmptyElement.Value;
            }

            if (options.RootElementLineBreakRule != null)
            {
                stylerOptions.RootElementLineBreakRule = options.RootElementLineBreakRule.Value;
            }

            if (options.ReorderVSM != null)
            {
                stylerOptions.ReorderVSM = options.ReorderVSM.Value;
            }

            if (options.ReorderGridChildren != null)
            {
                stylerOptions.ReorderGridChildren = options.ReorderGridChildren.Value;
            }

            if (options.ReorderCanvasChildren != null)
            {
                stylerOptions.ReorderCanvasChildren = options.ReorderCanvasChildren.Value;
            }

            if (options.ReorderSetters != null)
            {
                stylerOptions.ReorderSetters = options.ReorderSetters.Value;
            }

            if (options.FormatMarkupExtension != null)
            {
                stylerOptions.FormatMarkupExtension = options.FormatMarkupExtension.Value;
            }

            if (options.NoNewLineMarkupExtensions != null)
            {
                stylerOptions.NoNewLineMarkupExtensions = options.NoNewLineMarkupExtensions;
            }

            if (options.ThicknessStyle != null)
            {
                stylerOptions.ThicknessStyle = options.ThicknessStyle.Value;
            }

            if (options.ThicknessAttributes != null)
            {
                stylerOptions.ThicknessAttributes = options.ThicknessAttributes;
            }

            if (options.CommentSpaces != null)
            {
                stylerOptions.CommentSpaces = options.CommentSpaces.Value;
            }
        }

        public void Process(ProcessType processType)
        {
            int successCount = 0;
            IList<string> files;
            bool isRawBuffer = false;
            switch (processType)
            {
                case ProcessType.File:
                    files = this.options.File;
                    break;
                case ProcessType.Directory:
                    SearchOption searchOption = this.options.IsRecursive
                        ? SearchOption.AllDirectories
                        : SearchOption.TopDirectoryOnly;
                    if (File.GetAttributes(this.options.Directory).HasFlag(FileAttributes.Directory))
                    {
                        var directoryFiles = new List<string>();
                        foreach (var pattern in SupportedPatterns)
                        {
                            directoryFiles.AddRange(Directory.GetFiles(
                                this.options.Directory,
                                pattern,
                                searchOption));
                        }

                        files = directoryFiles;
                    }
                    else
                    {
                        files = new List<string>();
                    }
                    break;
                case ProcessType.RawBuffer:
                    //whether the contents of the xaml file has been piped directly through stdin
                    isRawBuffer = true;
                    files = new List<string>() { "dummy" }; //there will only ever be one file
                    break;

                default:
                    throw new ArgumentException("Invalid ProcessType");
            }

            foreach (string file in files)
            {
                if (this.TryProcessFile(file, isRawBuffer))
                {
                    successCount++;
                }
            }

            if (this.options.IsPassive)
            {
                this.Log($"\n{successCount} of {files.Count} files pass format check.", LogLevel.Minimal);

                if (successCount != files.Count)
                {
                    Environment.Exit(1);
                }
            }
            else
            {
                this.Log($"\nProcessed {successCount} of {files.Count} files.", LogLevel.Minimal);
            }
        }

        private bool TryProcessFile(string file, bool isRawBuffer = false)
        {
            this.Log($"{(this.options.IsPassive ? "Checking" : "Processing")}: {file}");
            if (!this.options.Ignore && isRawBuffer == false)
            {
                string extension = Path.GetExtension(file);
                this.Log($"Extension: {extension}", LogLevel.Debug);

                if (!SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    this.Log($"Skipping... Can only process {string.Join(',', SupportedExtensions)} files. Use the --ignore parameter to override.");
                    return false;
                }
            }

            string path = Path.GetFullPath(file);
            this.Log($"Full Path: {file}", LogLevel.Debug);

            // If the options already has a configuration file set, we don't need to go hunting for one
            string configurationPath = String.IsNullOrEmpty(this.options.Configuration) ? this.GetConfigurationFromPath(path) : null;

            string originalContent = null;
            Encoding encoding = Encoding.UTF8; // Visual Studio by default uses UTF8
            if (isRawBuffer)
            {
                using (var reader = new StreamReader(System.Console.OpenStandardInput(), System.Console.InputEncoding))
                {
                    originalContent = reader.ReadToEnd();
                    encoding = reader.CurrentEncoding;
                }
            }
            if (originalContent == null)
            {
                try
                {
                    using (var reader = new StreamReader(path))
                    {
                        originalContent = reader.ReadToEnd();
                        encoding = reader.CurrentEncoding;
                        this.Log($"\nOriginal Content:\n\n{originalContent}\n", LogLevel.Insanity);
                    }
                }
                catch
                {
                    this.Log($"Skipping... Invalid file.");
                    return false;
                }
            }
            string formattedOutput = String.IsNullOrWhiteSpace(configurationPath)
                ? this.stylerService.StyleDocument(originalContent)
                : new StylerService(this.LoadConfiguration(configurationPath), new XamlLanguageOptions()
                {
                    IsFormatable = true
                }).StyleDocument(originalContent);

            if (this.options.IsPassive)
            {
                if (formattedOutput.Equals(originalContent, StringComparison.Ordinal))
                {
                    this.Log($"  PASS");
                }
                else
                {
                    // Fail fast in passive mode when detecting a file where formatting rules were not followed.
                    this.Log($"  FAIL");
                    return false;
                }
            }
            else if (this.options.WriteToStdout)
            {
                var prevEncoding = System.Console.OutputEncoding;
                try
                {
                    System.Console.OutputEncoding = encoding;
                    System.Console.Write(encoding.GetString(encoding.GetPreamble()));
                    System.Console.Write(formattedOutput);
                }
                finally
                {
                    System.Console.OutputEncoding = prevEncoding;
                }
            }
            else
            {
                this.Log($"\nFormatted Output:\n\n{formattedOutput}\n", LogLevel.Insanity);

                // Only modify the file on disk if the content would be changed
                if (!formattedOutput.Equals(originalContent, StringComparison.Ordinal))
                {
                    using var writer = new StreamWriter(path, false, encoding);
                    try
                    {
                        writer.Write(formattedOutput);
                        this.Log($"Finished Processing: {file}", LogLevel.Verbose);
                    }
                    catch (Exception e)
                    {
                        this.Log("Skipping... Error formatting XAML. Increase log level for more details.");
                        this.Log($"Exception: {e.Message}", LogLevel.Verbose);
                        this.Log($"StackTrace: {e.StackTrace}", LogLevel.Debug);
                    }
                }
                else
                {
                    this.Log($"Finished Processing (unmodified): {file}", LogLevel.Verbose);
                }
            }

            return true;
        }

        private IStylerOptions LoadConfiguration(string path)
        {
            var stylerOptions = new StylerOptions(path);
            this.Log(JsonConvert.SerializeObject(stylerOptions), LogLevel.Insanity);
            this.Log(JsonConvert.SerializeObject(stylerOptions.AttributeOrderingRuleGroups), LogLevel.Debug);
            return stylerOptions;
        }

        private string GetConfigurationFromPath(string path)
        {
            try
            {
                if (String.IsNullOrWhiteSpace(path))
                {
                    return null;
                }

                bool isSolutionRoot = false;

                while (!isSolutionRoot && ((path = Path.GetDirectoryName(path)) != null))
                {
                    isSolutionRoot = Directory.Exists(Path.Combine(path, ".vs"));
                    this.Log($"In solution root: {isSolutionRoot}", LogLevel.Debug);
                    string configFile = Path.Combine(path, "Settings.XamlStyler");
                    this.Log($"Looking in: {path}", LogLevel.Debug);

                    if (File.Exists(configFile))
                    {
                        this.Log($"Configuration Found: {configFile}", LogLevel.Verbose);
                        return configFile;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private void Log(string line, LogLevel logLevel = LogLevel.Default)
        {
            this.logger.Log(line, logLevel);
        }
    }
}

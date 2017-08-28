﻿//-----------------------------------------------------------------------
// <copyright file="Program.cs">
//      Copyright (c) Microsoft Corporation. All rights reserved.
// 
//      THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
//      EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
//      MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
//      IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
//      CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
//      TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
//      SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.PSharp.IO;
using Microsoft.PSharp.LanguageServices;
using Microsoft.PSharp.LanguageServices.Compilation;
using Microsoft.PSharp.LanguageServices.Parsing;

namespace Microsoft.PSharp
{
    /// <summary>
    /// The P# syntax rewriter.
    /// </summary>
    public class SyntaxRewriter
    {
        static void Main(string[] args)
        {
            var infile = string.Empty;
            var outfile = string.Empty;
            var csVersion = new Version(0, 0);

            var usage = "Usage: PSharpSyntaxRewriter.exe file.psharp [file.psharp.cs] [/csVersion:major.minor]";

            if (args.Length >= 1 && args.Length <= 3)
            {
                foreach (var arg in args)
                {
                    if (arg.StartsWith("/") || arg.StartsWith("-"))
                    {
                        var parts = arg.Substring(1).Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                        switch (parts[0].ToLower())
                        {
                            case "?":
                                Output.WriteLine(usage);
                                return;
                            case "csversion":
                                if (parts.Length != 2 || !Version.TryParse(parts[1], out csVersion))
                                {
                                    Output.WriteLine("Error: option csVersion requires a version (major.minor) value");
                                    return;
                                }
                                break;
                            default:
                                Output.WriteLine($"Error: unknown option {parts[0]}");
                                return;
                        }
                    }
                    else if (infile.Length == 0)
                    {
                        infile = arg;
                    }
                    else
                    {
                        outfile = arg;
                    }
                }
            }

            if (infile.Length == 0)
            {
                Output.WriteLine(usage);
                return;
            }

            // Gets input file as string.
            var input_string = "";
            try
            {
                input_string = System.IO.File.ReadAllText(args[0]);
            }
            catch (System.IO.IOException e)
            {
                Output.WriteLine("Error: {0}", e.Message);
                return;
            }

            // Translates and prints on console or to file.
            string errors = "";
            var output = Translate(input_string, out errors, csVersion);
            var result = string.Format("{0}", output == null ? "Parse Error: " + errors : output);
            if (!string.IsNullOrEmpty(outfile))
            {
                try
                {
                    File.WriteAllLines(outfile, new[] { result });
                    return;
                }
                catch (Exception ex)
                {
                    Output.WriteLine("Error writing to file: {0}", ex.Message);
                }
            }

            Output.WriteLine("{0}", output == null ? "Parse Error: " + errors : output);
        }

        /// <summary>
        /// Translates the specified text from P# to C#.
        /// </summary>
        /// <param name="text">Text</param>
        /// <returns>Text</returns>
        public static string Translate(string text, out string errors, Version csVersion)
        {
            var configuration = Configuration.Create();
            configuration.Verbose = 2;
            configuration.RewriteCSharpVersion = csVersion;
            errors = null;

            var context = CompilationContext.Create(configuration).LoadSolution(text);

            try
            {
                ParsingEngine.Create(context).Run();
                RewritingEngine.Create(context).Run();

                var syntaxTree = context.GetProjects()[0].PSharpPrograms[0].GetSyntaxTree();

                return syntaxTree.ToString();
            }
            catch (ParsingException ex)
            {
                errors = ex.Message;
                return null;
            }
            catch (RewritingException ex)
            {
                errors = ex.Message;
                return null;
            }
        }
    }

    /// <summary>
    /// This is the MSBuild task referenced by the UsingTask element in the PSharp.targets file.
    /// </summary>
    public class Rewriter : ITask
    {
        public IBuildEngine BuildEngine { get; set; }
        public ITaskHost HostObject { get; set; }

        public ITaskItem[] InputFiles { get; set; }

        public string CSharpVersion
        {
            get { return this.csVersion.ToString(); }
            set { this.csVersion = Version.Parse(value); }
        }
        private Version csVersion = new Version();

        [Output]
        public ITaskItem[] OutputFiles { get; set; }

        public bool Execute()
        {
            for (int i = 0; i < InputFiles.Length; i++)
            {
                var inp = File.ReadAllText(InputFiles[i].ItemSpec);
                string errors = "";
                var outp = SyntaxRewriter.Translate(inp, out errors, this.csVersion);
                if (outp != null)
                {
                    File.WriteAllText(OutputFiles[i].ItemSpec, outp);
                }
                else
                {
                    // Replaces Program.psharp with the actual file name.
                    errors = errors.Replace("Program.psharp", System.IO.Path.GetFileName(InputFiles[i].ItemSpec));
                    
                    // Prints a compiler error with log.
                    File.WriteAllText(OutputFiles[i].ItemSpec, 
                        string.Format("#error Psharp Compiler Error {0} /* {0} {1} {0} */ ", "\n", errors));
                }
            }

            return true;
        }
    }
}

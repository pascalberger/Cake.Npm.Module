using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cake.Core;
using Cake.Core.Configuration;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Core.Packaging;

namespace Cake.Npm.Module
{
    public class NpmPackageInstaller : IPackageInstaller
    {
        private ICakeEnvironment _environment;
        private IProcessRunner _processRunner;
        private ICakeLog _log;
        private INpmContentResolver _contentResolver;
        private ICakeConfiguration _config;

        public NpmPackageInstaller(ICakeEnvironment environment, IProcessRunner processRunner, ICakeLog log, INpmContentResolver contentResolver, ICakeConfiguration config)
        {
            if (environment == null)
            {
                throw new ArgumentNullException(nameof(environment));
            }

            if (processRunner == null)
            {
                throw new ArgumentNullException(nameof(processRunner));
            }

            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            if (contentResolver == null)
            {
                throw new ArgumentNullException(nameof(contentResolver));
            }

            _environment = environment;
            _processRunner = processRunner;
            _log = log;
            _contentResolver = contentResolver;
            _config = config;
        }
        public bool CanInstall(PackageReference package, PackageType type)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            return package.Scheme.Equals(Constants.PackageScheme, StringComparison.OrdinalIgnoreCase);
        }

        public IReadOnlyCollection<IFile> Install(PackageReference package, PackageType type, DirectoryPath path)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            // Install the package.
            _log.Debug("Installing package {0} with npm...", package.Package);
            var process = _processRunner.Start(
                "npm",
                new ProcessSettings { Arguments = GetArguments(package, _config), RedirectStandardOutput = true, Silent = _log.Verbosity < Verbosity.Diagnostic });

            process.WaitForExit();

            var exitCode = process.GetExitCode();
            if (exitCode != 0)
            {
                _log.Warning("npm exited with {0}", exitCode);
                var output = string.Join(Environment.NewLine, process.GetStandardOutput());
                _log.Verbose(Verbosity.Diagnostic, "Output:\r\n{0}", output);
            }

            var result = _contentResolver.GetFiles(package, type, package.GetSwitch("global"));
            if (result.Count != 0)
            {
                return result;
            }

            _log.Warning("Could not determine installed package files! Installation may not be complete.");
            // TODO: maybe some warnings here
            return result;
        }

        private ProcessArgumentBuilder GetArguments(
            PackageReference definition,
            ICakeConfiguration config)
        {
            string source;
            string scope;
            string version;

            var arguments = new ProcessArgumentBuilder();

            arguments.Append("install");

            if (_log.Verbosity == Verbosity.Verbose || _log.Verbosity == Verbosity.Diagnostic)
            {
                arguments.Append("--verbose");
            }

            if (definition.Address != null) {
                arguments.Append($"--registry \"{definition.Address}\"");
            } else {
                var npmSource = config.GetValue(Constants.ConfigKey);
                if (!string.IsNullOrWhiteSpace(npmSource))
                {
                    arguments.Append($"--registry \"{npmSource}\"");
                }
            }

            if (definition.GetSwitch("force"))
            {
                arguments.Append("--force");
            }

            if (definition.GetSwitch("global"))
            {
                arguments.Append("--global");
            }

            if (definition.GetSwitch("ignore-scripts"))
            {
                arguments.Append("--ignore-scripts");
            }

            if (definition.GetSwitch("no-optional"))
            {
                arguments.Append("--no-optional");
            }

            if (definition.GetSwitch("save"))
            {
                GetSaveArguments(arguments, definition);
            }

            var packageString = new StringBuilder();

            if (definition.HasValue("source", out source))
            {
                packageString.Append(source.Trim(':') + ":");
            }
            if (definition.HasValue("scope", out scope))
            {
                packageString.Append("@" + scope.Trim('@') + "/");
            }

            packageString.Append(definition.Package);

            if (definition.HasValue("version", out version))
            {
                packageString.Append(("@" + version.Trim(':', '=', '@')).Quote());
            }

            arguments.Append(packageString.ToString());
            return arguments;
        }

        private void GetSaveArguments(ProcessArgumentBuilder arguments, PackageReference definition)
        {
                var values = definition.Parameters["save"].ToList();
                foreach (var value in values)
                {
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        arguments.Append("--save");
                    }
                    else
                    {
                        switch (value)
                        {
                            case "dev":
                                arguments.Append("--save-dev");
                                break;
                            case "optional":
                                arguments.Append("--save-optional");
                                break;
                            case "exact":
                                arguments.Append("--save-exact");
                                break;
                            case "bundle":
                                arguments.Append("--save-bundle");
                                break;
                            default:
                                break;
                        }
                    }

                }
        }
    }
}
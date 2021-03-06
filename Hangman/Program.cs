﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Numerics;
using Hangman.Instance;
using Hangman.Queue;

namespace Hangman
{
    public class Program : ProgramInstance
    {
        private ExecutorQueue<Command> _commandQueue;

        private void List(ref TextWriter writer)
        {
            var ids = new List<string>();
            var status = new List<string>();
            var jobs = new List<string>();
            foreach (var ip in _commandQueue)
            {
                ids.Add(ip.QueueId.ToString());
                status.Add(ip.Item.Status);
                jobs.Add(ip.Item.ToString());
            }

            var idWid = Math.Max(ids.Count > 0 ? ids.Max(id => id.Length) + 1 : 20, 20);
            var statWid = Math.Max(status.Count > 0 ? status.Max(stat => stat.Length) + 1 : 20, 20);
            var jobWid = Math.Max(jobs.Count > 0 ? jobs.Max(job => job.Length) + 1 : 20, 20);

            writer.WriteLine("Id".PadRight(idWid, ' ') + "Status".PadRight(statWid, ' ') + "Job".PadRight(jobWid, ' '));
            writer.WriteLine(new string('-', idWid + jobWid + statWid));
            for (var i = 0; i < ids.Count; i++)
            {
                writer.WriteLine(ids[i].PadRight(idWid, ' ') + status[i].PadRight(statWid, ' ') + jobs[i].PadRight(jobWid, ' '));
            }
        }

        private void ClearBacklog(ref TextWriter writer)
        {
            _commandQueue.ClearBacklog();
            writer.WriteLine("Backlog Cleared");
        }

        private void Kill(ref TextWriter writer, string id)
        {
            _commandQueue.MarkComplete(BigInteger.Parse(id)).Item.Kill();
            writer.WriteLine($"Killed {id}.");
        }

        private void SetMax(ref TextWriter writer, string max)
        {
            _commandQueue.MaximumParallelExecutions = int.Parse(max);
            writer.WriteLine($"Maximum jobs set to {max}.");
        }

        [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
        public static string WhereSearch(string filename)
        {
            var paths = new[] { Environment.CurrentDirectory }.Concat(Environment.GetEnvironmentVariable("PATH").Split(';'));
            string[] extensions = { "" };
            try
            {
                extensions = extensions.Concat(Environment.GetEnvironmentVariable("PATHEXT").Split(';').Where(e => e.StartsWith("."))).ToArray();
            }
            catch
            {
                // ignored - don't care because in this case everything is already setup to succeed
            }
            var combinations = paths.SelectMany(x => extensions, (path, extension) => Path.Combine(path, filename + extension));
            return combinations.FirstOrDefault(File.Exists);
        }

        private void ProcessCliArguments(ref string[] args, TextWriter console)
        {
            var i = 0;
            for (; i < args.Length; i++)
            {
                if (!args[i].StartsWith("-") && WhereSearch(args[i]) != null)
                {
                    break;
                }
            }
            var cliArgs = args.Take(i);
            var cliOptions = new OptionSet
            {
                {"c|clear", "Clear the current work backlog.", _ => ClearBacklog(ref console)},
                {"k|kill", "Kill a currently in-progress item. This may have side affects.", id => Kill(ref console, id)},
                {"l|list", "List all backlog and in-progress jobs.", _ => List(ref console)},
                {"m|max", "Set the maximum number of parallel jobs", max => SetMax(ref console, max)}
            };
            cliOptions.Add("h|help", "Displays this help.",_ =>
            {
                cliOptions.ShowHelp(Process.GetCurrentProcess().ProcessName, "A parallel command executor which does not block", "Matthew Knox",
                    "https://github.com/mrkno/Hangman/issues", $"Copyright {DateTime.Now.Year} (c) Matthew Knox", false, console);
                throw new Exception();
            });
            
            try
            {
                cliOptions.ParseExceptionally(cliArgs);
                args = args.Skip(i).Take(args.Length - i + 1).ToArray();
            }
            catch
            {
                args = new string[] { };
            }
        }

        private void CommandShouldExecute(object sender, ExecutorQueue<Command>.ExecutorQueueItem e)
        {
            e.Item.OnExecutionComplete += (s, v) => e.MarkComplete();
            e.Item.BeginExecute();
        }

        protected override void RunMain(string[] args, string workingDirectory, IDictionary<string, string> environment)
        {
            _commandQueue = new ExecutorQueue<Command>();
            _commandQueue.ShouldExecute += CommandShouldExecute;

            NewInstance(args, workingDirectory, environment, Console.Out);
            PreventExit();
        }

        protected override void NewInstance(string[] args, string workingDirectory, IDictionary<string, string> environment, TextWriter console)
        {
            ProcessCliArguments(ref args, console);
            if (args.Length > 0)
            {
                _commandQueue.Enqueue(new Command(args, workingDirectory, environment));
            }
        }
    }
}

using System;
using System.IO;

namespace Furball.Engine.Engine.DevConsole.ConFuncs.Client {
    public class ClearLogFolder : ConFunc {

        public ClearLogFolder() : base("cl_clear_log_folder") {}
        public override (ExecutionResult result, string message) Run(string consoleInput) {
            string[] directories = Directory.GetFiles(DevConsole.LogPath);

            try {
                for (int i = 0; i != directories.Length; i++) {
                    string currentFile = directories[i];

                    File.Delete(currentFile);
                }
            }
            catch (Exception e) {
                return (ExecutionResult.Error, "Something went wrong. Make sure the `log` directory exists, and that the `log` directory and its files aren't write protected.");
            }

            return (ExecutionResult.Success, "Successfully cleared the log directory.");
        }
    }
}

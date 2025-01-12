using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Furball.Volpe.Evaluation;
using Furball.Volpe.Exceptions;
using Furball.Volpe.LexicalAnalysis;
using Furball.Volpe.SyntaxAnalysis;
using Kettu;

namespace Furball.Engine.Engine.Config; 

internal class LoggerLevelConfigError : LoggerLevel {
    public override string Name => "ConfigError";

    public static readonly LoggerLevel Instance = new LoggerLevelConfigError();
        
    private LoggerLevelConfigError() {}
}
    
public abstract class Config {
    public Dictionary<string, Value> Values = new();

    public abstract string Name { get; }

    public abstract void Save();
    public abstract void Load();
}

public abstract class VolpeConfig : Config {
    private Environment _environment = new();
        
    public const string VOLPE_CONFIG_FOLDER = "config";
        
    public string Filename => $"{this.Name}.cfg";
        
    public string FilePath => Path.Combine(VOLPE_CONFIG_FOLDER, this.Filename);

    public override void Save() {
        if (!Directory.Exists(VOLPE_CONFIG_FOLDER))
            Directory.CreateDirectory(VOLPE_CONFIG_FOLDER);

        using FileStream   stream = File.Create(this.FilePath);
        using StreamWriter writer = new(stream);
        writer.AutoFlush = true;

        foreach (KeyValuePair<string,Value> pair in this.Values) {
            string line = $"${pair.Key} = {pair.Value.Representation};\n";
            writer.Write(line);
        }
    }

    public override void Load() {
        if (!File.Exists(this.FilePath))
            return;
            
        using FileStream   stream = File.OpenRead(this.FilePath);
        using StreamReader reader = new(stream);

        string script = reader.ReadToEnd().Trim('\0');

        IEnumerable<Token> tokenStream = new Lexer(script).GetTokenEnumerator();
        Parser             parser      = new(tokenStream);
            
        try {
            while (parser.TryParseNextExpression(out Expression expression)) {
                new EvaluatorContext(expression!, this._environment).Evaluate();
            }
        }
        catch (VolpeException exception) {
            Logger.Log($"Unable to parse config script {this.Name}! Message:{exception.Message}", LoggerLevelConfigError.Instance);
            Debugger.Break();
        }

        IReadOnlyDictionary<string, IVariable> vars = this._environment.Variables;
            
        foreach (KeyValuePair<string, IVariable> pair in vars) {
            this.Values[pair.Key] = pair.Value.ToVariable().RawValue;
        }
    }
}
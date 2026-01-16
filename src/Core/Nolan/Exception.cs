using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace FrozenFrogFramework.NolanTech
{
    public enum ENolanScriptError
    {
        NullOrEmpty,
        OutOfRange,
        KeyNotFound,
        DuplicateKey,
        SyntaxError,
        FileNotFound
    }

    public class NolanException : System.Exception
    {
        public ENolanScriptError Error { get; }

        internal NolanException(string message, ENolanScriptError error) : base(message)
        {
            Error = error;
        }

        static public NolanException ScriptError(string message,  ENolanScriptError error = ENolanScriptError.SyntaxError) => new NolanException(message, error);

        static public NolanScriptException ContextError(string message,  ENolanScriptContext context,  ENolanScriptError error = ENolanScriptError.SyntaxError) => new NolanScriptException(message, context, error);
    }

    public class NolanScriptException : NolanException
    {
        public ENolanScriptContext Context { get; }

        internal NolanScriptException(string message, ENolanScriptContext context, ENolanScriptError error = ENolanScriptError.SyntaxError) : base(message, error) 
        { 
            Context = context; 
        }
    }
}
namespace ReleaseConsole.Core;

public enum ScriptOutputMode
{
    Silent     // No streaming
  , ErrorsOnly // Only stderr
  , Normal     // What you have now
  , Verbose    // Future: timestamps, prefixes, etc
}
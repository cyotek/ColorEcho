using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Cyotek.ColorEcho
{
  internal class Program
  {
    #region Constants

    private static readonly Stack<ConsoleColor> _backgroundHistory;

    private static readonly StringBuilder _builder;

    private static readonly IDictionary<string, ConsoleColor> _colorLookup;

    private static readonly StringBuilder _controlBuilder;

    private static readonly Stack<ConsoleColor> _foregroundHistory;

    #endregion

    #region Static Constructors

    static Program()
    {
      _colorLookup = new Dictionary<string, ConsoleColor>(StringComparer.InvariantCultureIgnoreCase);
      _builder = new StringBuilder();
      _controlBuilder = new StringBuilder();
      _foregroundHistory = new Stack<ConsoleColor>();
      _backgroundHistory = new Stack<ConsoleColor>();
    }

    #endregion

    #region Static Methods

    private static void CreateColorLookup()
    {
      _colorLookup.Add("black", ConsoleColor.Black);

      _colorLookup.Add("navy", ConsoleColor.DarkBlue);
      _colorLookup.Add("dark blue", ConsoleColor.DarkBlue);

      _colorLookup.Add("green", ConsoleColor.DarkGreen);
      _colorLookup.Add("dark green", ConsoleColor.DarkGreen);

      _colorLookup.Add("teal", ConsoleColor.DarkCyan);
      _colorLookup.Add("dark cyan", ConsoleColor.DarkCyan);

      _colorLookup.Add("maroon", ConsoleColor.DarkRed);
      _colorLookup.Add("dark red", ConsoleColor.DarkRed);

      _colorLookup.Add("purple", ConsoleColor.DarkMagenta);
      _colorLookup.Add("dark magenta", ConsoleColor.DarkMagenta);

      _colorLookup.Add("olive", ConsoleColor.DarkYellow);
      _colorLookup.Add("dark yellow", ConsoleColor.DarkYellow);
      _colorLookup.Add("brown", ConsoleColor.DarkYellow);

      _colorLookup.Add("silver", ConsoleColor.Gray);
      _colorLookup.Add("light gray", ConsoleColor.Gray);
      _colorLookup.Add("light grey", ConsoleColor.Gray);

      _colorLookup.Add("gray", ConsoleColor.DarkGray);
      _colorLookup.Add("grey", ConsoleColor.DarkGray);
      _colorLookup.Add("dark gray", ConsoleColor.DarkGray);
      _colorLookup.Add("dark grey", ConsoleColor.DarkGray);

      _colorLookup.Add("blue", ConsoleColor.Blue);
      _colorLookup.Add("light blue", ConsoleColor.Blue);

      _colorLookup.Add("lime", ConsoleColor.Green);
      _colorLookup.Add("light green", ConsoleColor.Green);

      _colorLookup.Add("aqua", ConsoleColor.Cyan);
      _colorLookup.Add("light cyan", ConsoleColor.Cyan);

      _colorLookup.Add("red", ConsoleColor.Red);
      _colorLookup.Add("light red", ConsoleColor.Red);

      _colorLookup.Add("fuschia", ConsoleColor.Magenta);
      _colorLookup.Add("magenta", ConsoleColor.Magenta);
      _colorLookup.Add("light magenta", ConsoleColor.Magenta);

      _colorLookup.Add("yellow", ConsoleColor.Yellow);

      _colorLookup.Add("white", ConsoleColor.White);
    }

    private static void Main(string[] args)
    {
      int count;

      // http://www.codeproject.com/Articles/17033/Add-Colors-to-Batch-Files

      /*
       * {XX}: colors coded as two hexadecimal digits. E.g., {0A} light green
       * {color}: color information as understandable text. E.g., {light red on black}
       * {\n \t}: New line character - Tab character.
       * {\u0000}: Unicode character code.
       * {{: escape character '{'.
       * {#}: restore initial colors.
       */

      // {0c}ERROR:{#} Signing failed for %1, retrying{\n}

      CreateColorLookup();

      count = args.Length;

      for (int i = 0; i < count; i++)
      {
        string arg;

        arg = args[i];

        ParseArgument(arg);

        // Originally I was using GetCommandLineW to get the command line as
        // a complete string but then this meant I had to parse out the
        // filename. Easier just to use the array of arguments passed into
        // Main and just add some additional processing to reintroduce spaces
        if (i < count - 1)
        {
          if (!_isControlBlockOpen)
          {
            _builder.Append(' ');
          }
          else if (!ProcessControl())
          {
            _controlBuilder.Append(' ');
          }
        }
      }

      // output any final content
      WriteContent();

      if (Debugger.IsAttached)
      {
        Console.ReadKey(true);
      }
    }

    private static void ParseArgument(string arg)
    {
      foreach (char c in arg)
      {
        switch (c)
        {
          case '{':
            if (_isControlBlockOpen)
            {
              // assume this is an escape character, not a true block
              _builder.Append(c);
              _isControlBlockOpen = false;
            }
            else
            {
              // starting a new block, clear any existing block and write any stored output
              WriteContent();
              _controlBuilder.Length = 0;
              _isControlBlockOpen = true;
            }
            break;

          case '}':
            if (!_isControlBlockOpen)
            {
              // not in a block, just an ordinary } char
              _builder.Append(c);
            }
            else
            {
              // in a block, parse it
              _isControlBlockOpen = false;
              ProcessControl();
            }
            break;

          case ' ':
            if (!_isControlBlockOpen)
            {
              // not in a block, just a normal space
              _builder.Append(c);
            }
            else if (!ProcessControl())
            {
              // in a control block that could not be parsed - assume multi argument command
              _controlBuilder.Append(c);
            }
            break;

          default:
            if (!_isControlBlockOpen)
            {
              // add it to the output block
              _builder.Append(c);
            }
            else
            {
              // add it to the control block
              _controlBuilder.Append(c);
            }
            break;
        }
      }
    }

    private static bool ProcessColorNames()
    {
      string colorString;
      ConsoleColor foregroundColor;
      bool processed;

      colorString = _controlBuilder.ToString();
      processed = true;

      if (_colorLookup.TryGetValue(colorString, out foregroundColor))
      {
        // found a matching single color, set the foreground
        PushForeground(foregroundColor);
      }
      else
      {
        int index;

        index = colorString.IndexOf(" on ", StringComparison.Ordinal);

        if (index != -1)
        {
          string foregroundString;
          string backgroundString;
          ConsoleColor backgroundColor;

          foregroundString = colorString.Substring(0, index);
          backgroundString = colorString.Substring(index + 4);

          if (_colorLookup.TryGetValue(foregroundString, out foregroundColor) && _colorLookup.TryGetValue(backgroundString, out backgroundColor))
          {
            // got a pair of colors, set both foreground and background
            PushForeground(foregroundColor);
            PushBackground(backgroundColor);
          }
          else
          {
            processed = false;
          }
        }
        else
        {
          processed = false;
        }
      }

      return processed;
    }

    private static bool ProcessControl()
    {
      bool processed;
      string control;

      control = _controlBuilder.ToString();
      processed = true;

      if (!string.IsNullOrEmpty(control))
      {
        int unicodeCode;
        int foreground;
        int background;

        if (control == "\\t")
        {
          _builder.Append('\t');
        }
        else if (control == "\\n")
        {
          _builder.Append('\n');
        }
        else if (control.Length > 2 && control.StartsWith("\\u") && int.TryParse(control.Substring(2), NumberStyles.HexNumber, null, out unicodeCode))
        {
          _builder.Append(char.ConvertFromUtf32(unicodeCode));
        }
        else if (control == "#")
        {
          // reset the foreground color
          ResetForegroundColor();
        }
        else if (control == "##")
        {
          // reset the foreground and background color
          ResetForegroundColor();
          ResetBackgroundColor();
        }
        else if (control.Length == 2 && int.TryParse(control[0].ToString(), NumberStyles.HexNumber, null, out background) && int.TryParse(control[1].ToString(), NumberStyles.HexNumber, null, out foreground))
        {
          // assume hex pair
          PushForeground((ConsoleColor)foreground);
          PushBackground((ConsoleColor)background);
        }
        else if (_isControlBlockOpen || !ProcessColorNames())
        {
          // can't handle it
          processed = false;
        }
      }

      // if we were able to process the command, clear the buffer
      if (processed)
      {
        _controlBuilder.Clear();
      }

      return processed;
    }

    private static void PushBackground(ConsoleColor color)
    {
      _backgroundHistory.Push(Console.BackgroundColor);

      Console.BackgroundColor = color;
    }

    private static void PushForeground(ConsoleColor color)
    {
      _foregroundHistory.Push(Console.ForegroundColor);

      Console.ForegroundColor = color;
    }

    private static void ResetBackgroundColor()
    {
      if (_backgroundHistory.Count != 0)
      {
        Console.BackgroundColor = _backgroundHistory.Pop();
      }
    }

    private static void ResetForegroundColor()
    {
      if (_foregroundHistory.Count != 0)
      {
        Console.ForegroundColor = _foregroundHistory.Pop();
      }
    }

    private static void WriteContent()
    {
      if (_builder.Length != 0)
      {
        Console.Write(_builder.ToString());
        _builder.Length = 0;
      }
    }

    #endregion

    #region Other

    private static bool _isControlBlockOpen;

    #endregion
  }
}

// This is free and unencumbered software released into the public domain. The most recent version can be found at:
// https://github.com/carterjones/logger/blob/master/Logger/Logger.cs
namespace Logger
{
    using System;

    /// <summary>
    /// A class that can be used for logging purposes.
    /// </summary>
    public class Logger
    {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the Logger class.
        /// </summary>
        /// <param name="defaultType">The default type of log message.</param>
        /// <param name="defaultLevel">The default level of log message.</param>
        /// <param name="filename">The name of the log file.</param>
        /// <param name="permittedLevels">The levels of log messages permitted by this logger.</param>
        public Logger(
            Type defaultType = Type.CONSOLE,
            Level defaultLevel = Level.NONE,
            string filename = "log.log",
            Level permittedLevels = Level.NONE | Level.DEBUG | Level.LOW | Level.MEDIUM | Level.HIGH)
        {
            this.DefaultType = defaultType;
            this.DefaultLevel = defaultLevel;
            this.Filename = filename;
            this.PermittedLevels = permittedLevels;
        }

        #endregion

        #region Enumerations

        /// <summary>
        /// The level of severity for the log. If multiple levels are provided when creating a log message, the
        /// highest permitted level will be chosen as the level of the log message. If no permitted log level is found
        /// within the provided log message level flags, no log will be created.
        /// </summary>
        [Flags]
        public enum Level
        {
            /// <summary>
            /// No severity is applicable. The log will still be shown/written, but with no level rating.
            /// </summary>
            NONE = 1,

            /// <summary>
            /// Debug level. Only logged in debugging builds.
            /// </summary>
            DEBUG = 2,

            /// <summary>
            /// Low severity, such as non-important, but useful output.
            /// </summary>
            LOW = 4,

            /// <summary>
            /// Medium severity, such as important, but non-essential output.
            /// </summary>
            MEDIUM = 8,

            /// <summary>
            /// High severity, such as extremely important information that requires attention.
            /// </summary>
            HIGH = 16
        }

        /// <summary>
        /// The type of logging that takes place. These are additive, so if Log() is called with both CONSOLE and ERROR
        /// enabled, two entries will be written to the console: one normal and one error.
        /// </summary>
        [Flags]
        public enum Type : ushort
        {
            /// <summary>
            /// Send output to stdout.
            /// </summary>
            CONSOLE = 1,

            /// <summary>
            /// Send output to a file.
            /// </summary>
            FILE = 2
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the default type of log message to be written using this Logger instance.
        /// </summary>
        public Type DefaultType { get; set; }

        /// <summary>
        /// Gets or sets the default level of log message to be written using this Logger instance.
        /// </summary>
        public Level DefaultLevel { get; set; }

        /// <summary>
        /// Gets or sets the levels that this logger will permit to be logged.
        /// </summary>
        public Level PermittedLevels { get; set; }

        /// <summary>
        /// Gets the name of the file where applicable logs are stored.
        /// </summary>
        public string Filename { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Clears the log file.
        /// </summary>
        /// <param name="filename">The name of the log file.</param>
        public static void ClearLog(string filename)
        {
            System.IO.File.Delete(filename);
            System.IO.File.Create(filename);
        }

        /// <summary>
        /// Logs the message in the specified type of log.
        /// </summary>
        /// <param name="message">The message to be logged.</param>
        /// <param name="filename">The name of the log file.</param>
        /// <param name="t">The type of log to be written.</param>
        public static void Log(string message, string filename, Type t = Type.CONSOLE)
        {
            Logger.Log(message, filename, Level.NONE, t);
        }

        /// <summary>
        /// Logs the message in the specified type of log.
        /// </summary>
        /// <param name="message">The message to be logged.</param>
        /// <param name="filename">The name of the log file.</param>
        /// <param name="l">The level of log to be written.</param>
        /// <param name="t">The type of log to be written.</param>
        public static void Log(string message, string filename, Level l = Level.NONE, Type t = Type.CONSOLE)
        {
            message = message.Trim(new char[] { '\n', '\r' }).TrimEnd(new char[] { '\t', ' ' }) + "\r\n";

            l = Logger.GetHighestLevel(l);

            if (l == Level.DEBUG)
            {
#if !DEBUG
                return;
#endif
            }

            if (l != Level.NONE)
            {
                message = l.ToString() + ": " + message;
            }

            if (((ushort)t & (ushort)Type.CONSOLE) > 0)
            {
                Console.Write(message);
            }

            if (((ushort)t & (ushort)Type.FILE) > 0)
            {
                bool writeComplete = false;
                while (!writeComplete)
                {
                    try
                    {
                        System.IO.File.AppendAllText(filename, message);
                        writeComplete = true;
                    }
                    catch (System.IO.IOException)
                    {
                        // Ignore the exception and retry the write.
                    }
                }
            }
        }

        /// <summary>
        /// Clears the log file.
        /// </summary>
        public void ClearLog()
        {
            System.IO.File.Delete(this.Filename);
            System.IO.File.Create(this.Filename);
        }

        /// <summary>
        /// Logs a message using the default level and type.
        /// </summary>
        /// <param name="message">The message to be logged.</param>
        public void Log(string message)
        {
            this.Log(message, this.DefaultLevel, this.DefaultType);
        }

        /// <summary>
        /// Logs the message in the specified type of log.
        /// </summary>
        /// <param name="message">The message to be logged.</param>
        /// <param name="t">The type of log to be written.</param>
        public void Log(string message, Type t)
        {
            this.Log(message, this.DefaultLevel, t);
        }

        /// <summary>
        /// Logs the message with the specified level of log.
        /// </summary>
        /// <param name="message">The message to be logged.</param>
        /// <param name="l">The level of log to be written.</param>
        public void Log(string message, Level l)
        {
            this.Log(message, l, this.DefaultType);
        }

        /// <summary>
        /// Logs the message in the specified type of log.
        /// </summary>
        /// <param name="message">The message to be logged.</param>
        /// <param name="l">The level of log to be written.</param>
        /// <param name="t">The type of log to be written.</param>
        public void Log(string message, Level l, Type t)
        {
            Level highestPermittedLevelChosen = Logger.GetHighestLevel(this.PermittedLevels & l);
            if (highestPermittedLevelChosen == 0)
            {
                return;
            }
            else
            {
                Logger.Log(message, this.Filename, highestPermittedLevelChosen, t);
            }
        }

        /// <summary>
        /// Retrieves the highest level contained within the provided Level instance.
        /// </summary>
        /// <param name="l">The level(s) to be evaluated.</param>
        /// <returns>The highest level found within the provided level(s).</returns>
        private static Level GetHighestLevel(Level l)
        {
            Level highestLevel = 0;

            foreach (Level subLevel in Enum.GetValues(typeof(Level)))
            {
                if (subLevel > highestLevel &&
                    (subLevel & l) > 0)
                {
                    highestLevel = subLevel;
                }
            }

            return highestLevel;
        }

        #endregion
    }
}

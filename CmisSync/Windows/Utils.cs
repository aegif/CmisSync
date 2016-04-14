using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace CmisSync
{
    /// <summary>
    /// Useful Windows-specific methods.
    /// </summary>
    public static class Utils
    {
        private static char[] splitChars = new char[] { ' ', '-', '\t', '\\' };


        /// <summary>
        /// Open a folder in Windows Explorer.
        /// </summary>
        /// <param name="path">Path to open</param>
        public static void OpenFolder(string path)
        {
            Process process = new Process();
            process.StartInfo.FileName = "explorer";
            process.StartInfo.Arguments = path;

            process.Start();
        }


        /// <summary>
        /// Trim a string to length size with an ellipsis.
        /// </summary>
        public static string Ellipsis(string text, int length)
        {
            if (text.Length > length)
            {
                if (length > 5)
                {
                    return text.Substring(0, length - 3) + "...";
                }
                else
                {
                    return text.Substring(0, length);
                }
            }
            else
            {
                return text;
            }
        }


        /// <summary>
        /// Word wrap a strign to width characters.
        /// </summary>
        public static string WordWrap(string str, int width)
        {
            string[] words = Explode(str, splitChars);

            int curLineLength = 0;
            StringBuilder strBuilder = new StringBuilder();
            for (int i = 0; i < words.Length; i += 1)
            {
                string word = words[i];
                // If adding the new word to the current line would be too long,
                // then put it on a new line (and split it up if it's too long).
                if (curLineLength + word.Length > width)
                {
                    // Only move down to a new line if we have text on the current line.
                    // Avoids situation where wrapped whitespace causes emptylines in text.
                    if (curLineLength > 0)
                    {
                        strBuilder.Append(Environment.NewLine);
                        curLineLength = 0;
                    }

                    // If the current word is too long to fit on a line even on it's own then
                    // split the word up.
                    while (word.Length > width)
                    {
                        strBuilder.Append(word.Substring(0, width - 1) + "-");
                        word = word.Substring(width - 1);

                        strBuilder.Append(Environment.NewLine);
                    }

                    // Remove leading whitespace from the word so the new line starts flush to the left.
                    word = word.TrimStart();
                }
                strBuilder.Append(word);
                curLineLength += word.Length;
            }

            return strBuilder.ToString();
        }


        /// <summary>
        /// Convert a string into an array of string based on split chars.
        /// </summary>
        private static string[] Explode(string str, char[] splitChars)
        {
            List<string> parts = new List<string>();
            int startIndex = 0;
            while (true)
            {
                int index = str.IndexOfAny(splitChars, startIndex);

                if (index == -1)
                {
                    parts.Add(str.Substring(startIndex));
                    return parts.ToArray();
                }

                string word = str.Substring(startIndex, index - startIndex);
                char nextChar = str.Substring(index, 1)[0];
                // Dashes and the likes should stick to the word occuring before it. Whitespace doesn't have to.
                if (char.IsWhiteSpace(nextChar))
                {
                    parts.Add(word);
                    parts.Add(nextChar.ToString());
                }
                else
                {
                    parts.Add(word + nextChar);
                }

                startIndex = index + 1;
            }
        }
    }
}


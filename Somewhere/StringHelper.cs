using System;
using System.Collections.Generic;
using System.Text;

namespace StringHelper
{
    public static class StringExtension
    {
        /// <summary>
        /// Given a command line style string, break it into seperate strings like usual Windows convention;
        /// I.e. each substring is seperated by space, and spaces are escaped by double quotes, and double qoutes are escapped by 
        /// double double quotes
        /// </summary>
        /// <remarks>
        /// Not necessarily super efficient, but it works for such one-shot circumstances
        /// </remarks>
        public static string[] BreakCommandLineArguments(this string v)
        {
            List<string> arguments = new List<string>();
            bool isInsideQuote = false;
            string sofar = string.Empty;
            // Below is a state machine approach, which is a bit hard to read in code but actually very intuitive 
            // in terms of logic: basically we start with various key conditions, then gradually add more detailed
            // branched within the conditions to achieve final results.
            // No comments are given because I find it better to keep things clean this way, and it's hard to make 
            // comments anyway. This kind of logics are better illustrated using a state machine diagram.
            // And the way to read it is to go through actual test cases, for that please see StringTest unit tests.
            for (int i = 0; i < v.Length; i++)
            {
                char c = v[i];
                switch (c)
                {
                    case ' ':
                        if (isInsideQuote)
                            sofar += c;
                        else if (sofar != string.Empty)
                        {
                            arguments.Add(sofar);
                            sofar = string.Empty;
                        }
                        break;
                    case '"':
                        if (isInsideQuote)
                        {
                            if(i + 1 < v.Length && v[i+1] == '"')
                            {
                                sofar += c;
                                i++;
                            }
                            else
                            {
                                arguments.Add(sofar);
                                sofar = string.Empty;
                                isInsideQuote = false;
                            }
                        }
                        else
                            isInsideQuote = true;
                        break;
                    default:
                        sofar += c;
                        break;
                }
            }
            // Handle ending
            if (sofar != string.Empty)
                arguments.Add(sofar);
            return arguments.ToArray();
        }
    }
}

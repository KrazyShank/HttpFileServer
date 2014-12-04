#region

using System;
using System.Collections.Generic;
using System.IO;

#endregion

namespace HttpFileServer
{
    public class Settings : IDisposable
    {
        private readonly string _cfgFile;
        private readonly Dictionary<string, string> _values;

        public Settings()
        {
            Console.WriteLine("Loading settings...");

            _values = new Dictionary<string, string>();

            _cfgFile = Path.Combine(Environment.CurrentDirectory, "config.cfg");
            if (File.Exists(_cfgFile))
                using (var rdr = new StreamReader(File.OpenRead(_cfgFile)))
                {
                    string line;
                    var lineNum = 1;
                    while ((line = rdr.ReadLine()) != null)
                    {
                        if (line.StartsWith("#")) continue;
                        var i = line.IndexOf(":", StringComparison.Ordinal);
                        if (i == -1)
                        {
                            Console.WriteLine("Invalid settings at line {0}.", lineNum);
                            throw new ArgumentException("Invalid settings.");
                        }
                        var val = line.Substring(i + 1);

                        _values.Add(line.Substring(0, i),
                            val.Equals("null", StringComparison.InvariantCultureIgnoreCase) ? null : val);
                        lineNum++;
                    }
                    Console.WriteLine("Settings loaded.");
                }
            else
                Console.WriteLine("Settings not found.");
        }

        public void Dispose()
        {
            try
            {
                Console.WriteLine("Saving settings...");
                using (var writer = new StreamWriter(File.OpenWrite(_cfgFile)))
                    foreach (var i in _values)
                        writer.WriteLine("{0}:{1}", i.Key, i.Value ?? "null");
            }
            catch (Exception e)
            {
                Console.WriteLine("Error when saving settings.", e);
            }
        }

        public string GetValueUnsafe(string key)
        {
            string value;
            _values.TryGetValue(key, out value);
            return value;
        }

        public string GetValue(string key, string def = null)
        {
            string ret;
            if (!_values.TryGetValue(key, out ret))
            {
                if (def == null)
                {
                    Console.WriteLine("Attempt to access nonexistant settings '{0}'.", key);
                    throw new ArgumentException(string.Format("'{0}' does not exist in settings.", key));
                }
                ret = _values[key] = def;
            }
            return ret;
        }

        public T GetValue<T>(string key, string def = null)
        {
            string ret;
            if (!_values.TryGetValue(key, out ret))
            {
                if (def == null)
                {
                    Console.WriteLine("Attempt to access nonexistant settings '{0}'.", key);
                    throw new ArgumentException(string.Format("'{0}' does not exist in settings.", key));
                }
                ret = _values[key] = def;
            }
            return (T) Convert.ChangeType(ret, typeof (T));
        }

        public void SetValue(string key, string val)
        {
            _values[key] = val;
        }
    }
}
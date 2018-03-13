namespace DupFind
{
    public class ArgReader
    {
        public string[] Args = new string[0];

        public ArgReader() { }

        public bool HasValues
        {
            get
            {
                return this.Args.Length > 0;
            }
        }

        public bool IsEmpty
        {
            get
            {
                return this.Args.Length == 0;
            }
        }

        public ArgReader(string[] args)
        {
            this.Args = args;
        }

        public bool this[string name]
        {
            get
            {
                foreach(var arg in Args)
                {
                    if (arg.Equals(name))
                        return true;
                }
                return false;
            }
        }

        public string GetValue(string name)
        {
            var found = false;
            for (var i = 0; i < this.Args.Length; i++)
            {
                if (found) return this.Args[i];
                if (this.Args[i].StartsWith(name))
                {
                    if (this.Args[i].Length > name.Length)
                    {
                        var parts = this.Args[i].Split(new char[] { '=' });
                        if(parts.Length > 1)
                        {
                            return parts[1];
                        }
                        return this.Args[i].Replace(name, "");
                    }
                    found = true;
                    
                }
            }

            return null;
        }
    }
}

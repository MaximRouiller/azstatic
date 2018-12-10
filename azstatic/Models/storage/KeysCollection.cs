using System.Collections.Generic;

namespace azstatic.Models.storage
{

    public class StorageKeysCollection
    {
        public IEnumerable<Key> Keys { get; set; }
    }

    public class Key
    {
        public string KeyName { get; set; }
        public string Value { get; set; }
        public string Permissions { get; set; }
    }

}

namespace azstatic.Models.storage
{

    public class GetProperties
    {
        public string id { get; set; }
        public string kind { get; set; }
        public string location { get; set; }
        public string name { get; set; }
        public Properties properties { get; set; }
    }

    public class Properties
    {
        public Primaryendpoints primaryEndpoints { get; set; }
        public string provisioningState { get; set; }
    }

    public class Primaryendpoints
    {
        public string web { get; set; }
        public string dfs { get; set; }
        public string blob { get; set; }
        public string file { get; set; }
        public string queue { get; set; }
        public string table { get; set; }
    }

}

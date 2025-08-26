namespace Employee_Survey.Infrastructure
{
    public class AppOptions
    {
        /// <summary>
        /// Domain + scheme + (optional) virtual path, ví dụ:
        /// https://localhost:5001  hoặc  https://your-domain.com/esurvey
        /// </summary>
        public string BaseUrl { get; set; } = "";
    }
}

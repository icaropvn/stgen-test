namespace GoodHamburger.Api.Exceptions {
    public class ApiException : Exception {
        public int StatusCode { get; }
        public string TypeUri { get; }

        public ApiException(string message, string typeUri, int statusCode) : base(message) {
            StatusCode = statusCode;
            TypeUri = typeUri;
        }
    }
}